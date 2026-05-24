namespace FCG.Notifications.Application.EventHandlers;

public interface IEventoHandler<in TEvento>
{
    Task TratarAsync(TEvento evento, CancellationToken cancellationToken = default);
}
