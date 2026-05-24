using FCG.Notifications.Application.Interfaces;
using FCG.Notifications.Application.Templates;
using FCG.Shared.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace FCG.Notifications.Application.EventHandlers;

public class UserCreatedHandler(ILogger<UserCreatedHandler> logger, IServicoEmail servicoEmail, ICarregadorTemplate templates)
    : IEventoHandler<UserCreatedEvent>
{
    public async Task TratarAsync(UserCreatedEvent evento, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[EMAIL] Boas-vindas para {Name} <{Email}>. UsuarioId: {UserId}",
            evento.Nome, evento.Email, evento.UsuarioId);

        var corpo = templates.Carregar("boas-vindas.html", new Dictionary<string, string>
        {
            ["NOME"]  = evento.Nome,
            ["EMAIL"] = evento.Email
        });

        await servicoEmail.EnviarAsync(evento.Email, "Bem-vindo ao FCG!", corpo);
    }
}
