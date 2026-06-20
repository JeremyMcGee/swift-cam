namespace SwiftCam;

/// <summary>
/// Validates and resolves capture file requests.
/// </summary>
public static class CaptureFileService
{
    /// <summary>
    /// Returns true if the filename is safe (no path traversal, has .jpg extension, not empty).
    /// </summary>
    /// <param name="filename">The filename to validate.</param>
    /// <returns>True if the filename is valid; otherwise false.</returns>
    public static bool IsValidFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            return false;

        if (!filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Validates the filename and returns the full path if valid and exists.
    /// Returns null if the file doesn't exist.
    /// Throws ArgumentException if the filename contains path traversal or has no .jpg extension.
    /// </summary>
    /// <param name="filename">The capture filename to resolve.</param>
    /// <param name="captureDirectory">Path to the capture directory.</param>
    /// <returns>Full path to the file if it exists; null otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when the filename is invalid.</exception>
    public static string? ResolveCaptureFile(string filename, string captureDirectory)
    {
        if (!IsValidFilename(filename))
            throw new ArgumentException("Invalid filename.", nameof(filename));

        var fullPath = Path.GetFullPath(Path.Combine(captureDirectory, filename));

        if (!File.Exists(fullPath))
            return null;

        return fullPath;
    }
}
