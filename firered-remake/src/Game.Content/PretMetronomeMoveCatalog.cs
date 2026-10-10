using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Content;

public sealed record MetronomeMoveInput(string Path, int ByteLength, string Sha256, string GitBlobSha1);
public sealed record MetronomeMoveConstant(string Symbol, int Value, PokemonSourceRef Source);
public sealed record MetronomeNaturePowerMove(string Terrain, MoveId Move);
public sealed record MetronomeEffectDispatch(string Effect, string ScriptLabel,
    PokemonSourceRef Source, PokemonSourceRef ScriptSource);
/// <summary>Source command/ability relationships, not a claim that their behavior is implemented.</summary>
public sealed record MetronomeAbilityPolicy(string Symbol, ImmutableArray<string> Abilities, PokemonSourceRef Source);
public sealed record MetronomeMovePolicies(ImmutableArray<MoveId> MetronomeForbiddenMoves,
    ImmutableArray<MoveId> DirectCallableMoves, ImmutableArray<MoveId> MirrorMoveExtraResults,
    ImmutableArray<MoveId> MimicForbiddenMoves, ImmutableArray<MoveId> InvalidSleepTalkOrAssistMoves,
    ImmutableArray<MoveId> SoundMoves, ImmutableArray<string> HighCriticalEffects,
    ImmutableArray<MetronomeNaturePowerMove> NaturePowerMoves,
    ImmutableArray<MetronomeEffectDispatch> EffectDispatch,
    ImmutableArray<MetronomeAbilityPolicy> AbilityPolicies, ImmutableArray<PokemonSourceRef> SourceReferences);
public sealed record MetronomeMoveDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<MetronomeMoveInput> Inputs, ImmutableArray<PokemonMoveDefinition> Moves,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<MetronomeMoveConstant> Constants,
    MetronomeMovePolicies Policies);

