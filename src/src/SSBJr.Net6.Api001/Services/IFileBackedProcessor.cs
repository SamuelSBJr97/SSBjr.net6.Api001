using SSBJr.Net6.Api001.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SSBJr.Net6.Api001.Services
{
    public interface IFileBackedProcessor
    {
        ValueTask EnqueueAsync(PendingUpdate pending);
        Task StopAsync(CancellationToken cancellationToken);
    }
}
