using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TechTeaStudio.NotificationMachine.Telegram;

/// <summary>
/// Sends the previous day's Serilog rolling log file via Telegram once a day. Public so hosts can
/// assert their hosted-service registrations by type.
/// </summary>
public sealed class TelegramLogDeliveryService : BackgroundService
{
    private readonly IOptionsMonitor<TelegramLogDeliveryOptions> _options;
    private readonly ITelegramLogSender _sender;
    private readonly TimeProvider _time;
    private readonly ILogger<TelegramLogDeliveryService> _logger;

    /// <summary>
    /// Constructs the service. Normally resolved from DI - see
    /// <see cref="TelegramLogDeliveryServiceCollectionExtensions"/> - rather than constructed directly.
    /// </summary>
    public TelegramLogDeliveryService(
        IOptionsMonitor<TelegramLogDeliveryOptions> options,
        ITelegramLogSender sender,
        TimeProvider time,
        ILogger<TelegramLogDeliveryService> logger)
    {
        _options = options;
        _sender = sender;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStartupState(_options.CurrentValue);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _time.GetUtcNow();
            var scheduleOptions = _options.CurrentValue;
            var next = TelegramSendSchedule.NextSendTime(now, scheduleOptions.Hour, scheduleOptions.UtcOffset);

            try
            {
                await Task.Delay(next - now, _time, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            var fireOptions = _options.CurrentValue;
            if (!fireOptions.IsConfigured)
            {
                _logger.LogDebug("Telegram log delivery skipped: {Reason}", fireOptions.DescribeDisabledReason());
                continue;
            }

            try
            {
                var result = await _sender.SendPreviousDayAsync(stoppingToken).ConfigureAwait(false);
                LogResult(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending the daily log via Telegram.");
            }
        }
    }

    private void LogStartupState(TelegramLogDeliveryOptions options)
    {
        if (options.IsConfigured)
        {
            _logger.LogInformation(
                "Telegram log delivery started for {Service}, daily at {Hour:00}:00 UTC{Offset}",
                LogCaption.ResolveServiceName(options.ServiceName), Math.Clamp(options.Hour, 0, 23), FormatOffset(options.UtcOffset));
        }
        else
        {
            _logger.LogInformation(
                "Telegram log delivery idle: {Reason}; re-checked at every scheduled time, so a config reload can enable it",
                options.DescribeDisabledReason());
        }
    }

    private void LogResult(LogDeliveryResult result)
    {
        switch (result.Status)
        {
            case LogDeliveryStatus.Sent:
                _logger.LogInformation("Daily log delivery: {Message}", result.Message);
                break;
            case LogDeliveryStatus.Disabled:
                _logger.LogDebug("Daily log delivery: {Message}", result.Message);
                break;
            default:
                _logger.LogWarning("Daily log delivery: {Message}", result.Message);
                break;
        }
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sanitized = TelegramSendSchedule.SanitizeOffset(offset);
        var sign = sanitized < TimeSpan.Zero ? "-" : "+";
        return $"{sign}{sanitized.Duration():hh\\:mm}";
    }
}
