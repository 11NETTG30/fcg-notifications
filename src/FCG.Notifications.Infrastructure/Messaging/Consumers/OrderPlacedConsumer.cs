using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Infrastructure.Templates;
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
            var corpo = CarregadorTemplate.Carregar("pedido-realizado.html", new Dictionary<string, string>
            {
                ["TITULO_JOGO"] = tituloJogo,
                ["PRECO"]       = evt.Price.ToString("F2")
            });

            await servicoEmail.EnviarAsync(evt.Email, $"Pedido realizado — {tituloJogo}", corpo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SMTP] Falha ao enviar e-mail de pedido para {Email}", evt.Email);
        }
    }
}
