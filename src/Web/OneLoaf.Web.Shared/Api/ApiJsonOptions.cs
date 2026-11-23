using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OneLoaf.Web.Shared.Api;

/// <summary>
/// JSON settings of the wire format (camelCase, strict numbers, enums as snake_case strings — same as the services)
/// over the source-generated contexts registered with <see cref="ApiServiceCollectionExtensions.AddApiJsonContext"/>.
/// </summary>
public sealed class ApiJsonOptions
{
    /// <summary>Creates the options from the registered source-generated contexts.</summary>
    public ApiJsonOptions(IEnumerable<IJsonTypeInfoResolver> resolvers)
    {
        Options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = JsonTypeInfoResolver.Combine([.. resolvers]),
        };
        Options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        Options.MakeReadOnly();
    }

    /// <summary>The read-only serializer options.</summary>
    public JsonSerializerOptions Options { get; }

    /// <summary>Type metadata of <typeparamref name="T"/>; throws when no registered context declares the type.</summary>
    public JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
}
