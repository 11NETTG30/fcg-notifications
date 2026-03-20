using FCG.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Infrastructure.Messaging.Consumers;

public class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly ILogger _logger;

    public PaymentProcessedConsumer(ILogger<PaymentProcessedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var evt = context.Message;

        if (evt.Status == "Pago")
        {
            Console.WriteLine($"[EMAIL ENVIADO] Confirmação de compra para {evt.Email}, OrderId: {evt.OrderId}, PaymentId: {evt.PaymentId}");

            _logger.LogInformation(
                "[EMAIL ENVIADO] Confirmação de compra para {Email}, OrderId: {OrderId}, PaymentId: {PaymentId}",
                evt.Email, evt.OrderId, evt.PaymentId);
        }
        else
        {
            Console.WriteLine($"[EMAIL NÃO ENVIADO] Pagamento rejeitado para {evt.Email}. OrderId: {evt.OrderId}");

            _logger.LogInformation(
                "[EMAIL NÃO ENVIADO] Pagamento rejeitado para {Email}. OrderId: {OrderId}",
                evt.Email, evt.OrderId);
        }

        return Task.CompletedTask;
    }
}