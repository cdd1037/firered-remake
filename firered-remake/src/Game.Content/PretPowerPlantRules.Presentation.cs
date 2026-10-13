using System.Text.Json;
namespace Game.Content;

public static partial class PretPowerPlantRules
{
    public const string PresentationSha256 = "60981c2d56a7104cbae659daa6110787e7f31005f76fb3dcf17fdefc442cc001";
    public const string PresentationLockSha256 = "3c0e20b5527adcf81bef68f59ffd5e1a167d09314ebb30c4b55792422517b7ff";
    public const string SynthManifestSha256 = "dfc0091de6f44432f3bfeba4afa86048a3201732b5ca00d1f34000048a7669bd";
    public static JsonElement LoadPresentationDocument(string root)
    {
        var d = JsonSerializer.Deserialize<JsonElement>(Read(root,"content/generated/power-plant-presentation/manifest.json",PresentationSha256));
        if (d.GetProperty("sourceCommit").GetString() != Game.Domain.Pokemon.PokemonFactory.SourceCommit ||
            d.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            d.GetProperty("lockSha256").GetString() != PresentationLockSha256 ||
            d.GetProperty("profile").GetString() != "firered-power-plant-presentation-v1" ||
            d.GetProperty("sprites").GetArrayLength()!=12 || d.GetProperty("pokemonIcons").GetArrayLength()!=3 ||
            d.GetProperty("footprints").GetArrayLength()!=3 || d.GetProperty("itemIcons").GetArrayLength()!=3 ||
            d.GetProperty("assets").GetArrayLength()!=19 || d.GetProperty("outputs").GetArrayLength()!=39 ||
            !d.GetProperty("newOwnedSpecies").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual([125,135,145]))
            throw new InvalidDataException("power_plant_presentation_scope");
        return d;
    }
    public static void ValidatePresentationEvidence(string root, string? dependencyRoot = null)
    {
        _ = Read(root,"scripts/power-plant-presentation.lock.json",PresentationLockSha256);
        var d=LoadPresentationDocument(root);
        foreach(var row in d.GetProperty("inputs").EnumerateArray())
        {
            var path=row.GetProperty("path").GetString()!;
            var bytes=Read(root,"content/raw/power-plant-presentation/"+path,row.GetProperty("sha256").GetString()!);
            if(bytes.Length!=row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData([.. System.Text.Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("power_plant_presentation_source:"+path);
        }
        foreach(var row in d.GetProperty("inheritedInputs").EnumerateArray())
            if(Read(dependencyRoot??root,row.GetProperty("path").GetString()!,row.GetProperty("sha256").GetString()!).Length!=row.GetProperty("byteLength").GetInt32())
                throw new InvalidDataException("power_plant_presentation_dependency");
        foreach(var row in d.GetProperty("outputs").EnumerateArray())
            if(Read(root,"content/generated/power-plant-presentation/"+row.GetProperty("path").GetString()!,row.GetProperty("sha256").GetString()!).Length!=row.GetProperty("byteLength").GetInt32())
                throw new InvalidDataException("power_plant_presentation_output");
    }
}
