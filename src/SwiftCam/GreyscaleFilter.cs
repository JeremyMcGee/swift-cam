using SkiaSharp;

namespace SwiftCam;

/// <summary>
/// Converts JPEG frames to greyscale using the ITU-R BT.601 luminance formula.
/// Applied before the timestamp overlay so the text remains crisp.
/// </summary>
public static class GreyscaleFilter
{
    /// <summary>
    /// Decodes a JPEG frame, converts to greyscale, and re-encodes as JPEG.
    /// </summary>
    /// <param name="jpegData">The raw JPEG frame bytes.</param>
    /// <param name="quality">JPEG re-encoding quality (1-100).</param>
    /// <returns>New JPEG bytes in greyscale.</returns>
    public static byte[] Apply(byte[] jpegData, int quality)
    {
        using var inputBitmap = SKBitmap.Decode(jpegData);
        if (inputBitmap is null)
            return jpegData;

        using var greyscaleBitmap = new SKBitmap(inputBitmap.Width, inputBitmap.Height);
        using var canvas = new SKCanvas(greyscaleBitmap);

        using var paint = new SKPaint();
        paint.ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
        {
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0.299f, 0.587f, 0.114f, 0, 0,
            0,      0,      0,      1, 0
        });

        canvas.DrawBitmap(inputBitmap, 0, 0, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(greyscaleBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        return data.ToArray();
    }
}
