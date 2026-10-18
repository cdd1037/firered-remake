using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Battle-only type/ability registers. PokemonInstance remains the authenticated identity.</summary>
public sealed record WildBattleCalledOverlay
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Type1 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Type2 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Ability { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceCastformForm? CastformForm { get; init; }
}
public enum WildBattleCalledBattleKind { Wild, Trainer }
public enum WildBattleCalledSwitchKind { Voluntary, BatonPass, FaintedReplacement, Forced }
/// <summary>Index identity is stable only within this battle. App ownership identity belongs to its adapter.</summary>
public sealed record WildBattleCalledPartyMember(PokemonInstance Pokemon, string HeldItem = "ITEM_NONE", uint Status = 0,
    bool IsEgg = false, string? KnockedOffItem = null)
{
    // Knock Off suppresses only the battle item. Recycle/consumption can subsequently change party data.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PersistentHeldItemOverride { get; init; }
}
public sealed record WildBattleCalledPendingSwitch(WildBattleSide Side, WildBattleCalledSwitchKind Kind,
    ImmutableArray<int> LegalPartyIndices, WildBattleMoveInvocation? Invocation = null);
/// <summary>Actual supplied parties, never synthesized from an active battler or a catalog entry.</summary>
public sealed record WildBattleCalledPartyState(ImmutableArray<WildBattleCalledPartyMember> Player,
    ImmutableArray<WildBattleCalledPartyMember> Opponent, int PlayerActiveIndex, int OpponentActiveIndex,
    WildBattleCalledBattleKind BattleKind = WildBattleCalledBattleKind.Wild)
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerSpikes { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentSpikes { get; init; }
    // gBattleStruct->usedHeldItems belongs to battler slots, surviving a switch.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PlayerUsedHeldItem { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OpponentUsedHeldItem { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledPendingSwitch? PendingSwitch { get; init; }
}
public sealed record WildBattleCalledOverlayChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    MoveId Move, WildBattleCalledOverlay? Before, WildBattleCalledOverlay? After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledPartyChangedEvent(long Sequence, int Turn, WildBattleSide Side, int PartyIndex,
    string Reason, WildBattleCalledPartyMember Before, WildBattleCalledPartyMember After) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? SourceSlot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public byte? TransientMoveSlots { get; init; }
}
public sealed record WildBattleCalledPartySwitchRequiredEvent(long Sequence, int Turn,
    WildBattleCalledPendingSwitch Pending) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledPartySwitchedEvent(long Sequence, int Turn, WildBattleSide Side,
    int OutgoingIndex, int IncomingIndex, WildBattleCalledSwitchKind Kind) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledSpikesChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    int Before, int After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCalledBeatUpMemberEvent(long Sequence, int Turn, WildBattleSide Side,
    int PartyIndex, SpeciesId Species) : WildBattleEvent(Sequence, Turn);

