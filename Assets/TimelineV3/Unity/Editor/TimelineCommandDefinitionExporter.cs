using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;

public sealed class TimelineCommandDefinitionExporter
{
    private readonly TimelineCommandScanner _scanner;
    private readonly IContractResolver _contractResolver;

    public TimelineCommandDefinitionExporter(
        TimelineCommandScanner scanner,
        IContractResolver contractResolver)
    {
        _scanner = scanner
            ?? throw new ArgumentNullException(nameof(scanner));

        _contractResolver = contractResolver
            ?? throw new ArgumentNullException(nameof(contractResolver));
    }

    public CommandDefinitionDocument CreateDefinitions()
    {
        var commands = _scanner.Scan()
            .Select(CreateCommandDefinition)
            .OrderBy(command => command.Id, StringComparer.Ordinal)
            .ToArray();

        EnsureUniqueCommandIds(commands);

        return new CommandDefinitionDocument
        {
            Commands = commands
        };
    }

    private CommandDefinition CreateCommandDefinition(
        MethodInfo method)
    {
        EnsureCommandMethodIsSupported(method);

        var attribute =
            method.GetCustomAttribute<TimelineCommandAttribute>();

        if (attribute == null)
            throw new InvalidOperationException(
                "TimelineCommandAttribute is required.");

        var id = new TimelineCommandId(attribute.Id);

        return new CommandDefinition
        {
            Id = id.Value,
            Parameters = method.GetParameters()
                .Select(CreateParameterDefinition)
                .ToArray()
        };
    }

    private ParameterDefinition CreateParameterDefinition(
        ParameterInfo parameter)
    {
        var name = parameter.Name
            ?? throw new InvalidOperationException(
                "Timeline command parameter has no name.");

        return CreateTypeDefinition(
            name,
            parameter.ParameterType,
            new HashSet<Type>(),
            true);
    }

    private ParameterDefinition CreateTypeDefinition(
        string name,
        Type type,
        HashSet<Type> ancestors,
        bool isCommandParameter)
    {
        if (type == typeof(TimelineCommandExecution))
            return CreateExecutionDefinition(
                name,
                isCommandParameter);

        if (type.IsEnum)
            return CreateEnumDefinition(name, type);

        if (TryGetScalarType(type, out var scalarType))
        {
            return new ParameterDefinition
            {
                Name = name,
                Type = scalarType
            };
        }

        return CreateObjectDefinition(
            name,
            type,
            ancestors);
    }

    private ParameterDefinition CreateExecutionDefinition(
        string name,
        bool isCommandParameter)
    {
        if (!isCommandParameter)
        {
            throw new NotSupportedException(
                "Execution must be a direct command parameter.");
        }

        return new ParameterDefinition
        {
            Name = name,
            Type = "execution"
        };
    }

    private ParameterDefinition CreateEnumDefinition(
        string name,
        Type type)
    {
        return new ParameterDefinition
        {
            Name = name,
            Type = "enum",
            Options = Enum.GetNames(type),
            MultiSelect = type.IsDefined(
                typeof(FlagsAttribute),
                false)
        };
    }

    private ParameterDefinition CreateObjectDefinition(
        string name,
        Type type,
        HashSet<Type> ancestors)
    {
        EnsureNotRecursive(type, ancestors);

        var contract = _contractResolver.ResolveContract(type);

        if (!(contract is JsonObjectContract objectContract))
        {
            throw new NotSupportedException(
                $"Timeline argument type '{type}' is not supported.");
        }

        var nextAncestors =
            new HashSet<Type>(ancestors)
            {
                type
            };

        var properties = CreateProperties(
            objectContract,
            nextAncestors);

        var source = type
            .GetCustomAttribute<RepositoryKeySourceAttribute>()
            ?.Source;

        EnsureRepositorySourceShape(
            type,
            source,
            properties);

        return new ParameterDefinition
        {
            Name = name,
            Type = "object",
            Source = source,
            Properties = properties
        };
    }

    private ParameterDefinition[] CreateProperties(
        JsonObjectContract contract,
        HashSet<Type> ancestors)
    {
        return contract.Properties
            .Where(property => !property.Ignored)
            .Select(property =>
            {
                var name = property.PropertyName
                    ?? throw new InvalidOperationException(
                        "JSON property has no name.");

                var type = property.PropertyType
                    ?? throw new InvalidOperationException(
                        $"JSON property '{name}' has no type.");

                return CreateTypeDefinition(
                    name,
                    type,
                    ancestors,
                    false);
            })
            .ToArray();
    }

    private bool TryGetScalarType(
        Type type,
        out string definitionType)
    {
        if (type == typeof(string))
            definitionType = "string";
        else if (type == typeof(int))
            definitionType = "int";
        else if (type == typeof(float))
            definitionType = "float";
        else if (type == typeof(bool))
            definitionType = "bool";
        else
        {
            definitionType = null;
            return false;
        }

        return true;
    }

    private void EnsureRepositorySourceShape(
        Type type,
        string source,
        ParameterDefinition[] properties)
    {
        if (source == null)
            return;

        if (properties.Length != 1 ||
            properties[0].Type != "string")
        {
            throw new NotSupportedException(
                $"Repository key type '{type.Name}' must expose " +
                "exactly one string JSON property.");
        }
    }

    private void EnsureNotRecursive(
        Type type,
        HashSet<Type> ancestors)
    {
        if (ancestors.Contains(type))
        {
            throw new NotSupportedException(
                $"Recursive timeline argument type " +
                $"'{type.Name}' is not supported.");
        }
    }

    private void EnsureUniqueCommandIds(
        IReadOnlyList<CommandDefinition> commands)
    {
        var duplicate = commands
            .GroupBy(command => command.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Timeline command '{duplicate.Key}' is duplicated.");
        }
    }

    private void EnsureCommandMethodIsSupported(
        MethodInfo method)
    {
        if (!method.IsPublic || method.IsStatic)
        {
            throw new InvalidOperationException(
                $"{method.DeclaringType?.Name}.{method.Name} must " +
                "be a public instance method.");
        }

        if (method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException(
                $"{method.DeclaringType?.Name}.{method.Name} " +
                "must return void.");
        }

        if (method.ContainsGenericParameters)
        {
            throw new InvalidOperationException(
                "Generic timeline commands are not supported.");
        }

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.IsOut ||
                parameter.ParameterType.IsByRef ||
                parameter.IsOptional)
            {
                throw new InvalidOperationException(
                    $"Unsupported parameter '{parameter.Name}' " +
                    $"in command '{method.Name}'.");
            }
        }
    }
}
