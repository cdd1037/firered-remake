using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.WildBattle;

/// <summary>Scripted ordinary wild flags. Application authenticates route script, actor and callback.</summary>
public static class SnorlaxBattleContract
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const int SourceBattleTypeFlags = 1 << 17;
    public const int CreationDrawCount = 4;
    public const int AiSetupDrawCount = 4;
    public const int HeldItemDrawCount = 1;
    public static string WildHeldItem(int roll) => roll is >= 0 and < 100 ? "ITEM_CHESTO_BERRY" : throw new ArgumentOutOfRangeException(nameof(roll));
    public static PokemonCreationResult Create(PokemonCatalog catalog, uint originalTrainerId, RngState rng)
    {
        PokemonFactory.ValidateCatalog(catalog);
        if (catalog.Species.Count != 102) throw new NotSupportedException("snorlax_catalog_required");
        return PokemonFactory.Create(catalog, new(new("pret:SPECIES_SNORLAX"),30,originalTrainerId,IvPolicy.Random,PersonalityPolicy.Random),rng);
    }
    public static bool SourceInitialInstance(PokemonInstance mon) => mon.SourceSpeciesId == 143 && mon.Level == 30 &&
        mon.Experience == 33750 && mon.Evs == SixStats.Zero && mon.CurrentHp == mon.Stats.MaxHp && mon.Friendship == 70 &&
        mon.Moves.Select(x => (x.Move.Value,x.CurrentPp,x.MaximumPp,x.PpUps)).SequenceEqual(new[] {
            ("pret:MOVE_HEADBUTT",15,15,0),("pret:MOVE_YAWN",10,10,0),("pret:MOVE_REST",10,10,0),("pret:MOVE_SNORE",15,15,0) });
}
public static partial class WildBattleRules
{
    public static WildBattleResult StartSnorlax(PokemonCatalog catalog, WildBattleCombatant player,
        WildBattleCombatant opponent, RngState rng, WildBattleTerrain terrain = WildBattleTerrain.Plain)
    {
        if (catalog?.Species.Count != 102) return Fail(null,"snorlax_catalog_required");
        if (opponent?.Pokemon is not {} mon || !SnorlaxBattleContract.SourceInitialInstance(mon) || opponent.Status != 0 || opponent.HeldItem != "ITEM_NONE" || opponent.KnockedOffItem is not null)
            return Fail(null,"snorlax_scripted_identity");
        return StartCore(catalog,player,opponent,rng,SnorlaxRulesVersion,terrain,false,null,true);
    }
    internal static bool SnorlaxSleepTalkCandidate(PokemonCatalog catalog, WildBattleCombatant mon, WildBattleCombatant? foe, int slot)
    {
        if (slot < 0 || slot >= mon.Pokemon.Moves.Length) return false;
        var id = mon.Pokemon.Moves[slot].Move; var move = catalog.Moves[id];
        if (id.Value is "pret:MOVE_NONE" or "pret:MOVE_SLEEP_TALK" or "pret:MOVE_ASSIST" or "pret:MOVE_MIRROR_MOVE" or "pret:MOVE_METRONOME" or "pret:MOVE_FOCUS_PUNCH" or "pret:MOVE_UPROAR") return false;
        if (move.Effect is "EFFECT_SKULL_BASH" or "EFFECT_RAZOR_WIND" or "EFFECT_SKY_ATTACK" or "EFFECT_SOLAR_BEAM" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_BIDE") return false;
        if (mon.DisabledMove == id && mon.DisableTurnsRemaining > 0) return false;
        if (foe?.Imprison == true && foe.Pokemon.Moves.Any(m => m.Move == id)) return false;
        if (mon.EncoreTurnsRemaining > 0 && mon.EncoreMoveSlot is {} encore && mon.Pokemon.Moves[encore].Move != id) return false;
        return true;
    }
    private static string? ValidateSnorlaxKind(WildBattleState state)
    {
        if (state.RulesVersion != SnorlaxRulesVersion && (state.ScriptedSnorlax || state.Player.YawnTurnsRemaining != 0 || state.Opponent.YawnTurnsRemaining != 0)) return "snorlax_state_profile";
        if (!state.ScriptedSnorlax) return null;
        if (state.TowerBattleKind is not null || state.Opponent.Pokemon.SourceSpeciesId != 143 || state.Opponent.Pokemon.Level != 30) return "snorlax_scripted_identity";
        return null;
    }
}
