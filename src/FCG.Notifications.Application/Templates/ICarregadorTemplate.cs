namespace FCG.Notifications.Application.Templates;

public interface ICarregadorTemplate
{
    string Carregar(string nomeArquivo, IDictionary<string, string> variaveis);
}
