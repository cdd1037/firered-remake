using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCeruleanItemsDocument(int SchemaVersion, PretMtMoonItem Item, EarlyShopStock Shop, PretPewterMachineIcon Icon);

/// <summary>Authenticates original source, the generated data and palette-derived Super Potion art.</summary>
public static class PretCeruleanItems
{
    public const string Profile = "firered-cerulean-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretCeruleanItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options = machines.CreateJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretCeruleanItemsDocument>(json, options);
        Require(document.SchemaVersion == 1,
            "Cerulean item scope differs.");
        CeruleanItemRules.ValidateSuperPotion(Definition(document.Item));
        CeruleanItemRules.ValidateStock(document.Shop);
        Require(document.Icon.Item == CeruleanItemRules.SuperPotionId, "Cerulean item icon identity differs.");
        PretPewterMachines.ValidateIcon(document.Icon);
        return document;
    }

    public static PretCeruleanItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/data/items/cerulean/cerulean-items.json")), (shared ?? SharedMachineCatalog.LoadProject(root)));
        return document;
    }

    private static EarlyShopItemDefinition Definition(PretMtMoonItem value)
    {
        var item = value.Metadata.Item;
        return new(item, value.Metadata.Description, 50, 0, value.EffectBytes, value.UseSupport);
    }
    private static byte[] Read(string root,string path)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Cerulean item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Cerulean item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Cerulean item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Cerulean item symlink rejected.");
        }
        return result;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
