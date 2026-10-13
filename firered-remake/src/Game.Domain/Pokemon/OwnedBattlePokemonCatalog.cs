using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Pokemon;

/// <summary>Battle adapter for exact authenticated campaign ownership roots. This sealed
/// bridge shares execution without widening Safari110 or the historical107 trainer factories.</summary>
public sealed class OwnedBattlePokemonCatalog
{
    private static readonly ConditionalWeakTable<PokemonCatalog, OwnedBattlePokemonCatalog> Authenticated = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, ConcurrentDictionary<string, OwnedBattlePokemonCatalog>> MechanicsOwners = new();
    private static readonly ConcurrentDictionary<string, ImmutableHashSet<SpeciesId>> SuccessorSpecies = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ImmutableHashSet<SpeciesId>> SuccessorOpponentSpecies = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> SuccessorParentProfiles = new(StringComparer.Ordinal);
    private OwnedBattlePokemonCatalog? CampaignParent { get; init; }
    private PokemonCatalog? SuccessorOpponents { get; set; }
    private string? SuccessorOpponentProfile { get; set; }
    public PokemonCatalog Catalog { get; }
    public PokemonCatalog Predecessor { get; }
    public SourceMechanicsCatalog Mechanics { get; }
    public string RulesVersion { get; }

    private OwnedBattlePokemonCatalog(PokemonCatalog catalog, PokemonCatalog predecessor,
        SourceMechanicsCatalog mechanics, string rulesVersion)
    { Catalog = catalog; Predecessor = predecessor; Mechanics = mechanics; RulesVersion = rulesVersion; }

    public static OwnedBattlePokemonCatalog? For(PokemonCatalog? catalog)
    {
        if (catalog is null) return null;
        if (Authenticated.TryGetValue(catalog, out var cached)) return cached;
        OwnedBattlePokemonCatalog proof;
        if (SafariOwnedPokemonCatalog.For(catalog) is { } safari)
            proof = new(catalog, safari.Predecessor, safari.Mechanics, SafariOwnedPokemonCatalog.Profile);
        else if (SurfOwnedPokemonCatalog.For(catalog) is { } surf)
            proof = new(catalog, surf.Predecessor.Predecessor, surf.Mechanics, SurfOwnedPokemonCatalog.Profile);
        else if (SilphOwnedPokemonCatalog.For(catalog) is { } silph)
            proof = new(catalog, silph.Predecessor.Predecessor.Predecessor, silph.Mechanics, SilphOwnedPokemonCatalog.Profile);
        else if (CinnabarOwnedPokemonCatalog.For(catalog) is { } cinnabar)
            proof = new(catalog, cinnabar.Predecessor.Predecessor.Predecessor.Predecessor, cinnabar.Mechanics, CinnabarOwnedPokemonCatalog.Profile);
        else if (PowerPlantOwnedPokemonCatalog.For(catalog) is { } plant)
            proof = new(catalog, For(plant.Predecessor.Catalog)!.Predecessor, plant.Mechanics, PowerPlantOwnedPokemonCatalog.Profile);
        else if (EventIslandOwnedPokemonCatalog.For(catalog) is { } events)
            proof = new(catalog, For(events.Predecessor.Catalog)!.Predecessor, events.Mechanics, EventIslandOwnedPokemonCatalog.Profile);
        else if (SevenIslandOwnedPokemonCatalog.For(catalog) is { } seven)
            proof = new(catalog, For(seven.Predecessor.Catalog)!.Predecessor, seven.Mechanics, SevenIslandOwnedPokemonCatalog.Profile);
        else if (CeruleanCaveOwnedPokemonCatalog.For(catalog) is { } cave)
            proof = new(catalog, For(cave.Predecessor.Catalog)!.Predecessor, cave.Mechanics, CeruleanCaveOwnedPokemonCatalog.Profile);
        else if (CelioOwnedPokemonCatalog.For(catalog) is { } celio)
            proof = new(catalog, For(celio.Predecessor.Catalog)!.Predecessor, celio.Mechanics, CelioOwnedPokemonCatalog.Profile);
        else if (FiveIslandOwnedPokemonCatalog.For(catalog) is { } five)
            proof = new(catalog, For(five.Predecessor.Catalog)!.Predecessor, five.Mechanics, FiveIslandOwnedPokemonCatalog.Profile);
        else if (SixIslandOwnedPokemonCatalog.For(catalog) is { } six)
            proof = new(catalog, For(six.Predecessor.Catalog)!.Predecessor, six.Mechanics, SixIslandOwnedPokemonCatalog.Profile);
        else if (FourIslandOwnedPokemonCatalog.For(catalog) is { } four)
            proof = new(catalog, For(four.Predecessor.Catalog)!.Predecessor, four.Mechanics, FourIslandOwnedPokemonCatalog.Profile);
        else if (SeviiRubyOwnedPokemonCatalog.For(catalog) is { } ruby)
            proof = new(catalog, For(ruby.Predecessor.Catalog)!.Predecessor, ruby.Mechanics, SeviiRubyOwnedPokemonCatalog.Profile);
        else return null;
        proof = Authenticated.GetValue(catalog, _ => proof);
        MechanicsOwners.GetValue(proof.Mechanics.Catalog, _ => new())[proof.RulesVersion] = proof;
        return proof;
    }

