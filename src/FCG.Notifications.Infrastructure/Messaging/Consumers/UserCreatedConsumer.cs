using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Infrastructure.Templates;
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
            var corpo = CarregadorTemplate.Carregar("boas-vindas.html", new Dictionary<string, string>
            {
                ["NOME"]  = evt.Nome,
                ["EMAIL"] = evt.Email
            });

            await servicoEmail.EnviarAsync(evt.Email, "Bem-vindo ao FCG!", corpo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SMTP] Falha ao enviar e-mail de boas-vindas para {Email}", evt.Email);
        }
    }
}
