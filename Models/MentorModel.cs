


namespace SubmissionProcessor.Worker.Models;

public class MentorModel {
    public int Id {get; set;}

    public string FirstName {get; set;} = string.Empty;

    public string LastName {get; set;} = string.Empty;

    public string Email {get; set;} = string.Empty;

    public string Expertise {get; set;} = string.Empty;

    public string Status {get; set;} = string.Empty;

    public DateTime CreatedDate {get; set;}

    public DateTime UpdatedDate {get; set;}

    public ICollection<TaskAssignmentModel> TaskAssignments { get; set; } = new List<TaskAssignmentModel>();

    public ICollection<ReviewModel> Reviews { get; set; } = new List<ReviewModel>();

}