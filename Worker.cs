using SubmissionProcessor.Worker.Services;

namespace SubmissionProcessor.Worker;

public class Worker : BackgroundService
{

    private IRabbitMqService _rabbitMqService;
    private ILogger<Worker> _logger;

    public Worker(IRabbitMqService rabbitMqService, ILogger<Worker> logger)
    {
        _rabbitMqService = rabbitMqService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        _logger.LogInformation("Main background worker starting up...");
        await _rabbitMqService.ConsumeAsync(async (request) =>
       {
           _logger.LogInformation("Processing business logic for Submission: {SubmissionId}", request.SubmissionId);

           await Task.Delay(500, stoppingToken);

           _logger.LogInformation("Finished processing Submission: {SubmissionId} with CorrelationId : {CorrelationId} and MessageId : {MessageId}", request.SubmissionId, request.CorrelationId, request.MessageId);

       });

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
