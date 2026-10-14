using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MiettisPark.Api.Contracts;

public sealed partial class OccurredAtJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Tapahtuma-ajan on oltava ISO 8601 -aika offsetilla.");
        }

        var value = reader.GetString();
        if (!TryParse(value, out var time))
        {
            throw new JsonException("Tapahtuma-ajan on oltava ISO 8601 -aika offsetilla.");
        }

        return time.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    public static bool TryParse(string? value, out DateTimeOffset time)
    {
        time = default;
        return value != null && OffsetTimestamp().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetTimestamp();
}
