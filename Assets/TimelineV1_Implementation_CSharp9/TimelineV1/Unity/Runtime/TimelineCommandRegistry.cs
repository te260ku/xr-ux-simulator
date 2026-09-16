using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VContainer;

namespace App.Timeline
{
    public sealed class TimelineCommandRegistry
    {
        private readonly Dictionary<string, TimelineCommandDescriptor> commands;

        public IReadOnlyCollection<TimelineCommandDescriptor> Commands => commands.Values;

        public TimelineCommandRegistry(
            TimelineCommandScanner scanner,
            TimelineCommandSchemaBuilder schemaBuilder,
            IObjectResolver resolver)
        {
            if (scanner == null) throw new ArgumentNullException(nameof(scanner));
            if (schemaBuilder == null) throw new ArgumentNullException(nameof(schemaBuilder));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            commands = new Dictionary<string, TimelineCommandDescriptor>(StringComparer.Ordinal);

            foreach (var method in scanner.Scan())
            {
                ValidateMethod(method);
                var id = TimelineCommandScanner.GetCommandId(method);

                if (commands.ContainsKey(id))
                {
                    var existing = commands[id].Method;
                    throw new InvalidOperationException(
                        $"Duplicate TimelineCommand ID '{id}': " +
                        $"'{existing.DeclaringType?.FullName}.{existing.Name}' and " +
                        $"'{method.DeclaringType?.FullName}.{method.Name}'.");
                }

                object target = null;
                if (!method.IsStatic)
                {
                    var declaringType = method.DeclaringType
                        ?? throw new InvalidOperationException($"Command '{id}' has no declaring type.");

                    try
                    {
                        target = resolver.Resolve(declaringType);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException(
                            $"TimelineCommand target '{declaringType.FullName}' for command '{id}' " +
                            "must be registered in VContainer as its concrete type (or otherwise resolvable by that type).",
                            exception);
                    }
                }

                var schema = schemaBuilder.Build(method);
                commands.Add(id, new TimelineCommandDescriptor(id, target, method, schema));
            }
        }

        public TimelineCommandDescriptor Get(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID must not be empty.", nameof(commandId));

            if (!commands.TryGetValue(commandId.Trim(), out var descriptor))
                throw new KeyNotFoundException($"Unknown TimelineCommand '{commandId}'.");

            return descriptor;
        }

        public bool Contains(string commandId)
        {
            return !string.IsNullOrWhiteSpace(commandId) && commands.ContainsKey(commandId.Trim());
        }

        private static void ValidateMethod(MethodInfo method)
        {
            var id = TimelineCommandScanner.GetCommandId(method);
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("TimelineCommand ID must not be empty.");

            if (method.IsGenericMethodDefinition || method.ContainsGenericParameters ||
                method.DeclaringType?.ContainsGenericParameters == true)
                throw new InvalidOperationException($"TimelineCommand '{id}' cannot be generic.");

            if (method.ReturnType != typeof(void))
                throw new InvalidOperationException($"TimelineCommand '{id}' must return void.");

            if (!method.IsStatic && (method.DeclaringType == null || method.DeclaringType.IsAbstract || method.DeclaringType.IsInterface))
                throw new InvalidOperationException(
                    $"TimelineCommand '{id}' must be declared on a concrete type when it is an instance method.");

            var invalidParameter = method.GetParameters()
                .FirstOrDefault(p => p.ParameterType.IsByRef || p.IsOut);
            if (invalidParameter != null)
            {
                throw new InvalidOperationException(
                    $"TimelineCommand '{id}' cannot use ref/out parameter '{invalidParameter.Name}'.");
            }
        }
    }
}
