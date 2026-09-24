namespace TechTeaStudio.NotificationMachine.Telegram;

internal static class TelegramSendSchedule
{
    private static readonly TimeSpan MaxOffset = TimeSpan.FromHours(14);

    /// <summary>Today at hour:00 in that offset if strictly ahead of now, else tomorrow; clamps hour to 0..23 and sanitizes offset.</summary>
    internal static DateTimeOffset NextSendTime(DateTimeOffset nowUtc, int hour, TimeSpan utcOffset)
    {
        var offset = SanitizeOffset(utcOffset);
        var now = nowUtc.ToOffset(offset);
        var clampedHour = Math.Clamp(hour, 0, 23);

        var next = new DateTimeOffset(now.Year, now.Month, now.Day, clampedHour, 0, 0, offset);
        if (next <= now)
            next = next.AddDays(1);

        return next;
    }

    /// <summary>Clamps to +/-14h and rounds to whole minutes.</summary>
    internal static TimeSpan SanitizeOffset(TimeSpan offset)
    {
        var clamped = offset > MaxOffset ? MaxOffset : offset < -MaxOffset ? -MaxOffset : offset;
        return TimeSpan.FromMinutes(Math.Round(clamped.TotalMinutes));
    }
}
