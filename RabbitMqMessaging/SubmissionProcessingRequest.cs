namespace SubmissionProcessor.Worker.Messaging;


public class SubmissionProcessingRequest
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string CorrelationId { get; set; } = string.Empty;
    public int SubmissionId { get; set; }

    public int? FileId { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public int ContractVersion { get; set; } = 1;
}