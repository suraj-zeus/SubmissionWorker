


namespace SubmissionProcessor.Worker.Configurations;

public class RabbitMqConfig
{
    public const string SectionName = "RabbitMq";
    public string HostName {get; set; } = string.Empty;
    public int Port {get; set;} = 5672;
    public string VirtualHost {get; set;} = string.Empty;
    public string  UserName {get; set;} = string.Empty;
    public string Password {get; set;} = string.Empty;
    public string SubmissionQueue {get; set;} = string.Empty;

    public string DlxName {get; set;} = string.Empty;

    public string DlqName {get; set;} = string.Empty;

    public string RoutingKey {get; set;} = string.Empty;

    public int MaxRetryAttempts {get; set;} = 3;

}