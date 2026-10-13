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

internal sealed record CampaignCatalogBinding(bool IsCurrent, string RulesVersion, string WildRulesVersion,
    string EncounterRulesVersion, PokemonCatalog? Pokemon, OwnedBattlePokemonCatalog? Ownership);

public sealed partial class CampaignSession
{
    private static CampaignCatalogBinding BindCurrentCatalog(ICampaignCatalog content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if(!content.CelioEnabled)throw new InvalidDataException("current_requires_celio_catalog");
        ValidateCampaignCatalog(content,current:true);
        var owned=content.CelioOwnership!;
        var battle=OwnedBattlePokemonCatalog.For(owned.Catalog);
        if(battle is null || !ReferenceEquals(battle.Catalog,content.PokemonContent!.Catalog) ||
            !ReferenceEquals(battle.Mechanics,owned.Mechanics) ||
            !ReferenceEquals(CelioOpponentPokemonCatalog.For(owned),content.CelioOpponents) ||
            ReferenceEquals(content.CelioOpponents!.Catalog,owned.Catalog))
            throw new InvalidDataException("current_battle_catalog_identity");
        return new(true,CurrentGameProfile.RulesVersion,WildBattleRules.FuchsiaRulesVersion,
            EarlyLandEncounterRules.FiveIslandRulesVersion,owned.Catalog,battle);
    }

    private static CampaignCatalogBinding BindDiagnosticCatalog(ICampaignCatalog content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateCampaignCatalog(content,current:false);
        var pokemon=content.PokemonContent?.Catalog;
        return new(false,DiagnosticRulesVersion(content),DiagnosticWildRulesVersion(content),
            DiagnosticEncounterRulesVersion(content),pokemon,OwnedBattlePokemonCatalog.For(pokemon));
    }

