using System;
using System.Collections.Generic;

public sealed class DiagCommand
{
    private readonly List<TimelineCommandExecution>
        _executions = new();

    public DiagCommandId Id { get; }

    public DiagCommand(DiagCommandId id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }

    public void Add(TimelineCommandExecution execution)
    {
        if (execution == null)
            throw new ArgumentNullException(nameof(execution));

        _executions.Add(execution);
    }

    public void Execute()
    {
        var count = _executions.Count;

        for (var i = 0; i < count; i++)
            _executions[i].Execute();
    }
}
