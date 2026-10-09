using System;

public sealed class TimelineCommandId : IEquatable<TimelineCommandId>
{
    public string Value { get; }

    public TimelineCommandId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Command ID must not be empty.", nameof(value));

        Value = value;
    }

    public bool Equals(TimelineCommandId other)
    {
        return other != null &&
               string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TimelineCommandId);
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
