using FCG.Notifications.Application.Events;
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

        if (evt.PaymentStatus == "Approved")
        {
            Console.WriteLine($"[EMAIL ENVIADO] Confirmação de compra para {evt.UserEmail}, OrderId: {evt.OrderId}, PaymentId: {evt.PaymentId}");

            _logger.LogInformation(
                "[EMAIL ENVIADO] Confirmação de compra para {UserEmail}, OrderId: {OrderId}, PaymentI: {PaymentId}",
                evt.UserEmail, evt.OrderId, evt.PaymentId);
        }
        else
        {
            Console.WriteLine($"[EMAIL NÃO ENVIADO] Pagamento rejeitado para {evt.UserEmail}. OrderId: {evt.OrderId}"); 
            
            _logger.LogInformation(
                "[EMAIL NÃO ENVIADO] Pagamento rejeitado para {UserEmail}. OrderId: {OrderId}",
                evt.UserEmail, evt.OrderId);
        }

        return Task.CompletedTask;
    }
}