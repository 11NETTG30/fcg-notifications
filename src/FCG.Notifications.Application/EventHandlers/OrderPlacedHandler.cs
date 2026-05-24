using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Application.Templates;
using FCG.Shared.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Application.EventHandlers;

public class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger, IServicoEmail servicoEmail, ICarregadorTemplate templates)
    : IEventoHandler<OrderPlacedEvent>
{
    public async Task TratarAsync(OrderPlacedEvent evento, CancellationToken cancellationToken = default)
    {
        var tituloJogo = evento.TituloJogo ?? evento.GameId.ToString();

        logger.LogInformation(
            "[EMAIL] Pedido realizado para {Email}. Jogo: {TituloJogo}, UserId: {UserId}, Price: {Price}",
            evento.Email, tituloJogo, evento.UserId, evento.Price);

        var corpo = templates.Carregar("pedido-realizado.html", new Dictionary<string, string>
        {
            ["TITULO_JOGO"] = tituloJogo,
            ["PRECO"]       = evento.Price.ToString("F2")
        });

        await servicoEmail.EnviarAsync(evento.Email, $"Pedido realizado — {tituloJogo}", corpo);
    }
}
