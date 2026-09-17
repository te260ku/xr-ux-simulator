using System;
using System.Collections.Generic;

namespace App.Timeline.V2
{
    /// <summary>
    /// Ordered list of timeline events ready for playback.
    /// </summary>
    public sealed class TimelineScenario
    {
        public IReadOnlyList<TimelineEvent> Events { get; }

        public TimelineScenario(IReadOnlyList<TimelineEvent> events)
        {
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }
    }
}
