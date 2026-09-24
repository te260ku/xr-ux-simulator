using System;

namespace TimelineSystem.Examples
{
    [RepositoryKeySource("Sound")]
    public sealed class SoundId : IRepositoryKey
    {
        public string Value { get; }

        public SoundId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "SoundId must not be empty.",
                    nameof(value));

            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
