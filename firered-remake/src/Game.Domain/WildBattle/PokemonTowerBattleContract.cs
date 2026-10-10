using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
namespace Game.Domain.WildBattle;

/// <summary>Source battle flags, not species. Unveiled Marowak retains the Ghost capture restriction.</summary>
public enum PokemonTowerBattleKind { OrdinaryWild, DisguisedWild, DisguisedMarowak, RevealedMarowak }
public sealed record PokemonTowerBattlePolicy(bool GhostFlag, bool UnveiledFlag, bool RevealSpecies,
    bool OrdinaryAttacks, bool RegisterSeen, bool NormalCapture, bool GuaranteedRun,
    bool SuppressEntryIntimidateAndTrace, bool SuppressOpponentWeatherDamage);

/// <summary>Source Ghost flags consumed by the explicit Tower entry. Presentation owns the initial reveal animation.</summary>
public static class PokemonTowerBattleContract
{
    public const string SourceCommit = WildBattleRules.SourceCommit;
    public static PokemonTowerBattleKind Classify(bool scriptedMarowak, bool hasSilphScope) =>
        scriptedMarowak ? hasSilphScope ? PokemonTowerBattleKind.RevealedMarowak : PokemonTowerBattleKind.DisguisedMarowak :
            hasSilphScope ? PokemonTowerBattleKind.OrdinaryWild : PokemonTowerBattleKind.DisguisedWild;
    public static PokemonTowerBattlePolicy Policy(PokemonTowerBattleKind kind) => kind switch
    {
        PokemonTowerBattleKind.OrdinaryWild => new(false, false, true, true, true, true, false, false, false),
        PokemonTowerBattleKind.DisguisedWild or PokemonTowerBattleKind.DisguisedMarowak => new(true, false, false, false, false, false, true, true, true),
        PokemonTowerBattleKind.RevealedMarowak => new(true, true, true, true, true, false, false, false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    /// <summary>Species held table, only when SetWildMonHeldItem runs. RevealedMarowak skips it: UNVEILED aliases LEGENDARY.</summary>
    public static string WildHeldItem(int sourceSpeciesId, int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return sourceSpeciesId switch
        {
            92 => "ITEM_NONE",
            93 => roll >= 95 ? "ITEM_SPELL_TAG" : "ITEM_NONE",
            104 or 105 => roll >= 95 ? "ITEM_THICK_CLUB" : "ITEM_NONE",
            _ => throw new NotSupportedException("pokemon_tower_species_held_item")
        };
    }
    public static bool ClearsMarowakGate(WildBattleOutcome outcome) => outcome == WildBattleOutcome.Won;
    // Ball/storage capacity checks precede this branch. An authorized throw spends the ball and action;
    // all Ghost battles dodge before capture RNG, including Master Ball. No caught instance is returned.
    public static bool BallDodges(PokemonTowerBattleKind kind) => Policy(kind).GhostFlag;
}

public static partial class WildBattleRules
{
    /// <summary>Four source battle categories. Ordinary entry preserves the historical null flag.
    /// Caller supplies the source-created underlying Pokémon and persists TowerBattleKind with state.</summary>
    public static WildBattleResult StartPokemonTower(PokemonCatalog catalog, WildBattleCombatant player,
        WildBattleCombatant opponent, RngState rng, PokemonTowerBattleKind kind, bool snorlaxRules = false, bool fuchsiaRules = false)
    {
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) != (fuchsiaRules ? 107 : snorlaxRules ? 102 : 101)) return Fail(null,"snorlax_catalog_profile");
        if (!Enum.IsDefined(kind)) return Fail(null, "pokemon_tower_battle_kind");
        if (opponent?.Pokemon is null) return Fail(null, "pokemon_tower_opponent_missing");
        if (kind == PokemonTowerBattleKind.DisguisedWild && opponent.Pokemon.SourceSpeciesId is not (92 or 93 or 104))
            return Fail(null, "pokemon_tower_disguised_species");
        if (kind is PokemonTowerBattleKind.DisguisedMarowak or PokemonTowerBattleKind.RevealedMarowak &&
            (opponent.Pokemon.SourceSpeciesId != 105 || opponent.Pokemon.Level != 30))
            return Fail(null, "pokemon_tower_marowak_identity");
        if (kind == PokemonTowerBattleKind.RevealedMarowak &&
            (!SourceRevealedMarowak(opponent.Pokemon) || opponent.HeldItem != "ITEM_NONE" ||
            !opponent.Pokemon.Moves.Select(m => m.Move.Value).SequenceEqual(new[] {
                "pret:MOVE_HEADBUTT", "pret:MOVE_LEER", "pret:MOVE_FOCUS_ENERGY", "pret:MOVE_BONEMERANG" })))
            return Fail(null, "pokemon_tower_revealed_marowak_identity");
        return StartCore(catalog, player, opponent, rng, fuchsiaRules ? FuchsiaRulesVersion : snorlaxRules ? SnorlaxRulesVersion : TowerRulesVersion, WildBattleTerrain.Building, false,
            kind == PokemonTowerBattleKind.OrdinaryWild ? null : kind);
    }

    private static bool SourceRevealedMarowak(PokemonInstance mon) => mon.SourceSpeciesId == 105 && mon.Level == 30 &&
        mon.Gender == PokemonGender.Female && mon.Nature == Nature.Serious &&
        mon.Ivs == new SixStats(31, 31, 31, 31, 31, 31) && mon.Evs == SixStats.Zero;

    private static bool IsTowerGhost(WildBattleState? state) => state?.RulesVersion is TowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion &&
        state.TowerBattleKind is PokemonTowerBattleKind.DisguisedWild or PokemonTowerBattleKind.DisguisedMarowak or PokemonTowerBattleKind.RevealedMarowak;

    private static string? ValidateTowerKind(WildBattleState state)
    {
        if (state.TowerBattleKind is not { } kind) return null;
        if (state.RulesVersion is not (TowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion) || !Enum.IsDefined(kind) || kind == PokemonTowerBattleKind.OrdinaryWild)
            return "pokemon_tower_battle_kind";
        if (state.Opponent?.Pokemon is not { } mon) return "pokemon_tower_opponent_missing";
        if (kind == PokemonTowerBattleKind.DisguisedWild && mon.SourceSpeciesId is not (92 or 93 or 104))
            return "pokemon_tower_disguised_species";
        if (kind is PokemonTowerBattleKind.DisguisedMarowak or PokemonTowerBattleKind.RevealedMarowak &&
            (mon.SourceSpeciesId != 105 || mon.Level != 30)) return "pokemon_tower_marowak_identity";
        if (kind == PokemonTowerBattleKind.RevealedMarowak && !SourceRevealedMarowak(mon))
            return "pokemon_tower_revealed_marowak_identity";
        if (state.Outcome == WildBattleOutcome.Caught) return "pokemon_tower_ghost_cannot_be_caught";
        return null;
    }

    internal sealed partial class Execution
    {
        private bool TowerDisguised => Tower && State.TowerBattleKind is
            PokemonTowerBattleKind.DisguisedWild or PokemonTowerBattleKind.DisguisedMarowak;
    }
}
