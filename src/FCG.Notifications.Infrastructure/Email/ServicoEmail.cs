using FCG.Notifications.Application.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace FCG.Notifications.Infrastructure.Email;

public class ServicoEmail(IConfiguration configuration, ILogger<ServicoEmail> logger) : IServicoEmail
{
    public async Task EnviarAsync(string destinatario, string assunto, string corpo)
    {
        var host = configuration["Smtp:Host"] ?? "localhost";
        var porta = int.Parse(configuration["Smtp:Porta"] ?? "1025");
        var remetente = configuration["Smtp:Remetente"] ?? "noreply@fcg.com";

        var mensagem = new MimeMessage();
        mensagem.From.Add(MailboxAddress.Parse(remetente));
        mensagem.To.Add(MailboxAddress.Parse(destinatario));
        mensagem.Subject = assunto;
        mensagem.Body = new TextPart("html") { Text = corpo };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, porta, useSsl: false);
        await client.SendAsync(mensagem);
        await client.DisconnectAsync(true);

        logger.LogInformation("[SMTP] E-mail enviado para {Destinatario}: {Assunto}", destinatario, assunto);
    }
}
