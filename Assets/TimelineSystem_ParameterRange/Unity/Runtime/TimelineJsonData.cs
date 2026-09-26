using Newtonsoft.Json.Linq;

internal sealed class TimelineEventData
{
    public float Time { get; set; }
    public TimelineExecutionData Execution { get; set; }
}

internal sealed class TimelineExecutionData
{
    public string Command { get; set; }
    public JObject Arguments { get; set; }
}
