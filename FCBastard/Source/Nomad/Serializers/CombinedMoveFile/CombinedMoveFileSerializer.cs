using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Linq;

namespace Nomad
{
    public class MoveResourceInfo
    {
        public int RootNodeId;
        public int Size;

        public MoveResourceInfo(int rootNodeId, int size)
        {
            RootNodeId = rootNodeId;
            Size = size;
        }
    }

    public class FixupInfo
    {
        public int Hash;
        public int Offset;

        public NomadObject ToObject()
        {
            var obj = new NomadObject("FIXUP");

            obj.SetAttributeValue("offset", DataType.BinHex, $"#{Offset:X8}");
            obj.SetAttributeValue("hash", DataType.BinHex, $"#{Hash:X8}");

            return obj;
        }

        public FixupInfo(int hash, int offset)
        {
            Hash = hash;
            Offset = offset;
        }
    }

    public class MoveParamData
    {
        public static MoveParamData[] AllParams
        {
            get
            {
                return new[] {
                    new MoveParamData("ANIMPARAM",              "anim"),
                    new MoveParamData("POSEANIMPARAM",          "pose", 2),
                    new MoveParamData("MOTIONMATCHINGPARAM",    "mtch", 2),
                    new MoveParamData("BLENDPARAM",             "blnd"),
                    new MoveParamData("CLIPPARAM",              "clip"),
                    new MoveParamData("LAYERPARAM",             "layr"),
                    new MoveParamData("LOOAKATPARAM",           "look"),
                    new MoveParamData("MOVEBLENDPARAM",         "movb"),
                    new MoveParamData("MOVESTATEPARAM",         "movs"),
                    new MoveParamData("PMSVALUEPARAM"),
                    new MoveParamData("RAGDOLLPARAM",           "ragd"),
                    new MoveParamData("SECONDARYMOTIONPARAM",   "2mtn"),
                    new MoveParamData("BLENDADJUSTPARAM",       "ajst", 2),
                };
            }
        }

        public string Name { get; }
        public string Tag { get; }

        public int MinVersion { get; }

        public StringId ValuesId => $"{Name}_VALUES";
        public StringId FixupsId => $"{Name}_FIXUPS";

        public List<NomadObject> Values { get; set; }
        public List<FixupInfo> Fixups { get; set; }

        public bool IsEmpty
        {
            get { return (Values.Count == 0) && (Fixups.Count == 0); }
        }

        FixupInfo ParseFixup(NomadObject obj)
        {
            var offsetAttr = obj.GetAttribute("offset");
            var hashAttr = obj.GetAttribute("hash");

            var offset = Utils.UnpackData(offsetAttr, BitConverter.ToInt32);
            var hash = Utils.UnpackData(hashAttr, BitConverter.ToInt32);

            return new FixupInfo(hash, offset);
        }

        void ReadFixups(NomadObject obj)
        {
            var offsetsAttr = obj.GetAttribute("offsetsArray");
            var hashesAttr = obj.GetAttribute("hashesArray");

            if ((offsetsAttr != null) && (hashesAttr != null))
            {
                var offsets = Utils.UnpackArray(offsetsAttr, BitConverter.ToInt32, 4, out int nOffsets);
                var hashes = Utils.UnpackArray(hashesAttr, BitConverter.ToInt32, 4, out int nHashes);

                if (nOffsets != nHashes)
                    throw new InvalidDataException("Yikes!");

                // doesn't matter which one we use
                // but we'll be verbose anyways :)
                var count = (nOffsets & nHashes);

                Fixups = new List<FixupInfo>(count);

                for (int i = 0; i < count; i++)
                {
                    var offset = offsets[i];
                    var hash = hashes[i];

                    var fixup = new FixupInfo(hash, offset);

                    Fixups.Add(fixup);
                }
            }
            else
            {
                foreach (var child in obj.Children)
                {
                    if (child.Id != "FIXUP")
                        throw new InvalidDataException($"Expected a FIXUP but got '{child.Id}' instead.");

                    var fixup = ParseFixup(child);

                    Fixups.Add(fixup);
                }
            }
        }
        
        public void Deserialize(NomadObject root)
        {
            var values = root.GetChild(ValuesId);
            var fixups = root.GetChild(FixupsId);

            if (values != null)
                Values.AddRange(values.Children);
            if (fixups != null)
                ReadFixups(fixups);
        }

        public void Serialize(NomadObject root)
        {
            var values = new NomadObject(ValuesId);
            var fixups = new NomadObject(FixupsId);

            values.Children.AddRange(Values);

            foreach (var fixup in Fixups)
            {
                var obj = fixup.ToObject();

                fixups.Children.Add(obj);
            }

            root.Children.Add(values);
            root.Children.Add(fixups);
        }

