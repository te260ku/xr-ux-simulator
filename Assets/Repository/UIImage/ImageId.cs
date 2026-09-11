using System;

public record ImageId
{
    public string Value { get; }

    public ImageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "ImageId must not be empty.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}