using System.Security.Claims;
using System.Threading.RateLimiting;
using Application.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace WebProj.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingSettings>(configuration.GetSection(RateLimitingSettings.SectionName));
        var settings = configuration.GetSection(RateLimitingSettings.SectionName).Get<RateLimitingSettings>() ?? new RateLimitingSettings();
        services.Configure<HubRateLimitOptions>(opts =>
        {
            opts.SendMessagePermitLimit = settings.SignalR.SendMessagePermitLimit;
            opts.SendMessageWindow = TimeSpan.FromSeconds(settings.SignalR.SendMessageWindowSeconds);
        });

        if (settings.SignalR.Enabled)
        {
            services.AddSingleton<IHubFilter, ChatHubRateLimitFilter>();
        }

        if (!settings.Http.Enabled)
        {
            return services;
        }

        services.AddRateLimiter(options =>
        {
            options.OnRejected = (_, _) => throw new RateLimitExceededException("Rate limit exceeded. Please slow down.");
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.Http.Global.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.Http.Global.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = settings.Http.Global.QueueLimit
                });
            });
            
            options.AddPolicy(settings.Http.PerUser.PolicyName, httpContext =>
            {
                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var key = string.IsNullOrWhiteSpace(userId)
                    ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon"
                    : $"user:{userId}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.Http.PerUser.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.Http.PerUser.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = settings.Http.PerUser.QueueLimit
                });
            });
        });

        return services;
    }

    public static IApplicationBuilder UseAppRateLimiting(this IApplicationBuilder app)
    {
        return app.UseRateLimiter();
    }

    public static IEndpointConventionBuilder RequireAppRateLimiting(this IEndpointConventionBuilder builder, string? policyName = null)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            policyName = "per-user";
        }

        return builder.RequireRateLimiting(policyName);
    }
}
