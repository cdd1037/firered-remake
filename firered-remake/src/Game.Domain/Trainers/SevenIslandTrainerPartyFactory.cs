using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>Exact original singles and preset doubles through the existing trainer construction kernel.</summary>
public static class SevenIslandTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-seven-island-trainers-v1";
    public static readonly ImmutableHashSet<TrainerId> TrainerIds = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_CRUSH_GIRL_CYNDY"),
        new("pret:TRAINER_TAMER_EVAN"),
        new("pret:TRAINER_PKMN_RANGER_JACKSON"),
        new("pret:TRAINER_PKMN_RANGER_KATELYN"),
        new("pret:TRAINER_COOLTRAINER_LEROY"),
        new("pret:TRAINER_COOLTRAINER_MICHELLE"),
        new("pret:TRAINER_COOL_COUPLE_LEX_NYA"),
        new("pret:TRAINER_AROMA_LADY_MIAH"),
        new("pret:TRAINER_YOUNG_COUPLE_EVE_JON"),
        new("pret:TRAINER_PKMN_RANGER_NICOLAS"),
        new("pret:TRAINER_PKMN_RANGER_MADELINE"),
        new("pret:TRAINER_JUGGLER_MASON"),
        new("pret:TRAINER_RUIN_MANIAC_BRANDON"),
        new("pret:TRAINER_RUIN_MANIAC_BENJAMIN"),
        new("pret:TRAINER_PAINTER_EDNA"),
        new("pret:TRAINER_GENTLEMAN_CLIFFORD"),
        new("pret:TRAINER_PSYCHIC_DARIO"),
        new("pret:TRAINER_PSYCHIC_RODETTE")]);
    public static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "SevenIsland_SevaultCanyon_EventScript_Cyndy",
        "SevenIsland_SevaultCanyon_EventScript_Evan",
        "SevenIsland_SevaultCanyon_EventScript_Jackson",
        "SevenIsland_SevaultCanyon_EventScript_Katelyn",
        "SevenIsland_SevaultCanyon_EventScript_Leroy",
        "SevenIsland_SevaultCanyon_EventScript_Michelle",
        "SevenIsland_SevaultCanyon_EventScript_Lex",
        "SevenIsland_SevaultCanyon_EventScript_Nya",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Miah",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Eve",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Jon",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Nicolas",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Madeline",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Mason",
        "SevenIsland_TanobyRuins_EventScript_Brandon",
        "SevenIsland_TanobyRuins_EventScript_Benjamin",
        "SevenIsland_TanobyRuins_EventScript_Edna",
        "SevenIsland_TanobyRuins_EventScript_Clifford",
        "SevenIsland_TrainerTower_EventScript_Dario",
        "SevenIsland_TrainerTower_EventScript_Rodette");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [new("pret:TRAINER_CRUSH_GIRL_CYNDY"), new("pret:TRAINER_TAMER_EVAN"), new("pret:TRAINER_PKMN_RANGER_JACKSON"), new("pret:TRAINER_PKMN_RANGER_KATELYN"), new("pret:TRAINER_COOLTRAINER_LEROY"), new("pret:TRAINER_COOLTRAINER_MICHELLE"), new("pret:TRAINER_COOL_COUPLE_LEX_NYA"), new("pret:TRAINER_AROMA_LADY_MIAH"), new("pret:TRAINER_YOUNG_COUPLE_EVE_JON"), new("pret:TRAINER_PKMN_RANGER_NICOLAS"), new("pret:TRAINER_PKMN_RANGER_MADELINE"), new("pret:TRAINER_JUGGLER_MASON"), new("pret:TRAINER_RUIN_MANIAC_BRANDON"), new("pret:TRAINER_RUIN_MANIAC_BENJAMIN"), new("pret:TRAINER_PAINTER_EDNA"), new("pret:TRAINER_GENTLEMAN_CLIFFORD"), new("pret:TRAINER_PSYCHIC_DARIO"), new("pret:TRAINER_PSYCHIC_RODETTE")];
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    public static int BasePrize(SevenIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("seven_island_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(SevenIslandTrainerCatalog trainers,
        SevenIslandOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(SevenIslandOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("seven_island_trainer_catalog");
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("seven_island_trainer_species");
        var source = opponents.Mechanics;
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // Custom move replacement is applied after CreateMon, without changing its PID or OT draws.
        return made with { Party = made.Party.Select((p, i) => trainer.Party[i].CustomMoves.IsEmpty ? p : p with {
            Pokemon = p.Pokemon with { Moves = trainer.Party[i].CustomMoves.Select(m => new PokemonMoveSlot(m,
                source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray() }
        }).ToImmutableArray() };
    }
    public static void ValidateCatalog(SevenIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 18 || catalog.BattlesByScript.Count != 20)
            throw new NotSupportedException("seven_island_trainer_scope");
        foreach (var id in TrainerIds)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault) throw new NotSupportedException("seven_island_trainer_source");
        foreach (var script in BattleScripts)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script) throw new NotSupportedException("seven_island_trainer_battle_source");
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("seven_island_trainer_battle_source");
    }
}
