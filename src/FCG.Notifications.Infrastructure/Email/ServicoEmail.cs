using FCG.Notifications.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
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
        var usuario = configuration["Smtp:Usuario"];
        var senha = configuration["Smtp:Senha"];

        var mensagem = new MimeMessage();
        mensagem.From.Add(MailboxAddress.Parse(remetente));
        mensagem.To.Add(MailboxAddress.Parse(destinatario));
        mensagem.Subject = assunto;
        mensagem.Body = new TextPart("html") { Text = corpo };

        using var client = new SmtpClient();

        // STARTTLS para SES (porta 587), nenhuma criptografia para Mailpit (1025)
        var seguranca = porta == 587 ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(host, porta, seguranca);

        if (!string.IsNullOrWhiteSpace(usuario) && !string.IsNullOrWhiteSpace(senha))
        {
            await client.AuthenticateAsync(usuario, senha);
        }

        await client.SendAsync(mensagem);
        await client.DisconnectAsync(true);

        logger.LogInformation("[SMTP] E-mail enviado para {Destinatario}: {Assunto}", destinatario, assunto);
    }
}
