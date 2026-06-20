using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for CaptureListService.GetCaptureFilenames .jpg-only filtering.
/// </summary>
public class CaptureListJpgFilterPropertyTests : IDisposable
{
    private readonly string _tempDir;

    public CaptureListJpgFilterPropertyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"swiftcam-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Property 2: Capture listing includes only .jpg files.
    /// For any directory containing a mix of files with various extensions,
    /// GetCaptureFilenames SHALL return only those filenames ending with .jpg
    /// (case-insensitive), and the returned count SHALL equal the number of
    /// .jpg files in the directory.
    ///
    /// Validates: Requirements 1.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetCaptureFilenames_ReturnsOnlyJpgFiles()
    {
        var gen = from fileSet in GenMixedFileSet()
                  select fileSet;

        return Prop.ForAll(gen.ToArbitrary(), fileSet =>
        {
            // Arrange: write all files to temp directory
            foreach (var filename in fileSet.AllFilenames)
            {
                File.WriteAllBytes(Path.Combine(_tempDir, filename), []);
            }

            // Act
            var result = CaptureListService.GetCaptureFilenames(_tempDir);

            // Assert: only .jpg files are returned (case-insensitive)
            var allEndWithJpg = result.All(f =>
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));

            var expectedCount = fileSet.JpgCount;
            var countMatches = result.Length == expectedCount;

            // Cleanup for next iteration
            foreach (var file in Directory.GetFiles(_tempDir))
                File.Delete(file);

            return (allEndWithJpg && countMatches).Label(
                $"Expected {expectedCount} .jpg files, got {result.Length}. " +
                $"All end with .jpg: {allEndWithJpg}. " +
                $"Files in dir: [{string.Join(", ", fileSet.AllFilenames.Take(10))}]");
        });
    }

    private static Gen<MixedFileSet> GenMixedFileSet()
    {
        var extensions = new[] { ".jpg", ".png", ".txt", ".jpeg", ".JPG", ".bmp", ".gif", ".mp4" };

        return from totalCount in Gen.Choose(1, 15)
               from extensionIndices in Gen.ListOf(totalCount, Gen.Choose(0, extensions.Length - 1))
               from baseNames in Gen.ListOf(totalCount, GenSafeBaseName())
               select BuildFileSet(baseNames.ToList(), extensionIndices.ToList(), extensions);
    }

    private static Gen<string> GenSafeBaseName()
    {
        // Generate simple alphanumeric base names to avoid filesystem issues
        return from length in Gen.Choose(3, 12)
               from chars in Gen.ListOf(length, Gen.Elements(
                   'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                   'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                   '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
               select new string(chars.ToArray());
    }

    private static MixedFileSet BuildFileSet(
        List<string> baseNames, List<int> extensionIndices, string[] extensions)
    {
        var allFilenames = new HashSet<string>();
        var jpgCount = 0;

        for (int i = 0; i < baseNames.Count; i++)
        {
            var ext = extensions[extensionIndices[i]];
            var filename = baseNames[i] + ext;

            if (allFilenames.Add(filename))
            {
                if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                    jpgCount++;
            }
        }

        return new MixedFileSet(allFilenames.ToList(), jpgCount);
    }

    private record MixedFileSet(List<string> AllFilenames, int JpgCount);
}
