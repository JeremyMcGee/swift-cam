using Xunit;

namespace SwiftCam.Tests.Unit;

public class CaptureDeleteServiceTests : IDisposable
{
    private readonly string _tempDir;

    public CaptureDeleteServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "swiftcam-delete-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void DeleteCapture_ExistingFile_DeletesFileSuccessfully()
    {
        // Arrange
        var filename = "2025-Jun-15_14-30-22.jpg";
        var filePath = Path.Combine(_tempDir, filename);
        File.WriteAllBytes(filePath, [0xFF, 0xD8, 0xFF, 0xE0]);

        // Act
        CaptureDeleteService.DeleteCapture(filename, _tempDir);

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void DeleteCapture_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filename = "does-not-exist.jpg";

        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(
            () => CaptureDeleteService.DeleteCapture(filename, _tempDir));

        Assert.Contains("the specified file does not exist", ex.Message);
    }

    [Theory]
    [InlineData("../test.jpg")]
    [InlineData("test/file.jpg")]
    [InlineData("test\\file.jpg")]
    [InlineData("..\\secret.jpg")]
    public void DeleteCapture_PathTraversalCharacters_ThrowsArgumentException(string filename)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => CaptureDeleteService.DeleteCapture(filename, _tempDir));

        Assert.Contains("path traversal characters are not allowed", ex.Message);
    }

    [Theory]
    [InlineData("test.png")]
    [InlineData("photo.gif")]
    [InlineData("capture.jpeg")]
    [InlineData("noextension")]
    public void DeleteCapture_NonJpgExtension_ThrowsArgumentException(string filename)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => CaptureDeleteService.DeleteCapture(filename, _tempDir));

        Assert.Contains("only .jpg files are supported", ex.Message);
    }

    [Fact]
    public void DeleteCapture_ReadOnlyFile_ThrowsIOException()
    {
        // Arrange
        var filename = "readonly-capture.jpg";
        var filePath = Path.Combine(_tempDir, filename);
        File.WriteAllBytes(filePath, [0xFF, 0xD8, 0xFF, 0xE0]);
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        try
        {
            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(
                () => CaptureDeleteService.DeleteCapture(filename, _tempDir));
        }
        finally
        {
            // Clean up: remove read-only attribute so temp dir cleanup works
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }
}
