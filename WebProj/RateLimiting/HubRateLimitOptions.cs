namespace WebProj.RateLimiting;

public sealed class HubRateLimitOptions
{
    public int SendMessagePermitLimit { get; set; } = 20;
    public TimeSpan SendMessageWindow { get; set; } = TimeSpan.FromSeconds(10);
    public static HubRateLimitOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting:SignalR");

        var permitLimit = section.GetValue<int?>(nameof(SendMessagePermitLimit)) ?? 20;
        var windowSeconds = section.GetValue<int?>("SendMessageWindowSeconds") ?? 10;

        return new HubRateLimitOptions
        {
            SendMessagePermitLimit = permitLimit,
            SendMessageWindow = TimeSpan.FromSeconds(windowSeconds)
        };
    }
}
