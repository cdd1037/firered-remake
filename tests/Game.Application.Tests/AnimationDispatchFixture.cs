using System.Text.Json;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application;

/// <summary>Current move metadata and generated renderer availability for dispatch tests.
/// Explicit metadata, including null and malformed dictionaries, is forwarded unchanged.</summary>
internal static class AnimationDispatchFixture
{
    private static readonly Lazy<Dictionary<int, string>> GeneratedMoves = new(ReadGeneratedMoves);
    private static IReadOnlyDictionary<MoveId, PokemonMoveDefinition> Moves =>
        CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves;

    public static bool Supports(int sourceMoveId, string move)
    {
        const string prefix = "pret:MOVE_";
        return move.StartsWith(prefix, StringComparison.Ordinal) &&
            GeneratedMoves.Value.TryGetValue(sourceMoveId, out string? label) && label == "Move_" + move[prefix.Length..];
    }

    private static Dictionary<int, string> ReadGeneratedMoves()
    {
        var moves = new Dictionary<int, string>();
        string folder = Path.Combine(TestAssets.Root, "content/generated/battle/animations");
        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.TryGetProperty("scripts", out var scripts))
                foreach (var script in scripts.EnumerateArray()) Add(script);
            if (root.TryGetProperty("script", out var single)) Add(single);
        }
        if (moves.Count == 0) throw new InvalidDataException("No generated move animation scripts were found.");
        return moves;

        void Add(JsonElement script)
        {
            int id = script.GetProperty("moveId").GetInt32();
            // The generic stat mask is a material script, not a move renderer.
            if (id > 0) moves.Add(id, script.GetProperty("label").GetString() ??
                throw new InvalidDataException("Generated move animation label is missing."));
        }
    }

    public static CampaignMoveAnimationDecision AtAttackText(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene) => AtAttackText(battle, sourceRules, scene, Moves);
    public static CampaignMoveAnimationDecision AtAttackText(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) =>
        CampaignMoveAnimationPresentation.AtAttackText(battle, sourceRules, scene, moves, Supports);
    public static CampaignMoveAnimationDecision AtAttackText(CampaignDoubleBattle battle,
        SourceBattleScene scene) => AtAttackText(battle, scene, Moves);
    public static CampaignMoveAnimationDecision AtAttackText(CampaignDoubleBattle battle,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) =>
        CampaignMoveAnimationPresentation.AtAttackText(battle, scene, moves, Supports);

    public static CampaignMoveAnimationDecision AtHitResult(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene) => AtHitResult(battle, sourceRules, scene, Moves);
    public static CampaignMoveAnimationDecision AtHitResult(CampaignWildBattle battle, bool sourceRules,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) =>
        CampaignMoveAnimationPresentation.AtHitResult(battle, sourceRules, scene, moves, Supports);
    public static CampaignMoveAnimationDecision AtHitResult(CampaignDoubleBattle battle,
        SourceBattleScene scene) => AtHitResult(battle, scene, Moves);
    public static CampaignMoveAnimationDecision AtHitResult(CampaignDoubleBattle battle,
        SourceBattleScene scene, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) =>
        CampaignMoveAnimationPresentation.AtHitResult(battle, scene, moves, Supports);

    public static CampaignStatAnimationDispatch? StatAtResult(CampaignWildBattle battle, bool sourceRules,
        bool affectedBehindSubstitute = false) => StatAtResult(battle, sourceRules, affectedBehindSubstitute, Moves);
    public static CampaignStatAnimationDispatch? StatAtResult(CampaignWildBattle battle, bool sourceRules,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) => StatAtResult(battle, sourceRules, false, moves);
    public static CampaignStatAnimationDispatch? StatAtResult(CampaignWildBattle battle, bool sourceRules,
        bool affectedBehindSubstitute, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) =>
        CampaignStatAnimationPresentation.AtResult(battle, sourceRules, affectedBehindSubstitute, moves);
    public static CampaignStatAnimationDispatch? StatAtResult(CampaignDoubleBattle battle,
        bool affectedBehindSubstitute = false) => StatAtResult(battle, affectedBehindSubstitute, Moves);
    public static CampaignStatAnimationDispatch? StatAtResult(CampaignDoubleBattle battle,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) => StatAtResult(battle, false, moves);
    public static CampaignStatAnimationDispatch? StatAtResult(CampaignDoubleBattle battle,
        bool affectedBehindSubstitute, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves) =>
        CampaignStatAnimationPresentation.AtResult(battle, affectedBehindSubstitute, moves);
}
