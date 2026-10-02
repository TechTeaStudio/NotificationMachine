# Changelog

All notable changes to this package are documented here.
Format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-10-02

Cosmetic release: the package icon was redrawn. No code or API changes - the assembly is functionally identical to 0.1.0, so there is nothing to do on upgrade.

### Changed

- New package icon (`icon.png`): a notification bell carrying the Tech Tea Studio mark, in front of a gear. It replaces the previous "document with a paper plane" icon everywhere the package icon is shown - nuget.org, the IDE package manager, and the README header.

## [0.1.0] - 2026-09-24

First release. Extracted from DiscordBotMike's `TelegramLogService`, where daily Telegram log delivery started, and its two in-repo ports: Chronos's `TelegramLogDeliveryService` and HyperionOmniClient's more generalized `Hyperion.Application.Common.Telegram`. All three copies did the same job with small differences that had drifted apart over time; this package replaces them with one implementation and normalizes those differences deliberately (see below).

### Added

- `ITelegramLogSender` with `SendLatestAsync` (today's file, falling back to yesterday's - what an on-demand "send the log now" command wants), `SendPreviousDayAsync` (yesterday's file, falling back to today's - what the daily schedule sends), and `SendDailyLogAsync(DateOnly)` (exactly that day's file).
- `TelegramLogDeliveryService : BackgroundService` - sends the previous day's log once a day at a configurable hour and UTC offset. Public, so a host can assert its hosted-service registrations by type.
- `TelegramLogDeliveryOptions` - `Enabled`, `BotToken`, `ChatId`, `Hour`, `UtcOffset` (default Moscow, fixed +3, no DST since 2014), `ServiceName`, `CaptionTemplate`, `LogFileName`, `LogDirectories`, and a computed `IsConfigured`.
- `AddTelegramLogDelivery(IServiceCollection, Action<TelegramLogDeliveryOptions>? configure = null, string configSectionPath = "Telegram:Logs")` - binds configuration, applies an optional configure delegate on top of the bound values, registers a named `HttpClient` with a 5 minute timeout and no logging handlers, and adds the hosted service.
- `LogDeliveryResult` / `LogDeliveryStatus` (`Sent`, `Disabled`, `FileNotFound`, `FileTooLarge`, `Failed`) so a caller can tell "nothing to send" apart from an actual failure.

### Changed compared with the in-repo copies

- **Local calendar date, not UTC.** Serilog's `RollingInterval.Day` stamps file names using `DateTime.Now` (local time), but all three original copies computed "today"/"yesterday" from `DateTime.UtcNow`. Identical in a UTC container, subtly wrong anywhere else - a host running in a non-UTC timezone could look for the wrong day's file right around midnight. This package uses `TimeProvider.GetLocalNow()` instead.
- **`FileShare.ReadWrite | FileShare.Delete`, not `FileShare.Read`.** The original DiscordBotMike code opened the log file with `FileShare.Read`, which can throw a sharing violation on Windows while Serilog's own writer holds the file open (Serilog's shared-file mode uses `FileShare.ReadWrite`). The share mode is widened on the read side to tolerate that.
- **A byte snapshot, not a streamed `FileStream`.** The file being sent can be the same file Serilog is actively appending to. Streaming it straight into the multipart request body risks the body growing past the `Content-Length` computed at request start, which aborts the upload mid-transfer. This package reads the length once and copies exactly that many bytes with `ReadExactlyAsync` before building the request.
- **The scheduling loop stays alive while unconfigured.** The original copies returned from `ExecuteAsync` for good if `BotToken`/`ChatId` were empty at startup. DiscordBotMike reloads `config.json` at runtime, so a service that only becomes configured later never got a chance to start sending. This package re-checks `IsConfigured` at every scheduled fire time instead of only once at startup.
- **HttpClient logging handlers removed, and the bot token is scrubbed from failure messages.** The token is part of the request URL (`.../bot{token}/sendDocument`); the default `HttpClientFactory` logging handler would otherwise write that URL at Information level. `RemoveAllLoggers()` is called on the named client, and every `Failed` result has the token replaced with `***`, including when it leaks into an underlying exception message.
- **Fail fast over the Telegram 50 MB bot upload cap.** None of the three original copies checked the file size before attempting to send; a log file that grew past the limit would fail the HTTP call with no clear signal. This package checks the snapshotted length up front and returns `FileTooLarge` without making a request.
- **`SendPreviousDayAsync` now falls back to today's file on every host - new behavior for DiscordBotMike's schedule.** Chronos's and HyperionOmniClient's scheduled sends already fell back to today's file when yesterday's was missing (neither host has an on-demand path at all). DiscordBotMike's scheduled send had no fallback - yesterday's file only, nothing else; only its manual `/send_logs` path had a fallback, in the opposite direction (today, then yesterday). `SendLatestAsync` keeps that today-then-yesterday order for on-demand sends.