        public MoveParamData(string name, string tag = "xxxx", int minVersion = 1)
        {
            Name = name;
            Tag = tag;

            MinVersion = minVersion;

            Values = new List<NomadObject>();
            Fixups = new List<FixupInfo>();
        }
    }

    public class PerMoveResourceInfo
    {
        public List<MoveResourceInfo> Infos { get; set; }

        void ReadInfos(NomadObject obj)
        {
            var sizesAttr = obj.GetAttribute("sizes");
            var rootNodeIdsAttr = obj.GetAttribute("rootNodeIds");

            var sizes = Utils.UnpackArray(sizesAttr, BitConverter.ToInt32, 4, out int nSizes);
            var rootNodeIds = Utils.UnpackArray(rootNodeIdsAttr, BitConverter.ToInt32, 4, out int nRootNodeIds);

            if (nSizes != nRootNodeIds)
                throw new InvalidDataException("Yikes!");

            var count = (nSizes & nRootNodeIds);

            for (int i = 0; i < count; i++)
            {
                var size = sizes[i];
                var rootNodeId = rootNodeIds[i];

                var info = new MoveResourceInfo(rootNodeId, size);

                Infos.Add(info);
            }
        }

        public void Deserialize(NomadObject root)
        {
            var obj = root.GetChild("PerMoveResourceInfo");

            if (obj != null)
                ReadInfos(obj);
        }

        public void Serialize(NomadObject root)
        {
            var obj = new NomadObject("PerMoveResourceInfo");

            var sizes = new List<int>();
            var rootNodeIds = new List<int>();
            
            foreach (var info in Infos)
            {
                sizes.Add(info.Size);
                rootNodeIds.Add(info.RootNodeId);
            }

            var sizesAttr = new NomadValue("sizes", DataType.Array);
            var rootNodeIdsAttr = new NomadValue("rootNodeIds", DataType.Array);

            Utils.PackArray(sizesAttr, sizes, BitConverter.GetBytes, 4);
            Utils.PackArray(rootNodeIdsAttr, rootNodeIds, BitConverter.GetBytes, 4);

            obj.Attributes.Add(sizesAttr);
            obj.Attributes.Add(rootNodeIdsAttr);

            root.Children.Add(obj);
        }

        public PerMoveResourceInfo()
        {
            Infos = new List<MoveResourceInfo>();
        }
    }

    public class MoveResourceData
    {
        public readonly MoveParamData[] AllParams = MoveParamData.AllParams;

        public int RootNodeId;

        public byte[] MoveData;

        public void Serialize(NomadObject root)
        {
            root.SetAttributeValue("rootNodeId", DataType.BinHex, $"#{RootNodeId:X8}");
            root.SetAttributeValue("data", DataType.BinHex, Utils.Bytes2HexString(MoveData));

            foreach (var param in AllParams)
            {
                if (!param.IsEmpty)
                    param.Serialize(root);
            }
        }

        public void Deserialize(NomadObject root)
        {
            RootNodeId = Utils.UnpackData(root.GetAttribute("rootNodeId"), BitConverter.ToInt32);
            MoveData = root.GetAttribute("data").Data.Buffer;

            foreach (var param in AllParams)
                param.Deserialize(root);
        }

        public MoveResourceInfo GetInfo()
        {
            return new MoveResourceInfo(RootNodeId, MoveData.Length);
        }

        public NomadObject ToObject()
        {
            var obj = new NomadObject("MoveResource");

            Serialize(obj);

            return obj;
        }
    }

    public class CombinedMoveFileSerializer : NomadResourceSerializer, INomadXmlFileSerializer
    {
        public readonly MoveParamData[] AllParams = MoveParamData.AllParams;

        public readonly PerMoveResourceInfo PerMoveResourceInfo = new PerMoveResourceInfo();
        
        public int MoveCount { get; set; }
        public byte[] MoveData { get; set; }

        public int Version { get; set; }
        
