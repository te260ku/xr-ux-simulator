using System;

public record SoundId
{
    public string Value { get; }

    public SoundId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "SoundId must not be empty.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}