using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SSBJr.Net6.Api001.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using SSBJr.Net6.Api001.Models;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SSBJr.Net6.Api001.Tests
{
    [TestClass]
    public class ControllerAndMiddlewareTests
    {
        private IHostBuilder CreateHostBuilder(string dataDir)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddDbContext<ApiDbContext>(opt => opt.UseInMemoryDatabase("testdb"));
                        // register channel background queue so controller's dependency is satisfied during tests
                        services.AddSingleton<SSBJr.Net6.Api001.Services.IBackgroundQueue<SSBJr.Net6.Api001.Models.PendingUpdate>, SSBJr.Net6.Api001.Services.ChannelBackgroundQueue<SSBJr.Net6.Api001.Models.PendingUpdate>>();
                        services.AddHostedService(provider => (SSBJr.Net6.Api001.Services.ChannelBackgroundQueue<SSBJr.Net6.Api001.Models.PendingUpdate>)provider.GetRequiredService<SSBJr.Net6.Api001.Services.IBackgroundQueue<SSBJr.Net6.Api001.Models.PendingUpdate>>());
                        // Add controllers from the main project assembly so TestServer discovers them
                        services.AddControllers()
                                .AddApplicationPart(typeof(SSBJr.Net6.Api001.Controllers.NotificationsController).Assembly);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
                })
                .ConfigureAppConfiguration((ctx, cb) => cb.AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("DataDirectory", dataDir) }));
        }

        [TestMethod]
        public async Task NotificationsController_EnqueueAndPendingWorks()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "ssbjr_tests_ctrl");
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            Directory.CreateDirectory(dataDir);

            var builder = CreateHostBuilder(dataDir);
            using var host = await builder.StartAsync();
            var client = host.GetTestClient();

            var pending = new PendingUpdate { TargetExternalId = "X", Action = PendingUpdateAction.AddStatus, StatusMask = NotificationStatus.SucessoOs };
            var resp = await client.PostAsJsonAsync("/api/notifications/enqueue", pending);
            resp.EnsureSuccessStatusCode();

            // Channel queue is registered in tests; it will process the pending item into the in-memory DB.
            await Task.Delay(1000);
            var db = host.Services.GetRequiredService<ApiDbContext>();
            Assert.IsTrue(db.Notifications.Any(n => n.ExternalId == "X"));
        }
    }
}
