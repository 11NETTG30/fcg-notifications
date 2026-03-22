namespace FCG.Notifications.Application.Interfaces;

public interface IServicoEmail
{
    Task EnviarAsync(string destinatario, string assunto, string corpo);
}
