using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace App.Timeline.V2
{
    /// <summary>
    /// Centralizes the reflection rules used by both runtime argument conversion
    /// and command-definition CSV export.
    /// </summary>
    public static class TimelineTypeUtility
    {
        public static IReadOnlyList<TimelineArgumentDefinition> GetFlatArgumentDefinitions(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));

            var definitions = new List<TimelineArgumentDefinition>();
            var typeStack = new HashSet<Type>();

            foreach (var parameter in method.GetParameters())
            {
                var rootName = parameter.Name ?? $"arg{parameter.Position + 1}";
                AppendFlatDefinitions(
                    parameter.ParameterType,
                    rootName,
                    string.Empty,
                    parameter,
                    null,
                    !parameter.HasDefaultValue,
                    GetDefaultValue(parameter),
                    definitions,
                    typeStack);
            }

            return definitions;
        }

        public static void ValidateCommandParameterType(Type type)
        {
            ValidateType(type, new HashSet<Type>());
        }

        public static bool IsSimpleValue(Type type)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType == typeof(string) ||
                actualType == typeof(char) ||
                actualType == typeof(bool) ||
                actualType.IsEnum ||
                typeof(IRepositoryKey).IsAssignableFrom(actualType))
                return true;

            switch (Type.GetTypeCode(actualType))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        public static object ConvertSimpleValue(
            string rawValue,
            Type targetType,
            bool required,
            object defaultValue)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            rawValue = rawValue ?? string.Empty;
            var trimmed = rawValue.Trim();
            var nullableType = Nullable.GetUnderlyingType(targetType);
            var actualType = nullableType ?? targetType;

            if (trimmed.Length == 0)
            {
                if (defaultValue != null && defaultValue != DBNull.Value && defaultValue != Type.Missing)
                    return defaultValue;

                if (!required)
                {
                    if (nullableType != null || !targetType.IsValueType)
                        return null;

                    return Activator.CreateInstance(actualType);
                }

                throw new FormatException($"A value is required for '{targetType.Name}'.");
            }

            if (actualType == typeof(string))
                return rawValue;

            if (actualType == typeof(char))
            {
                if (trimmed.Length != 1)
                    throw new FormatException($"'{rawValue}' is not a valid Char value.");
                return trimmed[0];
            }

            if (actualType == typeof(bool))
            {
                if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) || trimmed == "1")
                    return true;
                if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) || trimmed == "0")
                    return false;

                throw new FormatException($"'{rawValue}' is not a valid Boolean value.");
            }

            if (actualType.IsEnum)
            {
                var normalized = trimmed.Replace("|", ",");
                try
                {
                    return Enum.Parse(actualType, normalized, true);
                }
                catch (Exception exception)
                {
                    throw new FormatException(
                        $"'{rawValue}' is not a valid value for enum '{actualType.Name}'.",
                        exception);
                }
            }

            if (typeof(IRepositoryKey).IsAssignableFrom(actualType))
            {
                var constructor = actualType.GetConstructor(new[] { typeof(string) });
                if (constructor == null)
                {
                    throw new InvalidOperationException(
                        $"Repository key '{actualType.FullName}' must have a public constructor with one string argument.");
                }

                return constructor.Invoke(new object[] { trimmed });
            }

            try
            {
                return Convert.ChangeType(trimmed, actualType, CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                throw new FormatException(
                    $"'{rawValue}' cannot be converted to '{actualType.Name}'.",
                    exception);
            }
        }

        public static ConstructorInfo GetBindingConstructor(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var parameterizedConstructors = type
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(constructor => constructor.GetParameters().Length > 0)
                .ToArray();

            if (parameterizedConstructors.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Timeline argument type '{type.FullName}' has multiple public constructors with parameters. " +
                    "Keep one public parameterized constructor, or use a parameterless constructor with writable members.");
            }

            return parameterizedConstructors.Length == 1
                ? parameterizedConstructors[0]
                : null;
        }

        public static IReadOnlyList<MemberInfo> GetWritableMembers(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var members = new List<MemberInfo>();

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length == 0 &&
                    property.SetMethod != null &&
                    property.SetMethod.IsPublic)
                {
                    members.Add(property);
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!field.IsInitOnly && !field.IsLiteral)
                    members.Add(field);
            }

            return members
                .OrderBy(member => member.MetadataToken)
                .ToArray();
        }

        public static MemberInfo FindMatchingMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
                return null;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length == 0 &&
                    string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
                    return field;
            }

            return null;
        }

        public static Type GetMemberType(MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property != null) return property.PropertyType;

            var field = member as FieldInfo;
            if (field != null) return field.FieldType;

            throw new ArgumentException("Only properties and fields are supported.", nameof(member));
        }

        public static void SetMemberValue(object target, MemberInfo member, object value)
        {
            var property = member as PropertyInfo;
            if (property != null)
            {
                property.SetValue(target, value);
                return;
            }

            var field = member as FieldInfo;
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            throw new ArgumentException("Only properties and fields are supported.", nameof(member));
        }

        public static string GetOptionSource(ParameterInfo parameter, MemberInfo member, Type valueType)
        {
            var parameterAttribute = parameter?.GetCustomAttribute<TimelineOptionSourceAttribute>();
            if (parameterAttribute != null)
                return parameterAttribute.SourceName;

            var memberAttribute = member?.GetCustomAttribute<TimelineOptionSourceAttribute>();
            if (memberAttribute != null)
                return memberAttribute.SourceName;

            var actualType = Nullable.GetUnderlyingType(valueType) ?? valueType;
            var typeAttribute = actualType.GetCustomAttribute<TimelineOptionSourceAttribute>();
            return typeAttribute?.SourceName ?? string.Empty;
        }

        public static string GetEditorTypeName(TimelineArgumentDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.IsCommand) return "Command";

            var actualType = Nullable.GetUnderlyingType(definition.ValueType) ?? definition.ValueType;

            if (typeof(IRepositoryKey).IsAssignableFrom(actualType)) return "RepositoryKey";
            if (actualType.IsEnum) return "Enum";
            if (actualType == typeof(bool)) return "Bool";

            switch (Type.GetTypeCode(actualType))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return "Int";
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return "Float";
                default:
                    return "String";
            }
        }

        public static string GetEnumOptions(Type type)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;
            if (!actualType.IsEnum) return string.Empty;

            var isFlags = actualType.IsDefined(typeof(FlagsAttribute), false);
            var names = Enum.GetNames(actualType)
                .Where(name => !isFlags || !string.Equals(name, "None", StringComparison.Ordinal));
            return string.Join("|", names);
        }

        public static bool IsMultiSelectEnum(Type type)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;
            return actualType.IsEnum && actualType.IsDefined(typeof(FlagsAttribute), false);
        }

        public static string FormatDefaultValue(object value)
        {
            if (value == null || value == DBNull.Value || value == Type.Missing)
                return string.Empty;

            if (value is bool boolean)
                return boolean ? "true" : "false";

            if (value is Enum enumValue)
                return enumValue.ToString().Replace(", ", "|");

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static void AppendFlatDefinitions(
            Type type,
            string rootArgumentName,
            string memberPath,
            ParameterInfo parameter,
            MemberInfo member,
            bool required,
            object defaultValue,
            ICollection<TimelineArgumentDefinition> definitions,
            ISet<Type> typeStack)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType == typeof(TimelineCommandCall))
            {
                definitions.Add(new TimelineArgumentDefinition(
                    rootArgumentName,
                    memberPath,
                    actualType,
                    required,
                    defaultValue,
                    string.Empty,
                    true));
                return;
            }

            if (IsSimpleValue(type))
            {
                ValidateSimpleType(actualType);
                definitions.Add(new TimelineArgumentDefinition(
                    rootArgumentName,
                    memberPath,
                    type,
                    required,
                    defaultValue,
                    GetOptionSource(parameter, member, type),
                    false));
                return;
            }

            ValidateComplexType(actualType);

            if (!typeStack.Add(actualType))
            {
                throw new InvalidOperationException(
                    $"Circular timeline argument type detected at '{actualType.FullName}'.");
            }

            try
            {
                var constructor = GetBindingConstructor(actualType);
                if (constructor != null)
                {
                    foreach (var childParameter in constructor.GetParameters())
                    {
                        var matchingMember = FindMatchingMember(actualType, childParameter.Name);
                        var childName = matchingMember?.Name ?? childParameter.Name ?? $"arg{childParameter.Position + 1}";
                        var childPath = JoinPath(memberPath, childName);

                        AppendFlatDefinitions(
                            childParameter.ParameterType,
                            rootArgumentName,
                            childPath,
                            childParameter,
                            matchingMember,
                            required && !childParameter.HasDefaultValue,
                            GetDefaultValue(childParameter),
                            definitions,
                            typeStack);
                    }

                    return;
                }

                var writableMembers = GetWritableMembers(actualType);
                if (writableMembers.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Timeline argument type '{actualType.FullName}' has no bindable constructor or writable public members.");
                }

                foreach (var childMember in writableMembers)
                {
                    AppendFlatDefinitions(
                        GetMemberType(childMember),
                        rootArgumentName,
                        JoinPath(memberPath, childMember.Name),
                        null,
                        childMember,
                        required,
                        null,
                        definitions,
                        typeStack);
                }
            }
            finally
            {
                typeStack.Remove(actualType);
            }
        }

        private static void ValidateType(Type type, ISet<Type> typeStack)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType == typeof(TimelineCommandCall))
                return;

            if (IsSimpleValue(type))
            {
                ValidateSimpleType(actualType);
                return;
            }

            ValidateComplexType(actualType);

            if (!typeStack.Add(actualType))
                throw new InvalidOperationException($"Circular timeline argument type detected at '{actualType.FullName}'.");

            try
            {
                var constructor = GetBindingConstructor(actualType);
                if (constructor != null)
                {
                    foreach (var parameter in constructor.GetParameters())
                        ValidateType(parameter.ParameterType, typeStack);
                    return;
                }

                var writableMembers = GetWritableMembers(actualType);
                if (writableMembers.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Timeline argument type '{actualType.FullName}' must have one public parameterized constructor " +
                        "or a public parameterless constructor with writable public members.");
                }

                foreach (var member in writableMembers)
                    ValidateType(GetMemberType(member), typeStack);
            }
            finally
            {
                typeStack.Remove(actualType);
            }
        }

        private static void ValidateSimpleType(Type actualType)
        {
            if (!typeof(IRepositoryKey).IsAssignableFrom(actualType))
                return;

            if (actualType.GetConstructor(new[] { typeof(string) }) == null)
            {
                throw new InvalidOperationException(
                    $"Repository key '{actualType.FullName}' must have a public constructor with one string argument.");
            }
        }

        private static void ValidateComplexType(Type actualType)
        {
            if (actualType.IsArray ||
                actualType.IsPointer ||
                actualType.IsByRef ||
                actualType.IsInterface ||
                actualType.IsAbstract)
            {
                throw new NotSupportedException(
                    $"Timeline argument type '{actualType.FullName}' is not supported.");
            }

            if (actualType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(actualType))
            {
                throw new NotSupportedException(
                    $"Collection type '{actualType.FullName}' is not supported as a timeline argument. " +
                    "Use a dedicated class with explicit members instead.");
            }

            if (!actualType.IsValueType && actualType.GetConstructor(Type.EmptyTypes) == null)
            {
                var constructor = GetBindingConstructor(actualType);
                if (constructor == null)
                {
                    throw new InvalidOperationException(
                        $"Timeline argument type '{actualType.FullName}' has no usable public constructor.");
                }
            }
        }

        private static object GetDefaultValue(ParameterInfo parameter)
        {
            return parameter.HasDefaultValue ? parameter.DefaultValue : null;
        }

        private static string JoinPath(string parent, string child)
        {
            return string.IsNullOrEmpty(parent) ? child : parent + "." + child;
        }
    }
}
