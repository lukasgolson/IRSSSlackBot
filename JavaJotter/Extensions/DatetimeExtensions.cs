namespace JavaJotter.Extensions;

public static class DatetimeExtensions
{
    private static readonly Random Random = new();


    /// <summary>
    ///     Generates a sequence of sequential DateTime objects.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="numberOfMinutes">The number of minutes to generate times for.</param>
    /// <returns>A sequence of DateTime objects.</returns>
    public static IEnumerable<DateTime> GenerateSequentialTimes(DateTime startTime, int numberOfMinutes)
    {
        for (var i = 0; i < numberOfMinutes; i++) yield return startTime.AddMinutes(i);
    }

    /// <summary>
    ///     Generates a random DateTime within a specified number of days in the past.
    /// </summary>
    /// <param name="daysInThePast">The number of days in the past to consider for generating a random DateTime.</param>
    /// <returns>A random DateTime.</returns>
    public static DateTime GenerateRandomDateTimeFromPast(int daysInThePast)
    {
        var timeSpan = TimeSpan.FromDays(daysInThePast);
        var randomDate = DateTime.Now - TimeSpan.FromTicks((long)(Random.NextDouble() * timeSpan.Ticks));
        return randomDate;
    }

    public static long ToUnixTimeSeconds(this DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
    }

    public static long ToUnixTimeMilliseconds(this DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();
    }
}