using TechTeaStudio.NotificationMachine.Telegram;

using Xunit;

namespace TechTeaStudio.NotificationMachine.Tests.Telegram;

public class TelegramSendScheduleTests
{
    [Fact]
    public void NextSendTime_NowBeforeHour_ReturnsTodayAtHour()
    {
        var offset = TimeSpan.FromHours(3);
        var now = new DateTimeOffset(2026, 7, 13, 2, 15, 30, offset);

        var next = TelegramSendSchedule.NextSendTime(now, 6, offset);

        Assert.Equal(new DateTimeOffset(2026, 7, 13, 6, 0, 0, offset), next);
    }

    [Fact]
    public void NextSendTime_NowAtOrAfterHour_ReturnsTomorrow()
    {
        var offset = TimeSpan.FromHours(3);
        var now = new DateTimeOffset(2026, 7, 13, 6, 0, 0, offset);

        var next = TelegramSendSchedule.NextSendTime(now, 6, offset);

        Assert.Equal(new DateTimeOffset(2026, 7, 14, 6, 0, 0, offset), next);
    }

    [Fact]
    public void NextSendTime_HourAboveRange_ClampsTo23()
    {
        var offset = TimeSpan.FromHours(3);
        var now = new DateTimeOffset(2026, 7, 13, 2, 0, 0, offset);

        var next = TelegramSendSchedule.NextSendTime(now, 25, offset);

        Assert.Equal(new DateTimeOffset(2026, 7, 13, 23, 0, 0, offset), next);
    }

    [Fact]
    public void NextSendTime_HourBelowRange_ClampsTo0()
    {
        var offset = TimeSpan.FromHours(3);
        var now = new DateTimeOffset(2026, 7, 13, 2, 0, 0, offset);

        var next = TelegramSendSchedule.NextSendTime(now, -1, offset);

        Assert.Equal(new DateTimeOffset(2026, 7, 14, 0, 0, 0, offset), next);
    }

    [Fact]
    public void NextSendTime_Result_KeepsOffsetAndZeroesMinutesAndSeconds()
    {
        var offset = TimeSpan.FromHours(3);
        var now = new DateTimeOffset(2026, 7, 13, 2, 47, 12, offset);

        var next = TelegramSendSchedule.NextSendTime(now, 6, offset);

        Assert.Equal(offset, next.Offset);
        Assert.Equal(0, next.Minute);
        Assert.Equal(0, next.Second);
    }

    [Fact]
    public void NextSendTime_UtcOffsetZero_PastHourRollsToTomorrow()
    {
        var now = new DateTimeOffset(2026, 9, 24, 23, 30, 0, TimeSpan.Zero);

        var next = TelegramSendSchedule.NextSendTime(now, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void NextSendTime_MoscowOffset_MatchesEquivalentUtcInstant()
    {
        var offset = TimeSpan.FromHours(3);
        var now = new DateTimeOffset(2026, 9, 24, 2, 30, 0, TimeSpan.Zero).ToOffset(offset);

        var next = TelegramSendSchedule.NextSendTime(now, 6, offset);

        Assert.Equal(new DateTimeOffset(2026, 9, 24, 6, 0, 0, offset), next);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 3, 0, 0, TimeSpan.Zero), next.ToUniversalTime());
    }

    [Fact]
    public void NextSendTime_ExactlyAtHourUtcOffsetZero_ReturnsTomorrow()
    {
        var now = new DateTimeOffset(2026, 9, 24, 6, 0, 0, TimeSpan.Zero);

        var next = TelegramSendSchedule.NextSendTime(now, 6, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 9, 25, 6, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void NextSendTime_OffsetAboveRange_ClampedTo14Hours()
    {
        var now = new DateTimeOffset(2026, 9, 24, 2, 0, 0, TimeSpan.FromHours(14));

        var next = TelegramSendSchedule.NextSendTime(now, 6, TimeSpan.FromHours(20));

        Assert.Equal(TimeSpan.FromHours(14), next.Offset);
    }

    [Fact]
    public void NextSendTime_OffsetBelowRange_ClampedToMinus14Hours()
    {
        var now = new DateTimeOffset(2026, 9, 24, 2, 0, 0, TimeSpan.FromHours(-14));

        var next = TelegramSendSchedule.NextSendTime(now, 6, TimeSpan.FromHours(-20));

        Assert.Equal(TimeSpan.FromHours(-14), next.Offset);
    }

    [Theory]
    [InlineData(20, 14)]
    [InlineData(-20, -14)]
    [InlineData(3, 3)]
    public void SanitizeOffset_ClampsToPlusMinus14Hours(double inputHours, double expectedHours)
    {
        var result = TelegramSendSchedule.SanitizeOffset(TimeSpan.FromHours(inputHours));

        Assert.Equal(TimeSpan.FromHours(expectedHours), result);
    }

    [Fact]
    public void SanitizeOffset_FractionalMinutes_RoundsToWholeMinute()
    {
        var result = TelegramSendSchedule.SanitizeOffset(new TimeSpan(3, 30, 15));

        Assert.Equal(new TimeSpan(3, 30, 0), result);
    }
}
