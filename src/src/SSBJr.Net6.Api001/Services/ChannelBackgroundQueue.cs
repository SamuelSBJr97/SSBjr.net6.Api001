using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SSBJr.Net6.Api001.Models;
using Microsoft.EntityFrameworkCore;

namespace SSBJr.Net6.Api001.Services
{
    public class ChannelBackgroundQueue<T> : IBackgroundQueue<T>, IHostedService
    {
        private readonly Channel<T> _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true });
        private readonly ILogger<ChannelBackgroundQueue<T>> _logger;
        private readonly IServiceProvider _services;
        private Task? _runner;
        private readonly object _sync = new object();
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public ChannelBackgroundQueue(ILogger<ChannelBackgroundQueue<T>> logger, IServiceProvider services)
        {
            _logger = logger;
            _services = services;
        }

        public ValueTask EnqueueAsync(T item)
        {
            _channel.Writer.TryWrite(item);
            StartRunnerIfNeeded();
            return ValueTask.CompletedTask;
        }

        private void StartRunnerIfNeeded()
        {
            lock (_sync)
            {
                if (_runner == null || _runner.IsCompleted)
                {
                    var token = _cts.Token;
                    _runner = Task.Run(() => RunnerLoopAsync(token), CancellationToken.None);
                    _logger.LogInformation("Channel runner started");
                }
            }
        }

        private async Task RunnerLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Drain available items
                    while (_channel.Reader.TryRead(out var item))
                    {
                        try
                        {
                            await ProcessItemAsync(item, token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing item");
                        }
                    }

                    // Wait for new items with a short grace window. If none arrive, exit runner.
                    using var grace = CancellationTokenSource.CreateLinkedTokenSource(token);
                    grace.CancelAfter(TimeSpan.FromSeconds(1));
                    try
                    {
                        var has = await _channel.Reader.WaitToReadAsync(grace.Token);
                        if (!has) break;
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested) break;
                        break; // grace timeout -> exit
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Channel runner error");
            }
            finally
            {
                _logger.LogInformation("Channel runner stopped");
            }
        }

        // Default process: if T is PendingUpdate, use DB; else override via DI or extension
        private async Task ProcessItemAsync(T item, CancellationToken token)
        {
            // If T is PendingUpdate, process against DB
            if (item is PendingUpdate p)
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Data.ApiDbContext>();
                // simple processing: find or create notification
                var notif = await db.Notifications.FirstOrDefaultAsync(n => n.ExternalId == p.TargetExternalId, token);
                if (notif == null)
                {
                    notif = new Notification { ExternalId = p.TargetExternalId, Status = NotificationStatus.None, CreatedAt = DateTime.UtcNow };
                    db.Notifications.Add(notif);
                }

                switch (p.Action)
                {
                    case PendingUpdateAction.AddStatus:
                        if (!(HasSuccessMaskOverlap(notif.Status, p.StatusMask) && HasOnlyFailMask(p.StatusMask)))
                            notif.Status |= p.StatusMask;
                        break;
                    case PendingUpdateAction.RemoveStatus:
                        notif.Status &= ~p.StatusMask;
                        break;
                    case PendingUpdateAction.ReplaceStatus:
                        notif.Status = p.StatusMask;
                        break;
                }

                notif.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(token);
            }
            else
            {
                // no-op for unknown types
                await Task.CompletedTask;
            }
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

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            Task? runner;
            lock (_sync) runner = _runner;
            if (runner != null)
            {
                var timeout = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                await Task.WhenAny(runner, timeout);
            }
        }
    }
}
