using FsCheck;
using FsCheck.Xunit;

namespace SwiftCam.Tests.Unit;

/// <summary>
/// Property-based tests for CaptureFileService.IsValidFilename validation logic.
/// </summary>
public class CaptureFileValidationPropertyTests
{
    /// <summary>
    /// Property 3: Invalid filenames are always rejected.
    /// For any filename string that contains path traversal characters (.., /, \)
    /// OR does not end with .jpg, IsValidFilename SHALL return false.
    /// Conversely, for any filename string that has a .jpg extension AND contains
    /// no path traversal characters AND is not empty/whitespace, IsValidFilename
    /// SHALL return true.
    ///
    /// Validates: Requirements 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidFilenames_AreAlwaysRejected()
    {
        return Prop.ForAll(GenInvalidFilename().ToArbitrary(), filename =>
        {
            var result = CaptureFileService.IsValidFilename(filename);

            return (!result).Label(
                $"Expected IsValidFilename to return false for invalid filename: '{filename}'");
        });
    }

    /// <summary>
    /// Property 3 (converse): Valid filenames are always accepted.
    /// For any filename string that has a .jpg extension AND contains no path
    /// traversal characters AND is not empty/whitespace, IsValidFilename SHALL
    /// return true.
    ///
    /// Validates: Requirements 2.3, 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidFilenames_AreAlwaysAccepted()
    {
        return Prop.ForAll(GenValidFilename().ToArbitrary(), filename =>
        {
            var result = CaptureFileService.IsValidFilename(filename);

            return result.Label(
                $"Expected IsValidFilename to return true for valid filename: '{filename}'");
        });
    }

    /// <summary>
    /// Generates invalid filenames: filenames with path traversal characters
    /// and/or non-.jpg extensions.
    /// </summary>
    private static Gen<string> GenInvalidFilename()
    {
        var genWithPathTraversal = from baseName in GenSafeBaseName()
                                   from traversal in Gen.Elements("..", "/", "\\")
                                   from position in Gen.Elements("prefix", "middle", "suffix")
                                   from ext in Gen.Elements(".jpg", ".png", ".txt", ".jpeg")
                                   select position switch
                                   {
                                       "prefix" => traversal + baseName + ext,
                                       "middle" => baseName + traversal + "file" + ext,
                                       _ => baseName + ext + traversal
                                   };

        var genWrongExtension = from baseName in GenSafeBaseName()
                                from ext in Gen.Elements(".png", ".txt", ".jpeg", ".bmp", ".gif", ".mp4", "")
                                select baseName + ext;

        var genEmptyOrWhitespace = Gen.Elements("", " ", "  ", "\t", "\n");

        return Gen.OneOf(genWithPathTraversal, genWrongExtension, genEmptyOrWhitespace);
    }

    /// <summary>
    /// Generates valid filenames: safe base name + .jpg extension, no traversal chars.
    /// </summary>
    private static Gen<string> GenValidFilename()
    {
        return from baseName in GenSafeBaseName()
               from ext in Gen.Elements(".jpg", ".JPG", ".Jpg", ".jPg")
               select baseName + ext;
    }

    /// <summary>
    /// Generates simple alphanumeric base names without path separators or traversal chars.
    /// </summary>
    private static Gen<string> GenSafeBaseName()
    {
        return from length in Gen.Choose(3, 20)
               from chars in Gen.ListOf(length, Gen.Elements(
                   'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                   'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                   'u', 'v', 'w', 'x', 'y', 'z',
                   '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                   '-', '_'))
               select new string(chars.ToArray());
    }
}
