using System;

namespace Nomad
{
    public class FCXMapData
    {
        // Credits to Gibbed for figuring this out!
        // (Gibbed.Dunia2 -> Gibbed.FarCry3.FileFormats/CustomMapGameFile.cs)
        public static readonly MagicNumber Magic = Utils.GetHash("CCustomMapGameFile");

        public int Version;

        public FCXMapInfo Info;
    }
}
