using System;

public sealed class TimelineFilePath
{
    public string Value { get; }

    public TimelineFilePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Timeline file path must not be empty.", nameof(value));

        Value = value;
    }
}
