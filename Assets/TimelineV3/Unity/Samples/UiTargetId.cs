using System;
using Newtonsoft.Json;

[RepositoryKeySource("UiTarget")]
public sealed class UiTargetId : IEquatable<UiTargetId>
{
    public string Value { get; }

    [JsonConstructor]
    public UiTargetId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "UI target ID must not be empty.",
                nameof(value));

        Value = value;
    }

    public bool Equals(UiTargetId other)
    {
        return other != null &&
               string.Equals(
                   Value,
                   other.Value,
                   StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as UiTargetId);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Value);
    }
}
