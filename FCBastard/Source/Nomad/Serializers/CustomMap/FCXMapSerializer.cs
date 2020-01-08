using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

using LZ4Sharp;

namespace Nomad
{
    //
    // Credits to Gibbed!
    //
    public class FCXMapArchive
    {
        public static readonly MagicNumber Signature = "FC3M";

        public int Version { get; set; }
        
        public FCXCompressedData Data { get; set; }
        public FCXCompressedData Header { get; set; }
        public FCXCompressedData Descriptor { get; set; }

        public void Serialize(BinaryStream stream)
        {
            stream.Write(Signature);
            stream.Write(Version);

            uint offset = 0;

            offset += 20;
            stream.Write(offset);

            offset += 4 + (uint)Data.Data.Length + 4 + ((uint)Data.Blocks.Count * 8);
            stream.Write(offset);

            offset += 4 + (uint)Header.Data.Length + 4 + ((uint)Header.Blocks.Count * 8);
            stream.Write(offset);

            Data.Serialize(stream);
            Header.Serialize(stream);
            Descriptor.Serialize(stream);
        }

        public void Deserialize(BinaryStream stream)
        {
            var baseOffset = stream.Position;

            var magic = stream.ReadInt32();

            if (magic != Signature)
                throw new FormatException();

            Version = stream.ReadInt32();

            if (Version != 1)
                throw new FormatException();

            uint offsetData = stream.ReadUInt32();
            uint offsetHeader = stream.ReadUInt32();
            uint offsetDescriptor = stream.ReadUInt32();

            if (offsetData != 20)
                throw new FormatException();

            Data = new FCXCompressedData();
            Data.Deserialize(stream);

            if (baseOffset + offsetHeader != stream.Position)
                throw new FormatException();

            Header = new FCXCompressedData();
            Header.Deserialize(stream);

            if (baseOffset + offsetDescriptor != stream.Position)
                throw new FormatException();

            Descriptor = new FCXCompressedData();
            Descriptor.Deserialize(stream);
        }
        
        public FCXMapArchive()
        {
            Version = 1;
        }
    }

    //
    // Credits to Gibbed!
    //
    public class FCXCompressedData
    {
        public byte[] Data { get; set; }

        public List<Block> Blocks { get; set; }

        public void Serialize(BinaryStream stream)
        {
            stream.Write(4 + Data.Length);
            stream.Write(Data, 0, Data.Length);

            stream.Write(Blocks.Count);

            foreach (var block in Blocks)
            {
                stream.Write(block.VirtualOffset);

                uint foic = 0;

                foic |= block.FileOffset;
                foic &= 0x7FFFFFFF;
                foic |= (block.IsCompressed == true ? 1u : 0u) << 31;

                stream.Write(foic);
            }
        }

        public void Deserialize(BinaryStream stream)
        {
            var offset = stream.ReadInt32();
            var length = offset - 4;

            Data = new byte[length];

            if (stream.Read(Data, 0, length) != length)
                throw new FormatException();

            var blockCount = stream.ReadInt32();

            Blocks = new List<Block>();

            for (int i = 0; i < blockCount; i++)
            {
                var block = new Block() {
                    VirtualOffset = stream.ReadUInt32(),
                    FileOffset = stream.ReadUInt32(),
                };

                block.IsCompressed = (block.FileOffset & 0x80000000) != 0;
                block.FileOffset &= 0x7FFFFFFF;
                
                Blocks.Add(block);
            }

            if (Blocks.Count == 0)
                throw new FormatException();

            if (Blocks.First().FileOffset != 4)
                throw new FormatException();

            if (Blocks.Last().FileOffset != (4 + length))
                throw new FormatException();
        }

        public struct Block
        {
            public uint VirtualOffset;
            public uint FileOffset;
            public bool IsCompressed;
        }

        public static FCXCompressedData Pack(BinaryStream stream)
        {
            var compressedData = new FCXCompressedData();

            using (var bs = new BinaryStream(1024))
            {
                uint virtualOffset = 0;
                uint realOffset = 4;

                while (stream.Position < stream.Length)
                {
                    var length = (int)Math.Min(0x40000, (stream.Length - stream.Position));

                    using (var block = new BinaryStream(16))
                    {
                        var zlib = new DeflaterOutputStream(block);
                        var buffer = stream.ReadBytes(length);

                        zlib.Write(buffer, 0, length);
                        zlib.Finish();

                        compressedData.Blocks.Add(new Block() {
                            VirtualOffset = virtualOffset,
                            FileOffset = realOffset,
                            IsCompressed = true,
                        });

                        block.Position = 0;
                        block.CopyTo(bs);

                        realOffset += (uint)block.Length;
                    }

                    virtualOffset += (uint)length;
                }

                compressedData.Data = bs.ToArray();

                compressedData.Blocks.Add(new Block() {
                    VirtualOffset = virtualOffset,
                    FileOffset = realOffset,
                    IsCompressed = true,
                });
            }

            return compressedData;
        }

        public byte[] Unpack()
        {
            var memory = new BinaryStream(1024);

            using (var bs = new BinaryStream(Data))
            {
                for (int i = 1; i < Blocks.Count; i++)
                {
                    var block = Blocks[i - 1];
                    var next = Blocks[i];

                    var size = (int)(next.VirtualOffset - block.VirtualOffset);

                    bs.Seek(block.FileOffset - 4, SeekOrigin.Begin);

                    memory.Seek(block.VirtualOffset, SeekOrigin.Begin);

                    if (block.IsCompressed == true)
                    {
                        var zlib = new InflaterInputStream(bs);
                        zlib.CopyTo(memory);
                    }
                    else
                    {
                        var buffer = bs.ReadBytes(size);

                        memory.Write(buffer, 0, size);
                    }
                }
            }
            
            return memory.ToArray();
        }

