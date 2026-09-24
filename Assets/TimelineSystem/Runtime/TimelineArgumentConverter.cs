using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TimelineSystem
{
    public sealed class TimelineArgumentConverter
    {
        private readonly JsonSerializer _serializer;

        public TimelineArgumentConverter(JsonSerializer serializer)
        {
            _serializer =
                serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public object[] ConvertToMethodArguments(
            JObject arguments,
            IReadOnlyList<ParameterInfo> parameters)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var methodArguments = new object[parameters.Count];

            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];

                var parameterName =
                    parameter.Name
                    ?? throw new InvalidOperationException(
                        "Timeline command parameter name was not found.");

                if (!arguments.TryGetValue(
                        parameterName,
                        StringComparison.Ordinal,
                        out var jsonValue))
                {
                    throw new TimelineLoadException(
                        $"Argument '{parameterName}' is missing.");
                }

                methodArguments[i] =
                    jsonValue.ToObject(
                        parameter.ParameterType,
                        _serializer);
            }

            foreach (var property in arguments.Properties())
            {
                var matched = false;

                for (var i = 0; i < parameters.Count; i++)
                {
                    if (string.Equals(
                            parameters[i].Name,
                            property.Name,
                            StringComparison.Ordinal))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    throw new TimelineLoadException(
                        $"Unknown argument '{property.Name}'.");
                }
            }

            return methodArguments;
        }
    }
}