        public override void Serialize(Stream stream, NomadObject _unused)
        {
            var root = new NomadObject("ROOT");

            root.Children.Add(new NomadObject("PMSVALUEDESCLIST"));

            var allFixups = new List<NomadObject>();

            foreach (var param in AllParams)
            {
                if (param.MinVersion > Version)
                    continue;

                var values = new NomadObject(param.ValuesId);
                values.Children.AddRange(param.Values);

                root.Children.Add(values);
                
                var offsets = new List<int>();
                var hashes = new List<int>();

                foreach (var fixup in param.Fixups)
                {
                    offsets.Add(fixup.Offset);
                    hashes.Add(fixup.Hash);
                }
                
                var offsetsAttr = new NomadValue("offsetsArray", DataType.Array);
                var hashesAttr = new NomadValue("hashesArray", DataType.Array);

                Utils.PackArray(offsetsAttr, offsets, BitConverter.GetBytes, 4);
                Utils.PackArray(hashesAttr, hashes, BitConverter.GetBytes, 4);

                var fixups = new NomadObject(param.FixupsId);

                fixups.Attributes.Add(offsetsAttr);
                fixups.Attributes.Add(hashesAttr);

                allFixups.Add(fixups);
            }

            root.Children.AddRange(allFixups);

            PerMoveResourceInfo.Serialize(root);

            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            byte[] fcbData = null;

            using (var nb = new BinaryStream(1024))
            {
                Format = FormatType.Objects;

                base.Serialize(nb, root);
                fcbData = nb.ToArray();
            }

            bs.Write(MoveCount);

            bs.Write(MoveData.Length);
            bs.Write(fcbData.Length);

            bs.Write(MoveData);
            bs.Write(fcbData);
        }

        public override NomadObject Deserialize(Stream stream)
        {
            var bs = (stream as BinaryStream)
                ?? new BinaryStream(stream);

            MoveCount = bs.ReadInt32();

            var moveDataSize = bs.ReadInt32();
            var fcbDataSize = bs.ReadInt32();

            MoveData = bs.ReadBytes(moveDataSize);

            // since we're already at the FCB data, just read it
            var root = base.Deserialize(bs);
            
            foreach (var param in AllParams)
                param.Deserialize(root);
            
            PerMoveResourceInfo.Deserialize(root);
            
            // don't return anything
            return null;
        }
        
        public void LoadXml(string filename)
        {
            var srl = new NomadXmlSerializer();

            NomadObject root = null;

            using (var fs = File.Open(filename, FileMode.Open))
            {
                root = srl.Deserialize(fs);
            }

            Version = int.Parse(root.GetAttributeValue("Version"));
            MoveCount = int.Parse(root.GetAttributeValue("Count"));

            var resources = new List<MoveResourceData>();
            var infos = new List<MoveResourceInfo>();

            var size = 0;
            
            foreach (var child in root.Children)
            {
                var resource = new MoveResourceData();
                resource.Deserialize(child);
                
                resources.Add(resource);

                var info = resource.GetInfo();
                infos.Add(info);

                size += info.Size;
            }
            
            var buffer = new byte[size];
            var offset = 0;
            
            for (int i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                var info = infos[i];

                foreach (var param in resource.AllParams)
                {
                    var globalParam = AllParams.First((p) => p.Name == param.Name);

                    globalParam.Values.AddRange(param.Values);

                    foreach (var fixup in param.Fixups)
                    {
                        var fixupInfo = new FixupInfo(fixup.Hash, fixup.Offset + offset);
                        globalParam.Fixups.Add(fixupInfo);
                    }
                }

                Buffer.BlockCopy(resource.MoveData, 0, buffer, offset, info.Size);
                offset += info.Size;
            }

            PerMoveResourceInfo.Infos = infos;
            MoveData = buffer;
        }

