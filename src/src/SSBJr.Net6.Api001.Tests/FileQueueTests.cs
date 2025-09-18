using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;
using SSBJr.Net6.Api001.Infrastructure;
using SSBJr.Net6.Api001.Models;
using System.Linq;

namespace SSBJr.Net6.Api001.Tests
{
    [TestClass]
    public class FileQueueTests
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssbjr_tests");

        [TestInitialize]
        public void Init()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
        }

        [TestMethod]
        public async Task AppendAndClaimBatch_Works()
        {
            var pendingFile = Path.Combine(_dir, "pending.log");
            var workFile = Path.Combine(_dir, "work.log");

            var p1 = new PendingUpdate { TargetExternalId = "A", Action = PendingUpdateAction.AddStatus, StatusMask = NotificationStatus.LeituraOs };
            var p2 = new PendingUpdate { TargetExternalId = "B", Action = PendingUpdateAction.AddStatus, StatusMask = NotificationStatus.SucessoOs };

            await FileQueue.AppendLineAsync(pendingFile, p1);
            await FileQueue.AppendLineAsync(pendingFile, p2);

            var claimed = await FileQueue.ClaimBatchAsync(pendingFile, workFile, 1);
            Assert.IsNotNull(claimed);
            var workLines = File.ReadAllLines(workFile);
            Assert.AreEqual(1, workLines.Length);

            var remaining = File.ReadAllLines(pendingFile);
            Assert.AreEqual(1, remaining.Length);
        }
    }
}
