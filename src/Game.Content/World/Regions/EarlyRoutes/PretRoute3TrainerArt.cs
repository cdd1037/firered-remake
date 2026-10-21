using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated FireRed Youngster/Lass static fronts, with source palette expansion.</summary>
public static class PretRoute3TrainerArt
{

    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        byte[] Read(string relative)
        {
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("Route3 trainer art path differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("Route3 trainer art symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Route3 trainer art directory symlink rejected.");
            byte[] bytes = File.ReadAllBytes(info.FullName);
            return bytes;
        }
        var manifest = Read("content/generated/sprites/trainers/route3/route3-trainer-art.json");
        using var json = JsonDocument.Parse(manifest);
        var document = json.RootElement;
        if (            document.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)" || document.GetProperty("assets").GetArrayLength() != 2)
            throw new InvalidDataException("Route3 trainer art profile differs.");
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            if (id is not ("youngster-front" or "lass-front") || !ids.Add(id) ||
                asset.GetProperty("texturePath").GetString() != id + ".png" ||
                asset.GetProperty("width").GetInt32() != 64 || asset.GetProperty("height").GetInt32() != 64 ||
                asset.GetProperty("sourceFrame").GetInt32() != 0 ||
                asset.GetProperty("sourceTrainerPicId").GetInt32() != (id == "youngster-front" ? 82 : 84) ||
                asset.GetProperty("x").GetInt32() != 144 || asset.GetProperty("y").GetInt32() != 8)
                throw new InvalidDataException("Route3 trainer portrait identity differs.");
            var pngBytes = Read("content/generated/sprites/trainers/route3/" + id + ".png");
            if (pngBytes.Length == 0) throw new InvalidDataException("Route3 trainer portrait size differs.");
            result.Add(new(id, 144, 8, pngBytes));
        }
        return result.ToImmutable();
    }
}
