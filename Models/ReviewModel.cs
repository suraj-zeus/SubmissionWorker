


namespace SubmissionProcessor.Worker.Models;

public class ReviewModel
{
    public int Id { get; set; }

    public int SubmissionId { get; set; }
    public SubmissionModel Submission { get; set; } = null!;

    public int MentorId { get; set; }
    public MentorModel Mentor { get; set; } = null!;

    public string Feedback { get; set; }

    public string? Score { get; set; }

    public string Status { get; set; }
    public DateTime ReviewedDate { get; set; }    
   
}