        public void SaveXml(string filename)
        {
            var xml = new XDocument();
            var root = new XElement("CombinedMoveFile");

            root.SetAttributeValue("Version", 1);
            root.SetAttributeValue("Count", MoveCount);
            
            var srl = new NomadXmlSerializer();
            
            //foreach (var child in Root.Children)
            //    srl.CreateXmlElement(child, root);

            var fixups = new List<(int offset, int hash, string param)>();

            foreach (var param in AllParams)
            {
                foreach (var fixup in param.Fixups)
                    fixups.Add((fixup.Offset, fixup.Hash, param.Name));
            }

            fixups = fixups.OrderBy((e) => e.offset).ToList();

            var resources = new List<MoveResourceData>();
            var offset = 0;

            foreach (var info in PerMoveResourceInfo.Infos)
            {
                var hash = BitConverter.ToInt32(MoveData, offset);
                var bound = (offset + info.Size);

                var resource = new MoveResourceData();
                var hashes = new List<int>();

                foreach (var fixup in fixups.Where((e) => e.offset > offset))
                {
                    if (fixup.offset > bound)
                        break;

                    var param = resource.AllParams.First((m) => m.Name == fixup.param);
                    var globalParam = AllParams.First((m) => m.Name == fixup.param);

                    var fixupInfo = new FixupInfo(fixup.hash, fixup.offset - offset);

                    param.Fixups.Add(fixupInfo);

                    // add value once
                    if (!hashes.Contains(fixup.hash))
                    {
                        hashes.Add(fixup.hash);

                        foreach (var value in globalParam.Values)
                        {
                            var hashAttr = value.GetAttribute("hash");
                            var hashData = hashAttr.Data;

                            int paramHash = 0;

                            // HACKS!!!!!
                            if (hashData.Size < 4)
                            {
                                if (hashData.Size == 1)
                                {
                                    paramHash = (sbyte)hashData.Buffer[0];
                                }
                                else
                                {
                                    paramHash = Utils.UnpackData(hashAttr, BitConverter.ToInt16);
                                }
                            }
                            else
                            {
                                paramHash = Utils.UnpackData(hashAttr, BitConverter.ToInt32);
                            }

                            if (paramHash == fixup.hash)
                            {
                                param.Values.Add(value);
                                break;
                            }
                        }
                    }
                }

                var buffer = new byte[info.Size];

                Buffer.BlockCopy(MoveData, offset, buffer, 0, info.Size);

                resource.RootNodeId = info.RootNodeId;
                resource.MoveData = buffer;

                resources.Add(resource);

                offset += info.Size;
            }

            foreach (var resource in resources)
                srl.CreateXmlElement(resource.ToObject(), root);

#if PATCH_FIXUP_TAGS
            var buffer = new byte[MoveData.Length];

            Buffer.BlockCopy(MoveData, 0, buffer, 0, buffer.Length);

            foreach (var param in AllParams)
            {
                if (param.Tag == "xxxx")
                    continue;

                MagicNumber mgx = param.Tag;

                var tag = BitConverter.GetBytes((int)mgx);

                foreach (var fixup in param.Fixups)
                    Buffer.BlockCopy(tag, 0, buffer, fixup.Offset, tag.Length);
            }

            File.WriteAllBytes(Path.ChangeExtension(filename, ".tags.bin"), buffer);
#endif
#if DUMP_FIXUP_LOG_INFO
            var dbg = new List<(int offset, string name, int old, int hash)>();
            var sb = new StringBuilder();
            
            foreach (var param in AllParams)
            {
                var idx = 0;

                foreach (var fixup in param.Fixups)
                {
                    var orig = BitConverter.ToInt32(MoveData, fixup.Offset);

                    dbg.Add((fixup.Offset, $"{param.Name} fixup {idx++}", orig, fixup.Hash));
                }
            }

            var fixups = dbg.OrderBy((e) => e.offset).ToList();
            
            var infoOffset = 0;
            var infoIndex = 0;

            var rootDir = Path.GetDirectoryName(filename);
            var outDir = Path.Combine(rootDir, "movedata_files");

            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            foreach (var info in PerMoveResourceInfo.Infos)
            {
                var hash = BitConverter.ToInt32(MoveData, infoOffset);

                var unk_04 = BitConverter.ToInt32(MoveData, infoOffset + 4);

                var unk_08 = MoveData[infoOffset + 8];
                var unk_09 = MoveData[infoOffset + 9];
                var unk_0a = MoveData[infoOffset + 10];
                var unk_0b = MoveData[infoOffset + 11];

                sb.AppendLine($"MoveData({hash:X8})[{infoIndex++}] @ {infoOffset:X8}");
                sb.AppendLine($"  Unk_04: {unk_04}");
                sb.AppendLine($"  Unk_08: ({unk_08}, {unk_09}, {unk_0a}, {unk_0b})");
                sb.AppendLine($"  RootNodeId: {info.RootNodeId:X8}");

                var upperBound = (infoOffset + info.Size);
                
                var fixupOffset = infoOffset;

                foreach (var fixup in fixups.Where((e) => e.offset < upperBound))
                {
                    if (fixup.offset < infoOffset)
                        continue;

                    sb.AppendLine($" - {fixup.offset - infoOffset:X8}: {fixup.old:X8} -> {fixup.hash:X8} ; {fixup.name}");
                }

                var buffer = new byte[info.Size];

                Buffer.BlockCopy(MoveData, infoOffset, buffer, 0, info.Size);
                File.WriteAllBytes(Path.Combine(outDir, $"{unk_08}_{unk_09}_{unk_0a}_{unk_0b}#{hash:X8}.bin"), buffer);
                
                infoOffset += info.Size;
            }

            //var dbgTxt = String.Join("\r\n", dbg.OrderBy((e) => e.offset).Select((e) => $"{e.offset:X8}: {e.old:X8} -> {e.hash:X8} ; {e.name}"));
            //sb.Append(dbgTxt);

            File.WriteAllBytes(Path.ChangeExtension(filename, ".data.bin"), MoveData);
            File.WriteAllText(Path.ChangeExtension(filename, ".debug.log"), sb.ToString());
#endif
            xml.Add(root);
            xml.SaveFormatted(filename, true);
        }
    }
}
