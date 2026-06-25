

using SubmissionProcessor.Worker.Messaging;

namespace SubmissionProcessor.Worker.Services;


public interface IRabbitMqService
{

    public Task StartAsync(CancellationToken cancellationToken);
    public Task ConsumeAsync(CancellationToken cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken);

}