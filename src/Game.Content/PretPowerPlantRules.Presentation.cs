using System.Text.Json;
namespace Game.Content;

public static partial class PretPowerPlantRules
{
    public static JsonElement LoadPresentationDocument(string root)
    {
        var d = JsonSerializer.Deserialize<JsonElement>(Read(root,"content/generated/sprites/pokemon/power-plant/manifest.json"));
        if (            d.GetProperty("sprites").GetArrayLength()!=12 || d.GetProperty("pokemonIcons").GetArrayLength()!=3 ||
            d.GetProperty("footprints").GetArrayLength()!=3 || d.GetProperty("itemIcons").GetArrayLength()!=3 ||
            d.GetProperty("assets").GetArrayLength()!=19 ||
            !d.GetProperty("newOwnedSpecies").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual([125,135,145]))
            throw new InvalidDataException("power_plant_presentation_scope");
        return d;
    }
}
