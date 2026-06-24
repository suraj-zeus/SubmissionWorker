






namespace SubmissionProcessor.Worker.Models;

public class SubmissionFileModel
{
    public int Id { get; set; }

    public int SubmissionId {get; set; }

    public SubmissionModel Submission { get; set; } = null!;


    public string OriginalFileName {get; set; } = string.Empty;
    
    public string StorageName {get; set; } = string.Empty;

    public string ContentType {get; set; } = string.Empty;

    public long FileSizeBytes {get; set; } 

    public string CheckSum {get; set;} = string.Empty;

    public int UploadedByUserId {get; set; } 

    public DateTime CreatedDate {get; set;} = DateTime.UtcNow;

    public DateTime UpdatedDate {get; set; } = DateTime.UtcNow;

   
}