namespace TechTeaStudio.NotificationMachine.Tests.TestSupport;

internal static class AsyncTestHelpers
{
    /// <summary>
    /// Polls <paramref name="condition"/> with short real-time waits until it is true or
    /// <paramref name="timeout"/> elapses. Used after advancing a FakeTimeProvider: the timer
    /// callback it triggers still completes on the thread pool, asynchronously.
    /// </summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;

            await Task.Delay(20);
        }

        return condition();
    }
}