    // Source validation is shared by New Game and Restore, and remains independent of rendering.
    private static void ValidateCampaignCatalog(ICampaignCatalog content,bool current)
    {
        ValidateCelioCatalog(content);ValidateFiveIslandCatalog(content);ValidateSixIslandCatalog(content);ValidateFourIslandCatalog(content);ValidateSeviiRubyCatalog(content);ValidateViridianGymCatalog(content);
        if(content.CinnabarEnabled)
        {
            if(!content.SaffronGymEnabled||content.CinnabarOwnership is null||content.CinnabarOpponents is null||content.CinnabarTrainerParties is null||
                !ReferenceEquals(content.CinnabarOwnership,content.CinnabarOpponents.Owned))throw new InvalidDataException("cinnabar_source_profile_missing");
            CinnabarTrainerPartyFactory.ValidateCatalog(content.CinnabarTrainerParties);
        }
        if(content.SaffronGymEnabled)
        {
            if(!content.SilphEnabled||content.SaffronGymOpponents is null||content.SaffronGymTrainerParties is null||!ReferenceEquals(content.SilphOwnership,content.SaffronGymOpponents.Owned))throw new InvalidDataException("saffron_gym_source_profile_missing");
            SaffronGymTrainerPartyFactory.ValidateCatalog(content.SaffronGymTrainerParties);
        }
        if(content.SilphEnabled&&(!content.SurfEnabled||content.SilphOwnership is null||content.SilphOpponents is null||content.SilphTrainerParties is null))throw new InvalidDataException("silph_requires_authenticated_successor");
        if(content.PresetDoublesEnabled&&(!content.FuchsiaGymEnabled||content.PresetDoublePokemon is null||content.PresetDoubleItems is null||content.PresetDoubleMechanics is null||
            SourceTwinsTrainerEncounter.ValidateDefinition(content.Route8Twins) is not null))throw new InvalidDataException("preset_double_requires_authenticated_successor");
        if(content.FuchsiaGymEnabled)
        {
            if(!content.FuchsiaRouteEnabled||content.FuchsiaGymTrainerParties is null||content.PokemonContent?.Catalog.Moves.Count!=354)
                throw new InvalidDataException("fuchsia_gym_requires_source_successor_profile");
            FuchsiaGymTrainerPartyFactory.ValidateCatalog(content.FuchsiaGymTrainerParties);
            if(content.SafariEnabled)
            {
                if(current)
                {
                    if(!CelioMachineCatalog.IsAuthenticated(content.PewterMachines))throw new InvalidDataException("current_machine_proof_missing");
                    if(FiveIslandShopCatalog.For(content.EarlyShop) is null)throw new InvalidDataException("current_shop_proof_missing");
                }
                else ValidateDiagnosticMachineAndShop(content);
            }
            else
            {
                FuchsiaGymMachineRules.ValidateCatalog(content.PewterMachines??throw new InvalidDataException("fuchsia_gym_machines_missing"));
                FuchsiaGymItemRules.ValidateShop(content.EarlyShop??throw new InvalidDataException("fuchsia_gym_items_missing"));
            }
        }
        if(content.SnorlaxEnabled&&!content.PokemonTowerEnabled)throw new InvalidDataException("snorlax_requires_tower_profile");
        if(content.PokemonTowerEnabled&&!content.RocketHideoutEnabled)throw new InvalidDataException("tower_requires_rocket_profile");
        if(content.RocketHideoutEnabled&&!content.CeladonGymEnabled)throw new InvalidDataException("rocket_hideout_requires_gym_profile");
        if(content.CeladonGymEnabled&&!content.CeladonApproachEnabled)throw new InvalidDataException("celadon_gym_requires_approach_profile");
        if(content.CeladonApproachEnabled&&!content.RockTunnelEnabled)throw new InvalidDataException("celadon_approach_requires_rocktunnel_profile");
        if(content.RockTunnelEnabled&&!content.Route11Enabled)throw new InvalidDataException("rocktunnel_requires_route11_profile");
        if(content.Route11Enabled&&!content.CityServicesEnabled)throw new InvalidDataException("route11_requires_city_services_profile");
        if(content.CityServicesEnabled&&!content.VermilionGymEnabled)throw new InvalidDataException("city_services_requires_vermilion_gym_profile");
        if(content.VermilionGymEnabled&&!content.SsAnneEnabled)throw new InvalidDataException("vermilion_gym_requires_ssanne_profile");
        if(content.SsAnneEnabled&&!content.VermilionEnabled)throw new InvalidDataException("ssanne_requires_vermilion_profile");
        if(content.VermilionEnabled&&!content.BillEnabled)throw new InvalidDataException("vermilion_requires_bill_profile");
        if(content.BillEnabled&&!content.CeruleanEnabled)throw new InvalidDataException("bill_requires_cerulean_profile");
        if(content.CeruleanEnabled&&!content.MtMoonEnabled)throw new InvalidDataException("cerulean_requires_mtmoon_profile");
        if(content.MtMoonEnabled&&!content.MagikarpEnabled)throw new InvalidDataException("mtmoon_requires_magikarp_profile");
        if(content.MagikarpEnabled&&!content.Route3Enabled)throw new InvalidDataException("magikarp_requires_route3_profile");
        if(content.Route3Enabled&&!content.Route22Enabled)throw new InvalidDataException("route3_requires_route22_profile");
        if(content.Route22Enabled&&!content.OakRatingEnabled)throw new InvalidDataException("route22_requires_oak_profile");
        if(content.OakRatingEnabled&&!content.TownMapEnabled)throw new InvalidDataException("oak_rating_requires_town_map_profile");
        if(content.TownMapEnabled&&(!content.RepelEnabled||content.QuestItems?.Profile!=QuestItemRules.TownMapProfile))throw new InvalidDataException("town_map_requires_repel_profile");
        if(content.RepelEnabled&&(!content.PokemonStorageEnabled||content.FieldPickups.Length!=(content.FiveIslandEnabled?235:content.SixIslandEnabled?227:content.FourIslandEnabled?218:content.SeviiRubyEnabled?210:content.VictoryRoadEnabled?202:content.FirstSeviiEnabled?184:content.CinnabarEnabled?162:content.SilphEnabled?146:content.SafariEnabled?117:content.PokemonTowerEnabled?102:content.RocketHideoutEnabled?93:content.CeladonApproachEnabled?75:content.RockTunnelEnabled?62:content.Route11Enabled?47:content.SsAnneEnabled?41:content.BillEnabled?29:content.MtMoonEnabled?27:8)||content.EarlyShop is null))throw new InvalidDataException("repel_requires_field_pickup_profile");
        if(content.PokemonStorageEnabled&&!content.EarlyPartyEnabled)throw new InvalidDataException("storage_requires_early_party_profile");
    }
}
