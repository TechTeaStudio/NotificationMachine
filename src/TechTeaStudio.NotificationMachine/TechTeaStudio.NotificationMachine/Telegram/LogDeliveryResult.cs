namespace TechTeaStudio.NotificationMachine.Telegram;

/// <summary>The result of a single <see cref="ITelegramLogSender"/> delivery attempt.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">A human-readable description, safe to show to an operator - the bot token is always scrubbed.</param>
/// <param name="FileName">The daily log file name that was resolved, when one was.</param>
public sealed record LogDeliveryResult(LogDeliveryStatus Status, string Message, string? FileName = null)
{
    /// <summary>True when <see cref="Status"/> is <see cref="LogDeliveryStatus.Sent"/>.</summary>
    public bool Success => Status == LogDeliveryStatus.Sent;
}
