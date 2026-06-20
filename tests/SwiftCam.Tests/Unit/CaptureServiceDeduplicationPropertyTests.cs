using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

// Feature: capture-management, Property 2: Filename deduplication uniqueness

/// <summary>
/// Property-based test verifying filename deduplication uniqueness.
/// For any timestamp and any number of pre-existing files with the same base filename,
/// GenerateUniqueFilename returns a filename that does not match any existing file
/// in the directory and follows the format yyyy-MMM-dd_HH-mm-ss_N.jpg where N is
/// the lowest positive integer producing a unique name.
///
/// **Validates: Requirements 1.6**
/// </summary>
public class CaptureServiceDeduplicationPropertyTests
{
    /// <summary>
    /// Property 2: Filename deduplication uniqueness
    /// For any valid DateTime timestamp and 0–10 pre-existing collision files,
    /// GenerateUniqueFilename returns a filename that:
    /// 1. Does NOT exist in the directory (it's unique)
    /// 2. If collisions exist, follows the suffix pattern _N.jpg where N is the next available number
    /// 3. If no collisions exist, the base filename is returned (no suffix)
    ///
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GenerateUniqueFilename_ReturnsUniqueFilename_WithCorrectSuffix()
    {
        var timestampGen = Gen.Choose(2000, 2030).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
            Gen.Choose(1, 28).SelectMany(day =>
            Gen.Choose(0, 23).SelectMany(hour =>
            Gen.Choose(0, 59).SelectMany(minute =>
            Gen.Choose(0, 59).Select(second =>
                new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc)))))));

        var collisionCountGen = Gen.Choose(0, 10);

        return Prop.ForAll(
            Arb.From(timestampGen),
            Arb.From(collisionCountGen),
            (timestamp, collisionCount) =>
            {
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    Directory.CreateDirectory(tempDir);

                    var baseFilename = CaptureWriter.GenerateFilename(timestamp);
                    var nameWithoutExtension = Path.GetFileNameWithoutExtension(baseFilename);
                    var extension = Path.GetExtension(baseFilename);

                    // Pre-create collision files: base, _1, _2, ..., _(collisionCount-1)
                    if (collisionCount > 0)
                    {
                        // Create the base file
                        File.WriteAllBytes(Path.Combine(tempDir, baseFilename), [0]);

                        // Create suffixed files _1 through _(collisionCount-1)
                        for (int i = 1; i < collisionCount; i++)
                        {
                            var suffixedName = $"{nameWithoutExtension}_{i}{extension}";
                            File.WriteAllBytes(Path.Combine(tempDir, suffixedName), [0]);
                        }
                    }

                    // Call the method under test
                    var result = CaptureService.GenerateUniqueFilename(timestamp, tempDir);

                    // Assert 1: The returned filename does NOT exist in the temp directory
                    var isUnique = !File.Exists(Path.Combine(tempDir, result));

                    // Assert 2 & 3: Check format correctness
                    bool hasCorrectFormat;
                    if (collisionCount == 0)
                    {
                        // No collisions: base filename should be returned
                        hasCorrectFormat = result == baseFilename;
                    }
                    else
                    {
                        // Collisions exist: should be _N.jpg where N is the collision count
                        // (since base and _1 through _(collisionCount-1) are taken,
                        //  the next available is _collisionCount)
                        var expectedSuffix = collisionCount == 1
                            ? $"{nameWithoutExtension}_1{extension}"
                            : $"{nameWithoutExtension}_{collisionCount}{extension}";

                        // For collisionCount == 1, only base exists so _1 is next
                        // For collisionCount > 1, base + _1.._(count-1) exist so _count is next
                        hasCorrectFormat = result == expectedSuffix;
                    }

                    return isUnique
                        .Label("Returned filename must not exist in directory")
                        .And(hasCorrectFormat)
                        .Label($"Expected format mismatch: got '{result}', collisions={collisionCount}, base='{baseFilename}'");
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
            });
    }
}
