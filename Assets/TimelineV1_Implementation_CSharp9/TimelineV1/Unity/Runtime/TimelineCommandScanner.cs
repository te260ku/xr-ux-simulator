using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace App.Timeline
{
    public sealed class TimelineCommandScanner
    {
        public IReadOnlyList<MethodInfo> Scan()
        {
            var methods = new List<MethodInfo>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in GetTypesSafely(assembly))
                {
                    var candidates = type.GetMethods(
                        BindingFlags.Public |
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.DeclaredOnly);

                    foreach (var method in candidates)
                    {
                        if (method.GetCustomAttribute<TimelineCommandAttribute>() != null)
                            methods.Add(method);
                    }
                }
            }

            return methods
                .OrderBy(m => m.DeclaringType?.FullName, StringComparer.Ordinal)
                .ThenBy(m => m.MetadataToken)
                .ToArray();
        }

        public static string GetCommandId(MethodInfo method)
        {
            var attribute = method.GetCustomAttribute<TimelineCommandAttribute>()
                ?? throw new InvalidOperationException(
                    $"Method '{method.DeclaringType?.FullName}.{method.Name}' is not a TimelineCommand.");

            return string.IsNullOrWhiteSpace(attribute.Id)
                ? method.Name
                : attribute.Id.Trim();
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(t => t != null).Cast<Type>();
            }
            catch
            {
                // Some dynamic / platform assemblies cannot enumerate their types.
                return Array.Empty<Type>();
            }
        }
    }
}
