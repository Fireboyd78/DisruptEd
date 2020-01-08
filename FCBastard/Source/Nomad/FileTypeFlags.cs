using System;

namespace Nomad
{
    [Flags]
    public enum FileTypeFlags
    {
        None,

        Xml = (1 << 0),
        Rml = (1 << 1),
    }
}
