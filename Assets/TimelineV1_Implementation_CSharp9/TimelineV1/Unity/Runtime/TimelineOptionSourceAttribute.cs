using System;

namespace App.Timeline
{
    [AttributeUsage(
        AttributeTargets.Struct |
        AttributeTargets.Class |
        AttributeTargets.Parameter |
        AttributeTargets.Property |
        AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class TimelineOptionSourceAttribute : Attribute
    {
        public string Source { get; }

        public TimelineOptionSourceAttribute(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Option source must not be empty.", nameof(source));

            Source = source;
        }
    }
}
