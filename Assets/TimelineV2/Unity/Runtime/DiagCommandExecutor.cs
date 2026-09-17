using System;

namespace App.Timeline.V2
{
    /// <summary>
    /// Executes every timeline command assigned to a diagnostic command ID.
    /// </summary>
    public sealed class DiagCommandExecutor
    {
        private readonly DiagCommandStore diagCommandStore;
        private readonly TimelineCommandInvoker commandInvoker;

        public DiagCommandExecutor(
            DiagCommandStore diagCommandStore,
            TimelineCommandInvoker commandInvoker)
        {
            this.diagCommandStore = diagCommandStore
                ?? throw new ArgumentNullException(nameof(diagCommandStore));
            this.commandInvoker = commandInvoker
                ?? throw new ArgumentNullException(nameof(commandInvoker));
        }

        /// <summary>
        /// Executes all commands assigned to the specified ID in assignment order.
        /// Returns the number of executed commands.
        /// </summary>
        public int ExecuteAssignedCommands(int diagCommandId)
        {
            var assignedCommands = diagCommandStore.GetAssignedCommands(diagCommandId);
            if (assignedCommands.Count == 0)
                return 0;

            // Take a snapshot so an invoked command can safely modify the store.
            var commandsToExecute = new TimelineCommandCall[assignedCommands.Count];
            for (var index = 0; index < assignedCommands.Count; index++)
                commandsToExecute[index] = assignedCommands[index];

            foreach (var command in commandsToExecute)
                commandInvoker.Invoke(command);

            return commandsToExecute.Length;
        }
    }
}
