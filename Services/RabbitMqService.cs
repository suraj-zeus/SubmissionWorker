using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;

using SubmissionProcessor.Worker.Configurations;
using SubmissionProcessor.Worker.Messaging;
using SubmissionProcessor.Worker.DatabaseContext;
using SubmissionProcessor.Worker.Models;
using SubmissionProcessor.Worker.Exceptions;



namespace SubmissionProcessor.Worker.Services;


public class RabbitMqService : IRabbitMqService
{

    private readonly RabbitMqConfig _rabbitMqConfig;
    private IConnection _connection;
    private ILogger<RabbitMqService> _logger;
    private IChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private AppDbContext _context;

    public RabbitMqService(
         IOptions<RabbitMqConfig> options,
         ILogger<RabbitMqService> logger,
        IServiceScopeFactory scopeFactory
    )
    {
        _logger = logger;
        _rabbitMqConfig = options.Value;
        _scopeFactory = scopeFactory;
    }



    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null) return _connection;

        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqConfig.HostName,
            Port = _rabbitMqConfig.Port,
            VirtualHost = _rabbitMqConfig.VirtualHost,
            UserName = _rabbitMqConfig.UserName,
            Password = _rabbitMqConfig.Password,
            ClientProvidedName = "trainee-api"
        };

        _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        return _connection;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // get connection
        _connection = await GetConnectionAsync(cancellationToken);

        // create channel
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // read configs
        string dlxName = _rabbitMqConfig.DlxName;
        string dlqName = _rabbitMqConfig.DlqName;
        string routingKey = _rabbitMqConfig.RoutingKey;

        // declare exchange
        await _channel.ExchangeDeclareAsync(
            exchange: dlxName,
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken: cancellationToken
        );

        // declare dead-letter queue
        await _channel.QueueDeclareAsync(
            queue: dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken
        );


        // bind exhange and dead-letter queue
        await _channel.QueueBindAsync(
            queue: dlqName,
            exchange: dlxName,
            routingKey: routingKey,
            cancellationToken: cancellationToken
        );

        // arguments for main queue
        var mainQueueArguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", dlxName },
            { "x-dead-letter-routing-key", routingKey }
        };

        // declare main queue
        await _channel.QueueDeclareAsync(
            queue: _rabbitMqConfig.SubmissionQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments,
            cancellationToken: cancellationToken
        );


        // Prefetch count = 1 tells RabbitMQ not to give this worker a new message 
        // until it has fully completed and acknowledged the current one
        // prefetch size = 0, no limit on amount of bytes in message
        // global = false, every individual consumer gets its own buffer limit of 1 message.
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: cancellationToken
        );
    }


    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {

        if (_channel == null) {
            throw new BadRequestException("Rabbitmq channel not initialized..");
            return;
        }

        // consume message
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            string messageId = eventArgs.BasicProperties.MessageId;
            string correlationId = eventArgs.BasicProperties.CorrelationId;

            try
            {
                var body = eventArgs.Body.ToArray();
                string jsonString = Encoding.UTF8.GetString(body);

                // extract submission processing request message
                SubmissionProcessingRequest request = JsonSerializer.Deserialize<SubmissionProcessingRequest>(jsonString);

                if (request == null)
                {
                    _logger.LogWarning("Received Invalid or Empty Message Payload");
                    await _channel.BasicNackAsync(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: false,
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                await simulateJobProcessing(request, sender, eventArgs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process message {messageId}. Requeuing message...");

                // Requeue the message back to the broker to retry execution
                await _channel.BasicNackAsync(
                    deliveryTag: eventArgs.DeliveryTag, 
                    multiple: false, 
                    requeue: true
                );
            }
        };


        // Start listening to the queue
        await _channel.BasicConsumeAsync(
            queue: _rabbitMqConfig.SubmissionQueue,
            autoAck: false, // Required for manual BasicAckAsync/BasicNackAsync safety
            consumer: consumer
        );

        _logger.LogInformation("Successfully subscribed and listening to RabbitMQ queue: {Queue}", _rabbitMqConfig.SubmissionQueue);

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken: cancellationToken);
            await _channel.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken: cancellationToken);
            await _connection.DisposeAsync();
        }
    }




    private async Task simulateJobProcessing(SubmissionProcessingRequest request, object sender, BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {

        using (var scope = _scopeFactory.CreateScope())
        {
            _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ProcessingJobModel processingJob = await _context.ProcessingJobs.FirstOrDefaultAsync(p => p.MessageId == request.MessageId);
            SubmissionFileModel fileMetaData = await _context.SubmissionFiles.FindAsync(request.FileId);


             // if job not found or max attemps limit crossed
            // mark it as permanent failure when max attempt is crossed
            if (fileMetaData == null || processingJob == null || processingJob.Attempts >= _rabbitMqConfig.MaxRetryAttempts)
            {
            
                if(processingJob != null) {
                    processingJob.Status = ProcessingJobStatus.Failed.ToString();
                    await _context.SaveChangesAsync();
                }

                // send negative acknowledgement
                await _channel.BasicNackAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false,
                    cancellationToken: cancellationToken
                );

                _logger.LogError($"Failed to process this request with message id : {request.MessageId}, submission id : {request.SubmissionId} and Correlation id : {request.CorrelationId}");
                return;
            }


            // job already completed
            if (processingJob != null && processingJob.Status == ProcessingJobStatus.Completed.ToString())
            {
                _logger.LogInformation($"Job with message id : {processingJob.MessageId} already completed. Duplicate message by RabbitMQ ignored");
                await _channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    false,
                    cancellationToken
                );

                return;
            }


           

            _logger.LogInformation($"Received message. MessageId:{processingJob.MessageId}, CorrelationId:{processingJob.CorrelationId}, SubmissionId:{processingJob.SubmissionId}");

            // start processing job
            if (processingJob.Status == ProcessingJobStatus.Queued.ToString())
            {
                processingJob.Status = ProcessingJobStatus.Processing.ToString();

                // TODO : can add start date here as well

                _logger.LogInformation($"Processing of Job {processingJob.Id} for message {processingJob.MessageId} has started.");
                await _context.SaveChangesAsync();
            }


            // simulate the process
            try
            {
                SubmissionFileModel submissionFile = await _context.SubmissionFiles.FindAsync(processingJob.FileId);
                _logger.LogInformation("Metadata of the File is: ID: {FileId}, Name: {FileName}, Size: {FileSize} bytes, ContentType: {ContentType}, Checksum: {Checksum}, CreatedDate: {CreatedDate}", submissionFile.Id, submissionFile.OriginalFileName, submissionFile.FileSizeBytes, submissionFile.ContentType, submissionFile.CheckSum, submissionFile.CreatedDate);

                // delay processing by 500ms
                await Task.Delay(500);

                processingJob.Attempts += 1;
                processingJob.Status = ProcessingJobStatus.Completed.ToString();
                processingJob.CompletedDate = DateTime.UtcNow;

                _logger.LogInformation($"Processing of Job {processingJob.Id} for message {processingJob.MessageId} has completed.");
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                processingJob.Status = ProcessingJobStatus.Failed.ToString();
                processingJob.ErrorSummary = ex.Message;
                processingJob.Attempts += 1;

                await _context.SaveChangesAsync();

                // retry processing with negative acknowledgement 
                await _channel.BasicNackAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: cancellationToken
                );

                _logger.LogError(ex, "Business processing logic failed for MessageId {MessageId}", processingJob.MessageId);
            }
        }

        await _channel.BasicAckAsync(
            deliveryTag: eventArgs.DeliveryTag, 
            multiple: false,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation($"Acknowledged Message {request.MessageId}");
    }

}







