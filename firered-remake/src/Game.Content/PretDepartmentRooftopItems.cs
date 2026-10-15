using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record DepartmentRooftopIconAlias(ItemId Item, ItemId ReusedItem, OpeningItemSourceRef Source, OpeningItemSourceRef ReusedSource);
public sealed record DepartmentRooftopMachinesDocument(string SourceCommit, string Profile, int PreviousMachineCount, int MachineCount, int SpeciesCount,
    ImmutableArray<MachineDefinition> Additions, ImmutableArray<DepartmentRooftopIconAlias> IconAliases);

/// <summary>One missing drink and three reward machines; all existing items, owners and icons remain shared.</summary>
public static class PretDepartmentRooftopItems
{
    public const string Base = "content/generated/department-rooftop-items/";
    public const string Profile = "firered-department-rooftop-items-v1";
    private static byte[] Read(string root,string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("department_rooftop_source_path");
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        return bytes;
    }
    private static T Deserialize<T>(string root, byte[] data, SharedMachineCatalog? shared = null) => JsonSerializer.Deserialize<T>(data,
        (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })) ??
        throw new InvalidDataException("department_rooftop_document_missing");
    public static NpcTradeItemsDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
        var doc = Deserialize<NpcTradeItemsDocument>(root, Read(root,Base + "items.json"), shared);
        if (            doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != ItemIds.LemonadeId ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != ItemIds.LemonadeId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("department_rooftop_item_scope");
        return doc;
    }
    public static DepartmentRooftopMachinesDocument LoadMachinesDocument(string root, SharedMachineCatalog? shared = null)
    {
        var doc = Deserialize<DepartmentRooftopMachinesDocument>(root, Read(root,Base + "machines.json"), shared);
        if (doc.Additions.IsDefault || doc.Additions.Length != 3 ||
            doc.IconAliases.IsDefault || doc.IconAliases.Length != 3 ||
            !doc.IconAliases.Select(a => (a.Item.Value, a.ReusedItem.Value)).SequenceEqual(new[] {
                ("pret:ITEM_TM16", "pret:ITEM_TM04"),("pret:ITEM_TM20", "pret:ITEM_TM05"),("pret:ITEM_TM33", "pret:ITEM_TM04") }))
            throw new InvalidDataException("department_rooftop_machine_scope");
        return doc;
    }

    public static JsonElement LoadSourceContract(string root, SharedMachineCatalog? shared = null) =>
        Deserialize<JsonElement>(root, Read(root,Base + "source-contract.json"), shared);
}
