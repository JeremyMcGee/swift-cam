using Innovative.SolarCalculator;

namespace SwiftCam;

/// <summary>
/// Wraps the SolarCalculator NuGet package, implementing <see cref="ISolarCalculator"/>.
/// Returns null times for polar edge cases where no solar event occurs.
/// </summary>
public class SolarCalculatorWrapper : ISolarCalculator
{
    /// <inheritdoc/>
    public SolarTimes Calculate(double latitude, double longitude, DateTime date)
    {
        // Create an unspecified-kind date to avoid ArgumentException when the caller passes UTC dates.
        // The SolarCalculator library needs a local date + offset for correct solar calculations.
        var localDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        var dateOffset = new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
        var solarTimes = new Innovative.SolarCalculator.SolarTimes(
            dateOffset, latitude, longitude);

        TimeOnly? civilTwilight = null;
        TimeOnly? sunrise = null;
        TimeOnly? sunset = null;

        if (!solarTimes.IsPolarDay && !solarTimes.IsPolarNight)
        {
            sunrise = ToValidTimeOnly(solarTimes.Sunrise);
            sunset = ToValidTimeOnly(solarTimes.Sunset);
            civilTwilight = ToValidTimeOnly(solarTimes.DawnCivil);
        }

        return new SolarTimes(civilTwilight, sunrise, sunset);
    }

    /// <summary>
    /// Converts a DateTime to TimeOnly if the value is valid (not a sentinel value and not NaN-like).
    /// Returns null for polar edge cases where the library returns sentinel values.
    /// </summary>
    private static TimeOnly? ToValidTimeOnly(DateTime dateTime)
    {
        // The SolarCalculator library returns DateTime.MinValue or DateTime.MaxValue
        // as sentinel values for polar conditions.
        if (dateTime == DateTime.MinValue || dateTime == DateTime.MaxValue)
            return null;

        // Check for NaN in the ticks (shouldn't happen, but guard against it)
        if (dateTime.TimeOfDay.TotalHours is < 0 or >= 24)
            return null;

        return TimeOnly.FromDateTime(dateTime);
    }
}
