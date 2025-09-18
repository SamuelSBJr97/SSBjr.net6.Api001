using System.Threading.Tasks;
using SSBJr.Net6.Api001.Models;
using System.Threading;

namespace SSBJr.Net6.Api001.Services
{
    public interface IBackgroundQueue<T>
    {
        ValueTask EnqueueAsync(T item);
    }
}
