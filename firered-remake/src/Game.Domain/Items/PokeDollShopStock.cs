using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

/// <summary>The complete original 4F shelf; retains its public Doll-era API for Copycat compatibility.</summary>
public sealed record PokeDollShopStock : AuthenticatedShopStock
{
    public const string ExpectedMapId = "map.celadon-city-department-store-4f";
    public const string ExpectedStockId = "CeladonCity_DepartmentStore_4F_Items";
    public const string ExpectedClerkScript = "CeladonCity_DepartmentStore_4F_EventScript_Clerk";
    public const string ExpectedSourcePath = "data/maps/CeladonCity_DepartmentStore_4F/scripts.inc";
    public override string Id => ExpectedStockId;
    private static readonly ImmutableArray<DepartmentStoreOrdinaryItem> Entries = [
        new(new("pret:ITEM_POKE_DOLL"), 80, "POKé DOLL", 1000, "POCKET_ITEMS"),
        new(new("pret:ITEM_RETRO_MAIL"), 132, "RETRO MAIL", 50, "POCKET_ITEMS"),
        new(new("pret:ITEM_FIRE_STONE"), 95, "FIRE STONE", 2100, "POCKET_ITEMS"),
        new(new("pret:ITEM_THUNDER_STONE"), 96, "THUNDERSTONE", 2100, "POCKET_ITEMS"),
        new(new("pret:ITEM_WATER_STONE"), 97, "WATER STONE", 2100, "POCKET_ITEMS"),
        new(new("pret:ITEM_LEAF_STONE"), 98, "LEAF STONE", 2100, "POCKET_ITEMS")
    ];
    public override ImmutableArray<ItemId> Items => Entries.Select(i => i.Id).ToImmutableArray();
    public bool IsSourceActor(FieldActorDefinition actor) => actor.SourceLocalId == 3 &&
        actor.ScriptId == ExpectedClerkScript && actor.GraphicsId == "OBJ_EVENT_GFX_CLERK" &&
        actor.X == 3 && actor.Y == 13 && actor.Elevation == 0 && actor.MovementType == "MOVEMENT_TYPE_FACE_UP" &&
        actor.HideFlag == "0" && !actor.IsClone && actor.TrainerType == "TRAINER_TYPE_NONE";
    public override void Validate(EarlyShopCatalog catalog)
    {
        EarlyShopInventoryRules.ValidateCatalog(catalog);
        if (!catalog.Items.Any(i => i.Id == ItemIds.RetroMailId))
            throw new NotSupportedException("fourth_floor_retro_mail_not_admitted");
        foreach (var entry in Entries)
        {
            var item = EarlyShopInventoryRules.Find(catalog, entry.Id);
            if (item is null || item.SourceItemId != entry.SourceItemId || item.Price != entry.Price ||
                item.Pocket != entry.Pocket || item.Importance != 0 || item.Name != entry.Name ||
                item.SourceItemSymbol != entry.Id.Value[5..])
                throw new NotSupportedException("fourth_floor_source_shelf");
        }
    }
}
