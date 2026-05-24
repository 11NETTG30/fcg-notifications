using Amazon.Lambda.Core;
using Amazon.Lambda.RabbitMQEvents;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Function;

public static class Function
{
    private static readonly IServiceProvider Servicos = CompositionRoot.Build();

    public static async Task Main()
    {
        Func<RabbitMQEvent, ILambdaContext, Task> handler = HandlerAsync;

        await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
            .Build()
            .RunAsync();
    }

    public static async Task HandlerAsync(RabbitMQEvent evento, ILambdaContext contexto)
    {
        var logger = Servicos.GetRequiredService<ILogger<Dispatcher>>();
        var dispatcher = new Dispatcher(Servicos, logger);

        if (evento.RmqMessagesByQueue is null || evento.RmqMessagesByQueue.Count == 0)
        {
            logger.LogWarning("Lambda invocada sem mensagens. AwsRequestId: {RequestId}", contexto.AwsRequestId);
            return;
        }

        foreach (var (chaveFila, mensagens) in evento.RmqMessagesByQueue)
        {
            foreach (var mensagem in mensagens)
            {
                await dispatcher.ProcessarAsync(chaveFila, mensagem.Data, CancellationToken.None);
            }
        }
    }
}
