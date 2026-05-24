using FCG.Notifications.Application.EventHandlers;
using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Application.Templates;
using FCG.Notifications.Infrastructure.Email;
using FCG.Notifications.Infrastructure.Templates;
using FCG.Shared.Contracts.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// FunctionsApplication.CreateBuilder ja aplica os defaults do worker.
// Nao chamamos ConfigureFunctionsWebApplication porque so usamos RabbitMQ trigger (sem HTTP).
var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<ICarregadorTemplate, CarregadorTemplate>();
builder.Services.AddSingleton<IServicoEmail, ServicoEmail>();

builder.Services.AddTransient<IEventoHandler<UserCreatedEvent>, UserCreatedHandler>();
builder.Services.AddTransient<IEventoHandler<OrderPlacedEvent>, OrderPlacedHandler>();
builder.Services.AddTransient<IEventoHandler<PaymentProcessedEvent>, PaymentProcessedHandler>();

builder.Build().Run();
