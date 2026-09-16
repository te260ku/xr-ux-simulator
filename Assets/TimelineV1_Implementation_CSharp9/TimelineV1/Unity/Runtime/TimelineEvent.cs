using System;

namespace App.Timeline
{
    public sealed class TimelineEvent
    {
        public double Time { get; }
        public TimelineCommandInvocation Command { get; }

        public TimelineEvent(double time, TimelineCommandInvocation command)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0)
                throw new ArgumentOutOfRangeException(nameof(time), "Time must be a finite value greater than or equal to zero.");

            Time = time;
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }
    }
}
