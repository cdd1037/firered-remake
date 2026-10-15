using System.Text.Json;

namespace Game.Content;

/// <summary>Reads only requested converted game data; original sources belong to import tools.</summary>
internal sealed class SilphSourcePackage
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal string Directory { get; }
    private readonly string _projectRoot;
    internal static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal SilphSourcePackage(string root, string name)
    { _projectRoot = root; Directory = Path.Combine(root, "content/generated/" + name); }
    internal byte[] Read(string path) => SafariSource.Read(path.StartsWith("content/generated/", StringComparison.Ordinal) ? _projectRoot : Directory, path);
    internal JsonElement Document(string path) => Json(Read(path));
    internal T Parse<T>(string path) => SafariSource.Parse<T>(Read(path), _projectRoot);
    internal static JsonElement Json(byte[] bytes) => SafariSource.Parse<JsonElement>(bytes);
    internal static SilphSourcePackage Catalogs(string root) => new(root, "silph-catalogs");
}
