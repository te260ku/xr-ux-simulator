using System;

public sealed class TimelineEvent
{
    private readonly TimelineCommandExecution _execution;

    public TimelineTime Time { get; }

    public TimelineEvent(TimelineTime time, TimelineCommandExecution execution)
    {
        Time = time ?? throw new ArgumentNullException(nameof(time));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
    }

    public void Execute()
    {
        _execution.Execute();
    }
}
