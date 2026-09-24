using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using TechTeaStudio.NotificationMachine.Telegram;
using TechTeaStudio.NotificationMachine.Tests.TestSupport;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class TelegramLogSenderTests : IDisposable
{
    private readonly string _root;

    public TelegramLogSenderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ttsnm-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static TelegramLogDeliveryOptions CreateOptions(string logDirectory, Action<TelegramLogDeliveryOptions>? configure = null)
    {
        var options = new TelegramLogDeliveryOptions
        {
            BotToken = "123:abc",
            ChatId = "42",
            LogDirectories = new List<string> { logDirectory },
        };
        configure?.Invoke(options);
        return options;
    }

    private static TelegramLogSender CreateSender(
        TelegramLogDeliveryOptions options,
        FakeHttpMessageHandler handler,
        TimeProvider? time = null) =>
        new(new TestOptionsMonitor<TelegramLogDeliveryOptions>(options),
            new FakeHttpClientFactory(handler),
            time ?? TimeProvider.System,
            NullLogger<TelegramLogSender>.Instance);

    private void WriteLogFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_root, fileName), content);

    [Fact]
    public async Task SendDailyLogAsync_PostsMultipartDocumentToTelegramApi()
    {
        WriteLogFile("log20260923.txt", "test log content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.True(result.Success, result.Message);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Request.Method);
        Assert.Equal("https://api.telegram.org/bot123:abc/sendDocument", request.Request.RequestUri!.ToString());
        Assert.Contains("42", request.Body);
        Assert.Contains("log20260923.txt", request.Body);
        Assert.Contains("test log content", request.Body);
    }

    [Fact]
    public async Task SendLatestAsync_TodayMissing_FallsBackToYesterday()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
        WriteLogFile("log20260922.txt", "yesterday content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler, time);

        var result = await sender.SendLatestAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("log20260922.txt", result.FileName);
    }

    [Fact]
    public async Task SendLatestAsync_TodayPresent_PrefersToday()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
        WriteLogFile("log20260923.txt", "today content");
        WriteLogFile("log20260922.txt", "yesterday content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler, time);

        var result = await sender.SendLatestAsync();

        Assert.Equal("log20260923.txt", result.FileName);
    }

    [Fact]
    public async Task SendPreviousDayAsync_YesterdayPresent_PrefersYesterday()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
        WriteLogFile("log20260922.txt", "yesterday content");
        WriteLogFile("log20260923.txt", "today content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler, time);

        var result = await sender.SendPreviousDayAsync();

        Assert.Equal("log20260922.txt", result.FileName);
    }

    [Fact]
    public async Task SendPreviousDayAsync_YesterdayMissing_FallsBackToToday()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
        WriteLogFile("log20260923.txt", "today content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler, time);

        var result = await sender.SendPreviousDayAsync();

        Assert.Equal("log20260923.txt", result.FileName);
    }

    [Fact]
    public async Task SendDailyLogAsync_FileMissing_ReturnsFileNotFound()
    {
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.FileNotFound, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendDailyLogAsync_Disabled_ReturnsDisabledWithoutHttpCall()
    {
        var options = CreateOptions(_root, o => o.Enabled = false);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.Disabled, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendDailyLogAsync_BlankBotToken_ReturnsDisabledWithoutHttpCall()
    {
        var options = CreateOptions(_root, o => o.BotToken = "   ");
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.Disabled, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendDailyLogAsync_BlankChatId_ReturnsDisabledWithoutHttpCall()
    {
        var options = CreateOptions(_root, o => o.ChatId = "   ");
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.Disabled, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendDailyLogAsync_ChatIdZero_ReturnsDisabledWithoutHttpCall()
    {
        var options = CreateOptions(_root, o => o.ChatId = "0");
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.Disabled, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendDailyLogAsync_TelegramReturns400_FailsWithCodeAndDescriptionButNoToken()
    {
        WriteLogFile("log20260923.txt", "content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"ok":false,"error_code":400,"description":"Bad Request: chat not found"}""")
            })
        };
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.Failed, result.Status);
        Assert.Contains("400", result.Message);
        Assert.Contains("Bad Request: chat not found", result.Message);
        Assert.DoesNotContain("123:abc", result.Message);
    }

    [Fact]
    public async Task SendDailyLogAsync_HandlerThrowsWithTokenInMessage_FailsWithTokenScrubbed()
    {
        WriteLogFile("log20260923.txt", "content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => throw new HttpRequestException("Failed to connect to https://api.telegram.org/bot123:abc/sendDocument")
        };
        var sender = CreateSender(options, handler);

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.Failed, result.Status);
        Assert.DoesNotContain("123:abc", result.Message);
        Assert.Contains("***", result.Message);
    }

    [Fact]
    public async Task SendDailyLogAsync_FileLargerThanCap_ReturnsFileTooLargeWithoutHttpCall()
    {
        WriteLogFile("log20260923.txt", new string('a', 100));
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);
        sender.MaxFileSizeBytes = 10;

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(LogDeliveryStatus.FileTooLarge, result.Status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendDailyLogAsync_FileHeldOpenByWriter_StillSends()
    {
        var path = Path.Combine(_root, "log20260923.txt");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);

        await using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await using var streamWriter = new StreamWriter(writer);
        await streamWriter.WriteAsync("still being written");
        await streamWriter.FlushAsync();

        var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task SendLatestAsync_LocalZoneAheadOfUtc_UsesLocalCalendarDate()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 22, 30, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test/+03", TimeSpan.FromHours(3), "Test/+03", "Test/+03"));
        WriteLogFile("log20260924.txt", "content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler, time);

        var result = await sender.SendLatestAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("log20260924.txt", result.FileName);
    }

    [Fact]
    public async Task SendDailyLogAsync_OptionsChangedBetweenCalls_SecondCallUsesNewChatId()
    {
        WriteLogFile("log20260923.txt", "content");
        var options = CreateOptions(_root);
        var monitor = new TestOptionsMonitor<TelegramLogDeliveryOptions>(options);
        var handler = new FakeHttpMessageHandler();
        var sender = new TelegramLogSender(
            monitor, new FakeHttpClientFactory(handler), TimeProvider.System, NullLogger<TelegramLogSender>.Instance);

        await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));
        monitor.CurrentValue = CreateOptions(_root, o => o.ChatId = "99");
        await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("42", handler.Requests[0].Body);
        Assert.Contains("99", handler.Requests[1].Body);
    }

    [Fact]
    public async Task SendDailyLogAsync_CallerCancellation_PropagatesOperationCanceledException()
    {
        WriteLogFile("log20260923.txt", "content");
        var options = CreateOptions(_root);
        var handler = new FakeHttpMessageHandler();
        var sender = CreateSender(options, handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendDailyLogAsync(new DateOnly(2026, 9, 23), cts.Token));
    }

    [Fact]
    public async Task SendDailyLogAsync_FileUnreadable_ReturnsFailedWithoutHttpCall()
    {
        // File permission bits do not exist on Windows; CI (ubuntu, non-root runner) exercises
        // this path, where removing every permission bit actually blocks the read.
        if (OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(_root, "log20260923.txt");
        WriteLogFile("log20260923.txt", "content");
        File.SetUnixFileMode(path, UnixFileMode.None);
        try
        {
            var options = CreateOptions(_root);
            var handler = new FakeHttpMessageHandler();
            var sender = CreateSender(options, handler);

            var result = await sender.SendDailyLogAsync(new DateOnly(2026, 9, 23));

            Assert.Equal(LogDeliveryStatus.Failed, result.Status);
            Assert.Empty(handler.Requests);
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
