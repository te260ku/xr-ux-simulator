using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public sealed class TimelineCommandScanner
{
    public IReadOnlyList<MethodInfo> Scan()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(GetTypes)
            .SelectMany(GetMethods)
            .Where(HasTimelineCommandAttribute)
            .ToArray();
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

    private IEnumerable<MethodInfo> GetMethods(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        return type.GetMethods(flags);
    }

    private bool HasTimelineCommandAttribute(MethodInfo method)
    {
        return method.IsDefined(typeof(TimelineCommandAttribute), false);
    }
}
