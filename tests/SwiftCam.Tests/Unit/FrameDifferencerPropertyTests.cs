using FsCheck;
using FsCheck.Xunit;
using SkiaSharp;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for the FrameDifferencer component.
/// </summary>
public class FrameDifferencerPropertyTests
{
    /// <summary>
    /// Property 1: Frame differencing correctness.
    /// For any two pixel arrays of equal dimensions and any pixel tolerance value,
    /// ComputeChangedPercentage shall return the ratio of pixels whose absolute
    /// luminance difference exceeds the tolerance, expressed as a percentage
    /// between 0.0 and 100.0 inclusive.
    /// 
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FrameDifferencing_ReturnsCorrectChangedPercentage()
    {
        var genWidth = Gen.Choose(1, 20);
        var genHeight = Gen.Choose(1, 20);
        var genTolerance = Gen.Choose(1, 255);

        var gen = from width in genWidth
                  from height in genHeight
                  from tolerance in genTolerance
                  from pixels1 in Gen.ArrayOf(width * height, GenPixel())
                  from pixels2 in Gen.ArrayOf(width * height, GenPixel())
                  select (width, height, tolerance, pixels1, pixels2);

        return Prop.ForAll(gen.ToArbitrary(), input =>
        {
            var (width, height, tolerance, pixels1, pixels2) = input;

            // Create bitmaps and encode to JPEG
            var jpegPrev = CreateJpeg(width, height, pixels1);
            var jpegCurr = CreateJpeg(width, height, pixels2);

            // Decode back to get actual pixel values after JPEG compression
            using var decodedPrev = SKBitmap.Decode(jpegPrev);
            using var decodedCurr = SKBitmap.Decode(jpegCurr);

            // Compute expected percentage from the decoded (post-compression) pixels
            var totalPixels = width * height;
            var changedPixels = 0;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var prevLum = GetLuminance(decodedPrev.GetPixel(x, y));
                    var currLum = GetLuminance(decodedCurr.GetPixel(x, y));

                    if (Math.Abs(prevLum - currLum) > tolerance)
                        changedPixels++;
                }
            }

            var expectedPercentage = (double)changedPixels / totalPixels * 100.0;

            // Call the system under test
            var actualPercentage = FrameDifferencer.ComputeChangedPercentage(
                jpegPrev, jpegCurr, tolerance);

            // Result must be in valid range
            var inRange = actualPercentage >= 0.0 && actualPercentage <= 100.0;

            // Result must match expected (exact, since we use the same decoded pixels)
            var matchesExpected = Math.Abs(actualPercentage - expectedPercentage) < 0.0001;

            return inRange && matchesExpected;
        });
    }

    private static Gen<SKColor> GenPixel()
    {
        return from r in Gen.Choose(0, 255)
               from g in Gen.Choose(0, 255)
               from b in Gen.Choose(0, 255)
               select new SKColor((byte)r, (byte)g, (byte)b);
    }

    private static byte[] CreateJpeg(int width, int height, SKColor[] pixels)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, pixels[y * width + x]);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    private static int GetLuminance(SKColor color)
    {
        // ITU-R BT.601 luminance formula (same as FrameDifferencer)
        return (int)(0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue);
    }
}
