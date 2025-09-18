using System;

namespace SSBJr.Net6.Api001.Models
{
    public class RequestLog
    {
        public int Id { get; set; }
        public string? Path { get; set; }
        public string? Method { get; set; }
        public string? Headers { get; set; }
        public string? Body { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    }
}
