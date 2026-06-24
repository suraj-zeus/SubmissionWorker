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



namespace SubmissionProcessor.Worker.Services;


public class RabbitMqService : IRabbitMqService
{

    private readonly RabbitMqConfig _rabbitMqConfig;
    private IConnection _connection;
    private ILogger<RabbitMqService> _logger;
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



    private async Task<IConnection> GetConnectionAsync()
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

        _connection = await connectionFactory.CreateConnectionAsync();
        return _connection;
    }


    public async Task ConsumeAsync(Func<SubmissionProcessingRequest, Task> onMessageReceived)
    {
        _connection = await GetConnectionAsync();

        var channel = await _connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: _rabbitMqConfig.SubmissionQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );


        // Prefetch count = 1 tells RabbitMQ not to give this worker a new message 
        // until it has fully completed and acknowledged the current one
        // prefetch size = 0, no limit on amount of bytes in message
        // global = false, every individual consumer gets its own buffer limit of 1 message.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);


        // consume message
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            string messageId = eventArgs.BasicProperties.MessageId;
            string correlationId = eventArgs.BasicProperties.CorrelationId;

            try
            {
                var body = eventArgs.Body.ToArray();
                string jsonString = Encoding.UTF8.GetString(body);

                SubmissionProcessingRequest request = JsonSerializer.Deserialize<SubmissionProcessingRequest>(jsonString);

                if (request != null)
                {
                    _logger.LogInformation($"Message received from queue. MessageId: {messageId}, CorrelationId: {correlationId}");

                    // Pass the message to the worker's processing logic
                    await onMessageReceived(request);
                }


                await simulateJobProcessing(request, sender, eventArgs, channel);


                await channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);
                _logger.LogInformation("Acknowledged Message {MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process message {messageId}. Requeuing message...");

                // Requeue the message back to the broker to retry execution
                await channel.BasicNackAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: true);
            }
        };


        // Start listening to the queue
        await channel.BasicConsumeAsync(
            queue: _rabbitMqConfig.SubmissionQueue,
            autoAck: false, // Required for manual BasicAckAsync/BasicNackAsync safety
            consumer: consumer
        );

        _logger.LogInformation("Successfully subscribed and listening to RabbitMQ queue: {Queue}", _rabbitMqConfig.SubmissionQueue);

    }


    private async Task simulateJobProcessing(SubmissionProcessingRequest request, object sender, BasicDeliverEventArgs eventArgs, IChannel channel)
    {

        using (var scope = _scopeFactory.CreateScope())
        {   
            _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ProcessingJobModel processingJob = await _context.ProcessingJobs.FirstOrDefaultAsync(p => p.MessageId == request.MessageId);

            if (processingJob != null && processingJob.Status == ProcessingJobStatus.Completed.ToString())
            {
                _logger.LogInformation($"Job with message id : {processingJob.MessageId} already completed. Duplicate message by RabbitMQ ignored");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            _logger.LogInformation($"Received message. MessageId:{processingJob.MessageId}, CorrelationId:{processingJob.CorrelationId}, SubmissionId:{processingJob.SubmissionId}");

            if (processingJob.Status == ProcessingJobStatus.Queued.ToString())
            {
                processingJob.Status = ProcessingJobStatus.Processing.ToString();
                processingJob.Attempts = 1;
                _logger.LogInformation($"Processing of Job {processingJob.Id} for message {processingJob.MessageId} has started.");

                await _context.SaveChangesAsync();
            }

            //SIMULATING THE PROCESSING
            try
            {
                SubmissionFileModel submissionFile = await _context.SubmissionFiles.FindAsync(processingJob.FileId);
                _logger.LogInformation("Metadata of the File is: ID: {FileId}, Name: {FileName}, Size: {FileSize} bytes, ContentType: {ContentType}, Checksum: {Checksum}, CreatedDate: {CreatedDate}", submissionFile.Id, submissionFile.OriginalFileName, submissionFile.FileSizeBytes, submissionFile.ContentType, submissionFile.CheckSum, submissionFile.CreatedDate);
                await Task.Delay(500);
                processingJob.Attempts += 1;
                processingJob.Status = "Completed";
                processingJob.CompletedDate = DateTime.UtcNow;
                _logger.LogInformation($"Processing of Job {processingJob.Id} for message {processingJob.MessageId} has completed.");
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Business processing logic failed for MessageId {MessageId}", processingJob.MessageId);
                processingJob.Status = ProcessingJobStatus.Failed.ToString();
                processingJob.ErrorSummary = ex.Message;

                await _context.SaveChangesAsync();

                await channel.BasicNackAsync(
                    deliveryTag: eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: false
                );
            }
        }


    }

}







