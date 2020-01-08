namespace Nomad
{
    public interface IResourceFile
    {
        void LoadBinary(string filename);
        void SaveBinary(string filename);

        void LoadXml(string filename);
        void SaveXml(string fileanme);
    }
}
