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

        var tituloJogo = evt.TituloJogo ?? evt.GameId.ToString();

        logger.LogInformation(
            "[EMAIL ENVIADO] Pedido realizado para {Email}. Jogo: {TituloJogo}, UserId: {UserId}, Price: {Price}",
            evt.Email, tituloJogo, evt.UserId, evt.Price);

        try
        {
            await servicoEmail.EnviarAsync(
                evt.Email,
                $"Pedido realizado — {tituloJogo}",
                $"<h1>Pedido recebido!</h1><p>Seu pedido do jogo <strong>{tituloJogo}</strong> foi registrado com sucesso.</p><p>Valor: R$ {evt.Price:F2}</p><p>Em breve você receberá a confirmação do pagamento.</p>");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SMTP] Falha ao enviar e-mail de pedido para {Email}", evt.Email);
        }
    }
}
