using System;
using System.Collections.Generic;

public sealed class DiagCommandRegistry
{
    private readonly Dictionary<DiagCommandId, DiagCommand>
        _commands = new();

    public void Assign(
        DiagCommandId id,
        TimelineCommandExecution execution)
    {
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        if (execution == null)
            throw new ArgumentNullException(nameof(execution));

        GetOrCreate(id).Add(execution);
    }

    public DiagCommand Get(DiagCommandId id)
    {
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        if (_commands.TryGetValue(id, out var command))
            return command;

        throw new KeyNotFoundException(
            $"Diag command '{id}' is not registered.");
    }

    public void Clear()
    {
        _commands.Clear();
    }

    private DiagCommand GetOrCreate(DiagCommandId id)
    {
        if (_commands.TryGetValue(id, out var command))
            return command;

        command = new DiagCommand(id);
        _commands.Add(id, command);

        return command;
    }
}
