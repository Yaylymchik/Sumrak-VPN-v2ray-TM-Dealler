using System.Text.Json;
using System.Text.Json.Nodes;
#if NET8_0_OR_GREATER
using System.Text.Json.Serialization.Metadata;
#endif

namespace SumraX.Infrastructure;

internal static class JsonCompat
{
    public static JsonSerializerOptions CreateIndentedWriteOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
#if NET8_0_OR_GREATER
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
#endif
        return options;
    }

    public static JsonObject DeepClone(JsonObject source) =>
        (JsonObject)DeepClone((JsonNode)source);

    public static JsonNode DeepClone(JsonNode source)
    {
#if NET8_0_OR_GREATER
        return source.DeepClone();
#else
        return JsonNode.Parse(source.ToJsonString())!;
#endif
    }
}