    // A bounded source owner authenticates its new definitions before registration. Shared execution
    // follows this ownership link; subsequent additions do not need a new regional profile ladder.
    internal static void RegisterSuccessor(PokemonCatalog catalog, OwnedBattlePokemonCatalog predecessor, string profile)
    {
        if (!ReferenceEquals(For(predecessor.Catalog), predecessor) || catalog.SourceCommit != predecessor.Catalog.SourceCommit ||
            !ReferenceEquals(catalog.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !catalog.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)))
            throw new ArgumentException("owned_successor_predecessor");
        var proof = new OwnedBattlePokemonCatalog(catalog, predecessor.Predecessor, predecessor.Mechanics, profile) { CampaignParent = predecessor };
        Authenticated.Add(catalog, proof);
        SuccessorSpecies[profile] = catalog.Species.Keys.ToImmutableHashSet();
        MechanicsOwners.GetValue(proof.Mechanics.Catalog, _ => new())[profile] = proof;
    }

    internal static void BindSuccessorOpponents(OwnedBattlePokemonCatalog owner, PokemonCatalog opponents, string profile)
    {
        if (!ReferenceEquals(For(owner.Catalog), owner) || owner.CampaignParent is null ||
            !ReferenceEquals(opponents.Moves, owner.Catalog.Moves) || !owner.Catalog.Species.Keys.All(opponents.Species.ContainsKey))
            throw new ArgumentException("owned_successor_opponents");
        owner.SuccessorOpponents = opponents; owner.SuccessorOpponentProfile = profile;
        SuccessorSpecies[profile] = owner.Catalog.Species.Keys.ToImmutableHashSet();
        SuccessorOpponentSpecies[profile] = opponents.Species.Keys.ToImmutableHashSet();
        SuccessorParentProfiles[profile] = owner.CampaignParent.TrainerOpponentProfile;
        MechanicsOwners.GetValue(owner.Mechanics.Catalog, _ => new())[profile] = owner;
    }

    internal static OwnedBattlePokemonCatalog ResolveCurrent(OwnedBattlePokemonCatalog predecessor, OwnedBattlePokemonCatalog? current)
    {
        current ??= predecessor;
        if (!ReferenceEquals(For(predecessor.Catalog), predecessor) || !ReferenceEquals(For(current.Catalog), current) ||
            !ReferenceEquals(current.Mechanics, predecessor.Mechanics) ||
            !ReferenceEquals(current, predecessor) && !IsCampaignSuccessorOf(predecessor, current))
            throw new ArgumentException("owned_successor_current");
        return current;
    }

    internal static bool RegisteredProfileAdmitsTrainerOpponent(string profile, SpeciesId species) =>
        SuccessorOpponentSpecies.TryGetValue(profile, out var admitted) && admitted.Contains(species);

    internal static string? InheritedTrainerProfile(string? profile)
    {
        while (profile is not null && SuccessorParentProfiles.TryGetValue(profile, out var predecessor)) profile = predecessor;
        return profile;
    }

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(SafariOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;
    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(SurfOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(SilphOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(CinnabarOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(SeviiRubyOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(FourIslandOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(SixIslandOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(FiveIslandOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(CelioOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(CeruleanCaveOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(SevenIslandOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(EventIslandOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    [return: NotNullIfNotNull(nameof(owned))]
    public static implicit operator OwnedBattlePokemonCatalog?(PowerPlantOwnedPokemonCatalog? owned) =>
        owned is null ? null : For(owned.Catalog)!;

    internal string TrainerOpponentProfile => SuccessorOpponentProfile ?? (RulesVersion == PowerPlantOwnedPokemonCatalog.Profile
        ? PowerPlantOpponentPokemonCatalog.Profile : RulesVersion == EventIslandOwnedPokemonCatalog.Profile
        ? EventIslandOpponentPokemonCatalog.Profile : RulesVersion == SevenIslandOwnedPokemonCatalog.Profile
        ? SevenIslandOpponentPokemonCatalog.Profile : RulesVersion == CeruleanCaveOwnedPokemonCatalog.Profile
        ? (CeruleanCaveOwnedPokemonCatalog.For(Catalog) is { } cave && LeagueRematchOpponentPokemonCatalog.For(cave) is not null
            ? LeagueRematchOpponentPokemonCatalog.Profile : CeruleanCaveOpponentPokemonCatalog.Profile) : RulesVersion == CelioOwnedPokemonCatalog.Profile
        ? CelioOpponentPokemonCatalog.Profile : RulesVersion == FiveIslandOwnedPokemonCatalog.Profile
        ? FiveIslandOpponentPokemonCatalog.Profile : RulesVersion == SixIslandOwnedPokemonCatalog.Profile
        ? SixIslandOpponentPokemonCatalog.Profile : RulesVersion == FourIslandOwnedPokemonCatalog.Profile
        ? FourIslandOpponentPokemonCatalog.Profile : SeviiRubyOpponentPokemonCatalog.Profile);

    internal static bool IsCampaignSuccessorOf(OwnedBattlePokemonCatalog predecessor, OwnedBattlePokemonCatalog successor) =>
        ReferenceEquals(For(successor.Catalog), successor) && successor.CampaignParent is { } parent &&
        successor.SuccessorOpponents is not null && ReferenceEquals(successor.Mechanics, predecessor.Mechanics) &&
        (ReferenceEquals(parent, predecessor) || IsCampaignSuccessorOf(predecessor, parent)) ||
        ReferenceEquals(For(successor.Catalog), successor) && PowerPlantOwnedPokemonCatalog.For(successor.Catalog) is { } plant &&
        ReferenceEquals(plant.Mechanics, predecessor.Mechanics) && PowerPlantOpponentPokemonCatalog.For(plant) is not null &&
        (ReferenceEquals(plant.Predecessor.Catalog, predecessor.Catalog) || IsCampaignSuccessorOf(predecessor, plant.Predecessor)) ||
        ReferenceEquals(For(successor.Catalog), successor) && EventIslandOwnedPokemonCatalog.For(successor.Catalog) is { } events &&
        ReferenceEquals(events.Mechanics, predecessor.Mechanics) && EventIslandOpponentPokemonCatalog.For(events) is not null &&
        (ReferenceEquals(events.Predecessor.Catalog, predecessor.Catalog) || IsCampaignSuccessorOf(predecessor, events.Predecessor)) ||
        ReferenceEquals(For(successor.Catalog), successor) && SevenIslandOwnedPokemonCatalog.For(successor.Catalog) is { } seven &&
        ReferenceEquals(seven.Mechanics, predecessor.Mechanics) && SevenIslandOpponentPokemonCatalog.For(seven) is not null &&
        (ReferenceEquals(seven.Predecessor.Catalog, predecessor.Catalog) || IsCampaignSuccessorOf(predecessor, seven.Predecessor)) ||
        ReferenceEquals(For(successor.Catalog), successor) && CeruleanCaveOwnedPokemonCatalog.For(successor.Catalog) is { } cave &&
        ReferenceEquals(cave.Mechanics, predecessor.Mechanics) && CeruleanCaveOpponentPokemonCatalog.For(cave) is not null &&
        (ReferenceEquals(cave.Predecessor.Catalog, predecessor.Catalog) || IsCampaignSuccessorOf(predecessor, cave.Predecessor)) ||
        ReferenceEquals(For(successor.Catalog), successor) && CelioOwnedPokemonCatalog.For(successor.Catalog) is { } celio &&
        ReferenceEquals(celio.Mechanics, predecessor.Mechanics) && CelioOpponentPokemonCatalog.For(celio) is not null &&
        (ReferenceEquals(celio.Predecessor.Catalog, predecessor.Catalog) || IsCampaignSuccessorOf(predecessor, For(celio.Predecessor.Catalog)!)) ||
        ReferenceEquals(For(successor.Catalog), successor) && FiveIslandOwnedPokemonCatalog.For(successor.Catalog) is { } five &&
        ReferenceEquals(five.Mechanics, predecessor.Mechanics) && FiveIslandOpponentPokemonCatalog.For(five) is not null &&
        (ReferenceEquals(five.Predecessor.Catalog, predecessor.Catalog) || IsCampaignSuccessorOf(predecessor, For(five.Predecessor.Catalog)!)) ||
        ReferenceEquals(For(successor.Catalog), successor) && SixIslandOwnedPokemonCatalog.For(successor.Catalog) is { } six &&
        ReferenceEquals(six.Mechanics, predecessor.Mechanics) && SixIslandOpponentPokemonCatalog.For(six) is not null &&
        (ReferenceEquals(six.Predecessor.Catalog, predecessor.Catalog) || IsCampaignSuccessorOf(predecessor, For(six.Predecessor.Catalog)!)) ||
        IsSeviiRubySuccessorOf(predecessor, successor) ||
        ReferenceEquals(For(successor.Catalog), successor) && FourIslandOwnedPokemonCatalog.For(successor.Catalog) is { } four &&
        ReferenceEquals(four.Mechanics, predecessor.Mechanics) && FourIslandOpponentPokemonCatalog.For(four) is not null &&
        (ReferenceEquals(four.Predecessor.Catalog, predecessor.Catalog) ||
         IsSeviiRubySuccessorOf(predecessor, For(four.Predecessor.Catalog)!));

    internal static bool IsSeviiRubySuccessorOf(OwnedBattlePokemonCatalog predecessor, OwnedBattlePokemonCatalog successor) =>
        ReferenceEquals(For(successor.Catalog), successor) && SeviiRubyOwnedPokemonCatalog.For(successor.Catalog) is { } ruby &&
        ReferenceEquals(ruby.Predecessor.Catalog, predecessor.Catalog) &&
        ReferenceEquals(ruby.Mechanics, predecessor.Mechanics) && SeviiRubyOpponentPokemonCatalog.For(ruby) is not null;

    internal bool AdmitsTrainerOpponent(SpeciesId species, string? trainerProfile = null) => Catalog.Species.ContainsKey(species) ||
        trainerProfile == SuccessorOpponentProfile && SuccessorOpponents?.Species.ContainsKey(species) == true ||
        trainerProfile == PowerPlantOpponentPokemonCatalog.Profile && PowerPlantOwnedPokemonCatalog.For(Catalog) is { } plantOwned &&
        PowerPlantOpponentPokemonCatalog.For(plantOwned) is { } plant && plant.Catalog.Species.ContainsKey(species) ||
        trainerProfile == EventIslandOpponentPokemonCatalog.Profile && EventIslandOwnedPokemonCatalog.For(Catalog) is { } eventOwned &&
        EventIslandOpponentPokemonCatalog.For(eventOwned) is { } events && events.Catalog.Species.ContainsKey(species) ||
        trainerProfile == SevenIslandOpponentPokemonCatalog.Profile && SevenIslandOwnedPokemonCatalog.For(Catalog) is { } sevenOwned &&
        SevenIslandOpponentPokemonCatalog.For(sevenOwned) is { } seven && seven.Catalog.Species.ContainsKey(species) ||
        trainerProfile == LeagueRematchOpponentPokemonCatalog.Profile &&
        CeruleanCaveOwnedPokemonCatalog.For(Catalog) is { } rematchOwned &&
        LeagueRematchOpponentPokemonCatalog.For(rematchOwned) is { } rematch && rematch.Catalog.Species.ContainsKey(species) ||
        trainerProfile == CeruleanCaveOpponentPokemonCatalog.Profile &&
        CeruleanCaveOwnedPokemonCatalog.For(Catalog) is { } caveOwned &&
        CeruleanCaveOpponentPokemonCatalog.For(caveOwned) is { } cave && cave.Catalog.Species.ContainsKey(species) ||
        trainerProfile == CelioOpponentPokemonCatalog.Profile &&
        CelioOwnedPokemonCatalog.For(Catalog) is { } celioOwned &&
        CelioOpponentPokemonCatalog.For(celioOwned) is { } celio && celio.Catalog.Species.ContainsKey(species) ||
        trainerProfile == FiveIslandOpponentPokemonCatalog.Profile &&
        FiveIslandOwnedPokemonCatalog.For(Catalog) is { } fiveOwned &&
        FiveIslandOpponentPokemonCatalog.For(fiveOwned) is { } five && five.Catalog.Species.ContainsKey(species) ||
        trainerProfile == SixIslandOpponentPokemonCatalog.Profile &&
        SixIslandOwnedPokemonCatalog.For(Catalog) is { } sixOwned &&
        SixIslandOpponentPokemonCatalog.For(sixOwned) is { } six && six.Catalog.Species.ContainsKey(species) ||
        trainerProfile == FourIslandOpponentPokemonCatalog.Profile &&
        FourIslandOwnedPokemonCatalog.For(Catalog) is { } fourOwned &&
        FourIslandOpponentPokemonCatalog.For(fourOwned) is { } four && four.Catalog.Species.ContainsKey(species) ||
        trainerProfile == SeviiRubyOpponentPokemonCatalog.Profile &&
        SeviiRubyOwnedPokemonCatalog.For(Catalog) is { } rubyOwned &&
        SeviiRubyOpponentPokemonCatalog.For(rubyOwned) is { } ruby && ruby.Catalog.Species.ContainsKey(species) ||
        trainerProfile == SaffronGymOpponentPokemonCatalog.Profile &&
        SilphOwnedPokemonCatalog.For(Catalog) is { } saffronOwned &&
        SaffronGymOpponentPokemonCatalog.For(saffronOwned) is { } saffron && saffron.Catalog.Species.ContainsKey(species) ||
        trainerProfile == PokemonLeagueOpponentPokemonCatalog.Profile &&
        CinnabarOwnedPokemonCatalog.For(Catalog) is { } leagueOwned &&
        PokemonLeagueOpponentPokemonCatalog.For(leagueOwned) is { } league && league.Catalog.Species.ContainsKey(species) ||
        trainerProfile == VictoryRoadOpponentPokemonCatalog.Profile &&
        CinnabarOwnedPokemonCatalog.For(Catalog) is { } victoryOwned &&
        VictoryRoadOpponentPokemonCatalog.For(victoryOwned) is { } victory && victory.Catalog.Species.ContainsKey(species) ||
        trainerProfile == CinnabarOpponentPokemonCatalog.Profile &&
        CinnabarOwnedPokemonCatalog.For(Catalog) is { } cinnabarOwned &&
        CinnabarOpponentPokemonCatalog.For(cinnabarOwned) is { } cinnabar && cinnabar.Catalog.Species.ContainsKey(species) ||
        SilphOwnedPokemonCatalog.For(Catalog) is { } silph && SilphOpponentPokemonCatalog.For(silph) is { } opponents &&
        opponents.Catalog.Species.ContainsKey(species);

    internal static OwnedBattlePokemonCatalog? ForMechanics(PokemonCatalog? catalog, string? rulesVersion)
    {
        if (catalog is null || rulesVersion is null) return null;
        if (rulesVersion == PowerPlantOpponentPokemonCatalog.Profile &&
            PowerPlantOpponentPokemonCatalog.ForMechanics(catalog) is { } plant) return plant.Owned.SharedBattle;
        if (rulesVersion == EventIslandOpponentPokemonCatalog.Profile &&
            EventIslandOpponentPokemonCatalog.ForMechanics(catalog) is { } events) return events.Owned.SharedBattle;
        if (rulesVersion == SevenIslandOpponentPokemonCatalog.Profile &&
            SevenIslandOpponentPokemonCatalog.ForMechanics(catalog) is { } seven) return seven.Owned.SharedBattle;
        if (rulesVersion == LeagueRematchOpponentPokemonCatalog.Profile &&
            LeagueRematchOpponentPokemonCatalog.ForMechanics(catalog) is { } rematch) return rematch.Owned.SharedBattle;
        if (rulesVersion == CeruleanCaveOpponentPokemonCatalog.Profile &&
            CeruleanCaveOpponentPokemonCatalog.ForMechanics(catalog) is { } cave) return cave.Owned.SharedBattle;
        if (rulesVersion == CelioOpponentPokemonCatalog.Profile &&
            CelioOpponentPokemonCatalog.ForMechanics(catalog) is { } celio) return For(celio.Owned.Catalog);
        if (rulesVersion == FiveIslandOpponentPokemonCatalog.Profile &&
            FiveIslandOpponentPokemonCatalog.ForMechanics(catalog) is { } five) return For(five.Owned.Catalog);
        if (rulesVersion == SixIslandOpponentPokemonCatalog.Profile &&
            SixIslandOpponentPokemonCatalog.ForMechanics(catalog) is { } six) return For(six.Owned.Catalog);
        if (rulesVersion == FourIslandOpponentPokemonCatalog.Profile &&
            FourIslandOpponentPokemonCatalog.ForMechanics(catalog) is { } four) return For(four.Owned.Catalog);
        if (rulesVersion == SeviiRubyOpponentPokemonCatalog.Profile &&
            SeviiRubyOpponentPokemonCatalog.ForMechanics(catalog) is { } ruby) return For(ruby.Owned.Catalog);
        if (rulesVersion == PokemonLeagueOpponentPokemonCatalog.Profile &&
            PokemonLeagueOpponentPokemonCatalog.ForMechanics(catalog) is { } league) return For(league.Owned.Catalog);
        if (rulesVersion == VictoryRoadOpponentPokemonCatalog.Profile &&
            VictoryRoadOpponentPokemonCatalog.ForMechanics(catalog) is { } victory) return For(victory.Owned.Catalog);
        if (rulesVersion == CinnabarOpponentPokemonCatalog.Profile &&
            CinnabarOpponentPokemonCatalog.ForMechanics(catalog) is { } cinnabar) return For(cinnabar.Owned.Catalog);
        if (rulesVersion == SaffronGymOpponentPokemonCatalog.Profile &&
            SaffronGymOpponentPokemonCatalog.ForMechanics(catalog) is { } saffron) return For(saffron.Owned.Catalog);
        if (MechanicsOwners.TryGetValue(catalog, out var owners) && owners.TryGetValue(rulesVersion, out var proof)) return proof;
        return rulesVersion == SafariOwnedPokemonCatalog.Profile && SafariOwnedPokemonCatalog.ForMechanics(catalog) is { } safari
            ? For(safari.Catalog) : null;
    }

    internal static bool IsKnownProfile(string profile) =>
        SuccessorSpecies.ContainsKey(profile) ||
        profile is PowerPlantOwnedPokemonCatalog.Profile or PowerPlantOpponentPokemonCatalog.Profile or EventIslandOwnedPokemonCatalog.Profile or EventIslandOpponentPokemonCatalog.Profile or SevenIslandOwnedPokemonCatalog.Profile or SevenIslandOpponentPokemonCatalog.Profile or LeagueRematchOpponentPokemonCatalog.Profile or CeruleanCaveOwnedPokemonCatalog.Profile or CeruleanCaveOpponentPokemonCatalog.Profile or CelioOwnedPokemonCatalog.Profile or CelioOpponentPokemonCatalog.Profile or FiveIslandOwnedPokemonCatalog.Profile or FiveIslandOpponentPokemonCatalog.Profile or SixIslandOwnedPokemonCatalog.Profile or SixIslandOpponentPokemonCatalog.Profile or SafariOwnedPokemonCatalog.Profile or SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or SaffronGymOpponentPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or FourIslandOpponentPokemonCatalog.Profile;
    internal static bool ProfileAdmits(string profile, SpeciesId species) =>
        SuccessorSpecies.TryGetValue(profile, out var admitted) && admitted.Contains(species) ||
        (profile is PowerPlantOwnedPokemonCatalog.Profile or PowerPlantOpponentPokemonCatalog.Profile) &&
        (ProfileAdmits(EventIslandOwnedPokemonCatalog.Profile, species) || PowerPlantOwnedPokemonCatalog.AdditionalSpecies.Contains(species)) ||
        (profile is EventIslandOwnedPokemonCatalog.Profile or EventIslandOpponentPokemonCatalog.Profile) &&
        (ProfileAdmits(SevenIslandOwnedPokemonCatalog.Profile, species) || EventIslandOwnedPokemonCatalog.AdditionalSpecies.Contains(species)) ||
        (profile is SevenIslandOwnedPokemonCatalog.Profile or SevenIslandOpponentPokemonCatalog.Profile) &&
        (ProfileAdmits(CeruleanCaveOwnedPokemonCatalog.Profile, species) || SevenIslandOwnedPokemonCatalog.AdditionalSpecies.Contains(species)) ||
        profile == LeagueRematchOpponentPokemonCatalog.Profile && ProfileAdmits(CeruleanCaveOwnedPokemonCatalog.Profile, species) ||
        (profile is CeruleanCaveOwnedPokemonCatalog.Profile or CeruleanCaveOpponentPokemonCatalog.Profile) &&
        (ProfileAdmits(CelioOwnedPokemonCatalog.Profile, species) || CeruleanCaveOwnedPokemonCatalog.AdditionalSpecies.Contains(species)) ||
        (profile is CelioOwnedPokemonCatalog.Profile or CelioOpponentPokemonCatalog.Profile) &&
        (ProfileAdmits(FiveIslandOwnedPokemonCatalog.Profile, species) || CelioOwnedPokemonCatalog.AdditionalSpecies.Contains(species)) ||
        (profile is FiveIslandOwnedPokemonCatalog.Profile or FiveIslandOpponentPokemonCatalog.Profile) &&
        (ProfileAdmits(SixIslandOwnedPokemonCatalog.Profile, species) || FiveIslandOwnedPokemonCatalog.AdditionalSpecies.Contains(species)) ||
        (profile is SixIslandOwnedPokemonCatalog.Profile or SixIslandOpponentPokemonCatalog.Profile) &&
        (ProfileAdmits(FourIslandOwnedPokemonCatalog.Profile, species) || SixIslandOwnedPokemonCatalog.AdditionalSpecies.Contains(species)) || IsKnownProfile(profile) &&
        (Battle.CampaignAdmissionPolicy.CurrentFuchsia.Admits(species) || SafariOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or SaffronGymOpponentPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or FourIslandOpponentPokemonCatalog.Profile && SurfOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is SilphOwnedPokemonCatalog.Profile or SaffronGymOpponentPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or FourIslandOpponentPokemonCatalog.Profile && SilphOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or FourIslandOpponentPokemonCatalog.Profile && CinnabarOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or FourIslandOpponentPokemonCatalog.Profile && SeviiRubyOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is FourIslandOwnedPokemonCatalog.Profile or FourIslandOpponentPokemonCatalog.Profile && FourIslandOwnedPokemonCatalog.AdditionalSpecies.Contains(species));

    // Surf adds only Psyduck to historical wild membership. Golduck is an owned
    // evolution; Safari's three extra catches still use their separate encounter owner.
    internal bool AdmitsWildOpponent(PokemonInstance pokemon) =>
        CampaignParent is { } parent && parent.AdmitsWildOpponent(pokemon) ||
        PowerPlantOwnedPokemonCatalog.For(Catalog) is { } plant && For(plant.Predecessor.Catalog)!.AdmitsWildOpponent(pokemon) ||
        EventIslandOwnedPokemonCatalog.For(Catalog) is { } events && For(events.Predecessor.Catalog)!.AdmitsWildOpponent(pokemon) ||
        SevenIslandOwnedPokemonCatalog.For(Catalog) is { } seven && For(seven.Predecessor.Catalog)!.AdmitsWildOpponent(pokemon) ||
        CeruleanCaveOwnedPokemonCatalog.For(Catalog) is { } cave && For(cave.Predecessor.Catalog)!.AdmitsWildOpponent(pokemon) ||
        Predecessor.Species.ContainsKey(pokemon.Species) || RulesVersion is SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or SixIslandOwnedPokemonCatalog.Profile or FiveIslandOwnedPokemonCatalog.Profile or CelioOwnedPokemonCatalog.Profile &&
        pokemon.Species == SurfOwnedPokemonCatalog.AdditionalSpecies[0] ||
        RulesVersion is CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or SixIslandOwnedPokemonCatalog.Profile or FiveIslandOwnedPokemonCatalog.Profile or CelioOwnedPokemonCatalog.Profile && Game.Domain.Encounters.CinnabarWildPokemonFactory.WildSpecies.Contains(pokemon.Species);

    internal bool SupportsOrdinaryWildOpponent(PokemonInstance pokemon) => AdmitsWildOpponent(pokemon) &&
        (CampaignParent is { } parent && parent.SupportsOrdinaryWildOpponent(pokemon) ||
         PowerPlantOwnedPokemonCatalog.For(Catalog) is { } plant && For(plant.Predecessor.Catalog)!.SupportsOrdinaryWildOpponent(pokemon) ||
         EventIslandOwnedPokemonCatalog.For(Catalog) is { } events && For(events.Predecessor.Catalog)!.SupportsOrdinaryWildOpponent(pokemon) ||
         SevenIslandOwnedPokemonCatalog.For(Catalog) is { } seven && For(seven.Predecessor.Catalog)!.SupportsOrdinaryWildOpponent(pokemon) ||
         CeruleanCaveOwnedPokemonCatalog.For(Catalog) is { } cave && For(cave.Predecessor.Catalog)!.SupportsOrdinaryWildOpponent(pokemon) ||
         FuchsiaBattleScope.SupportsWildOpponent(pokemon.SourceSpeciesId) ||
         RulesVersion is SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or SixIslandOwnedPokemonCatalog.Profile or FiveIslandOwnedPokemonCatalog.Profile or CelioOwnedPokemonCatalog.Profile && pokemon.SourceSpeciesId is 54 or 72 or 109 || RulesVersion is CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or FourIslandOwnedPokemonCatalog.Profile or SixIslandOwnedPokemonCatalog.Profile or FiveIslandOwnedPokemonCatalog.Profile or CelioOwnedPokemonCatalog.Profile && Game.Domain.Encounters.CinnabarWildPokemonFactory.WildSpecies.Contains(pokemon.Species));
}
