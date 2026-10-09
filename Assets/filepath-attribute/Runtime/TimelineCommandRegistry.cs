using System;
using System.Collections.Generic;
using System.Reflection;
using VContainer;

public sealed class TimelineCommandRegistry
{
    private readonly Dictionary<TimelineCommandId, TimelineCommand> _commands = new();

    public TimelineCommandRegistry(
        TimelineCommandScanner scanner,
        IObjectResolver resolver)
    {
        if (scanner == null)
            throw new ArgumentNullException(nameof(scanner));

        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));

        RegisterAll(scanner.Scan(), resolver);
    }

    public TimelineCommand Get(TimelineCommandId id)
    {
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        if (_commands.TryGetValue(id, out var command))
            return command;

        throw new KeyNotFoundException(
            $"Timeline command '{id}' is not registered.");
    }

    private void RegisterAll(
        IReadOnlyList<MethodInfo> methods,
        IObjectResolver resolver)
    {
        foreach (var method in methods)
            Register(method, resolver);
    }

    private void Register(MethodInfo method, IObjectResolver resolver)
    {
        var declaringType = method.DeclaringType
            ?? throw new InvalidOperationException(
                "Command declaring type was not found.");

        var target = resolver.Resolve(declaringType);
        var command = new TimelineCommand(method, target);

        if (_commands.ContainsKey(command.Id))
            throw new InvalidOperationException(
                $"Timeline command '{command.Id}' is duplicated.");

        _commands.Add(command.Id, command);
    }
}
