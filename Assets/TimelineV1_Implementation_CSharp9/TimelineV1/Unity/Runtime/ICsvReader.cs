using System.Collections.Generic;

namespace App.Timeline
{
    public interface ICsvReader
    {
        IReadOnlyList<IReadOnlyList<string>> Read(string csvText);
    }
}
