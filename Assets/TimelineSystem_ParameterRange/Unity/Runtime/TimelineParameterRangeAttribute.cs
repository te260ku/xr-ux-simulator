using System;

[AttributeUsage(
    AttributeTargets.Parameter |
    AttributeTargets.Field |
    AttributeTargets.Property,
    AllowMultiple = false,
    Inherited = false)]
public sealed class TimelineParameterRangeAttribute : Attribute
{
    public double Min { get; }
    public double Max { get; }

    public TimelineParameterRangeAttribute(
        double min,
        double max)
    {
        if (double.IsNaN(min) ||
            double.IsInfinity(min))
        {
            throw new ArgumentOutOfRangeException(
                nameof(min));
        }

        if (double.IsNaN(max) ||
            double.IsInfinity(max))
        {
            throw new ArgumentOutOfRangeException(
                nameof(max));
        }

        if (min > max)
        {
            throw new ArgumentException(
                "Min must be less than or equal to Max.");
        }

        Min = min;
        Max = max;
    }
}
