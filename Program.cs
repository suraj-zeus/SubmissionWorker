using DotNetEnv;

using SubmissionProcessor.Worker;
using SubmissionProcessor.Worker.Configurations;
using SubmissionProcessor.Worker.Services;


// load .env file
DotNetEnv.Env.Load();

var builder = Host.CreateApplicationBuilder(args);

// read env variables
builder.Configuration.AddEnvironmentVariables();

// bind custom configurations
builder.Services.Configure<RabbitMqConfig>(builder.Configuration.GetSection(RabbitMqConfig.SectionName));

// services configs
builder.Services.AddSingleton<IRabbitMqService, RabbitMqService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
