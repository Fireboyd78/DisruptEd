using System;
using System.Collections.Generic;

namespace Nomad
{
    /*
        [[ Watch_Dogs internal types ]]

        Boolean = 0

        Integer8 = 1
        Integer16 = 2
        Integer32 = 3
        Integer64 = 18

        Float = 4

        StringId = 5
        String = 6

        Vec2 = 7
        Vec3 = 8
        Vec4 = 9

        Quat = 10

        PathId = 13
        EntityId = 14

        [[ ??? ]]

        Group = 15

        StringIdNoCase = 16

        Pointer = 17
    */

    public enum DataType
    {
        BinHex = 0, /* MUST BE ZERO */

        Bool,

        Byte,

        Int16,
        Int32,

        UInt16,
        UInt32,

        Float,

        String,
        StringId,

        [Obsolete("This entry is for backwards compatibility only. Use 'StringId' instead.", true)]
        StringHash = StringId,

        Vector2,
        Vector3,
        Vector4,

        // Always a string!
        RML,

        // FNV-1a
        PathId,

        Array,
    }
}
