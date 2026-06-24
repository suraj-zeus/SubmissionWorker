

using Microsoft.EntityFrameworkCore;
using SubmissionProcessor.Worker.Models;


namespace SubmissionProcessor.Worker.DatabaseContext;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TraineeModel> Trainees { get; set; }

    public DbSet<UserModel> Users { get; set; }

    public DbSet<MentorModel> Mentors { get; set; }

    public DbSet<LearningTaskModel> LearningTasks { get; set; }

    public DbSet<TaskAssignmentModel> TaskAssignments { get; set; }

    public DbSet<SubmissionModel> Submissions { get; set; }

    public DbSet<ReviewModel> Reviews { get; set; }

    public DbSet<SubmissionFileModel> SubmissionFiles { get; set; }

    public DbSet<ProcessingJobModel> ProcessingJobs { get; set; }



}