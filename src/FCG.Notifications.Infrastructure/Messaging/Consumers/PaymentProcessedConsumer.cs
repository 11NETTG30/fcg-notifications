using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Infrastructure.Templates;
using FCG.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Infrastructure.Messaging.Consumers;

public class PaymentProcessedConsumer(ILogger<PaymentProcessedConsumer> logger, IServicoEmail servicoEmail)
    : IConsumer<PaymentProcessedEvent>
{
    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var evt = context.Message;

        if (evt.Status == "Approved")
        {
            logger.LogInformation(
                "[EMAIL ENVIADO] Confirmação de compra para {Email}, OrderId: {OrderId}, PaymentId: {PaymentId}",
                evt.Email, evt.OrderId, evt.PaymentId);

            try
            {
                var corpo = CarregadorTemplate.Carregar("compra-confirmada.html", new Dictionary<string, string>
                {
                    ["ORDER_ID"]   = evt.OrderId.ToString(),
                    ["PAYMENT_ID"] = evt.PaymentId.ToString()
                });

                await servicoEmail.EnviarAsync(evt.Email, "Compra confirmada — FCG", corpo);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[SMTP] Falha ao enviar confirmação para {Email}", evt.Email);
            }
        }
        else
        {
            logger.LogInformation(
                "[EMAIL ENVIADO] Pagamento rejeitado para {Email}. OrderId: {OrderId}",
                evt.Email, evt.OrderId);

            try
            {
                var corpo = CarregadorTemplate.Carregar("pagamento-recusado.html", new Dictionary<string, string>
                {
                    ["ORDER_ID"] = evt.OrderId.ToString()
                });

                await servicoEmail.EnviarAsync(evt.Email, "Pagamento recusado — FCG", corpo);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[SMTP] Falha ao enviar rejeição para {Email}", evt.Email);
            }
        }
    }
}
