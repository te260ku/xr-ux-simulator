using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TimelineSystem
{
    public sealed class FlagsEnumJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            var enumType =
                Nullable.GetUnderlyingType(objectType) ?? objectType;

            return enumType.IsEnum &&
                   enumType.GetCustomAttribute<FlagsAttribute>() != null;
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            var nullableType = Nullable.GetUnderlyingType(objectType);
            var enumType = nullableType ?? objectType;

            if (reader.TokenType == JsonToken.Null)
            {
                if (nullableType != null)
                    return null;

                throw new JsonSerializationException(
                    $"{enumType.Name} must not be null.");
            }

            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new JsonSerializationException(
                    $"{enumType.Name} must be represented as a JSON array.");
            }

            var values = JArray.Load(reader);

            var names = values
                .Select(value =>
                {
                    if (value.Type != JTokenType.String)
                    {
                        throw new JsonSerializationException(
                            $"{enumType.Name} values must be strings.");
                    }

                    return value.Value<string>();
                })
                .ToArray();

            var combined = string.Join(", ", names);

            try
            {
                return Enum.Parse(enumType, combined, false);
            }
            catch (Exception exception)
            {
                throw new JsonSerializationException(
                    $"Invalid {enumType.Name} value: [{string.Join(", ", names)}].",
                    exception);
            }
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();

            var names = value
                .ToString()
                .Split(
                    new[] { ", " },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (var name in names)
                writer.WriteValue(name);

            writer.WriteEndArray();
        }
    }
}
