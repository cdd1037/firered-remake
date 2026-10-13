using System.Collections.Concurrent;
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
        else if (SeviiRubyOwnedPokemonCatalog.For(catalog) is { } ruby)
            proof = new(catalog, For(ruby.Predecessor.Catalog)!.Predecessor, ruby.Mechanics, SeviiRubyOwnedPokemonCatalog.Profile);
        else return null;
        proof = Authenticated.GetValue(catalog, _ => proof);
        MechanicsOwners.GetValue(proof.Mechanics.Catalog, _ => new())[proof.RulesVersion] = proof;
        return proof;
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

    internal static bool IsSeviiRubySuccessorOf(OwnedBattlePokemonCatalog predecessor, OwnedBattlePokemonCatalog successor) =>
        ReferenceEquals(For(successor.Catalog), successor) && SeviiRubyOwnedPokemonCatalog.For(successor.Catalog) is { } ruby &&
        ReferenceEquals(ruby.Predecessor.Catalog, predecessor.Catalog) &&
        ReferenceEquals(ruby.Mechanics, predecessor.Mechanics) && SeviiRubyOpponentPokemonCatalog.For(ruby) is not null;

    internal bool AdmitsTrainerOpponent(SpeciesId species, string? trainerProfile = null) => Catalog.Species.ContainsKey(species) ||
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
        profile is SafariOwnedPokemonCatalog.Profile or SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or SaffronGymOpponentPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile;
    internal static bool ProfileAdmits(string profile, SpeciesId species) => IsKnownProfile(profile) &&
        (Battle.CampaignAdmissionPolicy.CurrentFuchsia.Admits(species) || SafariOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or SaffronGymOpponentPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile && SurfOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is SilphOwnedPokemonCatalog.Profile or SaffronGymOpponentPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile && SilphOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is CinnabarOwnedPokemonCatalog.Profile or CinnabarOpponentPokemonCatalog.Profile or VictoryRoadOpponentPokemonCatalog.Profile or PokemonLeagueOpponentPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile && CinnabarOwnedPokemonCatalog.AdditionalSpecies.Contains(species) ||
         profile is SeviiRubyOwnedPokemonCatalog.Profile or SeviiRubyOpponentPokemonCatalog.Profile && SeviiRubyOwnedPokemonCatalog.AdditionalSpecies.Contains(species));

    // Surf adds only Psyduck to historical wild membership. Golduck is an owned
    // evolution; Safari's three extra catches still use their separate encounter owner.
    internal bool AdmitsWildOpponent(PokemonInstance pokemon) =>
        Predecessor.Species.ContainsKey(pokemon.Species) || RulesVersion is SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile &&
        pokemon.Species == SurfOwnedPokemonCatalog.AdditionalSpecies[0] ||
        RulesVersion is CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile && Game.Domain.Encounters.CinnabarWildPokemonFactory.WildSpecies.Contains(pokemon.Species);

    internal bool SupportsOrdinaryWildOpponent(PokemonInstance pokemon) => AdmitsWildOpponent(pokemon) &&
        (FuchsiaBattleScope.SupportsWildOpponent(pokemon.SourceSpeciesId) ||
         RulesVersion is SurfOwnedPokemonCatalog.Profile or SilphOwnedPokemonCatalog.Profile or CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile && pokemon.SourceSpeciesId is 54 or 72 or 109 || RulesVersion is CinnabarOwnedPokemonCatalog.Profile or SeviiRubyOwnedPokemonCatalog.Profile && Game.Domain.Encounters.CinnabarWildPokemonFactory.WildSpecies.Contains(pokemon.Species));
}
