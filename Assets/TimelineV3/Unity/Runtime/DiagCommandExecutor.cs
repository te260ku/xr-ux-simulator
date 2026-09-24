using System;

public sealed class DiagCommandExecutor
{
    private readonly DiagCommandRegistry _registry;

    public DiagCommandExecutor(DiagCommandRegistry registry)
    {
        _registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
    }

    public void Execute(DiagCommandId id)
    {
        _registry.Get(id).Execute();
    }
}
