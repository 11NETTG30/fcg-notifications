using System.Text;
using FCG.Notifications.Application.EventHandlers;
using FCG.Notifications.Infrastructure.Messaging;
using FCG.Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Function;

internal sealed class Dispatcher(IServiceProvider services, ILogger<Dispatcher> logger)
{
    public async Task ProcessarAsync(string chaveFila, string corpoBase64, CancellationToken cancellationToken)
    {
        var nomeFila = chaveFila.Split("::")[0];

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(corpoBase64));
            logger.LogInformation("Mensagem recebida da fila {Fila} ({Bytes} bytes)", nomeFila, json.Length);

            switch (nomeFila)
            {
                case "user-created-queue":
                    await DespacharAsync<UserCreatedEvent>(json, cancellationToken);
                    break;

                case "order-placed-queue":
                    await DespacharAsync<OrderPlacedEvent>(json, cancellationToken);
                    break;

                case "payment-processed-queue":
                    await DespacharAsync<PaymentProcessedEvent>(json, cancellationToken);
                    break;

                default:
                    logger.LogWarning("Fila desconhecida: {Fila}. Mensagem ignorada.", nomeFila);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao processar mensagem da fila {Fila}", nomeFila);
        }
    }

    private async Task DespacharAsync<T>(string json, CancellationToken cancellationToken)
    {
        var evento = MassTransitEnvelopeReader.Extrair<T>(json);
        var handler = services.GetRequiredService<IEventoHandler<T>>();
        await handler.TratarAsync(evento, cancellationToken);
    }
}
