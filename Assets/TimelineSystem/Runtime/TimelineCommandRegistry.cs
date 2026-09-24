using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VContainer;

namespace TimelineSystem
{
    public sealed class TimelineCommandRegistry
    {
        private readonly IReadOnlyDictionary<string, TimelineCommand> _commands;

        public TimelineCommandRegistry(
            TimelineCommandScanner commandScanner,
            IObjectResolver resolver)
        {
            if (commandScanner == null)
                throw new ArgumentNullException(nameof(commandScanner));

            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            _commands = commandScanner
                .Scan()
                .Select(method => CreateCommand(method, resolver))
                .ToDictionary(
                    command => command.Id,
                    command => command,
                    StringComparer.Ordinal);
        }

        public TimelineCommand Get(string commandId)
        {
            if (_commands.TryGetValue(commandId, out var command))
                return command;

            throw new TimelineLoadException(
                $"Timeline command '{commandId}' was not found.");
        }

        private TimelineCommand CreateCommand(
            MethodInfo method,
            IObjectResolver resolver)
        {
            var attribute =
                method.GetCustomAttribute<TimelineCommandAttribute>();

            var declaringType =
                method.DeclaringType
                ?? throw new InvalidOperationException(
                    $"Declaring type was not found for method '{method.Name}'.");

            var target = resolver.Resolve(declaringType);

            return new TimelineCommand(
                attribute.CommandId,
                method,
                target);
        }
    }
}
