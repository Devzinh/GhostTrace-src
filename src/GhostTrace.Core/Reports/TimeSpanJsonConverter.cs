using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GhostTrace.Core.Reports;

public class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        if (string.IsNullOrEmpty(stringValue))
        {
            return TimeSpan.Zero;
        }

        if (TimeSpan.TryParseExact(stringValue, @"hh\:mm\:ss\.fff", null, out var parsed))
        {
            return parsed;
        }

        return TimeSpan.Parse(stringValue); // Fallback
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(@"hh\:mm\:ss\.fff"));
    }
}
