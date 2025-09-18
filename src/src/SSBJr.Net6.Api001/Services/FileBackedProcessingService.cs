using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SSBJr.Net6.Api001.Data;
using SSBJr.Net6.Api001.Infrastructure;
using SSBJr.Net6.Api001.Models;
using System.Text.Json;

namespace SSBJr.Net6.Api001.Services
{
    public class FileBackedProcessingService : BackgroundService
    {
        private readonly ILogger<FileBackedProcessingService> _logger;
        private readonly IServiceProvider _services;
        private readonly string _dataDir;
        private readonly int _batchSize = 50;

        public FileBackedProcessingService(ILogger<FileBackedProcessingService> logger, IServiceProvider services, IConfiguration configuration)
        {
            _logger = logger;
            _services = services;
            _dataDir = configuration.GetValue<string>("DataDirectory") ?? "data";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FileBackedProcessingService started");

            var pendingFile = Path.Combine(_dataDir, "pending_updates.log");
            var processedFile = Path.Combine(_dataDir, "processed_updates.log");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Claim a batch
                    var workFile = Path.Combine(_dataDir, $"work_{Guid.NewGuid()}.log");
                    var claimed = await FileQueue.ClaimBatchAsync(pendingFile, workFile, _batchSize);
                    if (claimed == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                        continue;
                    }

                    var lines = await File.ReadAllLinesAsync(workFile, stoppingToken);
                    var processed = new List<string>();
                    var failed = new List<string>();

                    using (var scope = _services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
                        foreach (var line in lines)
                        {
                            try
                            {
                                var pending = JsonSerializer.Deserialize<PendingUpdate>(line);
                                if (pending == null)
                                {
                                    failed.Add(line);
                                    continue;
                                }

                                // Process based on action
                                await ProcessPendingAsync(db, pending, stoppingToken);
                                processed.Add(line);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed processing a pending update");
                                failed.Add(line);
                            }
                        }

                        // Save changes after batch
                        await db.SaveChangesAsync(stoppingToken);
                    }

                    // append processed and requeue failed
                    if (processed.Any()) await FileQueue.AppendProcessedAsync(processedFile, processed);
                    if (failed.Any()) await FileQueue.RequeueFailedAsync(pendingFile, failed);

                    // delete work file
                    try { File.Delete(workFile); } catch { }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in FileBackedProcessingService main loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("FileBackedProcessingService stopping");
        }

        private async Task ProcessPendingAsync(ApiDbContext db, PendingUpdate pending, CancellationToken cancellationToken)
        {
            // Find notification by external id
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

            // Apply rules (simplified): AddStatus will OR the mask unless Sucesso exists
            switch (pending.Action)
            {
                case PendingUpdateAction.AddStatus:
                    // if adding Falha but Sucesso already present for same area, then ignore Falha
                    if (HasSuccessMaskOverlap(notif.Status, pending.StatusMask) && HasOnlyFailMask(pending.StatusMask))
                    {
                        // ignore
                    }
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
    }
}
