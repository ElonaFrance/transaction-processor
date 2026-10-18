using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TransactionProcessor.Core.Converters;

// MySQL DATETIME rejects the ISO "Z" suffix and more than 6 fractional digits,
// so dates going into stored procedure payloads are written as "yyyy-MM-dd HH:mm:ss.ffffff" (UTC).
public class MySqlDateTimeConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss.ffffff";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        writer.WriteStringValue(utc.ToString(Format, CultureInfo.InvariantCulture));
    }
}
