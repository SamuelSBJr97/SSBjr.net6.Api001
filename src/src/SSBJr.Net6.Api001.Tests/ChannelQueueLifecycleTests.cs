using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using SSBJr.Net6.Api001.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SSBJr.Net6.Api001.Tests
{
    [TestClass]
    public class ChannelQueueLifecycleTests
    {
        [TestMethod]
        public async Task Runner_Starts_Processes_And_Stops_Then_Restarts_On_New_Item()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "ssbjr_chan_life");
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            Directory.CreateDirectory(dataDir);

            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddDbContext<Data.ApiDbContext>(opt => opt.UseInMemoryDatabase("chanlife_db"));
                    services.AddSingleton<SSBJr.Net6.Api001.Services.IBackgroundQueue<PendingUpdate>, SSBJr.Net6.Api001.Services.ChannelBackgroundQueue<PendingUpdate>>();
                    services.AddHostedService(provider => (SSBJr.Net6.Api001.Services.ChannelBackgroundQueue<PendingUpdate>)provider.GetRequiredService<SSBJr.Net6.Api001.Services.IBackgroundQueue<PendingUpdate>>());
                })
                .Build();

            await host.StartAsync();
            var q = host.Services.GetRequiredService<SSBJr.Net6.Api001.Services.IBackgroundQueue<PendingUpdate>>();

            var p1 = new PendingUpdate { TargetExternalId = "LIFE_A", Action = PendingUpdateAction.AddStatus, StatusMask = NotificationStatus.LeituraOs };
            await q.EnqueueAsync(p1);

            // wait for processing (up to 2s)
            await Task.Delay(2000);

            var db = host.Services.GetRequiredService<Data.ApiDbContext>();
            Assert.IsTrue(db.Notifications.Any(n => n.ExternalId == "LIFE_A"));

            // wait for runner to stop (grace window is 1s in implementation)
            await Task.Delay(1500);

            // enqueue another item and ensure it is processed (runner restarted)
            var p2 = new PendingUpdate { TargetExternalId = "LIFE_B", Action = PendingUpdateAction.AddStatus, StatusMask = NotificationStatus.SucessoOs };
            await q.EnqueueAsync(p2);

            await Task.Delay(1000);
            db = host.Services.GetRequiredService<Data.ApiDbContext>();
            Assert.IsTrue(db.Notifications.Any(n => n.ExternalId == "LIFE_B"));

            await host.StopAsync();
        }
    }
}
