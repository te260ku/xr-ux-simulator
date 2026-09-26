using System;

public sealed class DiagCommandAssigner
{
    private readonly DiagCommandRegistry _registry;

    public DiagCommandAssigner(DiagCommandRegistry registry)
    {
        _registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
    }

    [TimelineCommand("SetDiagCommand")]
    public void Assign(
        DiagCommandId id,
        TimelineCommandExecution execution)
    {
        _registry.Assign(id, execution);
    }
}
