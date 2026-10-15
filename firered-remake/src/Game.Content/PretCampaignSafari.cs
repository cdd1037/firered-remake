using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public const string SafariContentProfile = "firered-safari-campaign-v1";
    public PretSafariRegion? SafariRegion { get; }
    public PretSafariPokemon? SafariPokemon { get; }
    public PretSafariArt? SafariArt { get; }
    public PretSafariItems? SafariItems { get; }
    public PretSafariEncounters? SafariEncounters { get; }
    public bool SafariEnabled => SafariRegion is not null;



    private static IEnumerable<FieldPickupDefinition> SafariPickupDefinitions(PretSafariRegion region,EarlyShopCatalog shop,PewterMachineCatalog machines,PretSafariItems items,PretStoryCompilation scripts)
    {
        foreach (var row in region.Contracts.GetProperty("pickups").EnumerateArray())
        {
            string symbol = row.GetProperty("item").GetString()!;
            var id = new ItemId("pret:" + symbol);
            var ordinary = shop.Items.SingleOrDefault(i => i.Id == id);
            var source = PewterMachineRules.Definition(machines, id)?.Machine.Item ??
                items.Items.SingleOrDefault(i => i.Metadata.Item.Id == id)?.Metadata.Item;
            int number = ordinary?.SourceItemId ?? source?.SourceItemId ?? throw new InvalidDataException("safari_pickup_item_missing: " + symbol);
            string name = ordinary?.Name ?? source!.Name,pocket = ordinary?.Pocket ?? source!.Pocket;
            var map = region.SourceMaps[new(row.GetProperty("map").GetString()!)];
            int local = row.GetProperty("localId").GetInt32(),x = row.GetProperty("x").GetInt32(),y = row.GetProperty("y").GetInt32();
            var actor = map.Events.GetProperty("object_events").EnumerateArray().ElementAt(local - 1);
            string flag = row.GetProperty("flag").GetString()!,label = row.GetProperty("label").GetString()!;
            if (actor.GetProperty("script").GetString() != label || actor.GetProperty("flag").GetString() != flag ||
                actor.GetProperty("x").GetInt32() != x || actor.GetProperty("y").GetInt32() != y ||
                !scripts.TryConstant(flag, out int sourceFlag)) throw new InvalidDataException("safari_pickup_source_mismatch");
            yield return new("safari:" + flag, map.SourceMapSymbol, false, local, x, y,
                actor.GetProperty("elevation").GetInt32(), label, symbol, number, id, name, pocket, 1, flag, sourceFlag);
        }
        foreach (var row in region.Contracts.GetProperty("hiddenItems").EnumerateArray())
        {
            var declaration = row.GetProperty("declaration");
            var id = new ItemId("pret:" + declaration.GetProperty("item").GetString()!);
            var item = shop.Items.Single(i => i.Id == id);
            var map = region.SourceMaps[new(row.GetProperty("map").GetString()!)];
            string flag = declaration.GetProperty("flag").GetString()!;
            if (!scripts.TryConstant(flag, out int sourceFlag) || declaration.GetProperty("underfoot").GetBoolean())
                throw new InvalidDataException("safari_hidden_pickup_source");
            yield return new("safari:" + flag, map.SourceMapSymbol, true, null,
                declaration.GetProperty("x").GetInt32(), declaration.GetProperty("y").GetInt32(),
                declaration.GetProperty("elevation").GetInt32(), "EventScript_HiddenItemScript", item.SourceItemSymbol,
                item.SourceItemId, item.Id, item.Name, item.Pocket, declaration.GetProperty("quantity").GetInt32(), flag, sourceFlag);
        }
    }
}
