using System.Text.Json;
using System.Text.Json.Serialization;

namespace FCG.Notifications.Infrastructure.Messaging;

public sealed class MassTransitEnvelope<T>
{
    [JsonPropertyName("messageId")]   public string? MessageId   { get; set; }
    [JsonPropertyName("messageType")] public string[]? MessageType { get; set; }
    [JsonPropertyName("message")]     public T? Message          { get; set; }
}

public static class MassTransitEnvelopeReader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    
    public static T Extrair<T>(string json)
    {
        using var documento = JsonDocument.Parse(json);
        if (documento.RootElement.TryGetProperty("message", out var elementoMensagem))
        {
            return elementoMensagem.Deserialize<T>(Options)
                   ?? throw new InvalidOperationException($"Envelope sem payload para {typeof(T).Name}.");
        }

        return JsonSerializer.Deserialize<T>(json, Options)
               ?? throw new InvalidOperationException($"Payload vazio para {typeof(T).Name}.");
    }
}
