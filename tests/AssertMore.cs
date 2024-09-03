namespace TemporalioSamples.Tests;

public static class AssertMore
{
    public static Task EventuallyAsync(
        Func<Task> func, TimeSpan? interval = null, int iterations = 15) =>
        EventuallyAsync(
            async () =>
            {
                await func();
                return ValueTuple.Create();
            },
            interval,
            iterations);

    public static async Task<T> EventuallyAsync<T>(
        Func<Task<T>> func, TimeSpan? interval = null, int iterations = 15)
    {
        var tick = interval ?? TimeSpan.FromMilliseconds(300);
        for (var i = 0; ; i++)
        {
            try
            {
                return await func();
            }
            catch (Xunit.Sdk.XunitException)
            {
                if (i >= iterations - 1)
                {
                    throw;
                }
            }
            await Task.Delay(tick);
        }
    }
}