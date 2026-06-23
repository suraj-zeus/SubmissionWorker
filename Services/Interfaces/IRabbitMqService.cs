

using SubmissionProcessor.Worker.Messaging;

namespace SubmissionProcessor.Worker.Services;


public interface IRabbitMqService
{
    public Task ConsumeAsync(Func<SubmissionProcessingRequest, Task> onMessageReceived);

}