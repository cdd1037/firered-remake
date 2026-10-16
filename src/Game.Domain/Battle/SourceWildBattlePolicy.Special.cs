using Game.Domain.Encounters;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public static partial class SourceWildBattlePolicy
{
    internal static PokemonTowerBattlePolicy? TowerPolicy(SourceBattleFrame frame) =>
        frame.WildEncounter?.TowerBattleKind is { } kind ? PokemonTowerBattleContract.Policy(kind) : null;
    internal static bool Disguised(SourceBattleFrame frame) => TowerPolicy(frame) is { OrdinaryAttacks: false };

    private static bool AdmitsOpponent(GamePokemonCatalog owned, SourceWildEncounterContext context, WildBattleCombatant opponent)
    {
        var mon = opponent.Pokemon;
        if (!owned.Catalog.Species.ContainsKey(mon.Species)) return false;
        if (context.OrdinaryLand is { } ordinary)
            return ordinary.SourceInitialInstance(owned, mon) && opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.RockSmash is { } rock)
            return RockSmashWildBattleContract.SourceInitialInstance(owned, rock, mon) &&
                opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.SourceTable is { } sourceTable)
            return SourceTableWildBattleContract.SourceInitialInstance(owned, sourceTable, mon) &&
                opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.Seafoam is {} seafoam)
            return SeafoamPokemonFactory.SourceInitialInstance(owned,seafoam,mon,context.FishingTables) && opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.Fishing is { } fishing)
            return FishingBattleContract.SourceInitialInstance(owned, fishing, mon, context.FishingTables) && opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.CelioRoamer is { } roamer)
            return CelioRoamerBattleContract.MatchesIdentity(roamer, mon);
        bool sourceEntry = context switch {
            { PowerPlant: { } origin } => PowerPlantPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { EventIsland: { } origin } => EventIslandPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { SevenIsland: { } origin } => SevenIslandWildPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { CeruleanCave: { } origin } => CeruleanCaveWildPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { FiveIsland: { } origin } => FiveIslandWildPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { SixIsland: { } origin } => SixIslandWildPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { FourIsland: { } origin } => FourIslandWildPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { SeviiRuby: { } origin } => SeviiRubyWildPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { VictoryRoad: { } origin } => VictoryRoadWildPokemonFactory.SourceInitialInstance(owned, origin, mon),
            { FirstSeviiWild: true } => FirstSeviiWildPokemonFactory.WildSpecies.Contains(mon.Species),
            _ => owned.AdmitsWildOpponent(mon)
        };
        bool regional = context.PowerPlant is not null || context.EventIsland is not null || context.SevenIsland is not null || context.CeruleanCave is not null || context.FiveIsland is not null || context.SixIsland is not null || context.FourIsland is not null ||
            context.SeviiRuby is not null || context.VictoryRoad is not null;
        return sourceEntry && (!regional || opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE");
    }

    private static string? ValidateSpecialEntry(SourceWildEncounterContext context, WildBattleCombatant opponent)
    {
        var mon = opponent.Pokemon;
        if (context.LostelleHypno && (!context.FirstSeviiWild || !FirstSeviiHypnoBattleContract.SourceInitialInstance(mon) ||
            opponent.Status != 0 || opponent.HeldItem != "ITEM_NONE")) return "first_sevii_hypno_source_identity";
        if (context.ScriptedSnorlax && (!SnorlaxBattleContract.SourceInitialInstance(mon) || opponent.Status != 0 || opponent.HeldItem != "ITEM_NONE"))
            return "snorlax_scripted_identity";
        if (ValidateTower(context, mon, WildBattleOutcome.Ongoing) is { } invalid) return invalid;
        if (context.TowerBattleKind == PokemonTowerBattleKind.RevealedMarowak &&
            (opponent.HeldItem != "ITEM_NONE" || !mon.Moves.Select(m => m.Move.Value).SequenceEqual(new[] {
                "pret:MOVE_HEADBUTT", "pret:MOVE_LEER", "pret:MOVE_FOCUS_ENERGY", "pret:MOVE_BONEMERANG" })))
            return "pokemon_tower_revealed_marowak_identity";
        return null;
    }

    private static string? ValidateTower(SourceWildEncounterContext context, PokemonInstance mon, WildBattleOutcome outcome)
    {
        if (context.TowerBattleKind is not { } kind) return null;
        if (!Enum.IsDefined(kind)) return "pokemon_tower_battle_kind";
        if (kind is PokemonTowerBattleKind.OrdinaryWild or PokemonTowerBattleKind.DisguisedWild && mon.SourceSpeciesId is not (92 or 93 or 104))
            return "pokemon_tower_disguised_species";
        if (kind is PokemonTowerBattleKind.DisguisedMarowak or PokemonTowerBattleKind.RevealedMarowak &&
            (mon.SourceSpeciesId != 105 || mon.Level != 30)) return "pokemon_tower_marowak_identity";
        if (kind == PokemonTowerBattleKind.RevealedMarowak && (mon.Gender != PokemonGender.Female || mon.Nature != Nature.Serious ||
            mon.Ivs != new SixStats(31, 31, 31, 31, 31, 31) || mon.Evs != SixStats.Zero)) return "pokemon_tower_revealed_marowak_identity";
        if (outcome == WildBattleOutcome.Caught && PokemonTowerBattleContract.BallDodges(kind)) return "pokemon_tower_ghost_cannot_be_caught";
        return null;
    }
}
