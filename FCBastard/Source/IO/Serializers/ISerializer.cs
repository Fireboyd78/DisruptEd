namespace Nomad
{
    public interface ISerializer<T>
    {
        void Serialize(T input);
        void Deserialize(T output);
    }
}
