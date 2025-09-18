using Microsoft.AspNetCore.Mvc;
using SSBJr.Net6.Api001.Infrastructure;
using SSBJr.Net6.Api001.Models;
using System.Text.Json;

namespace SSBJr.Net6.Api001.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly string _dataDir;

        private readonly SSBJr.Net6.Api001.Services.IFileBackedProcessor _processor;
        public NotificationsController(IConfiguration configuration, SSBJr.Net6.Api001.Services.IFileBackedProcessor processor)
        {
            _dataDir = configuration.GetValue<string>("DataDirectory") ?? "data";
            FileQueue.EnsureDataDirectoryAsync(_dataDir).GetAwaiter().GetResult();
            _processor = processor;
        }

        [HttpPost("enqueue")]
        public async Task<IActionResult> Enqueue([FromBody] PendingUpdate pending)
        {
            await _processor.EnqueueAsync(pending);
            return Accepted(new { pending.Id, pending.EnqueuedAt });
        }

        [HttpGet("pending")]
        public IActionResult GetPendingSample(int max = 50)
        {
            var pendingFile = Path.Combine(_dataDir, "pending_updates.log");
            if (!System.IO.File.Exists(pendingFile)) return Ok(Array.Empty<object>());
            var lines = System.IO.File.ReadAllLines(pendingFile).Take(max);
            var items = lines.Select(l =>
            {
                try { return JsonSerializer.Deserialize<PendingUpdate>(l); } catch { return null; }
            }).Where(x => x != null);
            return Ok(items);
        }
    }
}
