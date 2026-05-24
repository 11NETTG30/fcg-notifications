using System.Reflection;
using FCG.Notifications.Application.Templates;

namespace FCG.Notifications.Infrastructure.Templates;

public sealed class CarregadorTemplate : ICarregadorTemplate
{
    public string Carregar(string nomeArquivo, IDictionary<string, string> variaveis)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var recurso = $"FCG.Notifications.Infrastructure.Templates.{nomeArquivo}";

        using var stream = assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"Template '{recurso}' não encontrado.");

        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        foreach (var (chave, valor) in variaveis)
            html = html.Replace($"{{{{{chave}}}}}", valor);

        return html;
    }
}
