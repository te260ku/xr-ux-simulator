namespace App.Timeline
{
    public sealed class TimelineCommandDefinitionRow
    {
        public string Command { get; set; }
        public int ArgIndex { get; set; }
        public string ArgName { get; set; }
        public string MemberPath { get; set; }
        public string Type { get; set; }
        public bool Required { get; set; }
        public string Source { get; set; }
        public string Default { get; set; }
        public string Min { get; set; }
        public string Max { get; set; }
        public string Options { get; set; }
        public bool MultiSelect { get; set; }
    }
}
