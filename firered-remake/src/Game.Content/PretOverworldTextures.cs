namespace Game.Content;

/// <summary>Resolves a shared overworld sheet from a generated package without general parent traversal.</summary>
public static class PretOverworldTextures
{
    public static IReadOnlyDictionary<string, OverworldSprite> Load(string generatedDirectory)
        => ContentJson.ParseStrict<Dictionary<string, OverworldSprite>>(
            File.ReadAllText(Path.Combine(generatedDirectory, "shared-overworld", "catalog.json")), ContentJson.CreateOptions());

    public const string RelativePrefix = "../shared-overworld/";

    public static string Resolve(string packageDirectory, string relative)
    {
        if (!relative.StartsWith(RelativePrefix, StringComparison.Ordinal))
            throw new InvalidDataException("Expected a shared overworld texture reference.");
        string name = relative[RelativePrefix.Length..];
        if (!name.EndsWith(".png", StringComparison.Ordinal) || name.Length <= 4 ||
            name[..^4].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new InvalidDataException("Invalid shared overworld texture filename.");
        var package = new DirectoryInfo(Path.GetFullPath(packageDirectory));
        if (package.Parent is not { Name: "generated", Parent.Name: "content" } generated)
            throw new InvalidDataException("Overworld textures require a direct generated package directory.");
        for (var cursor = package; cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null)
                throw new InvalidDataException("Overworld package directory symlink is unsupported.");
        string directory = Path.Combine(generated.FullName, "shared-overworld");
        string file = Path.Combine(directory, name);
        if (new DirectoryInfo(directory).LinkTarget is not null || new FileInfo(file).LinkTarget is not null)
            throw new InvalidDataException("Overworld texture symlink is unsupported.");
        return file;
    }
}

/// <summary>One animation sheet, stored once in the shared overworld catalog.</summary>
public sealed record OverworldSprite(string TexturePath, ImageRegion Region, int FrameWidth, int FrameHeight,
    int FrameCount, int AnchorX, int AnchorY, bool Inanimate);
