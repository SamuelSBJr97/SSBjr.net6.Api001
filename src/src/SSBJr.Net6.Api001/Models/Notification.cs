using System;

namespace SSBJr.Net6.Api001.Models
{
    [Flags]
    public enum NotificationStatus
    {
        None = 0,
        LeituraOs = 1 << 0,
        FalhaOs = 1 << 1,
        SucessoOs = 1 << 2,
        LeituraAgendamento = 1 << 3,
        FalhaAgendamento = 1 << 4,
        SucessoAgendamento = 1 << 5
    }

    public class Notification
    {
        public int Id { get; set; }
        public string? ExternalId { get; set; }
        public NotificationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
