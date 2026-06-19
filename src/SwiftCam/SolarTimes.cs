namespace SwiftCam;

/// <summary>
/// Represents calculated solar event times for a given date and location.
/// Nullable properties handle polar edge cases where no solar event occurs.
/// </summary>
/// <param name="CivilTwilight">The time of civil twilight (dawn), or null if it does not occur.</param>
/// <param name="Sunrise">The time of sunrise, or null if it does not occur.</param>
/// <param name="Sunset">The time of sunset, or null if it does not occur.</param>
public record SolarTimes(TimeOnly? CivilTwilight, TimeOnly? Sunrise, TimeOnly? Sunset);
