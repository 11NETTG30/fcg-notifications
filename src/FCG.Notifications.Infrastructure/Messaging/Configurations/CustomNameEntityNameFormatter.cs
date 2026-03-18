using MassTransit;

namespace FCG.Notifications.Infrastructure.Messaging.Configurations;

public class CustomNameEntityNameFormatter : IEntityNameFormatter
{
    public string FormatEntityName<T>()
    {
        return typeof(T).Name;
    }
}