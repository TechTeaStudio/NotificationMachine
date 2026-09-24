using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TechTeaStudio.NotificationMachine.Telegram;

internal sealed class TelegramLogSender : ITelegramLogSender
{
    internal const string HttpClientName = "TechTeaStudio.NotificationMachine.Telegram";

    /// <summary>Telegram's bot upload limit. Settable so tests can lower it.</summary>
    internal long MaxFileSizeBytes { get; set; } = 50L * 1024 * 1024;

    private readonly IOptionsMonitor<TelegramLogDeliveryOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<TelegramLogSender> _logger;

    public TelegramLogSender(
        IOptionsMonitor<TelegramLogDeliveryOptions> options,
        IHttpClientFactory httpClientFactory,
        TimeProvider time,
        ILogger<TelegramLogSender> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _time = time;
        _logger = logger;
    }

    public Task<LogDeliveryResult> SendLatestAsync(CancellationToken ct = default)
    {
        var today = Today();
        return SendFirstFoundAsync(new[] { today, today.AddDays(-1) }, ct);
    }

    public Task<LogDeliveryResult> SendPreviousDayAsync(CancellationToken ct = default)
    {
        var today = Today();
        return SendFirstFoundAsync(new[] { today.AddDays(-1), today }, ct);
    }

    public Task<LogDeliveryResult> SendDailyLogAsync(DateOnly date, CancellationToken ct = default) =>
        SendFirstFoundAsync(new[] { date }, ct);

    // Serilog's RollingInterval.Day stamps files with DateTime.Now (local time), not UTC.
    private DateOnly Today() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private async Task<LogDeliveryResult> SendFirstFoundAsync(IReadOnlyList<DateOnly> candidateDates, CancellationToken ct)
    {
        var options = _options.CurrentValue;

        if (!options.IsConfigured)
        {
            var reason = options.DescribeDisabledReason();
            _logger.LogDebug("Telegram log delivery skipped: {Reason}", reason);
            return new LogDeliveryResult(LogDeliveryStatus.Disabled, reason);
        }

        var directories = LogFileLocator.ResolveDirectories(options.LogDirectories);
        var fileNames = candidateDates.Select(date => LogFileNaming.BuildFileName(options.LogFileName, date)).ToArray();

        for (var i = 0; i < candidateDates.Count; i++)
        {
            var path = LogFileLocator.Find(directories, fileNames[i]);
            if (path is null)
                continue;

            var caption = LogCaption.Build(options.CaptionTemplate, options.ServiceName, candidateDates[i]);
            return await SendFileAsync(options, path, fileNames[i], caption, ct).ConfigureAwait(false);
        }

        var message = $"Log file not found. Tried: {string.Join(", ", fileNames)}.";
        _logger.LogWarning("Telegram log delivery failed: {Message}", message);
        return new LogDeliveryResult(LogDeliveryStatus.FileNotFound, message);
    }

    private async Task<LogDeliveryResult> SendFileAsync(
        TelegramLogDeliveryOptions options, string path, string fileName, string caption, CancellationToken ct)
    {
        var token = options.BotToken!;

        try
        {
            await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = fileStream.Length;

            if (length > MaxFileSizeBytes)
            {
                var tooLargeMessage = $"Log file '{fileName}' is {length} bytes, exceeding the {MaxFileSizeBytes} byte limit.";
                _logger.LogWarning("Telegram log delivery failed: {Message}", tooLargeMessage);
                return new LogDeliveryResult(LogDeliveryStatus.FileTooLarge, tooLargeMessage, fileName);
            }

            // Snapshot the length up front and read exactly that many bytes: Serilog may still be
            // appending to today's file, and a request body that grows past the Content-Length
            // computed up front aborts the upload.
            var buffer = new byte[length];
            await fileStream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);

            var chatId = options.ChatId!.Trim();
            using var content = new MultipartFormDataContent
            {
                { new StringContent(chatId), "chat_id" },
                { new StringContent(caption), "caption" },
            };
            var document = new ByteArrayContent(buffer);
            document.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(document, "document", fileName);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = $"https://api.telegram.org/bot{token}/sendDocument";
            using var response = await client.PostAsync(url, content, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Sent {FileName} ({Length} bytes) to Telegram chat {ChatId}.", fileName, length, chatId);
                return new LogDeliveryResult(LogDeliveryStatus.Sent, $"Log file '{fileName}' sent successfully to Telegram.", fileName);
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var preview = body.Length > 300 ? body[..300] : body;
            var failedMessage = Scrub($"Telegram returned HTTP {(int)response.StatusCode}: {preview}", token);
            _logger.LogWarning("Telegram log delivery failed: {Message}", failedMessage);
            return new LogDeliveryResult(LogDeliveryStatus.Failed, failedMessage, fileName);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var message = Scrub("Telegram request timed out.", token);
            _logger.LogWarning("Telegram log delivery failed: {Message}", message);
            return new LogDeliveryResult(LogDeliveryStatus.Failed, message, fileName);
        }
        catch (HttpRequestException ex)
        {
            var message = Scrub($"Telegram request failed: {ex.Message}", token);
            _logger.LogWarning("Telegram log delivery failed: {Message}", message);
            return new LogDeliveryResult(LogDeliveryStatus.Failed, message, fileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var message = Scrub($"Telegram request failed: {ex.Message}", token);
            _logger.LogWarning("Telegram log delivery failed: {Message}", message);
            return new LogDeliveryResult(LogDeliveryStatus.Failed, message, fileName);
        }
    }

    // Never let the bot token reach a message or a log line.
    private static string Scrub(string message, string token) =>
        string.IsNullOrEmpty(token) ? message : message.Replace(token, "***");
}