/// <summary>Shared battle mechanics must read these registers rather than permanent species/ability.</summary>
public static class WildBattleEffective
{
    public static string Ability(WildBattleCombatant mon) => mon.CalledOverlay?.Ability ?? mon.Pokemon.AbilityId;
    public static (string Type1, string Type2) Types(PokemonCatalog catalog, WildBattleCombatant mon)
    {
        var species = catalog.Species[mon.Pokemon.Species];
        return (mon.CalledOverlay?.Type1 ?? species.Type1, mon.CalledOverlay?.Type2 ?? species.Type2);
    }
    public static bool HasType(PokemonCatalog catalog, WildBattleCombatant mon, string type)
    { var pair = Types(catalog, mon); return pair.Type1 == type || pair.Type2 == type; }
    public static bool IsSourceType(string? type) => type is "TYPE_NORMAL" or "TYPE_FIGHTING" or "TYPE_FLYING" or
        "TYPE_POISON" or "TYPE_GROUND" or "TYPE_ROCK" or "TYPE_BUG" or "TYPE_GHOST" or "TYPE_STEEL" or
        "TYPE_FIRE" or "TYPE_WATER" or "TYPE_GRASS" or "TYPE_ELECTRIC" or "TYPE_PSYCHIC" or "TYPE_ICE" or "TYPE_DRAGON" or "TYPE_DARK";
}
public static class WildBattleSourcePartyEffects
{
    public static ImmutableArray<WildBattleCalledPartyMember> Party(WildBattleCalledPartyState state, WildBattleSide side) =>
        side == WildBattleSide.Player ? state.Player : state.Opponent;
    public static int ActiveIndex(WildBattleCalledPartyState state, WildBattleSide side) =>
        side == WildBattleSide.Player ? state.PlayerActiveIndex : state.OpponentActiveIndex;
    public static ImmutableArray<int> LegalReserveIndices(WildBattleCalledPartyState state, WildBattleSide side) =>
        Party(state, side).Select((mon, index) => (mon, index)).Where(x => x.index != ActiveIndex(state, side) &&
            !x.mon.IsEgg && x.mon.Pokemon.CurrentHp > 0).Select(x => x.index).ToImmutableArray();
    public static bool HasLivingMember(WildBattleCalledPartyState state, WildBattleSide side) =>
        Party(state, side).Any(mon => !mon.IsEgg && mon.Pokemon.CurrentHp > 0);
    public static ImmutableArray<int> BeatUpIndices(WildBattleCalledPartyState state, WildBattleSide side) =>
        Party(state, side).Select((mon, index) => (mon, index)).Where(x => !x.mon.IsEgg && x.mon.Pokemon.CurrentHp > 0 &&
            x.mon.Status == 0).Select(x => x.index).ToImmutableArray();
    public static string PersistentHeldItem(WildBattleCalledPartyMember mon) => mon.PersistentHeldItemOverride ?? mon.KnockedOffItem ?? mon.HeldItem;
    internal static WildBattleCalledPartyMember WithPersistentHeldItem(WildBattleCalledPartyMember mon, string item) =>
        mon with { PersistentHeldItemOverride = item == (mon.KnockedOffItem ?? mon.HeldItem) ? null : item };
    internal static bool SamePokemon(PokemonInstance a, PokemonInstance b) =>
        a with { Moves = [] } == b with { Moves = [] } && a.Moves.SequenceEqual(b.Moves);
    // ImmutableArray record equality compares backing-array identity. State restored from JSON
    // has different arrays even when the permanent party data is unchanged.
    internal static bool SameMember(WildBattleCalledPartyMember a, WildBattleCalledPartyMember b) =>
        a with { Pokemon = b.Pokemon } == b && SamePokemon(a.Pokemon, b.Pokemon);
    public static int SpikesDamage(int maxHp, int layers) => SourceSpikesRules.Damage(maxHp, layers);
    public static int BeatUpBaseDamage(PokemonCatalog catalog, WildBattleCalledPartyMember contributor,
        WildBattleCombatant target, int power) =>
        catalog.Species[contributor.Pokemon.Species].BaseStats.Attack * power * (contributor.Pokemon.Level * 2 / 5 + 2) /
        catalog.Species[target.Pokemon.Species].BaseStats.Defense / 50 + 2;
    /// <summary>Modern Conversion2: distinct resistant/immune types in stable Gen III source-ID order.</summary>
    public static ImmutableArray<string> Conversion2Candidates(string attackType, string type1, string type2)
    {
        if (!attackType.StartsWith("TYPE_", StringComparison.Ordinal) ||
            !Enum.TryParse<ElementType>(attackType[5..], true, out var attack) || !Enum.IsDefined(attack) ||
            !WildBattleEffective.IsSourceType(type1) || !WildBattleEffective.IsSourceType(type2))
            throw new ArgumentOutOfRangeException(nameof(attackType));
        return Enum.GetValues<ElementType>().Where(type => type != ElementType.Mystery && TypeRules.EffectivenessTenths(attack, type) <= 5)
            .Select(type => "TYPE_" + type.ToString().ToUpperInvariant()).Where(type => type != type1 && type != type2).ToImmutableArray();
    }
    /// <summary>One standard engine uint16 draw. Bucket sizes differ by at most one out of65536 values.</summary>
    public static int Conversion2ChoiceIndex(ushort random, int candidateCount) => candidateCount is >= 1 and <= 17
        ? random % candidateCount : throw new ArgumentOutOfRangeException(nameof(candidateCount));
    public static string CamouflageType(WildBattleTerrain terrain) => terrain switch {
        WildBattleTerrain.Grass or WildBattleTerrain.LongGrass => "TYPE_GRASS",
        WildBattleTerrain.Sand => "TYPE_GROUND",
        WildBattleTerrain.Underwater or WildBattleTerrain.Water or WildBattleTerrain.Pond => "TYPE_WATER",
        WildBattleTerrain.Mountain or WildBattleTerrain.Cave => "TYPE_ROCK",
        WildBattleTerrain.Building or WildBattleTerrain.Plain => "TYPE_NORMAL",
        _ => throw new ArgumentOutOfRangeException(nameof(terrain)) };
    internal static WildBattleCalledPartyState SetParty(WildBattleCalledPartyState state, WildBattleSide side,
        ImmutableArray<WildBattleCalledPartyMember> party) => side == WildBattleSide.Player ? state with { Player = party } : state with { Opponent = party };
    internal static WildBattleCalledPartyState Synchronize(WildBattleCalledPartyState state, WildBattleSide side, WildBattleCombatant mon)
    {
        var party = Party(state, side); int index = ActiveIndex(state, side); var before = party[index];
        string persistentItem = before.HeldItem != mon.HeldItem && before.KnockedOffItem == mon.KnockedOffItem ? mon.HeldItem : PersistentHeldItem(before);
        var after = before with { Pokemon = WildBattleTransformationRules.PersistentPokemon(mon),
            Status = WildBattleTransformationRules.PersistentStatus(mon), HeldItem = mon.HeldItem, KnockedOffItem = mon.KnockedOffItem };
        after = WithPersistentHeldItem(after, persistentItem);
        return SameMember(before, after) ? state : SetParty(state, side, party.SetItem(index, after));
    }
}
