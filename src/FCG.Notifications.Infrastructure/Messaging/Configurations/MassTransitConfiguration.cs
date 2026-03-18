using FCG.Notifications.Infrastructure.Messaging.Consumers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Notifications.Infrastructure.Messaging.Configurations;

public static class MassTransitConfiguration
{
    extension(IServiceCollection services)
    {
        public void AddMassTransitConfiguration(IConfiguration config)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<UserCreatedConsumer>();
                x.AddConsumer<PaymentProcessedConsumer>();

                x.UsingRabbitMq((ctx, cfg) =>
                {
                    var host = config["RabbitMQ:Host"];
                    var user = config["RabbitMQ:User"];
                    var pass = config["RabbitMQ:Password"];

                    if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                    {
                        throw new Exception("Erro configuração do RabbitMQ.");
                    }

                    cfg.MessageTopology.SetEntityNameFormatter(new CustomNameEntityNameFormatter());
                    cfg.Host(host, "/", h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });

                    cfg.ReceiveEndpoint("user-created-queue", e =>
                    {
                        e.ConfigureConsumer<UserCreatedConsumer>(ctx);
                    });

                    cfg.ReceiveEndpoint("payment-processed-queue", e =>
                    {
                        e.ConfigureConsumer<PaymentProcessedConsumer>(ctx);
                    });
                });
            });
        }
    }
    
}