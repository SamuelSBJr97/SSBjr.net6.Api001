using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using SSBJr.Net6.Api001.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SSBJr.Net6.Api001.Tests
{
    [TestClass]
    public class NotificationProcessingLoadTests
    {
        [TestMethod]
        public async Task Process_100_Items_Under_5_Seconds()
        {
            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddDbContext<Data.ApiDbContext>(opt => opt.UseInMemoryDatabase("load_db"));
                    services.AddSingleton<SSBJr.Net6.Api001.Services.IBackgroundQueue<PendingUpdate>, SSBJr.Net6.Api001.Services.ChannelBackgroundQueue<PendingUpdate>>();
                    services.AddHostedService(provider => (SSBJr.Net6.Api001.Services.ChannelBackgroundQueue<PendingUpdate>)provider.GetRequiredService<SSBJr.Net6.Api001.Services.IBackgroundQueue<PendingUpdate>>());
                })
                .Build();

            await host.StartAsync();
            var q = host.Services.GetRequiredService<SSBJr.Net6.Api001.Services.IBackgroundQueue<PendingUpdate>>();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                await q.EnqueueAsync(new PendingUpdate { TargetExternalId = $"LOAD_{i}", Action = PendingUpdateAction.AddStatus, StatusMask = NotificationStatus.LeituraOs });
            }

            // Wait up to 10s for processing (conservative)
            var db = host.Services.GetRequiredService<Data.ApiDbContext>();
            var timeout = Task.Delay(10000);
            while (!db.Notifications.Any(n => n.ExternalId == "LOAD_99") && !timeout.IsCompleted)
            {
                await Task.Delay(100);
            }
            sw.Stop();

            Assert.IsTrue(db.Notifications.Count() >= 100);
            Assert.IsTrue(sw.Elapsed.TotalSeconds < 10, $"Processing took too long: {sw.Elapsed}");

            await host.StopAsync();
        }
    }
}
