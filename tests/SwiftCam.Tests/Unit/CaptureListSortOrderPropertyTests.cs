using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for CaptureListService.GetCaptureFilenames sort order.
/// </summary>
public class CaptureListSortOrderPropertyTests : IDisposable
{
    private readonly string _tempDir;

    public CaptureListSortOrderPropertyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"swiftcam-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Property 1: Capture listing is sorted most-recent-first.
    /// For any set of valid capture filenames (matching yyyy-MMM-dd_HH-mm-ss.jpg format)
    /// present in a directory, GetCaptureFilenames SHALL return them in descending
    /// alphabetical order (which corresponds to reverse chronological order given the
    /// filename format).
    ///
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetCaptureFilenames_ReturnsSortedDescending()
    {
        var gen = from count in Gen.Choose(1, 20)
                  from timestamps in Gen.ListOf(count, GenValidTimestamp())
                  select timestamps.ToList();

        return Prop.ForAll(gen.ToArbitrary(), timestamps =>
        {
            // Arrange: generate filenames and write empty files to temp directory
            var filenames = timestamps
                .Select(ts => CaptureWriter.GenerateFilename(ts))
                .Distinct()
                .ToList();

            foreach (var filename in filenames)
            {
                File.WriteAllBytes(Path.Combine(_tempDir, filename), []);
            }

            // Act
            var result = CaptureListService.GetCaptureFilenames(_tempDir);

            // Assert: result is sorted in descending alphabetical order
            var expectedOrder = result
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var isSortedDescending = result.SequenceEqual(expectedOrder);

            // Cleanup for next iteration
            foreach (var file in Directory.GetFiles(_tempDir))
                File.Delete(file);

            return isSortedDescending.Label(
                $"Expected descending order. Got: [{string.Join(", ", result.Take(5))}]");
        });
    }

    private static Gen<DateTime> GenValidTimestamp()
    {
        return from year in Gen.Choose(2020, 2030)
               from month in Gen.Choose(1, 12)
               from day in Gen.Choose(1, 28) // safe day range for all months
               from hour in Gen.Choose(0, 23)
               from minute in Gen.Choose(0, 59)
               from second in Gen.Choose(0, 59)
               select new DateTime(year, month, day, hour, minute, second);
    }
}
