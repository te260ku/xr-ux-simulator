using System;
using UnityEngine;

namespace App.Timeline.V2
{
    /// <summary>
    /// Maps one keyboard key to one diagnostic command ID.
    /// </summary>
    [Serializable]
    public sealed class DiagCommandKeyBinding
    {
        [SerializeField] private KeyCode key = KeyCode.None;
        [SerializeField] private int diagCommandId;

        public KeyCode Key => key;
        public int DiagCommandId => diagCommandId;
    }
}
