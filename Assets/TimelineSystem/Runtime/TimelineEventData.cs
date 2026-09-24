using Newtonsoft.Json.Linq;

namespace TimelineSystem
{
    internal sealed class TimelineEventData
    {
        public float Time { get; set; }
        public string Command { get; set; }
        public JObject Arguments { get; set; }

        public void Validate()
        {
            if (float.IsNaN(Time) ||
                float.IsInfinity(Time) ||
                Time < 0f)
            {
                throw new TimelineLoadException(
                    "Timeline event time must be a finite value greater than or equal to 0.");
            }

            if (string.IsNullOrWhiteSpace(Command))
            {
                throw new TimelineLoadException(
                    "Timeline event command is missing.");
            }

            if (Arguments == null)
            {
                throw new TimelineLoadException(
                    $"Timeline event '{Command}' has no arguments object.");
            }
        }
    }
}
