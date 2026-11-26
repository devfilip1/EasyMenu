using Microsoft.Extensions.Time.Testing;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>
/// The singleton <see cref="TimeProvider"/> registered in the API under test. It delegates to a replaceable
/// <see cref="FakeTimeProvider"/>, so each test can start from a fresh clock (a fake clock cannot go backwards)
/// while the API keeps the same singleton instance.
/// </summary>
public sealed class SwitchableTimeProvider : TimeProvider
{
    private FakeTimeProvider _current = new(DateTimeOffset.UtcNow);

    public FakeTimeProvider Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value);
    }

    public override DateTimeOffset GetUtcNow() => Current.GetUtcNow();

    public override long GetTimestamp() => Current.GetTimestamp();

    public override long TimestampFrequency => Current.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => Current.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        Current.CreateTimer(callback, state, dueTime, period);
}
