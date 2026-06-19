using SkiaSharp;

namespace SwiftCam;

/// <summary>
/// Decodes two JPEG frames and computes the percentage of pixels whose
/// absolute luminance difference exceeds a per-pixel tolerance.
/// </summary>
public static class FrameDifferencer
{
    public static double ComputeChangedPercentage(
        byte[] previousFrame,
        byte[] currentFrame,
        int pixelTolerance)
    {
        using var prev = SKBitmap.Decode(previousFrame);
        using var curr = SKBitmap.Decode(currentFrame);

        if (prev is null || curr is null)
            return 0.0;

        var width = Math.Min(prev.Width, curr.Width);
        var height = Math.Min(prev.Height, curr.Height);
        var totalPixels = width * height;

        if (totalPixels == 0)
            return 0.0;

        var changedPixels = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var prevLum = GetLuminance(prev.GetPixel(x, y));
                var currLum = GetLuminance(curr.GetPixel(x, y));

                if (Math.Abs(prevLum - currLum) > pixelTolerance)
                    changedPixels++;
            }
        }

        return (double)changedPixels / totalPixels * 100.0;
    }

    private static int GetLuminance(SKColor color)
    {
        // ITU-R BT.601 luminance formula
        return (int)(0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue);
    }
}
