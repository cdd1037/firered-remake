using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record ForestTrainerBattleDefinition(TrainerId Trainer, int Mode, uint BattleTypeFlags,
    string ScriptEntry, string IntroText, string DefeatText, string PostBattleText,
    string EntryScript, string Callback, TrainerSourceRef Source);

public sealed record ForestTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, ForestTrainerBattleDefinition> Battles);

/// <summary>The five original Viridian Forest Bug Catchers, no held items or custom moves.</summary>
public static class ForestTrainerPartyFactory
{
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        new[] { "RICK", "DOUG", "SAMMY", "ANTHONY", "CHARLIE" }
            .Select(x => new TrainerId("pret:TRAINER_BUG_CATCHER_" + x)).ToImmutableArray();

    public static TrainerPartyCreationResult Create(ForestTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {
        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Only the five Viridian Forest trainers are supported.");
        return TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
    }

    public static void ValidateCatalog(ForestTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(ForestTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (!SupportedTrainers.Contains(battle.Trainer)) throw new NotSupportedException("Unknown forest trainer.");
        string upper = battle.Trainer.Value["pret:TRAINER_BUG_CATCHER_".Length..];
        string name = upper[..1] + upper[1..].ToLowerInvariant();
        if (battle.Mode != 0 || battle.BattleTypeFlags != 8 || battle.ScriptEntry != "ViridianForest_EventScript_" + name ||
            battle.IntroText != "ViridianForest_Text_" + name + "Intro" || battle.DefeatText != "ViridianForest_Text_" + name + "Defeat" ||
            battle.PostBattleText != "ViridianForest_Text_" + name + "PostBattle" ||
            battle.EntryScript != "EventScript_TryDoNormalTrainerBattle" || battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("Unsupported forest normal-single battle parameters.");
    }

}
