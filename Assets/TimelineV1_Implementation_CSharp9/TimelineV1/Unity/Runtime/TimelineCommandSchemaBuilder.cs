using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace App.Timeline
{
    public sealed class TimelineCommandSchemaBuilder
    {
        public TimelineCommandSchema Build(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var commandId = TimelineCommandScanner.GetCommandId(method);
            var stack = new HashSet<Type>();
            var parameters = method.GetParameters()
                .Select(parameter => BuildFromParameter(
                    parameter,
                    parameter.Name ?? $"arg{parameter.Position + 1}",
                    string.Empty,
                    stack,
                    bindingMember: null))
                .ToArray();

            return new TimelineCommandSchema(commandId, parameters);
        }

        private TimelineParameterSchema BuildFromParameter(
            ParameterInfo parameter,
            string name,
            string memberPath,
            HashSet<Type> stack,
            MemberInfo bindingMember)
        {
            var required = !parameter.HasDefaultValue && Nullable.GetUnderlyingType(parameter.ParameterType) == null;
            var defaultValue = parameter.HasDefaultValue ? NormalizeDefault(parameter.DefaultValue) : null;
            var optionSource = FindOptionSource(parameter, bindingMember, parameter.ParameterType);

            return BuildCore(
                name,
                memberPath,
                parameter.ParameterType,
                required,
                defaultValue,
                optionSource,
                stack,
                bindingMember);
        }

        private TimelineParameterSchema BuildFromMember(
            MemberInfo member,
            Type memberType,
            string name,
            string memberPath,
            HashSet<Type> stack)
        {
            var optionSource = FindOptionSource(parameter: null, member, memberType);
            return BuildCore(
                name,
                memberPath,
                memberType,
                required: Nullable.GetUnderlyingType(memberType) == null,
                defaultValue: null,
                optionSource,
                stack,
                member);
        }

        private TimelineParameterSchema BuildCore(
            string name,
            string memberPath,
            Type type,
            bool required,
            object defaultValue,
            string optionSource,
            HashSet<Type> stack,
            MemberInfo bindingMember)
        {
            if (type == typeof(TimelineCommandInvocation))
            {
                return new TimelineParameterSchema(
                    name,
                    memberPath,
                    type,
                    TimelineParameterKind.Command,
                    required,
                    defaultValue,
                    optionSource,
                    bindingMember: bindingMember);
            }

            if (IsScalar(type))
            {
                ValidateScalarType(type);
                return new TimelineParameterSchema(
                    name,
                    memberPath,
                    type,
                    TimelineParameterKind.Scalar,
                    required,
                    defaultValue,
                    optionSource,
                    bindingMember: bindingMember);
            }

            var actualType = Nullable.GetUnderlyingType(type) ?? type;
            if (!stack.Add(actualType))
            {
                throw new InvalidOperationException(
                    $"Circular timeline parameter type detected: {actualType.FullName}");
            }

            try
            {
                var constructor = SelectConstructor(actualType);
                IReadOnlyList<TimelineParameterSchema> children;

                if (constructor != null && constructor.GetParameters().Length > 0)
                {
                    children = BuildConstructorChildren(constructor, memberPath, stack, actualType);
                }
                else
                {
                    children = BuildWritableMemberChildren(actualType, memberPath, stack);
                    if (children.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Timeline parameter type '{actualType.FullName}' cannot be constructed. " +
                            "Provide one public constructor with parameters, or a public parameterless " +
                            "constructor with writable public properties/fields.");
                    }
                }

                return new TimelineParameterSchema(
                    name,
                    memberPath,
                    type,
                    TimelineParameterKind.Complex,
                    required,
                    defaultValue,
                    optionSource,
                    children,
                    constructor,
                    bindingMember);
            }
            finally
            {
                stack.Remove(actualType);
            }
        }

        private IReadOnlyList<TimelineParameterSchema> BuildConstructorChildren(
            ConstructorInfo constructor,
            string parentPath,
            HashSet<Type> stack,
            Type declaringType)
        {
            var members = GetReadableMembers(declaringType)
                .ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

            var result = new List<TimelineParameterSchema>();
            foreach (var parameter in constructor.GetParameters())
            {
                var name = parameter.Name ?? $"arg{parameter.Position + 1}";
                members.TryGetValue(name, out var correspondingMember);
                var path = JoinPath(parentPath, name);
                result.Add(BuildFromParameter(
                    parameter,
                    name,
                    path,
                    stack,
                    correspondingMember));
            }

            return result;
        }

        private IReadOnlyList<TimelineParameterSchema> BuildWritableMemberChildren(
            Type type,
            string parentPath,
            HashSet<Type> stack)
        {
            var result = new List<TimelineParameterSchema>();

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0)
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                result.Add(BuildFromMember(
                    property,
                    property.PropertyType,
                    property.Name,
                    JoinPath(parentPath, property.Name),
                    stack));
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                         .Where(f => !f.IsInitOnly)
                         .OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                result.Add(BuildFromMember(
                    field,
                    field.FieldType,
                    field.Name,
                    JoinPath(parentPath, field.Name),
                    stack));
            }

            return result;
        }

        private static ConstructorInfo SelectConstructor(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(c => c.GetParameters().Length > 0)
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            if (constructors.Length == 0)
            {
                var parameterless = type.GetConstructor(Type.EmptyTypes);
                if (parameterless != null || type.IsValueType)
                    return parameterless;
                return null;
            }

            var maxParameterCount = constructors[0].GetParameters().Length;
            if (constructors.Count(c => c.GetParameters().Length == maxParameterCount) > 1)
            {
                throw new InvalidOperationException(
                    $"Timeline parameter type '{type.FullName}' has multiple equally suitable public constructors. " +
                    "Leave one unambiguous constructor for timeline binding.");
            }

            return constructors[0];
        }

        private static IEnumerable<MemberInfo> GetReadableMembers(Type type)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                    yield return property;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                yield return field;
        }

        private static string FindOptionSource(
            ParameterInfo parameter,
            MemberInfo member,
            Type type)
        {
            var fromParameter = parameter?.GetCustomAttribute<TimelineOptionSourceAttribute>();
            if (fromParameter != null) return fromParameter.Source;

            var fromMember = member?.GetCustomAttribute<TimelineOptionSourceAttribute>();
            if (fromMember != null) return fromMember.Source;

            return type.GetCustomAttribute<TimelineOptionSourceAttribute>()?.Source ?? string.Empty;
        }

        public static bool IsScalar(Type type)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType == typeof(string) ||
                actualType == typeof(char) ||
                actualType == typeof(bool) ||
                actualType.IsEnum ||
                typeof(IRepositoryKey).IsAssignableFrom(actualType))
                return true;

            if (actualType.IsPrimitive ||
                actualType == typeof(decimal) ||
                actualType == typeof(Guid) ||
                actualType == typeof(DateTime) ||
                actualType == typeof(TimeSpan))
                return true;

            var converter = TypeDescriptor.GetConverter(actualType);
            return converter.CanConvertFrom(typeof(string));
        }

        private static void ValidateScalarType(Type type)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;
            if (!typeof(IRepositoryKey).IsAssignableFrom(actualType)) return;

            if (actualType.GetConstructor(new[] { typeof(string) }) == null)
            {
                throw new InvalidOperationException(
                    $"Repository key '{actualType.FullName}' must have a public constructor with one string argument.");
            }
        }


        private static object NormalizeDefault(object value)
        {
            return value == DBNull.Value || value == Type.Missing ? null : value;
        }

        private static string JoinPath(string parent, string child)
        {
            return string.IsNullOrEmpty(parent) ? child : $"{parent}.{child}";
        }
    }
}
