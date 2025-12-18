namespace Infrastructure.Configurations;

public class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public HttpRateLimitingSettings Http { get; set; } = new();
    public SignalRRateLimitingSettings SignalR { get; set; } = new();

    public class HttpRateLimitingSettings
    {
        public bool Enabled { get; set; } = true;
        public GlobalRateLimitingSettings Global { get; set; } = new();
        public PerUserRateLimitingSettings PerUser { get; set; } = new();

        public class GlobalRateLimitingSettings
        {
            public int PermitLimit { get; set; } = 100;
            public int WindowSeconds { get; set; } = 60;
            public int QueueLimit { get; set; } = 0;
        }

        public class PerUserRateLimitingSettings
        {
            public bool Enabled { get; set; } = true;
            public int PermitLimit { get; set; } = 20;
            public int WindowSeconds { get; set; } = 10;
            public int QueueLimit { get; set; } = 0;
            public string PolicyName { get; set; } = "per-user";
        }
    }

    public class SignalRRateLimitingSettings
    {
        public bool Enabled { get; set; } = true;

        public int SendMessagePermitLimit { get; set; } = 20;
        public int SendMessageWindowSeconds { get; set; } = 10;
    }
}

