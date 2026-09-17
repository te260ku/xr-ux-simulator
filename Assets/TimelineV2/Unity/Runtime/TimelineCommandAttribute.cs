using System;
using UnityEngine.Scripting;

namespace App.Timeline.V2
{
    /// <summary>
    /// Marks a public method as callable from a timeline CSV.
    /// If no ID is specified, the method name is used as the command ID.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class TimelineCommandAttribute : PreserveAttribute
    {
        public string Id { get; }

        public TimelineCommandAttribute()
        {
        }

        public TimelineCommandAttribute(string id)
        {
            Id = id;
        }
    }
}
