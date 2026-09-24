using System;

namespace TimelineSystem
{
    public sealed class TimelineEvent
    {
        private readonly TimelineCommand _command;
        private readonly object[] _arguments;

        public float Time { get; }

        public TimelineEvent(
            float time,
            TimelineCommand command,
            object[] arguments)
        {
            Time = time;
            _command =
                command ?? throw new ArgumentNullException(nameof(command));
            _arguments =
                arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        public void Execute()
        {
            _command.Execute(_arguments);
        }
    }
}
