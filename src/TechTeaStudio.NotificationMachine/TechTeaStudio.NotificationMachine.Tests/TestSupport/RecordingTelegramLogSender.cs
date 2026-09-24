using TechTeaStudio.NotificationMachine.Telegram;

namespace TechTeaStudio.NotificationMachine.Tests.TestSupport;

internal sealed class RecordingTelegramLogSender : ITelegramLogSender
{
    private int _previousDayCallCount;

    public int PreviousDayCallCount => _previousDayCallCount;

    public Task<LogDeliveryResult> SendPreviousDayAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _previousDayCallCount);
        return Task.FromResult(new LogDeliveryResult(LogDeliveryStatus.Sent, "ok"));
    }

    public Task<LogDeliveryResult> SendLatestAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted service only calls SendPreviousDayAsync.");

    public Task<LogDeliveryResult> SendDailyLogAsync(DateOnly date, CancellationToken ct = default) =>
        throw new NotSupportedException("The hosted service only calls SendPreviousDayAsync.");
}
