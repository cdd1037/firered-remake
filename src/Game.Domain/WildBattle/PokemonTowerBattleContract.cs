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
