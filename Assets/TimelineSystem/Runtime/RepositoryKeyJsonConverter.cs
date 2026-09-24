using System;
using Newtonsoft.Json;

namespace TimelineSystem
{
    public sealed class RepositoryKeyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IRepositoryKey).IsAssignableFrom(objectType);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String)
            {
                throw new JsonSerializationException(
                    $"{objectType.Name} must be represented as a JSON string.");
            }

            var value = reader.Value as string;

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonSerializationException(
                    $"{objectType.Name} must not be empty.");
            }

            var constructor =
                objectType.GetConstructor(new[] { typeof(string) });

            if (constructor == null)
            {
                throw new JsonSerializationException(
                    $"{objectType.Name} must have a public constructor " +
                    "with a single string parameter.");
            }

            return constructor.Invoke(new object[] { value });
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

            var repositoryKey = (IRepositoryKey)value;
            writer.WriteValue(repositoryKey.Value);
        }
    }
}
