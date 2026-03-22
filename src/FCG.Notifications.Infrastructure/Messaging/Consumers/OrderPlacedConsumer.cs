using FCG.Notifications.Application.Interfaces;
using FCG.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Infrastructure.Messaging.Consumers;

public class OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger, IServicoEmail servicoEmail)
    : IConsumer<OrderPlacedEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var evt = context.Message;

        logger.LogInformation(
            "[EMAIL ENVIADO] Pedido realizado para {Email}. GameId: {GameId}, UserId: {UserId}, Price: {Price}",
            evt.Email, evt.GameId, evt.UserId, evt.Price);

        try
        {
            await servicoEmail.EnviarAsync(
                evt.Email,
                "Pedido realizado — FCG",
                $"<h1>Pedido recebido!</h1><p>Seu pedido foi registrado com sucesso.</p><p>Jogo: {evt.GameId}</p><p>Valor: R$ {evt.Price:F2}</p><p>Em breve você receberá a confirmação do pagamento.</p>");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SMTP] Falha ao enviar e-mail de pedido para {Email}", evt.Email);
        }
    }
}
