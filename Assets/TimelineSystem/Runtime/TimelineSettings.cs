using System;

namespace TimelineSystem
{
    public sealed class TimelineSettings
    {
        public string FilePath { get; }

        public TimelineSettings(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "Timeline file path must not be empty.",
                    nameof(filePath));

            FilePath = filePath;
        }
    }
}
