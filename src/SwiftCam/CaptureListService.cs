namespace SwiftCam;

/// <summary>
/// Lists capture filenames from disk, sorted most recent first.
/// </summary>
public static class CaptureListService
{
    /// <summary>
    /// Returns .jpg filenames from the capture directory, sorted most recent first.
    /// Returns empty array if directory doesn't exist or contains no .jpg files.
    /// </summary>
    /// <param name="captureDirectory">Path to the capture directory.</param>
    /// <returns>Array of .jpg filenames sorted in descending alphabetical order.</returns>
    public static string[] GetCaptureFilenames(string captureDirectory)
    {
        try
        {
            if (!Directory.Exists(captureDirectory))
                return [];

            var filenames = Directory.GetFiles(captureDirectory)
                .Select(Path.GetFileName)
                .Where(f => f!.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return filenames!;
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}
