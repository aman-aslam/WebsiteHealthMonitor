namespace WebsiteHealthMonitor.Models
{
    public class WebsiteHealthResult
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsUp { get; set; }
        public int? StatusCode { get; set; }
        public long ResponseTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CheckedAt { get; set; }
        public long PageLoadTimeMs { get; set; }
    }
}
