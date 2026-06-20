namespace SwiftCam;

/// <summary>
/// Static utility for performing capture file deletion with validation and error handling.
/// </summary>
public static class CaptureDeleteService
{
    /// <summary>
    /// Deletes the specified capture file from the directory.
    /// </summary>
    /// <param name="filename">The capture filename to delete.</param>
    /// <param name="captureDirectory">Path to the capture directory.</param>
    /// <exception cref="ArgumentException">Thrown when the filename contains path traversal characters or has an unsupported extension.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="IOException">Thrown when the delete operation fails due to a file system error.</exception>
    public static void DeleteCapture(string filename, string captureDirectory)
    {
        if (!CaptureFileService.IsValidFilename(filename))
        {
            if (string.IsNullOrWhiteSpace(filename) ||
                filename.Contains("..") ||
                filename.Contains('/') ||
                filename.Contains('\\'))
            {
                throw new ArgumentException(
                    "Invalid filename: path traversal characters are not allowed", nameof(filename));
            }

            throw new ArgumentException(
                "Invalid filename: only .jpg files are supported", nameof(filename));
        }

        var fullPath = Path.GetFullPath(Path.Combine(captureDirectory, filename));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Capture not found: the specified file does not exist", filename);
        }

        File.Delete(fullPath);
    }
}
