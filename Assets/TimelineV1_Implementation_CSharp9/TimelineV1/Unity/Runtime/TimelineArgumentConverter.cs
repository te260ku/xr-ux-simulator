using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace App.Timeline
{
    public sealed class TimelineArgumentConverter
    {
        public object Convert(string rawValue, Type targetType, bool required, object defaultValue)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            rawValue = rawValue ?? string.Empty;

            var nullableType = Nullable.GetUnderlyingType(targetType);
            var actualType = nullableType ?? targetType;
            var trimmed = rawValue.Trim();

            if (trimmed.Length == 0)
            {
                if (defaultValue != null) return defaultValue;
                if (required)
                    throw new FormatException($"A value is required for type '{targetType.Name}'.");
                if (nullableType != null || !targetType.IsValueType) return null;
                return Activator.CreateInstance(actualType);
            }

            if (actualType == typeof(string)) return rawValue;
            if (actualType == typeof(char))
            {
                if (trimmed.Length != 1) throw new FormatException("Char value must contain exactly one character.");
                return trimmed[0];
            }

            if (actualType == typeof(bool))
            {
                if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) || trimmed == "1") return true;
                if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) || trimmed == "0") return false;
                throw new FormatException($"'{rawValue}' is not a valid Boolean value.");
            }

            if (actualType.IsEnum)
            {
                var normalized = trimmed.Replace("|", ",");
                try
                {
                    return Enum.Parse(actualType, normalized, ignoreCase: true);
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
                    throw new InvalidOperationException(
                        $"Repository key '{actualType.FullName}' must have a public string constructor.");

                return constructor.Invoke(new object[] { rawValue });
            }

            if (actualType == typeof(Guid)) return Guid.Parse(trimmed);
            if (actualType == typeof(DateTime)) return DateTime.Parse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (actualType == typeof(TimeSpan)) return TimeSpan.Parse(trimmed, CultureInfo.InvariantCulture);

            var converter = TypeDescriptor.GetConverter(actualType);
            if (converter.CanConvertFrom(typeof(string)))
            {
                try
                {
                    return converter.ConvertFrom(null, CultureInfo.InvariantCulture, trimmed);
                }
                catch (Exception exception)
                {
                    throw new FormatException(
                        $"'{rawValue}' cannot be converted to '{actualType.Name}'.",
                        exception);
                }
            }

            throw new NotSupportedException(
                $"Timeline argument type '{actualType.FullName}' is not supported as a scalar value.");
        }
    }
}
