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


    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _rabbitMqService.StartAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {

        _logger.LogInformation("Main background worker starting up...");
        await _rabbitMqService.ConsumeAsync(cancellationToken);

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }


    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _rabbitMqService.StopAsync(cancellationToken);

        await base.StopAsync(cancellationToken);

    }
}
