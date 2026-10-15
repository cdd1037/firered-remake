using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated FireRed Super Nerd/Hiker/male Rocket grunt static fronts, with source palette expansion.</summary>
public static class PretMtMoonTrainerArt
{

    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        byte[] Read(string relative)
        {
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is ".." or "." or "") || relative.Contains('\\'))
                throw new InvalidDataException("MtMoon trainer art path differs.");
            var info = new FileInfo(Path.GetFullPath(Path.Combine(root, relative)));
            if (info.LinkTarget is not null) throw new InvalidDataException("MtMoon trainer art symlink rejected.");
            for (var directory = info.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("MtMoon trainer art directory symlink rejected.");
var bytes = File.ReadAllBytes(info.FullName);
return bytes;
        }
        var manifest = Read("content/generated/mtmoon-trainer-art/mtmoon-trainer-art.json");
        using var json = JsonDocument.Parse(manifest);
        var document = json.RootElement;
        if (            document.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)" || document.GetProperty("assets").GetArrayLength() != 3)
            throw new InvalidDataException("MtMoon trainer art profile differs.");
        var result = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            (string Stem, int PicId) expected = id switch
            {
                "super-nerd-front" => ("super_nerd", 89),
                "hiker-front" => ("hiker", 90),
                "rocket-m-front" => ("rocket_grunt_m", 109),
                _ => throw new InvalidDataException("MtMoon trainer portrait identity differs.")
            };
            if (!ids.Add(id) ||
                asset.GetProperty("texturePath").GetString() != id + ".png" ||
                asset.GetProperty("width").GetInt32() != 64 || asset.GetProperty("height").GetInt32() != 64 ||
                asset.GetProperty("sourceFrame").GetInt32() != 0 ||
                asset.GetProperty("sourceImage").GetString() != "graphics/trainers/front_pics/" + expected.Stem + "_front_pic.png" ||
                asset.GetProperty("sourcePalette").GetString() != "graphics/trainers/palettes/" + expected.Stem + ".pal" ||
                asset.GetProperty("sourceTrainerPicId").GetInt32() != expected.PicId ||
                asset.GetProperty("x").GetInt32() != 144 || asset.GetProperty("y").GetInt32() != 8)
                throw new InvalidDataException("MtMoon trainer portrait identity differs.");
            var rgba = PngRgba8.Decode(Read("content/generated/mtmoon-trainer-art/" + id + ".png"));
            if (rgba.Length != 64 * 64 * 4) throw new InvalidDataException("MtMoon trainer portrait size differs.");
            result.Add(new(id, 144, 8, rgba));
        }
        return result.ToImmutable();
    }
}
