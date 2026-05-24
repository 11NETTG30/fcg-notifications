using FCG.Notifications.Application.EventHandlers;
using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Application.Templates;
using FCG.Notifications.Infrastructure.Email;
using FCG.Notifications.Infrastructure.Templates;
using FCG.Shared.Contracts.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Function;

internal static class CompositionRoot
{
    public static IServiceProvider Build()
    {
        var configuracao = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuracao);
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton<ICarregadorTemplate, CarregadorTemplate>();
        services.AddSingleton<IServicoEmail, ServicoEmail>();

        services.AddTransient<IEventoHandler<UserCreatedEvent>, UserCreatedHandler>();
        services.AddTransient<IEventoHandler<OrderPlacedEvent>, OrderPlacedHandler>();
        services.AddTransient<IEventoHandler<PaymentProcessedEvent>, PaymentProcessedHandler>();

        return services.BuildServiceProvider();
    }
}
