namespace TechTeaStudio.NotificationMachine.Telegram;

/// <summary>The outcome of a single <see cref="ITelegramLogSender"/> delivery attempt.</summary>
public enum LogDeliveryStatus
{
    /// <summary>The document was uploaded to Telegram successfully.</summary>
    Sent,

    /// <summary>Delivery is disabled, or <c>BotToken</c>/<c>ChatId</c> are not set. No HTTP call was made.</summary>
    Disabled,

    /// <summary>None of the candidate daily log files could be found in any configured directory.</summary>
    FileNotFound,

    /// <summary>The log file exceeds the internal size cap (Telegram's bot upload limit). No HTTP call was made.</summary>
    FileTooLarge,

    /// <summary>The file was found but could not be sent - a Telegram API error, a network error, or a file I/O error.</summary>
    Failed
}
