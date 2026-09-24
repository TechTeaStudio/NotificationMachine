namespace TechTeaStudio.NotificationMachine.Telegram;

/// <summary>Options for <see cref="TelegramLogDeliveryService"/> and <see cref="ITelegramLogSender"/>.</summary>
public sealed class TelegramLogDeliveryOptions
{
    /// <summary>
    /// Default configuration section: <c>Telegram:Logs</c>. Chronos and Hyperion production
    /// secrets already use these keys - never rename them.
    /// </summary>
    public const string DefaultSectionPath = "Telegram:Logs";

    /// <summary>Master switch. Delivery also stays off while <see cref="BotToken"/> or <see cref="ChatId"/> are missing.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The bot token from @BotFather. Never appears in a log line or a returned message - see <see cref="ITelegramLogSender"/>.</summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// "-100123..." or "@channel". "0" counts as unset - DiscordBotMike configs use 0 as the
    /// placeholder and store ChatId as a JSON number.
    /// </summary>
    public string? ChatId { get; set; }

    /// <summary>Hour of day (0-23, clamped) in the <see cref="UtcOffset"/> zone.</summary>
    public int Hour { get; set; } = 6;

    /// <summary>Default Moscow: fixed +3, no DST since 2014. Clamped to +/-14h and whole minutes when used.</summary>
    public TimeSpan UtcOffset { get; set; } = TimeSpan.FromHours(3);

    /// <summary>{Service} in the caption. Null/blank falls back to the entry assembly name.</summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Telegram document caption. <c>{Service}</c> and <c>{Date}</c> (<c>yyyy-MM-dd</c>) are
    /// replaced; the result is plain text and truncated to 1024 characters, Telegram's caption limit.
    /// </summary>
    public string CaptionTemplate { get; set; } = "{Service} daily log for {Date}";

    /// <summary>
    /// The file name given to Serilog's WriteTo.File with RollingInterval.Day, e.g. "log.txt" or
    /// "authservice-.txt".
    /// </summary>
    public string LogFileName { get; set; } = "log.txt";

    /// <summary>
    /// Searched in order. Empty means the built-in defaults - kept empty here because the
    /// configuration binder APPENDS to a pre-filled list instead of replacing it.
    /// </summary>
    public IList<string> LogDirectories { get; set; } = new List<string>();

    /// <summary>True when <see cref="Enabled"/> is set and both <see cref="BotToken"/> and <see cref="ChatId"/> are present.</summary>
    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(BotToken) &&
        !string.IsNullOrWhiteSpace(ChatId) &&
        ChatId.Trim() != "0";

    internal string DescribeDisabledReason()
    {
        if (!Enabled) return "disabled in configuration";
        if (string.IsNullOrWhiteSpace(BotToken)) return "BotToken is not set";
        return "ChatId is not set";
    }
}
