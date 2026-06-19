using SkiaSharp;

namespace SwiftCam;

/// <summary>
/// Overlays a timestamp on JPEG frames in dark blue at the bottom-right corner.
/// </summary>
public static class TimestampOverlay
{
    private const float FontSize = 14f;
    private const float Padding = 8f;

    private static readonly SKColor TextColor = new(0, 0, 139); // Dark blue

    /// <summary>
    /// Decodes a JPEG frame, draws a timestamp overlay, and re-encodes as JPEG.
    /// </summary>
    /// <param name="jpegData">The raw JPEG frame bytes.</param>
    /// <param name="quality">JPEG re-encoding quality (1-100).</param>
    /// <returns>New JPEG bytes with timestamp overlay.</returns>
    public static byte[] Apply(byte[] jpegData, int quality)
    {
        using var inputBitmap = SKBitmap.Decode(jpegData);
        if (inputBitmap == null)
            return jpegData; // If decode fails, return original

        using var canvas = new SKCanvas(inputBitmap);

        var timestamp = DateTime.Now.ToString("yyyy-MMM-dd HH:mm");

        using var font = new SKFont(SKTypeface.FromFamilyName("monospace", SKFontStyle.Normal), FontSize);
        using var paint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true
        };

        var textWidth = font.MeasureText(timestamp);

        var x = inputBitmap.Width - textWidth - Padding;
        var y = inputBitmap.Height - Padding;

        canvas.DrawText(timestamp, x, y, SKTextAlign.Left, font, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(inputBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        return data.ToArray();
    }
}
