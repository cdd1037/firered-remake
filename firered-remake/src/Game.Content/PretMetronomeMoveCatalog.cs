using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Content;

public sealed record MetronomeMoveName(MoveId Id, string Name);
public sealed record MetronomeMoveDocument(ImmutableArray<PokemonMoveDefinition> Moves,
    ImmutableArray<MetronomeMoveName> MoveNames);

/// <summary>Ordinary move definitions and names. Loading data does not grant battle execution.</summary>
public sealed class PretMetronomeMoveCatalog
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/data/services/metronome-moves.json";
    public ImmutableDictionary<MoveId, PokemonMoveDefinition> Moves { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }

    private PretMetronomeMoveCatalog(MetronomeMoveDocument document)
    {
        Moves = document.Moves.ToImmutableDictionary(m => m.Id);
        MoveNames = document.MoveNames.ToImmutableDictionary(m => m.Id, m => m.Name);
        Require(Moves.Values.All(SourceMoveDefinitionRules.IsValid), "Invalid move definition.");
        Require(MoveNames.Count == Moves.Count && MoveNames.All(n => Moves.ContainsKey(n.Key) && n.Value.Length > 0),
            "Move names must match move definitions.");
    }

    public static PretMetronomeMoveCatalog Load(string projectRoot) => new(
        ContentJson.ParseStrict<MetronomeMoveDocument>(File.ReadAllText(Path.Combine(projectRoot, CatalogRelativePath)),
            ContentJson.CreateOptions()));

    public PokemonCatalog Extend(PokemonCatalog predecessor)
    {
        PokemonFactory.ValidateDefinitions(predecessor);
        foreach (var (id, move) in predecessor.Moves)
            Require(id == move.Id && MatchesSource(move), "Metronome predecessor move conflicts with pinned source: " + id.Value);
        var moves = predecessor.Moves.ToBuilder();
        foreach (var (id, move) in Moves)
            if (!moves.ContainsKey(id)) moves.Add(id, move);
        var result = new PokemonCatalog(SourceCommit, predecessor.Species, moves.ToImmutable());
        PokemonFactory.ValidateDefinitions(result);
        Require(result.Moves.Count == 354 , "Metronome inherited records differ.");
        return result;
    }

    public bool MatchesSource(PokemonMoveDefinition? candidate) => candidate is not null &&
        Moves.TryGetValue(candidate.Id, out var expected) && DefinitionsEqual(candidate, expected);

    // Record equality alone compares ImmutableArray backing storage, not the actual source flags.
    public static bool DefinitionsEqual(PokemonMoveDefinition? left, PokemonMoveDefinition? right) =>
        left is not null && right is not null && !left.Flags.IsDefault && !right.Flags.IsDefault &&
        left.Id == right.Id && left.SourceMoveId == right.SourceMoveId && left.Pp == right.Pp && left.Power == right.Power &&
        left.Type == right.Type && left.Accuracy == right.Accuracy && left.Effect == right.Effect &&
        left.SecondaryEffectChance == right.SecondaryEffectChance && left.Target == right.Target && left.Priority == right.Priority &&
        left.Flags.SequenceEqual(right.Flags) && left.BattleEffectImplemented == right.BattleEffectImplemented;

    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
