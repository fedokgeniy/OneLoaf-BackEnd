using System.Text.Json;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>JSON settings shared by the publisher and the consumers (camelCase, snake_case enums).</summary>
internal static class MessagingJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ServiceDefaults.Extensions.ConfigureJson(json);
        return json;
    }
}
