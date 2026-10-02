using Microsoft.Extensions.Time.Testing;

namespace TechTeaStudio.NotificationMachine.Tests.TestSupport;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that counts the timers created against it. The delivery
/// service arms exactly one timer per scheduled wait, so the count tells a test that the loop has
/// finished handling a fire and is waiting on the next one. Advancing the clock before that point
/// is a race: the loop then computes its next wait from the already-advanced time and sleeps a
/// full extra day.
/// </summary>
internal sealed class TimerCountingTimeProvider : FakeTimeProvider
{
    private int _timersCreated;

    public TimerCountingTimeProvider(DateTimeOffset startDateTime) : base(startDateTime) { }

    public int TimersCreated => Volatile.Read(ref _timersCreated);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        // Counted after the base call, so an observed count means the timer is already registered.
        var timer = base.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref _timersCreated);
        return timer;
    }
}
