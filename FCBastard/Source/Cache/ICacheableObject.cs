using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomad
{
    public interface ICacheableObject
    {
        int Size { get; }
        int GetHashCode();
    }
}
