using FCG.Notifications.Application.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Infrastructure.Messaging.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly ILogger _logger;

    public UserCreatedConsumer(ILogger<UserCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var evt = context.Message;

        Console.WriteLine($"[EMAIL ENVIADO] Boas-vindas para {evt.Nome} <{evt.Email}>. UsuarioId: {evt.UsuarioId}");

        _logger.LogInformation(
            "[EMAIL ENVIADO] Boas-vindas para {Name} <{Email}>. UsuarioId: {UserId}",
            evt.Nome, evt.Email, evt.UsuarioId);

        return Task.CompletedTask;
    }
}