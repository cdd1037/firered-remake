using System.Collections.Frozen;
using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

/// <summary>One maintained game; save schema and gameplay rules have distinct compatibility boundaries.</summary>
public static class CurrentGameProfile
{
    public const string Id = "current";
    public const string RulesVersion = "firered-current-campaign-v4";
}

internal sealed record CampaignCatalogBinding(string RulesVersion, string EncounterRulesVersion,
    PokemonCatalog Pokemon, GamePokemonCatalog Ownership, OrdinaryLandEncounterCatalog OrdinaryLand,
    FrozenSet<ItemId> HoldableItems, ImmutableDictionary<SpeciesId, SourceBreedingSpecies> BreedingSpecies,
    ImmutableDictionary<TrainerTowerChallengeKind, CampaignTrainerTowerMaps> TrainerTowerMaps);

internal sealed record CampaignTrainerTowerMaps(IReadOnlyDictionary<MapId, IFieldMap> Raw,
    IReadOnlyDictionary<MapId, ConnectedFieldMap> Connected);

public sealed partial class CampaignSession
{
    private static CampaignCatalogBinding BindCurrentCatalog(ICampaignCatalog content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateCampaignCatalog(content);
        var battle = content.GamePokemon;
        if (content.LandEncounters is null || content.LandEncounters.Count == 0)
            throw new InvalidDataException("current_land_encounters_missing");
        OrdinaryLandEncounterCatalog ordinaryLand;
        try { ordinaryLand = new(content.LandEncounters, content.Maps, battle.Catalog); }
        catch (ArgumentException error) { throw new InvalidDataException("current_land_encounter_reference", error); }
        if (content.CyclingRoadTrainers is { } cyclingRoadTrainers)
            CyclingRoadTrainerPartyFactory.ValidateCatalog(cyclingRoadTrainers);
        if (content.MemorialPillarTrainers is { } memorialTrainers)
            MemorialPillarTrainerPartyFactory.ValidateCatalog(memorialTrainers);
        if (content.LostCaveTrainers is { } lostCaveTrainers)
            LostCaveTrainerPartyFactory.ValidateCatalog(lostCaveTrainers);
        var water = content.WaterEncounters ?? throw new InvalidDataException("current_water_encounters_missing");
        var fishing = content.FishingTables ?? throw new InvalidDataException("current_fishing_encounters_missing");
        // Acquirable held items must participate in the same inventory/transfer rules as bag items.
        // Check this closure once at composition, using actual encounters rather than all species.
        var wildSpecies = content.LandEncounters.Values.SelectMany(table => table.Slots.Select(slot => slot.Species))
            .Concat(water.Values.SelectMany(table => table.Slots.Select(slot => slot.Species)))
            .Concat(fishing.Tables.Values.SelectMany(table => table.Slots.Select(slot => slot.Species)))
            .Distinct().Where(battle.Catalog.Species.ContainsKey);
        var sourceHeldItems = wildSpecies.Select(species => battle.Mechanics.SpeciesMechanics[species])
            .SelectMany(species => new[] { species.ItemCommon, species.ItemRare })
            .Concat(VermilionPickupRules.Items).Where(item => item != "ITEM_NONE").Distinct(StringComparer.Ordinal);
        var holdableItems = HeldItemTransferRules.BuildHoldableItems(content.EarlyShop!, content.Machines);
        if (sourceHeldItems.Any(item => !holdableItems.Contains(new("pret:" + item))))
            throw new InvalidDataException("current_held_item_inventory_scope_missing");
        return new(CurrentGameProfile.RulesVersion, EarlyLandEncounterRules.FiveIslandRulesVersion,
            battle.Catalog, battle, ordinaryLand, holdableItems,
            content.BreedingMetadata!.Species.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Breeding),
            BindTrainerTowerMaps(content));
    }

    // Source validation is shared by New Game and Restore, and remains independent of rendering.
    private static void ValidateCampaignCatalog(ICampaignCatalog content)
    {
        if (content.GamePokemon is not {} pokemon || content.PokemonContent is not {} presentation ||
            !ReferenceEquals(pokemon, presentation.Pokemon) ||
            !ReferenceEquals(content.OpponentPokemon, pokemon.Opponents) ||
            !ReferenceEquals(content.OpponentPokemon.Owned, pokemon) || content.Machines is null)
            throw new InvalidDataException("current_pokemon_catalog_binding");
        if(content.ConnectedFieldMaps is null || content.OpeningItems is null)
            throw new InvalidDataException("current_capabilities_missing");
        // Validate immutable Safari terrain once; command-local walking views only
        // materialize maps that the current movement actually reads.
        foreach (var map in content.ConnectedFieldMaps.Values.Where(map => SafariFieldMovementRules.IsSafariArea(map.Id)))
            _ = SafariFieldMovementRules.ForConnectedMovement(map, EarlyLandEncounterRules.SafariRulesVersion);
        if (content.BreedingMetadata is not {} metadata || !ReferenceEquals(metadata, content.PokemonContent?.Catalog.BreedingData)) throw new InvalidDataException("current_breeding_metadata_missing");
        if(content.NpcTrades is null||!content.NpcTrades.Definitions.OrderBy(p=>p.Key).SequenceEqual(content.PokemonContent!.Catalog.NpcTrades.OrderBy(p=>p.Key)))throw new InvalidDataException("current_npc_trade_proof_missing");
        if (content.Route16FlyTrainers is {} route16) Route16FlyTrainerPartyFactory.ValidateCatalog(route16);
        ValidateSourceScriptedCatalog(content);ValidateSeafoamEncounterCatalog(content);ValidateOldAmberCatalog(content);ValidateFossilLabCatalog(content);ValidateFightingDojoCatalog(content);ValidatePowerPlantCatalog(content);ValidateEventIslandCatalog(content);ValidateSevenIslandCatalog(content);ValidateCeruleanCaveCatalog(content);ValidateCelioCatalog(content);ValidateFiveIslandCatalog(content);ValidateSixIslandCatalog(content);ValidateFourIslandCatalog(content);ValidateSeviiRubyCatalog(content);ValidateViridianGymCatalog(content);
        if(content.GamePokemon is null||content.OpponentPokemon is null||content.CinnabarTrainerParties is null||
            !ReferenceEquals(content.GamePokemon,content.OpponentPokemon.Owned))
            throw new InvalidDataException("cinnabar_source_profile_missing");
        CinnabarTrainerPartyFactory.ValidateCatalog(content.CinnabarTrainerParties);
        if(content.OpponentPokemon is null||content.SaffronGymTrainerParties is null||
            !ReferenceEquals(content.GamePokemon,content.OpponentPokemon.Owned))
            throw new InvalidDataException("saffron_gym_source_profile_missing");
        SaffronGymTrainerPartyFactory.ValidateCatalog(content.SaffronGymTrainerParties);
        if(content.GamePokemon is null||content.OpponentPokemon is null||content.SilphTrainerParties is null)
            throw new InvalidDataException("silph_requires_authenticated_successor");
        if(content.PresetDoublePokemon is null||content.PresetDoubleItems is null||content.PresetDoubleMechanics is null||
            SourceTwinsTrainerEncounter.ValidateDefinition(content.Route8Twins) is not null)
            throw new InvalidDataException("preset_double_requires_authenticated_successor");
        if(content.FuchsiaGymTrainerParties is null)
            throw new InvalidDataException("fuchsia_gym_requires_source_successor_profile");
        FuchsiaGymTrainerPartyFactory.ValidateCatalog(content.FuchsiaGymTrainerParties);
        if (content.GameCornerPrizes is not {} prizes || !ReferenceEquals(prizes.Pokemon, content.PokemonContent?.Catalog) ||
            !prizes.Items.Items.SequenceEqual(content.EarlyShop!.Items) || !ReferenceEquals(prizes.Machines, content.Machines))
            throw new InvalidDataException("current_prize_catalog_identity");
        if (content.Machines is not { } machines)
            throw new InvalidDataException("current_machine_catalog_missing");
        MachineRules.ValidateCatalog(machines);
        EarlyShopInventoryRules.ValidateCatalog(content.EarlyShop!);
        if (content.EarlyShop!.Inventory is null) throw new InvalidDataException("current_inventory_scope_missing");
        if (content.QuestItems is not { } questItems)
            throw new InvalidDataException("current_quest_items_missing");
        QuestItemRules.ValidateCatalog(questItems);
        if (!questItems.Items.Any(item => item.Id == QuestItemRules.TeachyTvId) ||
            !questItems.Items.Any(item => item.Id == QuestItemRules.TownMapId))
            throw new InvalidDataException("current_quest_items_missing");
        if(content.FieldPickups.IsDefaultOrEmpty||content.EarlyShop is null)throw new InvalidDataException("current_field_pickup_profile_missing");
    }
}
