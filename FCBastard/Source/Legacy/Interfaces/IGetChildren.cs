using System.Collections.Generic;

namespace Nomad
{
    public interface IGetChildren<T>
        where T : Node
    {
        List<T> Children { get; }
    }
}
