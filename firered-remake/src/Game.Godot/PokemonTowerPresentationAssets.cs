using System.Collections.Immutable;
using System.Text.Json;

/// <summary>Tower presentation assets, including shared ghost and Marowak textures.</summary>
internal static class PokemonTowerPresentationAssets
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal sealed record Package(JsonElement Manifest, ImmutableDictionary<string, byte[]> Files);

    private static byte[] Read(string[] roots, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Tower presentation path.");
        foreach (string root in roots)
        {
            var file = new FileInfo(Path.Combine(root, relative));
            if (file.LinkTarget is not null) throw new InvalidDataException("Tower presentation symlink rejected.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
                if (parent.LinkTarget is not null) throw new InvalidDataException("Tower presentation directory symlink rejected.");
            if (file.Exists) return File.ReadAllBytes(file.FullName);
        }
        throw new FileNotFoundException("Tower presentation dependency missing.", relative);
    }

    internal static Package Load(string root, bool art = false, params string[] dependencyRoots)
    {
        string[] roots = [root, .. dependencyRoots];
        string stem = art ? "pokemon-tower-art" : "pokemon-tower-presentation";
        using var document = JsonDocument.Parse(Read([root], "content/generated/" + (art ? "sprites/pokemon/pokemon-tower" : "ui/pokemon-tower-presentation") + "/manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported Tower presentation format.");
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        if (!art)
        {
            string[] inherited =
            [
                manifest.GetProperty("ghost").GetProperty("texture").GetString()!,
                manifest.GetProperty("marowak").GetProperty("textureNormal").GetString()!,
                manifest.GetProperty("marowak").GetProperty("textureShiny").GetString()!
            ];
            foreach (string path in inherited) files.Add(path, Read(roots, path));
        }
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string relative = row.GetProperty("path").GetString()!;
            files.Add(relative, Read([root], "content/generated/" + (art ? "sprites/pokemon/pokemon-tower" : "ui/pokemon-tower-presentation") + "/" + relative));
        }
        return new(manifest.Clone(), files.ToImmutable());
    }
}
