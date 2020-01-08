using System.Collections.Generic;

namespace Nomad
{
    public interface IGetAttributes<T>
        where T : NodeAttribute
    {
        List<T> Attributes { get; }
    }
}
