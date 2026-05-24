using FCG.Notifications.Application.EventHandlers;
using FCG.Notifications.Infrastructure.Messaging;
using FCG.Shared.Contracts.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Function;

public class NotificationFunctions(
    ILogger<NotificationFunctions> logger,
    IEventoHandler<UserCreatedEvent> userCreatedHandler,
    IEventoHandler<OrderPlacedEvent> orderPlacedHandler,
    IEventoHandler<PaymentProcessedEvent> paymentProcessedHandler)
{
    [Function(nameof(UserCreatedTrigger))]
    public async Task UserCreatedTrigger(
        [RabbitMQTrigger("user-created-queue", ConnectionStringSetting = "RabbitMq")] string mensagem,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Mensagem recebida em user-created-queue ({Bytes} bytes)", mensagem.Length);
        var evento = MassTransitEnvelopeReader.Extrair<UserCreatedEvent>(mensagem);
        await userCreatedHandler.TratarAsync(evento, cancellationToken);
    }

    [Function(nameof(OrderPlacedTrigger))]
    public async Task OrderPlacedTrigger(
        [RabbitMQTrigger("order-placed-queue", ConnectionStringSetting = "RabbitMq")] string mensagem,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Mensagem recebida em order-placed-queue ({Bytes} bytes)", mensagem.Length);
        var evento = MassTransitEnvelopeReader.Extrair<OrderPlacedEvent>(mensagem);
        await orderPlacedHandler.TratarAsync(evento, cancellationToken);
    }

    [Function(nameof(PaymentProcessedTrigger))]
    public async Task PaymentProcessedTrigger(
        [RabbitMQTrigger("payment-processed-queue", ConnectionStringSetting = "RabbitMq")] string mensagem,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Mensagem recebida em payment-processed-queue ({Bytes} bytes)", mensagem.Length);
        var evento = MassTransitEnvelopeReader.Extrair<PaymentProcessedEvent>(mensagem);
        await paymentProcessedHandler.TratarAsync(evento, cancellationToken);
    }
}
