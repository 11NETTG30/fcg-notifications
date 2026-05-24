using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Application.Templates;
using FCG.Shared.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Application.EventHandlers;

public class PaymentProcessedHandler(ILogger<PaymentProcessedHandler> logger, IServicoEmail servicoEmail, ICarregadorTemplate templates)
    : IEventoHandler<PaymentProcessedEvent>
{
    public async Task TratarAsync(PaymentProcessedEvent evento, CancellationToken cancellationToken = default)
    {
        if (evento.Status == "Approved")
        {
            logger.LogInformation(
                "[EMAIL] Confirmação de compra para {Email}, OrderId: {OrderId}, PaymentId: {PaymentId}",
                evento.Email, evento.OrderId, evento.PaymentId);

            var corpo = templates.Carregar("compra-confirmada.html", new Dictionary<string, string>
            {
                ["ORDER_ID"]   = evento.OrderId.ToString(),
                ["PAYMENT_ID"] = evento.PaymentId.ToString()
            });

            await servicoEmail.EnviarAsync(evento.Email, "Compra confirmada — FCG", corpo);
            return;
        }

        logger.LogInformation(
            "[EMAIL] Pagamento rejeitado para {Email}. OrderId: {OrderId}",
            evento.Email, evento.OrderId);

        var corpoRecusado = templates.Carregar("pagamento-recusado.html", new Dictionary<string, string>
        {
            ["ORDER_ID"] = evento.OrderId.ToString()
        });

        await servicoEmail.EnviarAsync(evento.Email, "Pagamento recusado — FCG", corpoRecusado);
    }
}
