using System.IO;

namespace Nomad
{
    public static class FileFactory
    {
        public static FileType GetFileType(string filename)
        {
            var ext = Path.GetExtension(filename);

            switch (ext)
            {
            //case ".bin":
            //case ".dat":
            //case ".fcb":
            //case ".lib":
            //case ".obj":
            //case ".ndb":
            //case ".rml":
            //case ".rcache":
                //return FileType.Binary;

            case ".xml":
                return FileType.Xml;

            //case ".fc5map":
                //return FileType.FCXMap;
            }

            return FileType.Binary;
        }
    }
}