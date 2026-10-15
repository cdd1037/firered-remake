using Game.Domain.WildBattle;
using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Trainers;

public sealed record Route3TrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, ForestTrainerBattleDefinition> Battles);

/// <summary>The eight first-battle Route3 source parties, with natural moves and no items.</summary>
public static class Route3TrainerPartyFactory
{
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = new[] {
        "YOUNGSTER_BEN", "YOUNGSTER_CALVIN", "BUG_CATCHER_COLTON", "BUG_CATCHER_GREG", "BUG_CATCHER_JAMES",
        "LASS_JANICE", "LASS_SALLY", "LASS_ROBIN" }.Select(x => new TrainerId("pret:TRAINER_" + x)).ToImmutableArray();
    public static TrainerPartyCreationResult Create(Route3TrainerCatalog trainers, PokemonCatalog pokemon, TrainerId trainerId, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer)) throw new NotSupportedException("Unknown Route3 trainer.");
        foreach (var member in trainer.Party)
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !Route3TrainerAi.ValidSpecies(species))
                throw new NotSupportedException("Route3 party metadata differs from source.");
        return TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
    }
    public static void ValidateCatalog(Route3TrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(ForestTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        var expected = BattleIdentity(battle.Trainer); string name = expected.Name[..1] + expected.Name[1..].ToLowerInvariant();
        if (battle.Mode != 0 || battle.BattleTypeFlags != 8 || battle.ScriptEntry != "Route3_EventScript_" + name ||
            battle.IntroText != "Route3_Text_" + name + "Intro" || battle.DefeatText != "Route3_Text_" + name + "Defeat" ||
            battle.PostBattleText != "Route3_Text_" + name + "PostBattle" || battle.EntryScript != "EventScript_TryDoNormalTrainerBattle" ||
            battle.Callback != "CB2_EndTrainerBattle") throw new NotSupportedException("Route3 battle declaration differs from source.");
    }
    internal static int[] Encode(string text) => text.Select(c => c == '♀' ? 0xb6 : c == '♂' ? 0xb5 : 0xbb + c - 'A').ToArray();
    private static (int Id, string Name) BattleIdentity(TrainerId id) => id.Value switch
    {
        "pret:TRAINER_YOUNGSTER_BEN" => (89, "BEN"),
        "pret:TRAINER_YOUNGSTER_CALVIN" => (90, "CALVIN"),
        "pret:TRAINER_BUG_CATCHER_COLTON" => (105, "COLTON"),
        "pret:TRAINER_BUG_CATCHER_GREG" => (106, "GREG"),
        "pret:TRAINER_BUG_CATCHER_JAMES" => (107, "JAMES"),
        "pret:TRAINER_LASS_JANICE" => (116, "JANICE"),
        "pret:TRAINER_LASS_SALLY" => (117, "SALLY"),
        "pret:TRAINER_LASS_ROBIN" => (118, "ROBIN"),
        _ => throw new NotSupportedException("Unknown trainer battle binding.")
    };
}
