using System.Text.Json;
using System.Text;

namespace SSBJr.Net6.Api001.Infrastructure
{
    public static class FileQueue
    {
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public static async Task EnsureDataDirectoryAsync(string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);
            await Task.CompletedTask;
        }

        public static async Task AppendLineAsync<T>(string filePath, T item)
        {
            var json = JsonSerializer.Serialize(item);
            await _fileLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(filePath, json + "\n", Encoding.UTF8);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        // Atomically move up to batchSize lines from pendingFile to a workFile and return the workFile path
        public static async Task<string?> ClaimBatchAsync(string pendingFile, string workFile, int batchSize)
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(pendingFile)) return null;
                var lines = await File.ReadAllLinesAsync(pendingFile, Encoding.UTF8);
                if (lines.Length == 0) return null;
                var take = lines.Take(batchSize).ToArray();
                var remaining = lines.Skip(take.Length).ToArray();

                // write work file
                await File.WriteAllLinesAsync(workFile, take, Encoding.UTF8);

                // write remaining back to pending via temp file replace
                var temp = pendingFile + ".tmp";
                await File.WriteAllLinesAsync(temp, remaining, Encoding.UTF8);
                File.Replace(temp, pendingFile, null);

                return workFile;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public static async Task AppendProcessedAsync(string processedFile, IEnumerable<string> processedLines)
        {
            await _fileLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(processedFile, string.Join('\n', processedLines) + '\n', Encoding.UTF8);
            }
            finally { _fileLock.Release(); }
        }

        // Requeue failed lines back to pending
        public static async Task RequeueFailedAsync(string pendingFile, IEnumerable<string> failedLines)
        {
            await _fileLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(pendingFile, string.Join('\n', failedLines) + '\n', Encoding.UTF8);
            }
            finally { _fileLock.Release(); }
        }
    }
}
