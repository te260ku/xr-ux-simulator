using System;
using System.Reflection;

public sealed class TimelineCommandExecution
{
    private readonly TimelineCommand _command;
    private readonly object[] _arguments;

    public TimelineCommandExecution(TimelineCommand command, object[] arguments)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));

        if (arguments == null)
            throw new ArgumentNullException(nameof(arguments));

        EnsureArgumentsMatch(arguments);
        _arguments = (object[])arguments.Clone();
    }

    public void Execute()
    {
        _command.Execute(_arguments);
    }

    private void EnsureArgumentsMatch(object[] arguments)
    {
        var parameters = _command.Parameters;

        if (arguments.Length != parameters.Count)
            throw new ArgumentException("Argument count does not match the command.");

        for (var i = 0; i < arguments.Length; i++)
            EnsureArgumentMatches(arguments[i], parameters[i]);
    }

    private void EnsureArgumentMatches(object argument, ParameterInfo parameter)
    {
        if (argument == null)
            throw new ArgumentException($"Argument '{parameter.Name}' must not be null.");

        if (!parameter.ParameterType.IsInstanceOfType(argument))
            throw new ArgumentException($"Argument '{parameter.Name}' has an invalid type.");
    }
}
