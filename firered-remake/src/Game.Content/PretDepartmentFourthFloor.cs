using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original 4F Retro Mail definition and full shelf provenance; Mail composition and stone effects stay separate.</summary>
public static class PretDepartmentFourthFloor
{
    public const string Base = "content/generated/data/services/department-fourth-floor/";
    private static byte[] Read(string root,string relative)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        return bytes;
    }
    public static NpcTradeItemsDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root,Base + "items.json"),
            (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions())
            ?? throw new InvalidDataException("department_fourth_floor_document_missing");
        if (            doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != ItemIds.RetroMailId ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != ItemIds.RetroMailId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("department_fourth_floor_scope");
        return doc;
    }

    public static JsonElement LoadSourceContract(string root) =>
        JsonSerializer.Deserialize<JsonElement>(Read(root,Base + "source-contract.json"));
}
