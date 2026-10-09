using System;

[AttributeUsage(
    AttributeTargets.Parameter |
    AttributeTargets.Property |
    AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false)]
public sealed class TimelineFilePathAttribute : Attribute
{
}
