using TechTeaStudio.NotificationMachine.Telegram;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class TelegramLogDeliveryOptionsTests
{
    [Fact]
    public void Defaults_MatchDocumentedValues()
    {
        var options = new TelegramLogDeliveryOptions();

        Assert.True(options.Enabled);
        Assert.Null(options.BotToken);
        Assert.Null(options.ChatId);
        Assert.Equal(6, options.Hour);
        Assert.Equal(TimeSpan.FromHours(3), options.UtcOffset);
        Assert.Null(options.ServiceName);
        Assert.Equal("{Service} daily log for {Date}", options.CaptionTemplate);
        Assert.Equal("log.txt", options.LogFileName);
        Assert.Empty(options.LogDirectories);
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_Disabled_ReturnsFalseEvenWithTokenAndChat()
    {
        var options = new TelegramLogDeliveryOptions { Enabled = false, BotToken = "123:abc", ChatId = "42" };

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_BlankBotToken_ReturnsFalse()
    {
        var options = new TelegramLogDeliveryOptions { BotToken = "   ", ChatId = "42" };

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_BlankChatId_ReturnsFalse()
    {
        var options = new TelegramLogDeliveryOptions { BotToken = "123:abc", ChatId = "   " };

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ChatIdZero_ReturnsFalse()
    {
        var options = new TelegramLogDeliveryOptions { BotToken = "123:abc", ChatId = "0" };

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ChatIdZeroWithWhitespace_ReturnsFalse()
    {
        var options = new TelegramLogDeliveryOptions { BotToken = "123:abc", ChatId = " 0 " };

        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void IsConfigured_AllSet_ReturnsTrue()
    {
        var options = new TelegramLogDeliveryOptions { BotToken = "123:abc", ChatId = "42" };

        Assert.True(options.IsConfigured);
    }
}
