using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SSBJr.Net6.Api001.Infrastructure;
using SSBJr.Net6.Api001.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SSBJr.Net6.Api001.Services
{
    public class FileBackedTriggeredProcessor : IFileBackedProcessor
    {
        private readonly string _dataDir;
        private readonly string _pendingFile;
        private readonly ILogger<FileBackedTriggeredProcessor> _logger;
        private readonly IServiceProvider _services;

        private readonly object _sync = new object();
        private Task? _runner;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public FileBackedTriggeredProcessor(IConfiguration configuration, ILogger<FileBackedTriggeredProcessor> logger, IServiceProvider services)
        {
            _dataDir = configuration.GetValue<string>("DataDirectory") ?? "data";
            _pendingFile = Path.Combine(_dataDir, "pending_updates.log");
            _logger = logger;
            _services = services;
            FileQueue.EnsureDataDirectoryAsync(_dataDir).GetAwaiter().GetResult();
        }

        public async ValueTask EnqueueAsync(PendingUpdate pending)
        {
            await FileQueue.AppendLineAsync(_pendingFile, pending);
            StartRunnerIfNeeded();
        }

        private void StartRunnerIfNeeded()
        {
            lock (_sync)
            {
                if (_runner == null || _runner.IsCompleted)
                {
                    var token = _cts.Token;
                    _runner = Task.Run(() => RunnerLoopAsync(token), CancellationToken.None);
                    _logger.LogInformation("File-backed runner started");
                }
            }
        }

        private async Task RunnerLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var workFile = Path.Combine(_dataDir, $"work_{Guid.NewGuid()}.log");
                    var claimed = await FileQueue.ClaimBatchAsync(_pendingFile, workFile, 50);
                    if (claimed == null)
                    {
                        // no work -> exit
                        _logger.LogInformation("No more batches to process; runner exiting");
                        break;
                    }

                    var lines = await File.ReadAllLinesAsync(workFile, token);
                    var processed = new List<string>();
                    var failed = new List<string>();

                    using (var scope = _services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<Data.ApiDbContext>();
                        foreach (var line in lines)
                        {
                            try
                            {
                                var pending = JsonSerializer.Deserialize<PendingUpdate>(line);
                                if (pending == null) { failed.Add(line); continue; }
                                // ProcessPendingAsync similar to previous service
                                await ProcessPendingAsync(db, pending, token);
                                processed.Add(line);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Processing line failed");
                                failed.Add(line);
                            }
                        }

                        await db.SaveChangesAsync(token);
                    }

                    if (processed.Any()) await FileQueue.AppendProcessedAsync(Path.Combine(_dataDir, "processed_updates.log"), processed);
                    if (failed.Any()) await FileQueue.RequeueFailedAsync(_pendingFile, failed);

                    try { File.Delete(workFile); } catch { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runner loop error");
            }
            finally
            {
                _logger.LogInformation("Runner terminated");
            }
        }

        private async Task ProcessPendingAsync(Data.ApiDbContext db, PendingUpdate pending, CancellationToken cancellationToken)
        {
            var notif = await db.Notifications.FirstOrDefaultAsync(n => n.ExternalId == pending.TargetExternalId, cancellationToken);
            if (notif == null)
            {
                notif = new Notification
                {
                    ExternalId = pending.TargetExternalId,
                    Status = NotificationStatus.None,
                    CreatedAt = DateTime.UtcNow
                };
                db.Notifications.Add(notif);
            }

            switch (pending.Action)
            {
                case PendingUpdateAction.AddStatus:
                    if (HasSuccessMaskOverlap(notif.Status, pending.StatusMask) && HasOnlyFailMask(pending.StatusMask)) { }
                    else
                    {
                        notif.Status |= pending.StatusMask;
                    }
                    break;
                case PendingUpdateAction.RemoveStatus:
                    notif.Status &= ~pending.StatusMask;
                    break;
                case PendingUpdateAction.ReplaceStatus:
                    notif.Status = pending.StatusMask;
                    break;
            }

            notif.UpdatedAt = DateTime.UtcNow;
            await Task.CompletedTask;
        }

        private static bool HasSuccessMaskOverlap(NotificationStatus current, NotificationStatus mask)
        {
            var successMasks = NotificationStatus.SucessoOs | NotificationStatus.SucessoAgendamento;
            return (current & successMasks) != 0 && (mask & successMasks) != 0;
        }

        private static bool HasOnlyFailMask(NotificationStatus mask)
        {
            var failMasks = NotificationStatus.FalhaOs | NotificationStatus.FalhaAgendamento;
            return (mask & ~failMasks) == 0 && (mask & failMasks) != 0;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            Task? runner;
            lock (_sync) runner = _runner;
            if (runner != null)
            {
                var delay = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                await Task.WhenAny(runner, delay);
            }
        }
    }
}
