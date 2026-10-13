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
    public const string RulesVersion = "firered-current-campaign-v1";
}

internal sealed record CampaignCatalogBinding(string RulesVersion, string EncounterRulesVersion, PokemonCatalog? Pokemon, OwnedBattlePokemonCatalog? Ownership);

public sealed partial class CampaignSession
{
    private static CampaignCatalogBinding BindCurrentCatalog(ICampaignCatalog content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if(!content.CelioEnabled)throw new InvalidDataException("current_requires_celio_catalog");
        ValidateCampaignCatalog(content);
        var owned=content.CelioOwnership!;
        var battle=OwnedBattlePokemonCatalog.For(owned.Catalog);
        if(battle is null || !ReferenceEquals(battle.Catalog,content.PokemonContent!.Catalog) ||
            !ReferenceEquals(battle.Mechanics,owned.Mechanics) ||
            !ReferenceEquals(CelioOpponentPokemonCatalog.For(owned),content.CelioOpponents) ||
            ReferenceEquals(content.CelioOpponents!.Catalog,owned.Catalog))
            throw new InvalidDataException("current_battle_catalog_identity");
        return new(CurrentGameProfile.RulesVersion,EarlyLandEncounterRules.FiveIslandRulesVersion,owned.Catalog,battle);
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
            content.SixIslandEnabled && content.FiveIslandEnabled && content.CelioEnabled) ||
            content.ConnectedFieldMaps is null || content.OpeningItems is null)
            throw new InvalidDataException("current_capabilities_missing");
        ValidateCelioCatalog(content);ValidateFiveIslandCatalog(content);ValidateSixIslandCatalog(content);ValidateFourIslandCatalog(content);ValidateSeviiRubyCatalog(content);ValidateViridianGymCatalog(content);
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
        if(!CelioMachineCatalog.IsAuthenticated(content.PewterMachines))throw new InvalidDataException("current_machine_proof_missing");
        if(FiveIslandShopCatalog.For(content.EarlyShop) is null)throw new InvalidDataException("current_shop_proof_missing");
        if(content.QuestItems?.Profile!=QuestItemRules.TownMapProfile)throw new InvalidDataException("current_quest_item_profile_missing");
        if(content.FieldPickups.Length!=235||content.EarlyShop is null)throw new InvalidDataException("current_field_pickup_profile_missing");
    }
}
