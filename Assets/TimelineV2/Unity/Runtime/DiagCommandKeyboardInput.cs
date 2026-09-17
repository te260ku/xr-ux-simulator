using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace App.Timeline.V2
{
    /// <summary>
    /// Executes diagnostic commands when their configured keyboard keys are pressed.
    /// Add this component to a scene object and configure the key/ID pairs in the Inspector.
    /// </summary>
    public sealed class DiagCommandKeyboardInput : MonoBehaviour
    {
        [SerializeField]
        private List<DiagCommandKeyBinding> keyBindings = new List<DiagCommandKeyBinding>();

        private DiagCommandExecutor diagCommandExecutor;

        [Inject]
        public void Initialize(DiagCommandExecutor executor)
        {
            diagCommandExecutor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        private void Update()
        {
            if (diagCommandExecutor == null)
                return;

            foreach (var binding in keyBindings)
            {
                if (binding == null || binding.Key == KeyCode.None)
                    continue;

                if (Input.GetKeyDown(binding.Key))
                    diagCommandExecutor.ExecuteAssignedCommands(binding.DiagCommandId);
            }
        }
    }
}
