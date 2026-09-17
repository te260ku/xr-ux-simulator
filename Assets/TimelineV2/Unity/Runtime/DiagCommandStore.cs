using System;
using System.Collections.Generic;

namespace App.Timeline.V2
{
    /// <summary>
    /// Stores the timeline commands assigned to each diagnostic command ID.
    /// Multiple commands can be assigned to the same ID and are kept in assignment order.
    /// </summary>
    public sealed class DiagCommandStore
    {
        private readonly Dictionary<int, List<TimelineCommandCall>> commandsByDiagCommandId
            = new Dictionary<int, List<TimelineCommandCall>>();

        /// <summary>
        /// Assigns one timeline command to a diagnostic command ID.
        /// Calling this multiple times with the same ID appends commands in order.
        /// </summary>
        [TimelineCommand("SetDiagCommand")]
        public void AssignCommand(int diagCommandId, TimelineCommandCall assignedCommand)
        {
            if (assignedCommand == null)
                throw new ArgumentNullException(nameof(assignedCommand));

            if (!commandsByDiagCommandId.TryGetValue(diagCommandId, out var assignedCommands))
            {
                assignedCommands = new List<TimelineCommandCall>();
                commandsByDiagCommandId.Add(diagCommandId, assignedCommands);
            }

            assignedCommands.Add(assignedCommand);
        }

        public IReadOnlyList<TimelineCommandCall> GetAssignedCommands(int diagCommandId)
        {
            if (!commandsByDiagCommandId.TryGetValue(diagCommandId, out var assignedCommands))
                return Array.Empty<TimelineCommandCall>();

            return assignedCommands;
        }

        public void Clear()
        {
            commandsByDiagCommandId.Clear();
        }

        public void Clear(int diagCommandId)
        {
            commandsByDiagCommandId.Remove(diagCommandId);
        }
    }
}
