<p align="center">
  <img src="https://raw.githubusercontent.com/TechTeaStudio/NotificationMachine/product/src/TechTeaStudio.NotificationMachine/icon.png" alt="TechTeaStudio.NotificationMachine logo" width="160" />
</p>

<h1 align="center">TechTeaStudio.NotificationMachine</h1>

<p align="center">
  Ships a service's daily Serilog log file to a Telegram chat, once a day, plus an on-demand "send it now" API.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/TechTeaStudio.NotificationMachine"><img alt="NuGet" src="https://img.shields.io/nuget/v/TechTeaStudio.NotificationMachine.svg?logo=nuget&label=NuGet" /></a>
  <a href="https://www.nuget.org/packages/TechTeaStudio.NotificationMachine"><img alt="Downloads" src="https://img.shields.io/nuget/dt/TechTeaStudio.NotificationMachine.svg?logo=nuget&label=Downloads" /></a>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0%20%E2%86%92%2010.0-512BD4?logo=dotnet&logoColor=white" />
  <a href="https://github.com/TechTeaStudio/NotificationMachine/actions/workflows/dotnet.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/TechTeaStudio/NotificationMachine/dotnet.yml?branch=product&logo=github&label=build" /></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-MIT-blue.svg" /></a>
</p>

## What this gives you

A background service that finds your service's Serilog rolling log file and sends it to a Telegram chat as a document, once a day, plus an `ITelegramLogSender` seam for sending it on demand (a `/send_logs` command, an admin endpoint, whatever your host needs).

- One line to register: `AddTelegramLogDelivery()`. Stays disabled until `BotToken` and `ChatId` are set, and keeps checking on every scheduled tick, so enabling it later through a config reload works with no restart.
- Talks to the Bot API directly over `HttpClient` - no `Telegram.Bot` package, no ASP.NET Core `FrameworkReference`. Safe to reference from a plain console worker on the `mcr.microsoft.com/dotnet/runtime` image.
- Finds the right file the way Serilog names it: `{prefix}{yyyyMMdd}{extension}`, searched across a configurable list of directories.
- Never throws out of the schedule loop. A missing file, a Telegram outage, or an oversized log all come back as a typed `LogDeliveryStatus` and get retried at the next scheduled time.
- The bot token never reaches a log line: `HttpClient` logging handlers are removed on the named client, and the token is scrubbed from every failure message.

## Install

```bash
dotnet add package TechTeaStudio.NotificationMachine
```

## Quick start

Add the bot token and chat id to configuration, under the default `Telegram:Logs` section:

```json
{
  "Telegram": {
    "Logs": {
      "BotToken": "123456:AAExampleTokenFromBotFather",
      "ChatId": "-1001234567890"
    }
  }
}
```

Or as environment variables (Docker, Kubernetes, systemd):

```
Telegram__Logs__BotToken=123456:AAExampleTokenFromBotFather
Telegram__Logs__ChatId=-1001234567890
```

Register the service:

```csharp
// Program.cs
builder.Services.AddTelegramLogDelivery(o => o.ServiceName = "MyService");
```

`LogFileName` and `LogDirectories` must match the Serilog sink that writes the file. With the default Serilog setup below, the package's own defaults (`LogFileName = "log.txt"`, `LogDirectories` empty meaning `{cwd}/logs` first) already line up and need no extra configuration:

```csharp
.WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
```

If your Serilog sink writes a different file name or directory, set `LogFileName`/`LogDirectories` to match - see Recipes below.

