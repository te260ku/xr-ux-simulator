using System;

[AttributeUsage(
    AttributeTargets.Parameter |
    AttributeTargets.Field |
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false)]
public sealed class TimelineParameterDescriptionAttribute : Attribute
{
    public string Description { get; }

    public TimelineParameterDescriptionAttribute(
        string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                "Description must not be empty.",
                nameof(description));

        Description = description;
    }
}
