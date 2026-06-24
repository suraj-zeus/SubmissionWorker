using System.ComponentModel.DataAnnotations;

namespace SubmissionProcessor.Worker.Models;

public class TaskAssignmentModel
{
    public int Id { get; set; } 

    public required int TraineeId { get; set; }
    public TraineeModel Trainee { get; set; } = null!;

    public required int MentorId { get; set; }
    public MentorModel Mentor { get; set; } = null!;

    public required int LearningTaskId { get; set; }
    public LearningTaskModel LearningTask { get; set; } = null!;

    public DateTime AssignedDate { get; set; }
    public DateTime DueDate { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? Remarks { get; set; } = string.Empty;


     public ICollection<SubmissionModel> Submissions { get; set; } = new List<SubmissionModel>();
 
}
