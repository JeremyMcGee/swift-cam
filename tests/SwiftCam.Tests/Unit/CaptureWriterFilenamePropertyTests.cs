using System.Globalization;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for CaptureWriter.GenerateFilename format consistency.
/// </summary>
public class CaptureWriterFilenamePropertyTests
{
    private static readonly Regex FilenamePattern =
        new(@"^\d{4}-[A-Z][a-z]{2}-\d{2}_\d{2}-\d{2}-\d{2}\.jpg$", RegexOptions.Compiled);

    private static readonly string[] MonthAbbreviations =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>
    /// Property 4: Filename format consistency.
    /// For any valid DateTime value, CaptureWriter.GenerateFilename shall produce a string
    /// matching the regex pattern ^\d{4}-[A-Z][a-z]{2}-\d{2}_\d{2}-\d{2}-\d{2}\.jpg$
    /// and the date/time components in the filename shall correspond to the input DateTime.
    ///
    /// Validates: Requirements 3.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GenerateFilename_MatchesRegexAndComponentsCorrespondToInput()
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
            var filename = CaptureWriter.GenerateFilename(timestamp);

            // Assert: matches the required regex pattern
            var matchesRegex = FilenamePattern.IsMatch(filename);

            // Assert: parse components back and verify correspondence
            // Filename format: yyyy-MMM-dd_HH-mm-ss.jpg
            var withoutExtension = filename[..^4]; // strip ".jpg"
            var parts = withoutExtension.Split(['-', '_']);
            // parts: [yyyy, MMM, dd, HH, mm, ss]

            var yearStr = parts[0];
            var monthStr = parts[1];
            var dayStr = parts[2];
            var hourStr = parts[3];
            var minuteStr = parts[4];
            var secondStr = parts[5];

            var yearMatches = int.Parse(yearStr) == timestamp.Year;
            var monthMatches = monthStr == MonthAbbreviations[timestamp.Month - 1];
            var dayMatches = int.Parse(dayStr) == timestamp.Day;
            var hourMatches = int.Parse(hourStr) == timestamp.Hour;
            var minuteMatches = int.Parse(minuteStr) == timestamp.Minute;
            var secondMatches = int.Parse(secondStr) == timestamp.Second;

            return matchesRegex
                && yearMatches
                && monthMatches
                && dayMatches
                && hourMatches
                && minuteMatches
                && secondMatches;
        });
    }
}
