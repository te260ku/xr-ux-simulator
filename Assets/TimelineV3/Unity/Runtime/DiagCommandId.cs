using System;
using Newtonsoft.Json;

public sealed class DiagCommandId : IEquatable<DiagCommandId>
{
    public int Value { get; }

    [JsonConstructor]
    public DiagCommandId(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));

        Value = value;
    }

    public bool Equals(DiagCommandId other)
    {
        return other != null && Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as DiagCommandId);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
