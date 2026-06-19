using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based test verifying capture file round-trip integrity.
/// For any non-empty byte array, CaptureWriter.SaveAsync produces a file
/// whose contents are byte-for-byte identical to the input data.
///
/// **Validates: Requirements 3.1**
/// </summary>
public class CaptureWriterRoundTripPropertyTests
{
    /// <summary>
    /// Property 3: For any valid byte array and any valid directory path,
    /// calling CaptureWriter.SaveAsync shall produce a file on disk whose
    /// contents are byte-for-byte identical to the input data.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SaveAsync_RoundTrips_ByteArrayIdentically()
    {
        return Prop.ForAll(
            Arb.From(Gen.ArrayOf(Gen.Choose(0, 255).Select(i => (byte)i))
                .Where(arr => arr.Length > 0)),
            data =>
            {
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                try
                {
                    var timestamp = new DateTime(2025, 1, 15, 14, 30, 22);

                    var resultPath = CaptureWriter.SaveAsync(data, tempDir, timestamp)
                        .GetAwaiter().GetResult();

                    var readBack = File.ReadAllBytes(resultPath);

                    return data.SequenceEqual(readBack)
                        .Label($"Written {data.Length} bytes but read back {readBack.Length} bytes");
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
            });
    }
}
