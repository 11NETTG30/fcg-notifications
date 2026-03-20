using DotNetEnv;
using FCG.Notifications.API.Configurations;
using FCG.Shared.Infrastructure.Configurations;
using FCG.Notifications.Infrastructure.Messaging.Configurations;

var builder = WebApplication.CreateBuilder(args);

Env.Load();
builder.Configuration.AddEnvironmentVariables();

builder.AddLoggingConfiguration();
builder.AddObservabilidade();

builder.Services.AddControllers();
builder.Services.AddMassTransitConfiguration(builder.Configuration);
builder.Services.AddDocumentation();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDocumentation();
}

app.MapControllers();
app.Run();