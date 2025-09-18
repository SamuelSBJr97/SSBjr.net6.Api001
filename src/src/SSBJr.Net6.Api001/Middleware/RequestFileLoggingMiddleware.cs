using Microsoft.AspNetCore.Http;
using SSBJr.Net6.Api001.Infrastructure;
using SSBJr.Net6.Api001.Models;
using System.Text.Json;

namespace SSBJr.Net6.Api001.Middleware
{
    public class RequestFileLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestFileLoggingMiddleware> _logger;
        private readonly string _dataDir;

        private readonly SSBJr.Net6.Api001.Services.IFileBackedProcessor? _processor;

        public RequestFileLoggingMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<RequestFileLoggingMiddleware> logger, IServiceProvider services)
        {
            _next = next;
            _logger = logger;
            _dataDir = configuration.GetValue<string>("DataDirectory") ?? "data";
            FileQueue.EnsureDataDirectoryAsync(_dataDir).GetAwaiter().GetResult();
            _processor = services.GetService<SSBJr.Net6.Api001.Services.IFileBackedProcessor>();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Read request body (enable buffering)
            context.Request.EnableBuffering();
            string body = string.Empty;
            using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;
            }

            var log = new RequestLog
            {
                Path = context.Request.Path,
                Method = context.Request.Method,
                Headers = JsonSerializer.Serialize(context.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString())),
                Body = body
            };

            var requestsFile = Path.Combine(_dataDir, "requests.log");
            var pendingFile = Path.Combine(_dataDir, "pending_updates.log");

            try
            {
                await FileQueue.AppendLineAsync(requestsFile, log);

                // Try to parse pending update from the body or headers (this is a best-effort helper)
                PendingUpdate? pending = null;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        // If body is a PendingUpdate shape, serialize as such; otherwise, create a generic pending item
                        pending = JsonSerializer.Deserialize<PendingUpdate>(body);
                    }
                    catch { pending = null; }
                }

                if (pending == null)
                {
                    // Fallback: try to create a status update from headers
                    if (context.Request.Headers.TryGetValue("X-Notification-ExternalId", out var ext) && context.Request.Headers.TryGetValue("X-Notification-Status", out var status))
                    {
                        if (Enum.TryParse<NotificationStatus>(status.ToString(), out var parsed))
                        {
                            pending = new PendingUpdate
                            {
                                TargetExternalId = ext.ToString(),
                                Action = PendingUpdateAction.AddStatus,
                                StatusMask = parsed
                            };
                        }
                    }
                }

                if (pending != null)
                {
                    // prefer processor enqueue to start runner automatically
                    if (_processor != null) await _processor.EnqueueAsync(pending);
                    else await FileQueue.AppendLineAsync(pendingFile, pending);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log request or enqueue pending update");
            }

            await _next(context);
        }
    }

    // extension
    public static class RequestFileLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestFileLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestFileLoggingMiddleware>();
        }
    }
}
