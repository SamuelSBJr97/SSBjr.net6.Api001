using System;

namespace SSBJr.Net6.Api001.Models
{
    public enum PendingUpdateAction
    {
        AddStatus,
        RemoveStatus,
        ReplaceStatus
    }

    public class PendingUpdate
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? TargetExternalId { get; set; }
        public PendingUpdateAction Action { get; set; }
        public NotificationStatus StatusMask { get; set; }
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    }
}
