using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Events;

using SubmissionProcessor.Worker.Configurations;
using SubmissionProcessor.Worker.Messaging;



namespace SubmissionProcessor.Worker.Services;


public class RabbitMqService : IRabbitMqService
{

    private readonly RabbitMqConfig _rabbitMqConfig;
    private IConnection _connection;
    private ILogger<RabbitMqService> _logger;

    public RabbitMqService(
         IOptions<RabbitMqConfig> options,
         ILogger<RabbitMqService> logger
    )
    {
        _logger = logger;
        _rabbitMqConfig = options.Value;

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

                await channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);
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




}


