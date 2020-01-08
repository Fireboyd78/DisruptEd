using System;
using System.Diagnostics;
using System.IO;

namespace Nomad
{
    public class NomadFileInfo
    {
        public int Offset; // offset into the data

        public int Version;
        public int VersionFlags;

        public int Flags;

        public int NumObjects;
        public int NumSubObjects;

        public int NumChildren;

        public StringId RootId;
        
        // can this be used to import/export data?
        public bool IsValid;
        public bool IsNomad;

        // HACK: Some .ige.rml files have 'F2 8B' identifier? no idea what this is yet
        public bool HasExternalRef;

        public unsafe void Deserialize(BinaryStream stream)
        {
            var buffer = new byte[0x24];
            var len = stream.Read(buffer, 0, buffer.Length);

            IsValid = (len != 0);

            if (!IsValid)
                return;

            fixed (byte* ptr = buffer)
            {
                IsNomad = (*(int*)ptr == Nomad.Magic);

                if (!IsNomad)
                {
                    // rml hacks
                    if (*(short*)ptr == 0x00)
                    {
                        IsNomad = true;
                        
                        // allow the filters to do their job
                        RootId = "RML_DATA";
                        Version = 1;
                        
                        return;
                    }

                    if ((*(int*)ptr == FCXMapSerializer.MGX_CONTAINER) 
                        || (*(int*)(ptr + 0x14) == FCXMapArchive.Signature))
                    {
                        IsNomad = true;

                        RootId = "FCXMapData";
                        Version = 2;

                        return;
                    }

                    Offset = 2;

                    while (*(int*)(ptr + Offset) != Nomad.Magic)
                    {
                        Offset += 2;

                        if (Offset >= buffer.Length)
                        {
                            Offset = -1;
                            break;
                        }
                    }

                    // doesn't seem to be a nomad resource
                    if (Offset == -1)
                        return;

                    // found the nomad header
                    IsNomad = true;
                }

                var dataPtr = (ptr + Offset);
                
                switch (Offset)
                {
                // 'reference' files?
                case 0x2:
                    if (*(ushort*)ptr == 0x8BF2)
                        HasExternalRef = true;
                    break;
                // sequence?
                //case 0x14:
                // todo
                }

                if (IsNomad)
                {
                    var version = *(short*)(dataPtr + 4);

                    Flags = *(short*)(dataPtr + 6);

                    Version = version & 0xFF;
                    VersionFlags = (version >> 8) & 0xFF;

                    // validate EntityLibrary
                    if (Version == 5)
                    {
                        if (VersionFlags != 0x40)
                            throw new InvalidDataException("Malformed EntityLibrary data!");
                    }

                    NumObjects = *(int*)(dataPtr + 8);
                    NumSubObjects = *(int*)(dataPtr + 12);
                    NumChildren = *(dataPtr + 16);

                    if (NumChildren < 254)
                    {
                        RootId = *(int*)(dataPtr + 17);
                    }
                    else
                    {
                        NumChildren = *(int*)(dataPtr + 17);
                        RootId = *(int*)(dataPtr + 21);
                    }
                }
            }
        }
    }
}