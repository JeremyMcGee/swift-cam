namespace SwiftCam;

/// <summary>
/// Calculates morning and evening playback windows from solar times and audio settings.
/// </summary>
public static class PlaybackWindowCalculator
{
    /// <summary>
    /// Calculates the morning and evening playback windows for a given date.
    /// </summary>
    /// <param name="solarTimes">Solar event times (civil twilight, sunrise, sunset) for the date.</param>
    /// <param name="settings">Audio settings containing offset and duration configuration.</param>
    /// <param name="date">The date for which to calculate playback windows.</param>
    /// <returns>A tuple of nullable morning and evening playback windows.</returns>
    public static (PlaybackWindow? Morning, PlaybackWindow? Evening) Calculate(
        SolarTimes solarTimes, AudioSettings settings, DateTime date)
    {
        var morning = CalculateMorningWindow(solarTimes, settings, date);
        var evening = CalculateEveningWindow(solarTimes, settings, date);

        // Apply overlap prevention: if evening start < morning end, adjust evening start
        if (morning is not null && evening is not null && evening.Start < morning.End)
        {
            evening = evening with { Start = morning.End };

            // If after adjustment the evening window is fully consumed, discard it
            if (evening.Start >= evening.End)
            {
                evening = null;
            }
        }

        return (morning, evening);
    }

    private static PlaybackWindow? CalculateMorningWindow(
        SolarTimes solarTimes, AudioSettings settings, DateTime date)
    {
        if (solarTimes.CivilTwilight is null)
        {
            return null;
        }

        var start = date.Date + solarTimes.CivilTwilight.Value.ToTimeSpan()
            + TimeSpan.FromMinutes(settings.MorningOffsetMinutes);
        var end = start + TimeSpan.FromMinutes(settings.MorningDurationMinutes);

        return new PlaybackWindow(start, end);
    }

    private static PlaybackWindow? CalculateEveningWindow(
        SolarTimes solarTimes, AudioSettings settings, DateTime date)
    {
        if (solarTimes.Sunset is null)
        {
            return null;
        }

        var sunset = date.Date + solarTimes.Sunset.Value.ToTimeSpan();
        var start = sunset - TimeSpan.FromMinutes(settings.EveningPreSunsetMinutes);
        var end = sunset;

        return new PlaybackWindow(start, end);
    }
}
