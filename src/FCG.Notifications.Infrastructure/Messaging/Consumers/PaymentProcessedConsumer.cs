using FCG.Notifications.Application.Interfaces;
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
                await servicoEmail.EnviarAsync(
                    evt.Email,
                    "Compra confirmada — FCG",
                    $"<h1>Compra aprovada!</h1><p>OrderId: {evt.OrderId}</p><p>PaymentId: {evt.PaymentId}</p>");
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
                await servicoEmail.EnviarAsync(
                    evt.Email,
                    "Pagamento recusado — FCG",
                    $"<h1>Pagamento recusado</h1><p>Seu pagamento para o pedido {evt.OrderId} não foi aprovado.</p><p>Tente novamente ou utilize outro meio de pagamento.</p>");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[SMTP] Falha ao enviar rejeição para {Email}", evt.Email);
            }
        }
    }
}
