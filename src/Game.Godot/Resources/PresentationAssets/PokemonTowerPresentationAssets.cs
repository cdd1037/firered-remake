using Game.Content;

/// <summary>Tower presentation retains the explicit ghost and Marowak dependencies.</summary>
internal static class PokemonTowerPresentationAssets
{
    internal static PresentationPackage Load(string root, bool art = false, params string[] dependencyRoots)
    {
        var package = PresentationPackage.Load(root, "content/generated/" +
            (art ? "sprites/pokemon/pokemon-tower/" : "ui/pokemon-tower-presentation/"), relativeOutputKeys: true);
        if (art) return package;
        var manifest = package.Manifest;
        string[] inherited =
        [
            manifest.GetProperty("ghost").GetProperty("texture").GetString()!,
            manifest.GetProperty("marowak").GetProperty("textureNormal").GetString()!,
            manifest.GetProperty("marowak").GetProperty("textureShiny").GetString()!
        ];
        foreach (string path in inherited)
        {
            string[] roots = [root, .. dependencyRoots];
            string? existingRoot = roots.FirstOrDefault(r => File.Exists(GeneratedResourceFile.SafePath(r, path)));
            package.Files.Add(path, GeneratedResourceFile.Read(existingRoot ?? root, path));
        }
        return package;
    }
}
