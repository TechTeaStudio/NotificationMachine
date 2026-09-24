using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TechTeaStudio.NotificationMachine.Telegram;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class TelegramLogDeliveryServiceCollectionExtensionsTests
{
    private static IConfigurationRoot BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void AddTelegramLogDelivery_DefaultSection_BindsTelegramLogsSection()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Telegram:Logs:BotToken"] = "abc",
            ["Telegram:Logs:ChatId"] = "123",
            ["Telegram:Logs:Hour"] = "7",
        });
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddTelegramLogDelivery();
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TelegramLogDeliveryOptions>>().CurrentValue;

        Assert.Equal("abc", options.BotToken);
        Assert.Equal("123", options.ChatId);
        Assert.Equal(7, options.Hour);
    }

    [Fact]
    public void AddTelegramLogDelivery_CustomSection_BindsNumericChatIdAndEnabledFalse()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Telegram:Log:ChatId"] = "42",
            ["Telegram:Log:Enabled"] = "false",
        });
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddTelegramLogDelivery(configSectionPath: "Telegram:Log");
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TelegramLogDeliveryOptions>>().CurrentValue;

        Assert.Equal("42", options.ChatId);
        Assert.False(options.Enabled);
    }

    [Fact]
    public void AddTelegramLogDelivery_ConfigureDelegate_OverridesBoundValues()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Telegram:Logs:Hour"] = "7",
        });
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddTelegramLogDelivery(o => o.Hour = 9);
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<TelegramLogDeliveryOptions>>().CurrentValue;

        Assert.Equal(9, options.Hour);
    }

    [Fact]
    public void AddTelegramLogDelivery_ConfigReload_ChangesCurrentValue()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Telegram:Logs:Hour"] = "6",
        });
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddTelegramLogDelivery();
        var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<TelegramLogDeliveryOptions>>();
        Assert.Equal(6, monitor.CurrentValue.Hour);

        config["Telegram:Logs:Hour"] = "10";
        config.Reload();

        Assert.Equal(10, monitor.CurrentValue.Hour);
    }

    [Fact]
    public void AddTelegramLogDelivery_CalledTwice_RegistersExactlyOneHostedServiceAndSenderIsResolvable()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddTelegramLogDelivery();
        services.AddTelegramLogDelivery();
        var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>()
            .Where(s => s is TelegramLogDeliveryService)
            .ToList();
        Assert.Single(hostedServices);
        Assert.NotNull(provider.GetRequiredService<ITelegramLogSender>());
    }

    [Fact]
    public void AddTelegramLogDelivery_NamedHttpClient_HasFiveMinuteTimeout()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        services.AddTelegramLogDelivery();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(TelegramLogSender.HttpClientName);

        Assert.Equal(TimeSpan.FromMinutes(5), client.Timeout);
    }
}
