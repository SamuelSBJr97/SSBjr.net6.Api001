using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace SSBJr.Net6.Api001.Tests
{
    [TestClass]
    public class MemoryUsageTests
    {
        [TestMethod]
        public void Memory_Should_Not_Exceed_200MB_For_10000_SmallObjects()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var before = Process.GetCurrentProcess().PrivateMemorySize64;

            var list = new System.Collections.Generic.List<byte[]>();
            for (int i = 0; i < 10000; i++)
            {
                list.Add(new byte[256]); // 256 bytes each ~ 2.56MB total
            }

            var after = Process.GetCurrentProcess().PrivateMemorySize64;
            var deltaMb = (after - before) / (1024.0 * 1024.0);
            // allow some buffer
            Assert.IsTrue(deltaMb < 200, $"Memory increased by {deltaMb} MB");
        }
    }
}
