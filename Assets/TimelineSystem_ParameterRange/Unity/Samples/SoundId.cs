using System;
using Newtonsoft.Json;

[RepositoryKeySource("Sound")]
public sealed class SoundId : IEquatable<SoundId>
{
    public string Value { get; }

    [JsonConstructor]
    public SoundId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "Sound ID must not be empty.",
                nameof(value));

        Value = value;
    }

    public bool Equals(SoundId other)
    {
        return other != null &&
               string.Equals(
                   Value,
                   other.Value,
                   StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as SoundId);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Value);
    }

    public override string ToString()
    {
        return Value;
    }
}
