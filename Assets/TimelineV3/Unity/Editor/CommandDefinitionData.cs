public sealed class CommandDefinitionDocument
{
    public CommandDefinition[] Commands { get; set; }
}

public sealed class CommandDefinition
{
    public string Id { get; set; }
    public ParameterDefinition[] Parameters { get; set; }
}

public sealed class ParameterDefinition
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Source { get; set; }
    public string[] Options { get; set; }
    public bool? MultiSelect { get; set; }
    public ParameterDefinition[] Properties { get; set; }
}
