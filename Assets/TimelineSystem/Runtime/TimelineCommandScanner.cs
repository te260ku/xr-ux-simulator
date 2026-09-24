using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TimelineSystem
{
    public sealed class TimelineCommandScanner
    {
        public IReadOnlyList<MethodInfo> Scan()
        {
            var commands = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in GetTypes(assembly))
                {
                    var methods = type.GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);

                    foreach (var method in methods)
                    {
                        var attribute =
                            method.GetCustomAttribute<TimelineCommandAttribute>();

                        if (attribute == null)
                            continue;

                        ValidateCommandMethod(method, attribute);

                        if (commands.ContainsKey(attribute.CommandId))
                        {
                            var existing = commands[attribute.CommandId];

                            throw new InvalidOperationException(
                                $"Timeline command ID '{attribute.CommandId}' is duplicated. " +
                                $"Methods: '{existing.DeclaringType?.FullName}.{existing.Name}' and " +
                                $"'{method.DeclaringType?.FullName}.{method.Name}'.");
                        }

                        commands.Add(attribute.CommandId, method);
                    }
                }
            }

            return commands.Values.ToArray();
        }

        private void ValidateCommandMethod(
            MethodInfo method,
            TimelineCommandAttribute attribute)
        {
            if (string.IsNullOrWhiteSpace(attribute.CommandId))
            {
                throw new InvalidOperationException(
                    $"Timeline command ID is empty: " +
                    $"'{method.DeclaringType?.FullName}.{method.Name}'.");
            }

            if (!method.IsPublic)
            {
                throw new InvalidOperationException(
                    $"Timeline command '{attribute.CommandId}' must be public.");
            }

            if (method.IsStatic)
            {
                throw new InvalidOperationException(
                    $"Timeline command '{attribute.CommandId}' must be an instance method.");
            }

            if (method.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Timeline command '{attribute.CommandId}' must not be abstract.");
            }

            if (method.ReturnType != typeof(void))
            {
                throw new InvalidOperationException(
                    $"Timeline command '{attribute.CommandId}' must return void.");
            }

            if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    $"Timeline command '{attribute.CommandId}' must not be generic.");
            }

            foreach (var parameter in method.GetParameters())
            {
                if (parameter.IsOut || parameter.ParameterType.IsByRef)
                {
                    throw new InvalidOperationException(
                        $"Timeline command '{attribute.CommandId}' must not use ref/out parameters.");
                }

                if (parameter.IsOptional)
                {
                    throw new InvalidOperationException(
                        $"Timeline command '{attribute.CommandId}' must not use optional parameters.");
                }
            }
        }

        private IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types
                    .Where(type => type != null)
                    .Cast<Type>();
            }
        }
    }
}
