using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VContainer;

namespace App.Timeline.V2
{
    /// <summary>
    /// Finds all [TimelineCommand] methods in loaded assemblies at startup,
    /// resolves their target instances from VContainer, and stores them by command ID.
    /// </summary>
    public sealed class TimelineCommandRegistry
    {
        private readonly Dictionary<string, TimelineCommandInfo> commands;

        public IReadOnlyCollection<TimelineCommandInfo> Commands => commands.Values;

        public TimelineCommandRegistry(IObjectResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            commands = new Dictionary<string, TimelineCommandInfo>(StringComparer.Ordinal);

            foreach (var method in FindCommandMethods())
            {
                ValidateCommandMethod(method);
                var commandId = GetCommandId(method);

                if (commands.TryGetValue(commandId, out var existing))
                {
                    throw new InvalidOperationException(
                        $"Duplicate TimelineCommand ID '{commandId}': " +
                        $"'{GetMethodName(existing.Method)}' and '{GetMethodName(method)}'.");
                }

                object target = null;
                if (!method.IsStatic)
                {
                    var declaringType = method.DeclaringType
                        ?? throw new InvalidOperationException($"Command '{commandId}' has no declaring type.");

                    try
                    {
                        target = resolver.Resolve(declaringType);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException(
                            $"Timeline command target '{declaringType.FullName}' for '{commandId}' " +
                            "must be registered in VContainer as a resolvable concrete type.",
                            exception);
                    }
                }

                commands.Add(commandId, new TimelineCommandInfo(commandId, target, method));
            }
        }

        public TimelineCommandInfo Get(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command ID must not be empty.", nameof(commandId));

            var normalizedId = commandId.Trim();
            if (!commands.TryGetValue(normalizedId, out var command))
                throw new KeyNotFoundException($"Unknown TimelineCommand '{normalizedId}'.");

            return command;
        }

        public bool Contains(string commandId)
        {
            return !string.IsNullOrWhiteSpace(commandId) && commands.ContainsKey(commandId.Trim());
        }

        /// <summary>
        /// Used by both runtime registration and the editor CSV exporter.
        /// </summary>
        public static IReadOnlyList<MethodInfo> FindCommandMethods()
        {
            var result = new List<MethodInfo>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in GetTypesSafely(assembly))
                {
                    var methods = type.GetMethods(
                        BindingFlags.Public |
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.DeclaredOnly);

                    foreach (var method in methods)
                    {
                        if (method.GetCustomAttribute<TimelineCommandAttribute>() != null)
                            result.Add(method);
                    }
                }
            }

            return result
                .OrderBy(GetCommandId, StringComparer.Ordinal)
                .ThenBy(GetMethodName, StringComparer.Ordinal)
                .ToArray();
        }

        public static string GetCommandId(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var attribute = method.GetCustomAttribute<TimelineCommandAttribute>();
            if (attribute == null)
                throw new InvalidOperationException($"Method '{GetMethodName(method)}' is not a TimelineCommand.");

            return string.IsNullOrWhiteSpace(attribute.Id)
                ? method.Name
                : attribute.Id.Trim();
        }

        public static void ValidateCommandMethod(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var commandId = GetCommandId(method);

            if (method.ReturnType != typeof(void))
                throw new InvalidOperationException($"TimelineCommand '{commandId}' must return void.");

            if (method.IsGenericMethodDefinition ||
                method.ContainsGenericParameters ||
                method.DeclaringType?.ContainsGenericParameters == true)
            {
                throw new InvalidOperationException($"TimelineCommand '{commandId}' cannot be generic.");
            }

            if (!method.IsStatic &&
                (method.DeclaringType == null || method.DeclaringType.IsAbstract || method.DeclaringType.IsInterface))
            {
                throw new InvalidOperationException(
                    $"TimelineCommand '{commandId}' must be declared on a concrete type.");
            }

            foreach (var parameter in method.GetParameters())
            {
                if (parameter.IsOut || parameter.ParameterType.IsByRef)
                {
                    throw new InvalidOperationException(
                        $"TimelineCommand '{commandId}' cannot use ref/out parameter '{parameter.Name}'.");
                }

                TimelineTypeUtility.ValidateCommandParameterType(parameter.ParameterType);
            }
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null).Cast<Type>();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static string GetMethodName(MethodInfo method)
        {
            return (method.DeclaringType?.FullName ?? "<unknown>") + "." + method.Name;
        }
    }
}
