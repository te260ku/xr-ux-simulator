using System;
using System.Collections.Generic;
using System.Reflection;

public sealed class TimelineCommand
{
    private readonly object _target;
    private readonly MethodInfo _method;
    private readonly ParameterInfo[] _parameters;

    public TimelineCommandId Id { get; }
    public IReadOnlyList<ParameterInfo> Parameters => _parameters;

    public TimelineCommand(MethodInfo method, object target)
    {
        _method = method ?? throw new ArgumentNullException(nameof(method));
        _target = target ?? throw new ArgumentNullException(nameof(target));

        EnsureMethodIsSupported();
        EnsureTargetMatchesMethod();

        _parameters = _method.GetParameters();
        EnsureParametersAreSupported();

        Id = CreateId();
    }

    public void Execute(object[] arguments)
    {
        _method.Invoke(_target, arguments);
    }

    private TimelineCommandId CreateId()
    {
        var attribute = _method.GetCustomAttribute<TimelineCommandAttribute>();

        if (attribute == null)
            throw new ArgumentException("TimelineCommandAttribute is required.");

        return new TimelineCommandId(attribute.Id);
    }

    private void EnsureMethodIsSupported()
    {
        if (!_method.IsPublic || _method.IsStatic)
            throw new ArgumentException("Timeline command must be a public instance method.");

        if (_method.ReturnType != typeof(void))
            throw new ArgumentException("Timeline command must return void.");

        if (_method.ContainsGenericParameters)
            throw new ArgumentException("Generic timeline command is not supported.");
    }

    private void EnsureTargetMatchesMethod()
    {
        var declaringType = _method.DeclaringType;

        if (declaringType == null || !declaringType.IsInstanceOfType(_target))
            throw new ArgumentException("Target does not match the command declaring type.");
    }

    private void EnsureParametersAreSupported()
    {
        foreach (var parameter in _parameters)
        {
            if (parameter.IsOut || parameter.ParameterType.IsByRef)
                throw new ArgumentException("ref/out parameters are not supported.");

            if (parameter.IsOptional)
                throw new ArgumentException("Optional parameters are not supported.");
        }
    }
}
