using FCG.Notifications.Application.Interfaces;
using FCG.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Infrastructure.Messaging.Consumers;

public class UserCreatedConsumer(ILogger<UserCreatedConsumer> logger, IServicoEmail servicoEmail)
    : IConsumer<UserCreatedEvent>
{
    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var evt = context.Message;

        logger.LogInformation(
            "[EMAIL ENVIADO] Boas-vindas para {Name} <{Email}>. UsuarioId: {UserId}",
            evt.Nome, evt.Email, evt.UsuarioId);

        try
        {
            await servicoEmail.EnviarAsync(
                evt.Email,
                "Bem-vindo ao FCG!",
                $"<h1>Olá, {evt.Nome}!</h1><p>Sua conta foi criada com sucesso.</p>");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SMTP] Falha ao enviar e-mail de boas-vindas para {Email}", evt.Email);
        }
    }
}
