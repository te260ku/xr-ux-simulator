using System;
using System.Collections.Generic;
using System.Reflection;

namespace TimelineSystem
{
    public sealed class TimelineCommand
    {
        private readonly MethodInfo _method;
        private readonly object _target;

        public string Id { get; }
        public IReadOnlyList<ParameterInfo> Parameters { get; }

        public TimelineCommand(
            string id,
            MethodInfo method,
            object target)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _method = method ?? throw new ArgumentNullException(nameof(method));
            _target = target ?? throw new ArgumentNullException(nameof(target));

            Parameters = method.GetParameters();
        }

        public void Execute(object[] arguments)
        {
            _method.Invoke(_target, arguments);
        }
    }
}
