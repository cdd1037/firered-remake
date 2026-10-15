using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

public sealed record ForestTrainerPortrait(string Id, int X, int Y, byte[] Png);

/// <summary>Authenticated original static trainer frame pixels, never a runtime palette effect.</summary>
public static class PretForestTrainerArt
{
    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        byte[] Read(string relative)
        {
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is ".." or ".") || relative.Contains('\\'))
                throw new InvalidDataException("Trainer art path differs.");
            string path = Path.GetFullPath(Path.Combine(root, relative));
            var info = new FileInfo(path);
            if (info.LinkTarget is not null) throw new InvalidDataException("Trainer art symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Trainer art directory symlink rejected.");
            byte[] bytes = File.ReadAllBytes(path);
            return bytes;
        }
        var manifest = Read("content/generated/forest-trainer-art/forest-trainer-art.json");
        using var json = JsonDocument.Parse(manifest);
        var document = json.RootElement;
        if (document.GetProperty("assets").GetArrayLength() != 3)
            throw new InvalidDataException("Trainer art profile differs.");
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            if (id is not ("bug-catcher-front" or "red-back" or "leaf-back") || asset.GetProperty("texturePath").GetString() != id + ".png" ||
                asset.GetProperty("width").GetInt32() != 64 || asset.GetProperty("height").GetInt32() != 64 || asset.GetProperty("sourceFrame").GetInt32() != 0)
                throw new InvalidDataException("Trainer portrait identity differs.");
            var pngBytes = Read("content/generated/forest-trainer-art/" + id + ".png");
            if (pngBytes.Length == 0) throw new InvalidDataException("Trainer portrait size differs.");
            result.Add(new(id, asset.GetProperty("x").GetInt32(), asset.GetProperty("y").GetInt32(), pngBytes));
        }
        return result.ToImmutable();
    }
}
