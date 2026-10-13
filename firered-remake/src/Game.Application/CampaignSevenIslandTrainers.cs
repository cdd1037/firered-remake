using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsSevenIslandDoubleTrainer(TrainerId id) => _content.SevenIslandEnabled && SevenIslandDoubleTrainerEncounter.TrainerIds.Contains(id);
    private string SevenIslandDoubleTrainerClass(TrainerId id) => DoubleTrainer(id).TrainerClass switch
    { "TRAINER_CLASS_YOUNG_COUPLE" => "YOUNG COUPLE", "TRAINER_CLASS_COOL_COUPLE" => "COOL COUPLE", _ => throw new InvalidDataException("seven_island_double_class") };
    private string SevenIslandDoubleEncounterMusic(TrainerId id) => (DoubleTrainer(id).EncounterMusicGender & 0x7f) switch
    { 1 or 2 or 9 => "MUS_ENCOUNTER_GIRL", 0 or 4 or 5 or 8 or 10 or 11 or 12 or 13 => "MUS_ENCOUNTER_BOY", _ => "MUS_ENCOUNTER_ROCKET" };
    private static bool SevenIslandTrainerActorMap(MapId map, string script) =>
        (map.Value == "map.seven-island-sevault-canyon-entrance" && script.StartsWith("SevenIsland_SevaultCanyon_Entrance_", StringComparison.Ordinal)) ||
        (map.Value == "map.seven-island-sevault-canyon" && script.StartsWith("SevenIsland_SevaultCanyon_", StringComparison.Ordinal) && !script.StartsWith("SevenIsland_SevaultCanyon_Entrance_", StringComparison.Ordinal));
    private bool IsSevenIslandTrainer(TrainerId trainer) => _content.SevenIslandEnabled &&
        _content.SevenIslandTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    private bool IsSevenIslandDoubleScript(string script) => _content.SevenIslandEnabled &&
        _content.SevenIslandTrainers?.BattlesByScript.TryGetValue(script, out var source) == true &&
        source.Mode == 4 && _content.SevenIslandTrainers.Trainers[source.Trainer].DoubleBattle;

    private int SevenIslandTrainerSourceLine(string script)
    {
        bool rematch = script.EndsWith("Rematch", StringComparison.Ordinal);
        var entry = rematch ? script[..^7] : script;
        if (_content.SevenIslandTrainers?.BattlesByScript.TryGetValue(entry, out var source) != true || source is null)
            throw new InvalidDataException("seven_island_trainer_script_missing");
        SevenIslandTrainerPartyFactory.ValidateBattle(source);
        string opcode = source.Mode == 4 ? rematch ? "trainerbattle_rematch_double" : "trainerbattle_double"
            : rematch ? "trainerbattle_rematch" : "trainerbattle_single";
        string[] args = source.Mode == 4
            ? [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText, source.NotEnoughMonsText!]
            : [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText];
        // Every authenticated first wrapper is followed by its own original rematch
        // wrapper. The campaign has no VS Seeker; those branches only show post text.
        return RequireTrainerInstruction(source, script, opcode, args, source.Source.StartLine + (rematch ? 8 : 1), "seven_island_trainer_program_missing");
    }

    private void RequireSevenIslandTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro) =>
        RequireOrdinaryTrainerOccurrence(state, trainer, source, noIntro, TrainerRegion.SevenIsland, "seven_island_trainer_source_occurrence");

    private void RequireSevenIslandDoubleOccurrence(CampaignState state, StoryEffect effect, string script, bool rematch)
    {
        if (!IsSevenIslandDoubleScript(script) || !SevenIslandTrainerActorMap(state.World.Player.MapId, script) ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.ScriptId != script ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            effect.Source.Line != SevenIslandTrainerSourceLine(script + (rematch ? "Rematch" : "")))
            throw new InvalidDataException("seven_island_double_source_occurrence");
    }

    private bool SevenIslandTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        OrdinaryTrainerHistoryOriginMatches(history, TrainerRegion.SevenIsland);
}
