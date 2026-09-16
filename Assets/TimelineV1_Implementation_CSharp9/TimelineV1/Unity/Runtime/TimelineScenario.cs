using System;
using System.Collections.Generic;

namespace App.Timeline
{
    public sealed class TimelineScenario
    {
        public IReadOnlyList<TimelineEvent> Events { get; }

        public TimelineScenario(IReadOnlyList<TimelineEvent> events)
        {
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }
    }
}
