using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuoteService.Tools
{
    class DecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type objectType, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            return -1.0M;
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}