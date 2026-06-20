using System.Reflection;

namespace SwiftCam;

/// <summary>
/// Loads frontend assets (HTML, CSS, JS) from embedded resources.
/// Files are compiled into the assembly but can be edited with native tooling during development.
/// </summary>
internal static class HtmlPageContent
{
    internal static readonly string Html = LoadResource("SwiftCam.index.html");
    internal static readonly string Css = LoadResource("SwiftCam.style.css");
    internal static readonly string Js = LoadResource("SwiftCam.app.js");

    private static string LoadResource(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{name}' not found. Ensure the file is included as an EmbeddedResource in the .csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
