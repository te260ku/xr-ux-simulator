using System.Collections.Generic;

namespace App.Timeline.V2
{
    public interface ICsvReader
    {
        IReadOnlyList<IReadOnlyList<string>> Read(string csvText);
    }
}
