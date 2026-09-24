using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using TechTeaStudio.NotificationMachine.Telegram;
using TechTeaStudio.NotificationMachine.Tests.TestSupport;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class TelegramLogDeliveryServiceTests
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(5);

    private static TelegramLogDeliveryOptions ConfiguredOptions(int hour, TimeSpan offset) => new()
    {
        BotToken = "123:abc",
        ChatId = "42",
        Hour = hour,
        UtcOffset = offset,
    };

    [Fact]
    public async Task ExecuteAsync_CrossingScheduledTime_CallsSendPreviousDayOnceThenAgainNextDay()
    {
        var offset = TimeSpan.FromHours(3);
        // 2026-09-24T02:59:00Z == 2026-09-24 05:59 MSK.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 24, 2, 59, 0, TimeSpan.Zero));
        var monitor = new TestOptionsMonitor<TelegramLogDeliveryOptions>(ConfiguredOptions(6, offset));
        var sender = new RecordingTelegramLogSender();
        var service = new TelegramLogDeliveryService(monitor, sender, time, NullLogger<TelegramLogDeliveryService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            time.Advance(TimeSpan.FromMinutes(1));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => sender.PreviousDayCallCount == 1, PollTimeout));

            time.Advance(TimeSpan.FromHours(24));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => sender.PreviousDayCallCount == 2, PollTimeout));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_UnconfiguredAtFireTime_SkipsThenCallsOnceConfiguredAtNextFire()
    {
        var offset = TimeSpan.Zero;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 24, 5, 59, 0, TimeSpan.Zero));
        var options = new TelegramLogDeliveryOptions { Hour = 6, UtcOffset = offset }; // BotToken/ChatId unset.
        var monitor = new TestOptionsMonitor<TelegramLogDeliveryOptions>(options);
        var sender = new RecordingTelegramLogSender();
        var logger = new CapturingLogger<TelegramLogDeliveryService>();
        var service = new TelegramLogDeliveryService(monitor, sender, time, logger);

        await service.StartAsync(CancellationToken.None);
        try
        {
            time.Advance(TimeSpan.FromMinutes(1));
            // Wait for the "skipped" Debug entry rather than a fixed delay: it proves the fire
            // already happened and was evaluated, so the call-count assertion below is not a race.
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(
                () => logger.Any(LogLevel.Debug, m => m.Contains("skipped")), PollTimeout));
            Assert.Equal(0, sender.PreviousDayCallCount);

            monitor.CurrentValue = ConfiguredOptions(6, offset);
            time.Advance(TimeSpan.FromHours(24));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => sender.PreviousDayCallCount == 1, PollTimeout));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StopAsync_DuringWait_CompletesWithoutThrowing()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero));
        var monitor = new TestOptionsMonitor<TelegramLogDeliveryOptions>(ConfiguredOptions(6, TimeSpan.Zero));
        var sender = new RecordingTelegramLogSender();
        var service = new TelegramLogDeliveryService(monitor, sender, time, NullLogger<TelegramLogDeliveryService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var exception = await Record.ExceptionAsync(() => service.StopAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteAsync_SenderThrowsNonShutdownOperationCanceled_LoopSurvivesAndCallsAgainNextFire()
    {
        var offset = TimeSpan.Zero;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 24, 5, 59, 0, TimeSpan.Zero));
        var monitor = new TestOptionsMonitor<TelegramLogDeliveryOptions>(ConfiguredOptions(6, offset));
        var sender = new FlakyTelegramLogSender();
        var service = new TelegramLogDeliveryService(monitor, sender, time, NullLogger<TelegramLogDeliveryService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            time.Advance(TimeSpan.FromMinutes(1));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => sender.CallCount == 1, PollTimeout));

            time.Advance(TimeSpan.FromHours(24));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => sender.CallCount == 2, PollTimeout));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_HourOutOfRange_StartupLogUsesClampedHour()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero));
        var monitor = new TestOptionsMonitor<TelegramLogDeliveryOptions>(ConfiguredOptions(25, TimeSpan.Zero));
        var sender = new RecordingTelegramLogSender();
        var logger = new CapturingLogger<TelegramLogDeliveryService>();
        var service = new TelegramLogDeliveryService(monitor, sender, time, logger);

        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(logger.Any(LogLevel.Information, m => m.Contains("23:00") && !m.Contains("25:00")));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
