namespace FCG.Notifications.Application.Events;

public record PaymentProcessedEvent
{
    public Guid OrderId { get; init; }
    public Guid PaymentId  { get; init; }
    public string UserEmail { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
}