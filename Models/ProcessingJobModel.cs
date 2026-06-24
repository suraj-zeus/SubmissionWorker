namespace SubmissionProcessor.Worker.Models;

public class ProcessingJobModel
{
    public int Id { get; set; }
 
    public string MessageId {get; set;} = string.Empty;
    public string CorrelationId {get; set;} = string.Empty;
    public int SubmissionId {get; set;}
    public int FileId {get; set;}
    public string Status {get; set;} = string.Empty;
    public int Attempts {get; set;}=0;
    public string? ErrorSummary {get; set;} = string.Empty;
    public DateTime StartedDate {get; set;} = DateTime.UtcNow;
    public DateTime CompletedDate {get; set;}

    public SubmissionFileModel SubmissionFile = null!;
    public SubmissionModel Submission = null!;
}