        public FCXCompressedData()
        {
            Blocks = new List<Block>();
        }
    }

    public class FCXMapSerializer : NomadSerializer, INomadXmlFileSerializer
    {
        public static readonly MagicNumber MGX_CONTAINER = 0x110;

        // Credits to Gibbed for figuring this out!
        // (Gibbed.Dunia2 -> Gibbed.FarCry3.FileFormats/CustomMapGameFile.cs)
        public static readonly MagicNumber MGX_MAPDATA = Utils.GetHash("CCustomMapGameFile");

        public override FileType Type => FileType.Binary;

        public int Version = 0x26;

        public bool IsUserMap = false;

        public Guid UID = Guid.Empty;

        public byte[] MapData = null;
        public byte[] ThumbData = null;

        public NomadObject MetaData = null;
        public NomadObject ConfigData = null;

        public override void Serialize(Stream stream, NomadObject data)
        {
            throw new NotImplementedException("Haha nope");
        }

        protected NomadObject ReadFCBChunk(BinaryStream stream, NomadObject parent)
        {
            var fcbSize = stream.ReadInt32();
            var fcbData = stream.ReadBytes(fcbSize);

            using (var bs = new BinaryStream(fcbData))
            {
                var serializer = new NomadResourceSerializer();
                var root = serializer.Deserialize(bs);

                if (parent != null)
                    parent.Children.Add(root);

                return root;
            }
        }

        protected void ReadPadding(BinaryStream stream, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var value = stream.ReadInt32();

                if (value != 0)
                    throw new InvalidDataException($"Expected padding but got '{value:X8}' instead!");
            }
        }

        protected NomadObject ReadMapData(BinaryStream stream)
        {
            var check = stream.ReadInt32();

            var result = new NomadObject() {
                Id = "FCXMapData"
            };

            if (check != 0x26)
            {
                // non-editor map?
                stream.Position -= 4;

                UID = stream.Read<Guid>();
                ReadPadding(stream, 1);

                Version = 0;
                IsUserMap = false;
            }
            else
            {
                var magic = stream.ReadInt32();

                if (magic != MGX_MAPDATA)
                    throw new InvalidDataException("Invalid FCX map data -- bad data magic!");

                Version = check;
                IsUserMap = true;
                
                ReadPadding(stream, 3);

                MetaData = ReadFCBChunk(stream, result);
                ConfigData = ReadFCBChunk(stream, result);

                ReadPadding(stream, 5);

                var thumbSize = stream.ReadInt32();

                if (thumbSize > 0)
                    ThumbData = stream.ReadBytes(thumbSize);

                ReadPadding(stream, 1);
            }

            var mapSize = (int)(stream.Length - stream.Position);

            MapData = stream.ReadBytes(mapSize);
            
            return result;
        }

        public override NomadObject Deserialize(Stream stream)
        {
            if (Context.State == ContextStateType.End)
                Context.Reset();

            var _stream = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            var magic = _stream.ReadInt32();

            if (magic == MGX_CONTAINER)
            {
                var dataSize = _stream.ReadInt32();
                var compSize = _stream.ReadInt32();
                var checksum = _stream.ReadInt32();

                var buffer = new byte[dataSize];

                if (compSize != 0)
                {
                    var lz = new LZ4Decompressor64();
                    var tmp = new byte[compSize];

                    _stream.Read(tmp, 0, compSize);

                    lz.DecompressKnownSize(tmp, buffer, dataSize);
                }
                else
                {
                    _stream.Read(buffer, 0, dataSize);
                }

                using (var bs = new BinaryStream(buffer))
                {
                    return ReadMapData(bs);
                }
            }
            else
            {
                // move back and read normally
                _stream.Position -= 4;

                return ReadMapData(_stream);
            }
        }

        public void LoadXml(string filename)
        {
            throw new NotImplementedException("Sorry, can't do that.");
        }

        public void SaveXml(string filename)
        {
            var rootDir = Path.GetDirectoryName(filename);

            var xml = new XDocument();
            var root = new XElement("FCXMapData");

            root.SetAttributeValue("Version", Version);

            var srl = new NomadXmlSerializer();

            if (IsUserMap)
            {
                srl.CreateXmlElement(MetaData, root);
                srl.CreateXmlElement(ConfigData, root);
            }
            else
            {
                if (UID != Guid.Empty)
                    root.SetAttributeValue("UID", UID);
            }
            
            using (var bs = new BinaryStream(MapData))
            {
                var arc = new FCXMapArchive();
                arc.Deserialize(bs);

                var data = arc.Data.Unpack();
                var hdr = arc.Header.Unpack();
                var desc = arc.Descriptor.Unpack();
                
                File.WriteAllBytes(Path.Combine(rootDir, Path.ChangeExtension(filename, ".dat")), data);
                File.WriteAllBytes(Path.Combine(rootDir, Path.ChangeExtension(filename, ".fat")), hdr);
                File.WriteAllBytes(Path.Combine(rootDir, Path.ChangeExtension(filename, ".fat.xml")), desc);
            }
            
            xml.Add(root);
            xml.SaveFormatted(filename, true);
        }

        public FCXMapSerializer()
            : base()
        { }
    }
}
