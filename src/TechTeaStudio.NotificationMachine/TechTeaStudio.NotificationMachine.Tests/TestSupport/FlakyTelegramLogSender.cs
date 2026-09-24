using TechTeaStudio.NotificationMachine.Telegram;

namespace TechTeaStudio.NotificationMachine.Tests.TestSupport;

/// <summary>Throws a non-shutdown OperationCanceledException on its first call, then behaves like <see cref="RecordingTelegramLogSender"/>.</summary>
internal sealed class FlakyTelegramLogSender : ITelegramLogSender
{
    private int _callCount;

    public int CallCount => _callCount;

    public Task<LogDeliveryResult> SendPreviousDayAsync(CancellationToken ct = default)
    {
        var count = Interlocked.Increment(ref _callCount);
        if (count == 1)
            throw new OperationCanceledException("Simulated non-shutdown cancellation from a faulty sender.");

        return Task.FromResult(new LogDeliveryResult(LogDeliveryStatus.Sent, "ok"));
    }

    public Task<LogDeliveryResult> SendLatestAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted service only calls SendPreviousDayAsync.");

    public Task<LogDeliveryResult> SendDailyLogAsync(DateOnly date, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted service only calls SendPreviousDayAsync.");
}
