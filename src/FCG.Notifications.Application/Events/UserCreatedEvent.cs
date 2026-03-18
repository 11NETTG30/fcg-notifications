namespace FCG.Notifications.Application.Events;

public record UserCreatedEvent
{
    public Guid UsuarioId { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
