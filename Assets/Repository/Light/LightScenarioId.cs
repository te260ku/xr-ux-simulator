using System;

public record LightScenarioId
{
    public string Value { get; }

    public LightScenarioId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "LightScenarioId must not be empty.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}