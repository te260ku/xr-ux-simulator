using System;
using System.Collections.Generic;
using System.Reflection;

namespace App.Timeline.V2
{
    /// <summary>
    /// Converts flat CSV argument strings to the method parameter types and invokes the command.
    /// Complex classes are reconstructed recursively without JSON.
    /// </summary>
    public sealed class TimelineCommandInvoker
    {
        private readonly TimelineCommandRegistry commandRegistry;

        public TimelineCommandInvoker(TimelineCommandRegistry commandRegistry)
        {
            this.commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        }

        public void Invoke(TimelineEvent timelineEvent)
        {
            if (timelineEvent == null) throw new ArgumentNullException(nameof(timelineEvent));

            try
            {
                var command = commandRegistry.Get(timelineEvent.CommandId);
                var argumentIndex = 0;
                var convertedArguments = ReadMethodArguments(
                    command.Method,
                    timelineEvent.Arguments,
                    ref argumentIndex);

                EnsureNoUnexpectedArguments(
                    timelineEvent.CommandId,
                    timelineEvent.Arguments,
                    argumentIndex);

                InvokeMethod(command, convertedArguments);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                throw new InvalidOperationException(
                    $"Timeline CSV row {timelineEvent.SourceRowNumber}, command '{timelineEvent.CommandId}': {exception.Message}",
                    exception);
            }
        }

        /// <summary>
        /// Executes a command value previously supplied as a TimelineCommandCall argument.
        /// </summary>
        public void Invoke(TimelineCommandCall commandCall)
        {
            if (commandCall == null) throw new ArgumentNullException(nameof(commandCall));

            var command = commandRegistry.Get(commandCall.CommandId);
            if (command.Method.GetParameters().Length != commandCall.Arguments.Count)
            {
                throw new InvalidOperationException(
                    $"Command '{commandCall.CommandId}' expects {command.Method.GetParameters().Length} arguments, " +
                    $"but the command call contains {commandCall.Arguments.Count}.");
            }

            var arguments = new object[commandCall.Arguments.Count];
            for (var index = 0; index < arguments.Length; index++)
                arguments[index] = commandCall.Arguments[index];

            InvokeMethod(command, arguments);
        }

        private object[] ReadMethodArguments(
            MethodInfo method,
            IReadOnlyList<string> rawArguments,
            ref int argumentIndex)
        {
            var parameters = method.GetParameters();
            var convertedArguments = new object[parameters.Length];

            for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
            {
                var parameter = parameters[parameterIndex];
                convertedArguments[parameterIndex] = ReadValue(
                    parameter.ParameterType,
                    parameter,
                    !parameter.HasDefaultValue,
                    GetDefaultValue(parameter),
                    rawArguments,
                    ref argumentIndex);
            }

            return convertedArguments;
        }

        private object ReadValue(
            Type type,
            ParameterInfo parameter,
            bool required,
            object defaultValue,
            IReadOnlyList<string> rawArguments,
            ref int argumentIndex)
        {
            var actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType == typeof(TimelineCommandCall))
            {
                var nestedCommandId = ReadRawArgument(rawArguments, ref argumentIndex).Trim();
                if (nestedCommandId.Length == 0)
                    throw new FormatException("A nested command ID is required.");

                var nestedCommand = commandRegistry.Get(nestedCommandId);
                var nestedArguments = ReadMethodArguments(
                    nestedCommand.Method,
                    rawArguments,
                    ref argumentIndex);

                return new TimelineCommandCall(nestedCommandId, nestedArguments);
            }

            if (TimelineTypeUtility.IsSimpleValue(type))
            {
                var rawValue = ReadRawArgument(rawArguments, ref argumentIndex);
                return TimelineTypeUtility.ConvertSimpleValue(
                    rawValue,
                    type,
                    required,
                    defaultValue);
            }

            return ReadComplexValue(
                actualType,
                rawArguments,
                ref argumentIndex);
        }

        private object ReadComplexValue(
            Type type,
            IReadOnlyList<string> rawArguments,
            ref int argumentIndex)
        {
            var constructor = TimelineTypeUtility.GetBindingConstructor(type);
            if (constructor != null)
            {
                var constructorParameters = constructor.GetParameters();
                var constructorArguments = new object[constructorParameters.Length];

                for (var index = 0; index < constructorParameters.Length; index++)
                {
                    var childParameter = constructorParameters[index];
                    constructorArguments[index] = ReadValue(
                        childParameter.ParameterType,
                        childParameter,
                        !childParameter.HasDefaultValue,
                        GetDefaultValue(childParameter),
                        rawArguments,
                        ref argumentIndex);
                }

                return constructor.Invoke(constructorArguments);
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Timeline argument type '{type.FullName}' could not be created.",
                    exception);
            }

            foreach (var childMember in TimelineTypeUtility.GetWritableMembers(type))
            {
                var value = ReadValue(
                    TimelineTypeUtility.GetMemberType(childMember),
                    null,
                    true,
                    null,
                    rawArguments,
                    ref argumentIndex);

                TimelineTypeUtility.SetMemberValue(instance, childMember, value);
            }

            return instance;
        }

        private static string ReadRawArgument(
            IReadOnlyList<string> rawArguments,
            ref int argumentIndex)
        {
            var value = argumentIndex < rawArguments.Count
                ? rawArguments[argumentIndex] ?? string.Empty
                : string.Empty;

            argumentIndex++;
            return value;
        }

        private static void EnsureNoUnexpectedArguments(
            string commandId,
            IReadOnlyList<string> rawArguments,
            int consumedArgumentCount)
        {
            for (var index = consumedArgumentCount; index < rawArguments.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(rawArguments[index]))
                {
                    throw new FormatException(
                        $"Command '{commandId}' has an unexpected value in Arg{index + 1}: '{rawArguments[index]}'.");
                }
            }
        }

        private static void InvokeMethod(TimelineCommandInfo command, object[] arguments)
        {
            try
            {
                command.Method.Invoke(command.Target, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw new InvalidOperationException(
                    $"TimelineCommand '{command.CommandId}' threw an exception.",
                    exception.InnerException);
            }
        }

        private static object GetDefaultValue(ParameterInfo parameter)
        {
            return parameter.HasDefaultValue ? parameter.DefaultValue : null;
        }
    }
}