## Configuration reference

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. Delivery also stays off while `BotToken`/`ChatId` are missing. |
| `BotToken` | `null` | Bot token from [@BotFather](https://t.me/BotFather). |
| `ChatId` | `null` | Destination chat, e.g. `"-1001234567890"` or `"@channelname"`. `"0"` counts as unset. |
| `Hour` | `6` | Hour of day (0-23, clamped) the daily send fires, in the `UtcOffset` zone. |
| `UtcOffset` | `03:00:00` | Timezone offset `Hour` is measured in. Default is Moscow (fixed +3, no DST since 2014). Clamped to +/-14h. |
| `ServiceName` | `null` | Fills `{Service}` in the caption. Falls back to the entry assembly name, then `"app"`. |
| `CaptionTemplate` | `"{Service} daily log for {Date}"` | Telegram document caption. `{Date}` is `yyyy-MM-dd`. Truncated to 1024 characters (Telegram's caption limit). |
| `LogFileName` | `"log.txt"` | The file name given to Serilog's `WriteTo.File`, e.g. `"log.txt"` or `"authservice-.txt"`. |
| `LogDirectories` | `[]` | Directories searched in order. Empty means the built-in defaults: `{cwd}/logs`, `/logs`, `{baseDirectory}/logs`, `{baseDirectory}`. |

## Recipes

### A single service with an explicit log directory

```csharp
builder.Services.AddTelegramLogDelivery(o =>
{
    o.ServiceName = "MyService";
    o.LogDirectories = ["/var/log/myservice"];
});
```

```csharp
.WriteTo.File("/var/log/myservice/log.txt", rollingInterval: RollingInterval.Day)
```

### Several services on one bot, with per-service file names

Every service can point at the same `BotToken`/`ChatId` (the same Telegram chat) while keeping its own caption and its own log file:

```csharp
// AuthService
builder.Services.AddTelegramLogDelivery(o =>
{
    o.ServiceName = "AuthService";
    o.LogFileName = "authservice-.txt";
});
```

```csharp
.WriteTo.File("logs/authservice-.txt", rollingInterval: RollingInterval.Day)
```

### A legacy config section with a UTC schedule

`configSectionPath` points the binder at an existing section, but it only relocates the section - the key names underneath still have to match `TelegramLogDeliveryOptions`'s own property names. A section that predates this package, like DiscordBotMike's `Telegram:Log` with its `SendHourUtc` key, needs its mismatched key read manually and mapped in the `configure` delegate:

```csharp
var legacy = builder.Configuration.GetSection("Telegram:Log");
builder.Services.AddTelegramLogDelivery(o =>
{
    o.Hour = legacy.GetValue("SendHourUtc", 0);   // the delegate re-runs on every reload, so it reads the live value
    o.UtcOffset = TimeSpan.Zero;
}, configSectionPath: "Telegram:Log");
```

```json
{
  "Telegram": {
    "Log": {
      "BotToken": "123456:AAExampleTokenFromBotFather",
      "ChatId": 123456789,
      "SendHourUtc": 6
    }
  }
}
```

`BotToken` and `ChatId` still bind directly from the section (their names already match); only `SendHourUtc` needs the manual read. A numeric JSON `ChatId` binds fine either way - the configuration binder converts it to the `string` property as text.

### On-demand send from a command

```csharp
public sealed class SendLogsCommand
{
    private readonly ITelegramLogSender _sender;

    public SendLogsCommand(ITelegramLogSender sender) => _sender = sender;

    public async Task<string> HandleAsync(CancellationToken ct)
    {
        var result = await _sender.SendLatestAsync(ct);
        return result.Status switch
        {
            LogDeliveryStatus.Sent => $"Sent {result.FileName}.",
            LogDeliveryStatus.Disabled => "Telegram log delivery is not configured.",
            LogDeliveryStatus.FileNotFound => "No log file found for today or yesterday.",
            LogDeliveryStatus.FileTooLarge => "The log file is too large to send.",
            _ => $"Failed: {result.Message}",
        };
    }
}
```

## How it behaves

- **Schedule.** The hosted service computes the next occurrence of `Hour:00` in the `UtcOffset` zone, waits for it, and sends. If `Hour:00` today has already passed, it waits for tomorrow instead.
- **Which file, and why local date.** The scheduled send calls `SendPreviousDayAsync` (yesterday's file, falling back to today's if yesterday's is missing). "Today"/"yesterday" are computed from the local calendar date, because that is what Serilog's `RollingInterval.Day` stamps the file name with.
- **Lookup order.** `LogDirectories`, if you set any; otherwise `{cwd}/logs`, `/logs`, `{baseDirectory}/logs`, `{baseDirectory}`, in that order. The first directory that contains the expected file name wins.
- **Failures never throw, and are retried only at the next scheduled time.** A missing file, a Telegram error, or a cancelled/timed-out request all come back as a `LogDeliveryResult` with a `LogDeliveryStatus`; the hosted service logs it and waits for the next scheduled tick. Calling the token you passed in yourself (via `ITelegramLogSender`, not the hosted service) still throws `OperationCanceledException` if you cancel it.
- **Live config reload.** `IsConfigured` is re-checked at every scheduled fire time, not just once at startup, so flipping `Enabled` or filling in `BotToken`/`ChatId` through a config reload takes effect on the next tick without a restart.
- **Security.** The bot token only ever appears in the request URL, never in a log line: the named `HttpClient` has its logging handlers removed (`RemoveAllLoggers()`), and the token is scrubbed (`***`) from every `Failed` message, including one built from an underlying exception's own text.

## Limits

- Telegram's bot upload cap is 50 MB. A larger file returns `FileTooLarge` without an HTTP call.
- Serilog's size-based rolling (`fileSizeLimitBytes` + `rollOnFileSizeLimit`) produces extra `_001`, `_002`, ... parts once a day's file hits the size limit. Only the base file name for the day is looked up; the numbered parts are not sent.
- Changing `Hour` or `UtcOffset` through configuration takes effect after the currently pending wait completes, not immediately.
- If your host also calls `ConfigureHttpClientDefaults` (for example to add `AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience`), that applies to this package's named client too.

## Used by

- **DiscordBotMike** - where this code started, as the original `TelegramLogService`.
- **Chronos** - ported as `Chronos.Workers.TelegramLogDeliveryService`.
- **HyperionOmniClient** - ported as the more generalized `Hyperion.Application.Common.Telegram`.

## Target frameworks

`net8.0`, `net9.0`, `net10.0`. No ASP.NET Core dependency - works from a plain console worker, a Worker Service template project, or a full ASP.NET Core host equally.

## Project layout

```
src/
  Directory.Build.props                        version-free build settings shared by every project
  TechTeaStudio.NotificationMachine/
    TechTeaStudio.NotificationMachine.sln       the only solution
    icon.png                                    package icon
    TechTeaStudio.NotificationMachine/
      Telegram/                                 TelegramLogDeliveryOptions, ITelegramLogSender,
                                                 TelegramLogSender, TelegramLogDeliveryService,
                                                 TelegramLogDeliveryServiceCollectionExtensions,
                                                 and internal helpers (file naming, directory
                                                 lookup, caption building, schedule math)
    TechTeaStudio.NotificationMachine.Tests/
      Telegram/                                 tests for every file above
```

## Build & test

```bash
dotnet build src/TechTeaStudio.NotificationMachine/TechTeaStudio.NotificationMachine.sln -c Release
dotnet test  src/TechTeaStudio.NotificationMachine/TechTeaStudio.NotificationMachine.sln -c Release
dotnet pack  src/TechTeaStudio.NotificationMachine/TechTeaStudio.NotificationMachine/TechTeaStudio.NotificationMachine.csproj -c Release -o artifacts
```

There is no `GeneratePackageOnBuild` - packing is an explicit step, so three target frameworks do not repack the `.nupkg`/`.snupkg` on every build and every test run.

## Versioning & release

Commit format: `vX.Y.Z Short description` (one line, 72 characters max). Bump `<Version>` in `TechTeaStudio.NotificationMachine.csproj` before every release commit.

The release branch is `product`. GitHub Actions publishes to NuGet on push to `product`.

### CI setup

[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml) delegates to the org-wide reusable workflow `TechTeaStudio/.github/.github/workflows/nuget-publish-reusable.yml@main`, which restores, builds Release, runs the tests, packs every project matched by `src/*/*/*.csproj`, and pushes with `--skip-duplicate`.

**One secret, `NUGET_API_KEY`.** Add it at `Settings -> Secrets and variables -> Actions -> New repository secret` on `TechTeaStudio/NotificationMachine`, or as an organization secret on `TechTeaStudio` with access granted to this repo. Generate the value on nuget.org under `Account -> API Keys -> Create`:

| Field | Value |
|---|---|
| Key name | anything, e.g. `techteastudio-ci` |
| Scopes | **Push** -> *Push new packages and package versions* |
| Glob pattern | `TechTeaStudio.*` |

If `TechTeaStudio/.github` is private, enable `Organization -> Settings -> Actions -> General -> Allow access to workflows from repositories in this organization`, or the run fails at "workflow not found" before any dotnet step runs.

`dotnet nuget push` runs with `--skip-duplicate`, so re-pushing a version that already exists is a silent no-op, not an error. If a release "did not publish", check that the version was actually bumped.

## License

Licensed under the [MIT License](LICENSE). Copyright &copy; Tech Tea Studio.

<p align="center">
  Built as part of the Hyperion Ecosystem by <a href="https://techteastudio.cc">TechTeaStudio</a>.
</p>