/// <summary>
/// Explicit, authenticated metadata extension for the full 354-move source table.
/// Loading or extending this catalog never changes a battle scope or registers an executor.
/// All 336 direct Metronome choices and six additional legal Mirror Move results are data only.
/// </summary>
public sealed class PretMetronomeMoveCatalog
{
    public const string Profile = "firered-metronome-metadata-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockRelativePath = "content/sources/metronome-moves/source-lock.json";
    public const string CatalogRelativePath = "content/generated/metronome-moves/catalog.json";
    public const string ManifestRelativePath = "content/generated/metronome-moves/manifest.json";
    public const string LockSha256 = "449c78972943f57e680843b44e45d7e06509037b82d20149b706e2483ef2b747";
    public const string CatalogSha256 = "dd93a57abc65c8c313647c5dd78666551e716e97e1ea807f3d9a84485a57c54d";
    public const string ManifestSha256 = "0c60a21bc58f72d278b3f999d0fea97fab50b731363a162f25a677e80bcfd6d3";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public MetronomeMoveDocument Document { get; }
    public MetronomeMovePolicies Policies => Document.Policies;
    public ImmutableDictionary<MoveId, PokemonMoveDefinition> Moves { get; }
    public ImmutableDictionary<MoveId, string> MoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretMetronomeMoveCatalog(MetronomeMoveDocument document, ImmutableArray<PretWorldSourceFile> files)
    {
        Document = document;
        Moves = document.Moves.ToImmutableDictionary(m => m.Id);
        MoveNames = document.MoveNames.ToImmutableDictionary(m => m.Id, m => m.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        SourceFiles = files;
        AdditionalSourceHashes = ["metronome-moves-lock:" + LockSha256, "metronome-moves-catalog:" + CatalogSha256,
            "metronome-moves-manifest:" + ManifestSha256,
            .. files.Select(f => "metronome-moves:" + f.Path + ":" + f.Sha256)];
    }

    public static PretMetronomeMoveCatalog Load(string projectRoot)
    {
        string root = Path.GetFullPath(projectRoot);
        // Both generated metadata and original bytes are authenticated; an edited output cannot
        // acquire trust merely by retaining a legitimate source-reference hash.
        var sourceLock = Parse<SourceLock>(Read(root, LockRelativePath, LockSha256, 2355));
        Require(sourceLock.SchemaVersion == 1 && sourceLock.Repository == "pret/pokefirered" &&
            sourceLock.Commit == SourceCommit && sourceLock.Tree == SourceTree && sourceLock.Profile == Profile &&
            sourceLock.GameEdition == "FIRERED" && sourceLock.RomRevision == 0 && sourceLock.OrdinaryMoveCount == 354 &&
            sourceLock.Files.Length == 11, "Metronome source lock scope differs.");
        var manifest = Parse<Manifest>(Read(root, ManifestRelativePath, ManifestSha256, 477));
        Require(manifest.SchemaVersion == 1 && manifest.Profile == Profile && manifest.SourceCommit == SourceCommit &&
            manifest.Tree == SourceTree && manifest.LockSha256 == LockSha256 && manifest.InputCount == 11 &&
            manifest.OrdinaryMoveCount == 354 && manifest.DirectCallableMoveCount == 336 && manifest.MirrorExtraMoveCount == 6 &&
            manifest.Outputs.Length == 1 && manifest.Outputs[0] == new Output("catalog.json", 529625, CatalogSha256),
            "Metronome generated manifest scope differs.");
        var doc = Parse<MetronomeMoveDocument>(Read(root, CatalogRelativePath, CatalogSha256, 529625));
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.SequenceEqual(sourceLock.Files) && doc.Moves.Length == 354 && doc.MoveNames.Length == 354 &&
            doc.Constants.Length == 701, "Metronome metadata scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var lineCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var input in sourceLock.Files)
        {
            var bytes = Read(root, "content/raw/metronome-moves/" + input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == input.GitBlobSha1,
                "Metronome source Git blob differs: " + input.Path);
            lineCounts.Add(input.Path, Utf8.GetString(bytes).Count(c => c == '\n') + 1);
            files.Add(new(input.Path, input.GitBlobSha1, input.ByteLength, input.Sha256,
                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        void Reference(PokemonSourceRef source)
        {
            Require(source is not null && sourceLock.Files.Any(f => f.Path == source.Path && f.Sha256 == source.Sha256) &&
                source.StartLine > 0 && source.EndLine >= source.StartLine &&
                lineCounts.TryGetValue(source.Path, out int lines) && source.EndLine <= lines && !string.IsNullOrWhiteSpace(source.Symbol),
                "Metronome source reference differs.");
        }
        Require(doc.Moves.Select(m => m.SourceMoveId).SequenceEqual(Enumerable.Range(1, 354)) &&
            doc.Moves.Select(m => m.Id).Distinct().Count() == 354, "Metronome ordinary move closure differs.");
        var constants = doc.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        var ids = doc.Moves.Select(m => m.Id).ToImmutableHashSet();
        foreach (var move in doc.Moves)
        {
            Reference(move.Source);
            Require(move.Id.Value == "pret:" + move.Source.Symbol && move.Source.Path == "src/data/battle_moves.h" &&
                !move.BattleEffectImplemented && !move.Flags.IsDefault && move.Flags.Distinct(StringComparer.Ordinal).Count() == move.Flags.Length &&
                constants[move.Source.Symbol] == move.SourceMoveId && constants.ContainsKey(move.Effect) &&
                constants.ContainsKey(move.Type) && constants.ContainsKey(move.Target) &&
                move.Flags.All(f => f.StartsWith("FLAG_", StringComparison.Ordinal) && constants.ContainsKey(f)) &&
                MetronomeSourceMoveDefinition.Matches(move),
                "Metronome move identity, flags or execution claim differs: " + move.Id.Value);
        }
        foreach (var constant in doc.Constants) Reference(constant.Source);
        foreach (var name in doc.MoveNames) { Reference(name.Source); Require(ids.Contains(name.Id) && name.Name.Length > 0, "Metronome move name differs."); }
        var policies = doc.Policies;
        Require(policies.MetronomeForbiddenMoves.Length == 18 && policies.DirectCallableMoves.Length == 336 &&
            policies.MirrorMoveExtraResults.Length == 6 && policies.MimicForbiddenMoves.Length == 4 &&
            policies.InvalidSleepTalkOrAssistMoves.Length == 5 && policies.SoundMoves.Length == 10 &&
            policies.HighCriticalEffects.Length == 4 && policies.NaturePowerMoves.Length == 10 &&
            policies.EffectDispatch.Length == 214 && policies.AbilityPolicies.Length == 7 && policies.SourceReferences.Length == 5,
            "Metronome source policy scope differs.");
        Require(policies.DirectCallableMoves.SequenceEqual(doc.Moves.Where(m => !policies.MetronomeForbiddenMoves.Contains(m.Id)).Select(m => m.Id)) &&
            policies.MirrorMoveExtraResults.SequenceEqual(doc.Moves.Where(m => policies.MetronomeForbiddenMoves.Contains(m.Id) &&
                m.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED")).Select(m => m.Id)), "Metronome direct/transitive closure differs.");
        Require(policies.MetronomeForbiddenMoves.All(ids.Contains) && policies.SoundMoves.All(ids.Contains) &&
            policies.NaturePowerMoves.All(m => ids.Contains(m.Move) && constants.ContainsKey(m.Terrain)) &&
            policies.HighCriticalEffects.All(constants.ContainsKey), "Metronome policy reference differs.");
        foreach (var dispatch in policies.EffectDispatch) { Reference(dispatch.Source); Reference(dispatch.ScriptSource); }
        Require(policies.EffectDispatch.Select(d => d.Effect).Distinct(StringComparer.Ordinal).Count() == 214 &&
            doc.Moves.All(m => policies.EffectDispatch.Any(d => d.Effect == m.Effect)), "Metronome effect dispatch differs.");
        foreach (var policy in policies.AbilityPolicies)
        {
            Reference(policy.Source);
            Require(policy.Source.Symbol == policy.Symbol && policy.Abilities.All(a => a.StartsWith("ABILITY_", StringComparison.Ordinal) && constants.ContainsKey(a)),
                "Metronome ability policy reference differs.");
        }
        foreach (var reference in policies.SourceReferences) Reference(reference);
        return new(doc, files.ToImmutable());
    }

    /// <summary>
    /// Preserve every prior species and move object after full equality authentication, then
    /// add absent definitions. This is an explicit metadata operation, never battle admission.
    /// </summary>
    public PokemonCatalog Extend(PokemonCatalog predecessor)
    {
        PokemonFactory.ValidateCatalog(predecessor);
        foreach (var (id, move) in predecessor.Moves)
            Require(id == move.Id && MatchesSource(move), "Metronome predecessor move conflicts with pinned source: " + id.Value);
        var moves = predecessor.Moves.ToBuilder();
        foreach (var (id, move) in Moves)
            if (!moves.ContainsKey(id)) moves.Add(id, move);
        var result = new PokemonCatalog(SourceCommit, predecessor.Species, moves.ToImmutable());
        PokemonFactory.ValidateCatalog(result);
        Require(result.Moves.Count == 354 && ReferenceEquals(predecessor.Species, result.Species) &&
            predecessor.Moves.All(p => ReferenceEquals(p.Value, result.Moves[p.Key])), "Metronome inherited records differ.");
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
        left.Flags.SequenceEqual(right.Flags) && left.BattleEffectImplemented == right.BattleEffectImplemented && left.Source == right.Source;

    public int FlagMask(MoveId id) => Moves[id].Flags.Aggregate(0, (mask, flag) => mask | Constants[flag]);

    private static T Parse<T>(byte[] bytes) => ContentJson.ParseStrict<T>(Utf8.GetString(bytes), ContentJson.CreateOptions());
    private static byte[] Read(string root, string relative, string hash, int length) => PretCeladonApproachSource.Read(root, relative, hash, length);
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private sealed record SourceLock(int SchemaVersion, string Repository, string Commit, string Tree, string Profile,
        string GameEdition, int RomRevision, int OrdinaryMoveCount, ImmutableArray<MetronomeMoveInput> Files);
    private sealed record Output(string Path, int ByteLength, string Sha256);
    private sealed record Manifest(int SchemaVersion, string Profile, string SourceCommit, string Tree, string LockSha256,
        int InputCount, int OrdinaryMoveCount, int DirectCallableMoveCount, int MirrorExtraMoveCount, ImmutableArray<Output> Outputs);
}
