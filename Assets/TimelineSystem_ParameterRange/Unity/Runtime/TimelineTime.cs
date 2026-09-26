using System;

public sealed class TimelineTime : IComparable<TimelineTime>
{
    public float Seconds { get; }

    public TimelineTime(float seconds)
    {
        if (float.IsNaN(seconds) ||
            float.IsInfinity(seconds) ||
            seconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        Seconds = seconds;
    }

    public int CompareTo(TimelineTime other)
    {
        if (other == null)
            return 1;

        return Seconds.CompareTo(other.Seconds);
    }
}
