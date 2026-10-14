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

    private static CelioOwnedPokemonCatalog? CelioSourceOwnership(OwnedBattlePokemonCatalog? owned) =>
        OwnedBattlePokemonCatalog.SourceRoot(owned) is { } source
            ? CelioOwnedPokemonCatalog.For(source.Catalog) ?? CeruleanCaveOwnedPokemonCatalog.For(source.Catalog)?.Predecessor ?? SevenIslandOwnedPokemonCatalog.For(source.Catalog)?.Predecessor.Predecessor ?? (EventIslandOwnedPokemonCatalog.For(source.Catalog) ?? PowerPlantOwnedPokemonCatalog.For(source.Catalog)?.Predecessor)?.Predecessor.Predecessor.Predecessor
            : null;

    private static bool AdmitsOpponent(OwnedBattlePokemonCatalog owned, SourceWildEncounterContext context, WildBattleCombatant opponent)
    {
        var mon = opponent.Pokemon;
        if (!owned.Catalog.Species.ContainsKey(mon.Species) || OwnedBattlePokemonCatalog.SourceRoot(owned) is not { } source) return false;
        if (context.SourceTable is { } sourceTable)
            return SourceTableWildBattleContract.SourceInitialInstance(owned, sourceTable, mon) &&
                opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.Seafoam is {} seafoam)
            return (SeafoamOwnedPokemonCatalog.For(owned.Catalog) ?? SuperRodOwnedPokemonCatalog.For(owned.Catalog)?.Predecessor) is {} proof &&
                SeafoamPokemonFactory.SourceInitialInstance(proof,seafoam,mon) && opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.Fishing is { } fishing)
            return FishingBattleContract.SourceInitialInstance(owned, fishing, mon) && opponent.Status == 0 && opponent.HeldItem == "ITEM_NONE";
        if (context.CelioRoamer is { } roamer)
            return CelioSourceOwnership(owned) is not null && CelioRoamerBattleContract.MatchesIdentity(roamer, mon);
        var five = FiveIslandOwnedPokemonCatalog.For(source.Catalog) ?? CelioSourceOwnership(source)?.Predecessor;
        var six = SixIslandOwnedPokemonCatalog.For(source.Catalog) ?? five?.Predecessor;
        var four = FourIslandOwnedPokemonCatalog.For(source.Catalog) ?? six?.Predecessor;
        var ruby = SeviiRubyOwnedPokemonCatalog.For(source.Catalog) ?? four?.Predecessor;
        var cinnabar = CinnabarOwnedPokemonCatalog.For(source.Catalog) ?? ruby?.Predecessor;
        var events = EventIslandOwnedPokemonCatalog.For(source.Catalog) ?? PowerPlantOwnedPokemonCatalog.For(source.Catalog)?.Predecessor;
        var seven = SevenIslandOwnedPokemonCatalog.For(source.Catalog) ?? events?.Predecessor;
        bool sourceEntry = context switch {
            { PowerPlant: { } origin } => PowerPlantOwnedPokemonCatalog.For(source.Catalog) is { } plant && PowerPlantPokemonFactory.SourceInitialInstance(plant, origin, mon),
            { EventIsland: { } origin } => events is not null && EventIslandPokemonFactory.SourceInitialInstance(events, origin, mon),
            { SevenIsland: { } origin } => seven is not null && SevenIslandWildPokemonFactory.SourceInitialInstance(seven, origin, mon),
            { CeruleanCave: { } origin } => (CeruleanCaveOwnedPokemonCatalog.For(source.Catalog) ?? seven?.Predecessor) is { } cave &&
                CeruleanCaveWildPokemonFactory.SourceInitialInstance(cave, origin, mon),
            { FiveIsland: { } origin } => five is not null && FiveIslandWildPokemonFactory.SourceInitialInstance(five, origin, mon),
            { SixIsland: { } origin } => six is not null && SixIslandWildPokemonFactory.SourceInitialInstance(six, origin, mon),
            { FourIsland: { } origin } => four is not null && FourIslandWildPokemonFactory.SourceInitialInstance(four, origin, mon),
            { SeviiRuby: { } origin } => ruby is not null && SeviiRubyWildPokemonFactory.SourceInitialInstance(ruby, origin, mon),
            { VictoryRoad: { } origin } => cinnabar is not null && VictoryRoadWildPokemonFactory.SourceInitialInstance(cinnabar, origin, mon),
            { FirstSeviiWild: true } => cinnabar is not null && FirstSeviiWildPokemonFactory.WildSpecies.Contains(mon.Species),
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
