namespace TechTeaStudio.NotificationMachine.Telegram;

/// <summary>
/// Sends a daily Serilog rolling log file to Telegram. Every failure - a missing file, a Telegram
/// API error, a network error - comes back as a <see cref="LogDeliveryResult"/>; these methods
/// never throw for that. The only exception a caller sees is <see cref="OperationCanceledException"/>
/// from cancelling its own <see cref="CancellationToken"/>.
/// </summary>
public interface ITelegramLogSender
{
    /// <summary>Today's log file, falling back to yesterday's. What an on-demand "send the log now" command wants.</summary>
    Task<LogDeliveryResult> SendLatestAsync(CancellationToken ct = default);

    /// <summary>Yesterday's log file, falling back to today's. What the daily schedule sends.</summary>
    Task<LogDeliveryResult> SendPreviousDayAsync(CancellationToken ct = default);

    /// <summary>Exactly the given day's log file.</summary>
    Task<LogDeliveryResult> SendDailyLogAsync(DateOnly date, CancellationToken ct = default);
}
