using System;

namespace App.Timeline.V2
{
    /// <summary>
    /// Specifies the repository CSV used to populate choices in the dedicated editor.
    /// Usually applied to an IRepositoryKey implementation.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Parameter |
        AttributeTargets.Property |
        AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class TimelineOptionSourceAttribute : Attribute
    {
        public string SourceName { get; }

        public TimelineOptionSourceAttribute(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                throw new ArgumentException("Source name must not be empty.", nameof(sourceName));

            SourceName = sourceName;
        }
    }
}
