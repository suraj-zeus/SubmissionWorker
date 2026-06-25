using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Net;



using SubmissionProcessor.Worker;
using SubmissionProcessor.Worker.Configurations;
using SubmissionProcessor.Worker.DatabaseContext;
using SubmissionProcessor.Worker.ServiceClients;
using SubmissionProcessor.Worker.Services;


// load .env file
DotNetEnv.Env.Load();

var builder = Host.CreateApplicationBuilder(args);

// read env variables
builder.Configuration.AddEnvironmentVariables();

// bind custom configurations
builder.Services.Configure<RabbitMqConfig>(builder.Configuration.GetSection(RabbitMqConfig.SectionName));

//access correlation id
builder.Services.AddHttpContextAccessor();



builder.Services.AddHttpClient<TrainingDirectoryServiceClient>((serviceProvider, client) =>
{
    // Centralized BaseAddress and Timeout
    client.BaseAddress = new Uri("http://localhost:5024");
    // client.Timeout = TimeSpan.FromSeconds(15);

    // Centralized Required Headers
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    // Propagate Correlation ID 
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    var correlationId = httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();
    
    if (!string.IsNullOrEmpty(correlationId))
    {
        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
    }
    else
    {
        client.DefaultRequestHeaders.Add("X-Correlation-ID", Guid.NewGuid().ToString());
    }
}).AddStandardResilienceHandler(options =>
{   
    // timeout across all retry requests
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
    
    // individual request timeout
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.BackoffType = DelayBackoffType.Exponential;

    options.Retry.ShouldHandle = args => 
    {
        var outcome = args.Outcome;
        if (outcome.Exception is HttpRequestException) return ValueTask.FromResult(true);
        
        // retry if these status codes are received
        var statusCode = outcome.Result?.StatusCode;
        bool isTransient = statusCode == HttpStatusCode.InternalServerError || 
                           statusCode == HttpStatusCode.BadGateway || 
                           statusCode == HttpStatusCode.ServiceUnavailable || 
                           statusCode == HttpStatusCode.TooManyRequests;
                           
        return ValueTask.FromResult(isTransient);
    };



    // circuit breaker configurations
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.MinimumThroughput = 8;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

});


// db config
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);


// services configs
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
