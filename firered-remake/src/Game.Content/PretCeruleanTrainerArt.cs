using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>Only the three new source portraits. Existing rival and Rocket portraits remain in their prior catalogs.</summary>
public static class PretCeruleanTrainerArt
{
    public static ImmutableArray<ForestTrainerPortrait> Load(string root)
    {
        using var json = JsonDocument.Parse(PretCeruleanTrainers.ReadPinned(root,            "content/generated/cerulean-trainers/cerulean-trainers.json"));
        var document = json.RootElement;
        if (            document.GetProperty("paletteExpansion").GetString() != "(rgb5 << 3) | (rgb5 >> 2)" || document.GetProperty("assets").GetArrayLength() != 3)
            throw new InvalidDataException("Cerulean trainer art profile differs.");
        var portraits = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        foreach (var asset in document.GetProperty("assets").EnumerateArray())
        {
            string id = asset.GetProperty("id").GetString()!;
            var expected = id switch
            {
                "misty-front" => (Stem: "leader_misty", Id: 117),
                "picnicker-front" => (Stem: "picnicker", Id: 87),
                "swimmer-m-front" => (Stem: "swimmer_m", Id: 95),
                _ => throw new InvalidDataException("Unknown Cerulean source portrait.")
            };
            if (portraits.Any(p => p.Id == id) || asset.GetProperty("texturePath").GetString() != id + ".png" ||
                asset.GetProperty("sourceImage").GetString() != "graphics/trainers/front_pics/" + expected.Stem + "_front_pic.png" ||
                asset.GetProperty("sourcePalette").GetString() != "graphics/trainers/palettes/" + expected.Stem + ".pal" ||
                asset.GetProperty("sourceTrainerPicId").GetInt32() != expected.Id || asset.GetProperty("sourceFrame").GetInt32() != 0 ||
                asset.GetProperty("width").GetInt32() != 64 || asset.GetProperty("height").GetInt32() != 64 ||
                asset.GetProperty("x").GetInt32() != 144 || asset.GetProperty("y").GetInt32() != 8)
                throw new InvalidDataException("Cerulean source portrait geometry differs.");
            portraits.Add(new(id, 144, 8, PretCeruleanTrainers.ReadPinned(root,"content/generated/cerulean-trainers/" + id + ".png")));
        }
        return portraits.ToImmutable();
    }
}
