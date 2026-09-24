using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TechTeaStudio.NotificationMachine.Telegram;

/// <summary>DI registration for <see cref="TelegramLogDeliveryService"/> and <see cref="ITelegramLogSender"/>.</summary>
public static class TelegramLogDeliveryServiceCollectionExtensions
{
    /// <summary>
    /// Registers daily Telegram log delivery: binds <see cref="TelegramLogDeliveryOptions"/> from
    /// <paramref name="configSectionPath"/> (with reload change tokens for free), then applies
    /// <paramref name="configure"/> if given. Stays disabled until Enabled/BotToken/ChatId make
    /// <see cref="TelegramLogDeliveryOptions.IsConfigured"/> true.
    /// </summary>
    public static IServiceCollection AddTelegramLogDelivery(
        this IServiceCollection services,
        Action<TelegramLogDeliveryOptions>? configure = null,
        string configSectionPath = TelegramLogDeliveryOptions.DefaultSectionPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

        var optionsBuilder = services.AddOptions<TelegramLogDeliveryOptions>()
            .BindConfiguration(configSectionPath);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        // The bot token is part of the request URL; the default HttpClientFactory logging handler
        // would otherwise write that URL at Information level.
        services.AddHttpClient(TelegramLogSender.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(5))
            .RemoveAllLoggers();

        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
        services.TryAddSingleton<ITelegramLogSender, TelegramLogSender>();
        services.AddHostedService<TelegramLogDeliveryService>();

        return services;
    }
}
