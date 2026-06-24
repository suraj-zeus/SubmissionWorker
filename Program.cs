using DotNetEnv;
using Microsoft.EntityFrameworkCore;

using SubmissionProcessor.Worker;
using SubmissionProcessor.Worker.Configurations;
using SubmissionProcessor.Worker.DatabaseContext;
using SubmissionProcessor.Worker.Services;


// load .env file
DotNetEnv.Env.Load();

var builder = Host.CreateApplicationBuilder(args);

// read env variables
builder.Configuration.AddEnvironmentVariables();

// bind custom configurations
builder.Services.Configure<RabbitMqConfig>(builder.Configuration.GetSection(RabbitMqConfig.SectionName));

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
