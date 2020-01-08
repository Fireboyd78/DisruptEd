namespace Nomad
{
    public enum FormatType
    {
        Generic = 0,

        RML = 1,

        Resource = 2,

        // Disrupt-only?
        Objects = 3,
        Entities = 5 | (0x40 << 8), // 'entitylibrary.fcb'
    }
}
