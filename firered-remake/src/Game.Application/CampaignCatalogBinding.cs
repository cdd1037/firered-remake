using Game.Domain.Encounters;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

/// <summary>One maintained game. The payload schema stays at 1; the rules ID is a strict new save boundary.</summary>
public static class CurrentGameProfile
{
    public const string Id = "current";
    public const string RulesVersion = "firered-current-campaign-v2";
}

internal sealed record CampaignCatalogBinding(string RulesVersion, string EncounterRulesVersion, PokemonCatalog Pokemon, GamePokemonCatalog Ownership);

public sealed partial class CampaignSession
{
    private static CampaignCatalogBinding BindCurrentCatalog(ICampaignCatalog content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if(!content.LeagueRematchEnabled)throw new InvalidDataException("current_requires_league_rematch_catalog");
        ValidateCampaignCatalog(content);
        var battle = content.PokemonContent!.Pokemon;
        if (content.MemorialPillarTrainers is { } memorialTrainers)
            MemorialPillarTrainerPartyFactory.ValidateCatalog(memorialTrainers);
        if (content.LostCaveEnabled && content.LostCaveTrainers is { } lostCaveTrainers)
            LostCaveTrainerPartyFactory.ValidateCatalog(lostCaveTrainers);
        return new(CurrentGameProfile.RulesVersion, EarlyLandEncounterRules.FiveIslandRulesVersion, battle.Catalog, battle);
    }

    // Source validation is shared by New Game and Restore, and remains independent of rendering.
    private static void ValidateCampaignCatalog(ICampaignCatalog content)
    {
        // All currently shipped capabilities are mandatory; there is no predecessor runtime.
        if(!(content.CaptureEnabled && content.ViridianCenterEnabled && content.OldManTutorialEnabled &&
            content.PartyTrainingEnabled && content.ForestEnabled && content.ForestTrainersEnabled &&
            content.PewterEnabled && content.EarlyStarterEnabled && content.EarlyPartyEnabled &&
            content.PokemonStorageEnabled && content.RepelEnabled && content.TownMapEnabled &&
            content.OakRatingEnabled && content.Route22Enabled && content.Route3Enabled &&
            content.MagikarpEnabled && content.MtMoonEnabled && content.CeruleanEnabled && content.BillEnabled &&
            content.VermilionEnabled && content.SsAnneEnabled && content.VermilionGymEnabled &&
            content.CityServicesEnabled && content.Route11Enabled && content.RockTunnelEnabled &&
            content.CeladonApproachEnabled && content.CeladonGymEnabled && content.RocketHideoutEnabled &&
            content.PokemonTowerEnabled && content.SnorlaxEnabled && content.RegionalFlavorEnabled &&
            content.RegionalServicesEnabled && content.RegionalInteriorServicesEnabled &&
            content.FuchsiaRouteEnabled && content.FuchsiaGymEnabled && content.PresetDoublesEnabled &&
            content.SafariEnabled && content.SurfEnabled && content.SilphEnabled && content.SaffronGymEnabled &&
            content.CinnabarEnabled && content.FirstSeviiEnabled && content.ViridianGymEnabled &&
            content.LeagueRivalEnabled && content.VictoryRoadEnabled && content.PokemonLeagueEnabled &&
            content.NationalDexEnabled && content.SeviiRubyEnabled && content.FourIslandEnabled &&
            content.SixIslandEnabled && content.FiveIslandEnabled && content.CelioEnabled && content.CeruleanCaveEnabled && content.LeagueRematchEnabled && content.SevenIslandEnabled && content.EventIslandsEnabled && content.PowerPlantEnabled && content.TrainerTowerEnabled && content.FightingDojoEnabled && content.FossilLabEnabled && content.Route5DayCareEnabled && content.FourIslandDayCareEnabled && content.TrainerFanClubEnabled) ||
            content.ConnectedFieldMaps is null || content.OpeningItems is null)
            throw new InvalidDataException("current_capabilities_missing");
        if (content.BreedingMetadata is not {} metadata || !ReferenceEquals(metadata, content.PokemonContent?.Catalog.BreedingData)) throw new InvalidDataException("current_breeding_metadata_missing");
        if(content.NpcTrades is null||!content.NpcTrades.Definitions.OrderBy(p=>p.Key).SequenceEqual(content.PokemonContent!.Catalog.NpcTrades.OrderBy(p=>p.Key)))throw new InvalidDataException("current_npc_trade_proof_missing");
        if (content.Route16FlyTrainers is {} route16) Route16FlyTrainerPartyFactory.ValidateCatalog(route16);
        ValidateSeafoamEncounterCatalog(content);ValidateOldAmberCatalog(content);ValidateFossilLabCatalog(content);ValidateFightingDojoCatalog(content);ValidatePowerPlantCatalog(content);ValidateEventIslandCatalog(content);ValidateSevenIslandCatalog(content);ValidateCeruleanCaveCatalog(content);ValidateCelioCatalog(content);ValidateFiveIslandCatalog(content);ValidateSixIslandCatalog(content);ValidateFourIslandCatalog(content);ValidateSeviiRubyCatalog(content);ValidateViridianGymCatalog(content);
        if(content.CinnabarOwnership is null||content.CinnabarOpponents is null||content.CinnabarTrainerParties is null||
            !ReferenceEquals(content.CinnabarOwnership,content.CinnabarOpponents.Owned))
            throw new InvalidDataException("cinnabar_source_profile_missing");
        CinnabarTrainerPartyFactory.ValidateCatalog(content.CinnabarTrainerParties);
        if(content.SaffronGymOpponents is null||content.SaffronGymTrainerParties is null||
            !ReferenceEquals(content.SilphOwnership,content.SaffronGymOpponents.Owned))
            throw new InvalidDataException("saffron_gym_source_profile_missing");
        SaffronGymTrainerPartyFactory.ValidateCatalog(content.SaffronGymTrainerParties);
        if(content.SilphOwnership is null||content.SilphOpponents is null||content.SilphTrainerParties is null)
            throw new InvalidDataException("silph_requires_authenticated_successor");
        if(content.PresetDoublePokemon is null||content.PresetDoubleItems is null||content.PresetDoubleMechanics is null||
            SourceTwinsTrainerEncounter.ValidateDefinition(content.Route8Twins) is not null)
            throw new InvalidDataException("preset_double_requires_authenticated_successor");
        if(content.FuchsiaGymTrainerParties is null||content.PokemonContent?.Catalog.Moves.Count!=354)
            throw new InvalidDataException("fuchsia_gym_requires_source_successor_profile");
        FuchsiaGymTrainerPartyFactory.ValidateCatalog(content.FuchsiaGymTrainerParties);
        if (content.GameCornerPrizes is not {} prizes || !ReferenceEquals(prizes.Pokemon, content.PokemonContent?.Catalog) ||
            !prizes.Items.Items.SequenceEqual(content.EarlyShop!.Items) || !ReferenceEquals(prizes.Machines, content.PewterMachines))
            throw new InvalidDataException("current_prize_catalog_identity");
        if (!content.SuperRodEnabled || content.PewterMachines is not { } machines)
            throw new InvalidDataException("current_machine_catalog_missing");
        MachineRules.ValidateCatalog(machines);
        new DepartmentStoreMachineStock(machines).Validate();
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
