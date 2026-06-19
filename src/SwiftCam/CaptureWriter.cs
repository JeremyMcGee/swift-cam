namespace SwiftCam;

/// <summary>
/// Writes a JPEG frame to disk with a timestamped filename,
/// creating the directory if needed.
/// </summary>
public static class CaptureWriter
{
    public static string GenerateFilename(DateTime timestamp)
    {
        return timestamp.ToString("yyyy-MMM-dd_HH-mm-ss") + ".jpg";
    }

    public static async Task<string> SaveAsync(
        byte[] jpegData,
        string captureDirectory,
        DateTime timestamp,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(captureDirectory);

        var filename = GenerateFilename(timestamp);
        var fullPath = Path.Combine(captureDirectory, filename);

        await File.WriteAllBytesAsync(fullPath, jpegData, ct);

        return fullPath;
    }
}
