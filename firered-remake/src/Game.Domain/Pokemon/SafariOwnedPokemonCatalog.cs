using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Exact Safari ownership expansion of the unchanged Fuchsia Gym catalog.
/// Source mechanics authentication and battle capability remain separate from ownership.
/// Only the returned immutable root carries admission; record clones do not inherit it.</summary>
public sealed class SafariOwnedPokemonCatalog
{
    public const string Profile = "firered-safari-owned-pokemon-v1";
    public const string PredecessorSemanticSha256 = "ddf39d09ac617e4b3ef71211223812022424657fad286d4c6b68496992359575";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_CHANSEY"), new("pret:SPECIES_SCYTHER"), new("pret:SPECIES_TAUROS")];

    private static readonly ConditionalWeakTable<PokemonCatalog, SafariOwnedPokemonCatalog> Authenticated = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, SafariOwnedPokemonCatalog> MechanicsOwners = new();
    public PokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics { get; }

    private SafariOwnedPokemonCatalog(PokemonCatalog predecessor, PokemonCatalog catalog, SourceMechanicsCatalog mechanics)
    { Predecessor = predecessor; Catalog = catalog; Mechanics = mechanics; }

    public static SafariOwnedPokemonCatalog Authenticate(PokemonCatalog predecessor,
        PokemonCatalog candidate, SourceMechanicsCatalog mechanics)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(mechanics);
        PokemonFactory.ValidateCatalog(predecessor);
        if (predecessor.Species.Count != 107 || predecessor.Moves.Count != 354 ||
            Fingerprint(predecessor) != PredecessorSemanticSha256)
            throw new ArgumentException("safari_owned_predecessor");
        if (candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null ||
            candidate.Species.Count != 110 || !ReferenceEquals(candidate.Moves, predecessor.Moves) ||
            predecessor.Species.Any(pair => !candidate.Species.TryGetValue(pair.Key, out var inherited) ||
                !ReferenceEquals(pair.Value, inherited)) ||
            !candidate.Species.Keys.Except(predecessor.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("safari_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!MatchesSource(candidate.Species[id], mechanics.Catalog.Species[id]))
                throw new ArgumentException("safari_owned_species_source");
        // Every admitted species uses the existing ordinary permanent-stat calculation.
        // This proof never routes exceptional source species into unmodified storage/candy rules.
        if (candidate.Species.ContainsKey(mechanics.MaximumHpOneSpecies) ||
            mechanics.FormStatOverrides.Any(value => candidate.Species.ContainsKey(value.Species)))
            throw new ArgumentException("safari_owned_stat_scope");
        var proof = new SafariOwnedPokemonCatalog(predecessor, candidate with { }, mechanics);
        Authenticated.Add(proof.Catalog, proof);
        MechanicsOwners.GetValue(mechanics.Catalog, _ => proof);
        return proof;
    }

    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static SafariOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Authenticated.TryGetValue(catalog, out var proof) ? proof : null;

    internal static SafariOwnedPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsOwners.TryGetValue(catalog, out var proof) ? proof : null;

    private static bool MatchesSource(PokemonSpeciesDefinition? actual, PokemonSpeciesDefinition expected) =>
        actual is not null && !actual.LevelUpMoves.IsDefault && !actual.Evolutions.IsDefault &&
        actual.LevelUpMoves.SequenceEqual(expected.LevelUpMoves) && actual.Evolutions.SequenceEqual(expected.Evolutions) &&
        actual with { LevelUpMoves = expected.LevelUpMoves, Evolutions = expected.Evolutions } == expected;

    // Ordered complete records include provenance and the historical bounded evolution/learnset
    // projections. The fixed predecessor hash is independent of any caller-supplied trust flag.
    private static string Fingerprint(PokemonCatalog catalog) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new {
            catalog.SourceCommit,
            Species = catalog.Species.Values.OrderBy(species => species.SourceSpeciesId).ToArray(),
            Moves = catalog.Moves.Values.OrderBy(move => move.SourceMoveId).ToArray()
        })));
}
