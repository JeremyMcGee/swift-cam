using System.Globalization;
using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based test verifying filename timestamp parsing round-trip.
/// For any valid DateTime value, generating a filename via CaptureWriter.GenerateFilename
/// and parsing the timestamp back yields the same date/time components.
///
/// **Validates: Requirements 4.4**
/// </summary>
public class CaptureFilenameTimestampRoundTripPropertyTests
{
    private const string FilenameFormat = "yyyy-MMM-dd_HH-mm-ss";

    /// <summary>
    /// Property 4: Filename timestamp parsing round-trip.
    /// For any valid DateTime, generating a capture filename via CaptureWriter.GenerateFilename
    /// and then parsing the timestamp back from that filename SHALL yield the same
    /// year, month, day, hour, minute, and second components.
    ///
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GenerateFilename_ParseBack_YieldsOriginalTimestamp()
    {
        var gen = from year in Gen.Choose(1, 9999)
                  from month in Gen.Choose(1, 12)
                  from day in Gen.Choose(1, DateTime.DaysInMonth(year, month))
                  from hour in Gen.Choose(0, 23)
                  from minute in Gen.Choose(0, 59)
                  from second in Gen.Choose(0, 59)
                  select new DateTime(year, month, day, hour, minute, second);

        return Prop.ForAll(gen.ToArbitrary(), timestamp =>
        {
            // Generate filename using the production method
            var filename = CaptureWriter.GenerateFilename(timestamp);

            // Strip the .jpg extension
            var withoutExtension = Path.GetFileNameWithoutExtension(filename);

            // Parse it back using the same format string
            var parsed = DateTime.ParseExact(
                withoutExtension,
                FilenameFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);

            // Verify year/month/day/hour/minute/second match
            var yearMatches = parsed.Year == timestamp.Year;
            var monthMatches = parsed.Month == timestamp.Month;
            var dayMatches = parsed.Day == timestamp.Day;
            var hourMatches = parsed.Hour == timestamp.Hour;
            var minuteMatches = parsed.Minute == timestamp.Minute;
            var secondMatches = parsed.Second == timestamp.Second;

            return (yearMatches && monthMatches && dayMatches &&
                    hourMatches && minuteMatches && secondMatches)
                .Label($"Expected {timestamp:yyyy-MM-dd HH:mm:ss} but parsed {parsed:yyyy-MM-dd HH:mm:ss} from '{filename}'");
        });
    }
}
