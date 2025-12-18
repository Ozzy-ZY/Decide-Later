using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Application.Exceptions;

namespace WebProj.RateLimiting;

/// <summary>
/// Simple in-memory per-user fixed-window limiter for ChatHub method invocations.
/// Focuses on SendMessage to prevent spam.
/// </summary>
public sealed class ChatHubRateLimitFilter(IOptions<HubRateLimitOptions> options) : IHubFilter
{
    private sealed class WindowCounter
    {
        public long WindowStartTicks;
        public int Count;
    }

    private readonly HubRateLimitOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, WindowCounter> _sendMessageCounters = new();

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!string.Equals(invocationContext.HubMethodName, "SendMessage", StringComparison.Ordinal))
            return await next(invocationContext);
        var userId = invocationContext.Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? invocationContext.Context.ConnectionId;

        EnforceFixedWindow(userId);

        return await next(invocationContext);
    }

    private void EnforceFixedWindow(string key)
    {
        var nowTicks = DateTime.UtcNow.Ticks;

        var counter = _sendMessageCounters.GetOrAdd(key,
            _ => new WindowCounter
            {
                WindowStartTicks = nowTicks,
                Count = 0
            });

        lock (counter)
        {
            var windowTicks = _options.SendMessageWindow.Ticks;

            if (nowTicks - counter.WindowStartTicks >= windowTicks)
            {
                counter.WindowStartTicks = nowTicks;
                counter.Count = 0;
            }

            counter.Count++;

            if (counter.Count > _options.SendMessagePermitLimit)
            {
                throw new RateLimitExceededException("Rate limit exceeded. Try again in a moment.");
            }
        }
    }
}
