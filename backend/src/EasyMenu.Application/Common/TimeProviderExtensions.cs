namespace EasyMenu.Application.Common;

public static class TimeProviderExtensions
{
    /// <summary>
    /// Current UTC time truncated to microseconds, the precision of PostgreSQL <c>timestamptz</c>, so a value
    /// returned right after saving equals the one read back later.
    /// </summary>
    public static DateTimeOffset GetUtcNowForStorage(this TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
