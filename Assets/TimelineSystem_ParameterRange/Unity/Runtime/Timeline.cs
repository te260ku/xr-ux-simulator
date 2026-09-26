using System;
using System.Collections.Generic;

public sealed class Timeline
{
    private readonly TimelineEvent[] _events;

    public int Count => _events.Length;

    public Timeline(IReadOnlyList<TimelineEvent> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        _events = Copy(events);
        EnsureChronologicalOrder();
    }

    public TimelineEvent Get(int index)
    {
        return _events[index];
    }

    private TimelineEvent[] Copy(IReadOnlyList<TimelineEvent> source)
    {
        var result = new TimelineEvent[source.Count];

        for (var i = 0; i < source.Count; i++)
        {
            result[i] = source[i]
                ?? throw new ArgumentException(
                    "Timeline must not contain null events.",
                    nameof(source));
        }

        return result;
    }

    private void EnsureChronologicalOrder()
    {
        for (var i = 1; i < _events.Length; i++)
        {
            if (_events[i].Time.CompareTo(_events[i - 1].Time) < 0)
                throw new ArgumentException("Timeline events must be ordered by time.");
        }
    }
}
