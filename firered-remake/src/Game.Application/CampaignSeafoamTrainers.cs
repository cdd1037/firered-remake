using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsSeafoamDoubleTrainer(TrainerId id) => _content.SeafoamEnabled && SeafoamDoubleTrainerEncounter.TrainerIds.Contains(id);
    private string SeafoamDoubleTrainerClass(TrainerId id) => DoubleTrainer(id).TrainerClass switch
    { "TRAINER_CLASS_SIS_AND_BRO" => "SIS AND BRO", _ => throw new InvalidDataException("seafoam_double_class") };
    private string SeafoamDoubleEncounterMusic(TrainerId id) => (DoubleTrainer(id).EncounterMusicGender & 0x7f) switch
    { 1 or 2 or 9 => "MUS_ENCOUNTER_GIRL", 0 or 4 or 5 or 8 or 10 or 11 or 12 or 13 => "MUS_ENCOUNTER_BOY", _ => "MUS_ENCOUNTER_ROCKET" };
    private static bool SeafoamTrainerMap(MapId map) => map.Value is "map.route-19" or "map.route-20";
    private static bool SeafoamTrainerActorMap(MapId map, string script) =>
        (map.Value == "map.route-19" && script.StartsWith("Route19_EventScript_", StringComparison.Ordinal)) ||
        (map.Value == "map.route-20" && script.StartsWith("Route20_EventScript_", StringComparison.Ordinal));
    private bool IsSeafoamTrainer(TrainerId trainer) => _content.SeafoamEnabled &&
        _content.SeafoamTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    private bool IsSeafoamDoubleScript(string script) => _content.SeafoamEnabled &&
        _content.SeafoamTrainers?.BattlesByScript.TryGetValue(script, out var source) == true &&
        source.Mode == 4 && _content.SeafoamTrainers.Trainers[source.Trainer].DoubleBattle;

    private int SeafoamTrainerSourceLine(string script)
    {
        bool rematch = script.EndsWith("Rematch", StringComparison.Ordinal);
        var entry = rematch ? script[..^7] : script;
        if (_content.SeafoamTrainers?.BattlesByScript.TryGetValue(entry, out var source) != true || source is null)
            throw new InvalidDataException("seafoam_trainer_script_missing");
        SeafoamTrainerPartyFactory.ValidateBattle(source);
        string opcode = source.Mode == 4 ? rematch ? "trainerbattle_rematch_double" : "trainerbattle_double"
            : rematch ? "trainerbattle_rematch" : "trainerbattle_single";
        string[] args = source.Mode == 4
            ? [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText, source.NotEnoughMonsText!]
            : [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText];
        // Every authenticated first wrapper is followed by its own original rematch
        // wrapper. The campaign has no VS Seeker; those branches only show post text.
        return RequireTrainerInstruction(source, script, opcode, args, source.Source.StartLine + (rematch ? 8 : 1), "seafoam_trainer_program_missing");
    }

    private void RequireSeafoamTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro) =>
        RequireOrdinaryTrainerOccurrence(state, trainer, source, noIntro, TrainerRegion.Seafoam, "seafoam_trainer_source_occurrence");

    private void RequireSeafoamDoubleOccurrence(CampaignState state, StoryEffect effect, string script, bool rematch)
    {
        if (!IsSeafoamDoubleScript(script) || !SeafoamTrainerActorMap(state.World.Player.MapId, script) ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.ScriptId != script ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            effect.Source.Line != SeafoamTrainerSourceLine(script + (rematch ? "Rematch" : "")))
            throw new InvalidDataException("seafoam_double_source_occurrence");
    }

    private void RequireSeafoamRematchQuery(CampaignState state, StoryEffect effect)
    {
        if (!_content.SeafoamEnabled || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active ||
            !SeafoamTrainerActorMap(state.World.Player.MapId, actor.ScriptId) ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            !_content.SeafoamTrainers!.BattlesByScript.TryGetValue(actor.ScriptId, out var source))
            throw new InvalidDataException("seafoam_rematch_query_context");
        SeafoamTrainerPartyFactory.ValidateBattle(source);
        if (effect.Source.Path != source.Source.Path || effect.Source.Symbol != source.ScriptEntry ||
            effect.Source.Line != source.Source.StartLine + 2 || effect.Opcode != "specialvar" ||
            !effect.Args.SequenceEqual(new[] { "VAR_RESULT", "ShouldTryRematchBattle" }))
            throw new InvalidDataException("seafoam_rematch_query_source");
    }

    private void RequireSeafoamSingleRematch(CampaignState state, StoryEffect effect,
        ForestTrainerBattleDefinition source)
    {
        RequireSeafoamTrainerOccurrence(state, source.Trainer, source, false);
        if (effect.Source.Line != SeafoamTrainerSourceLine(effect.Source.Symbol))
            throw new InvalidDataException("seafoam_rematch_source_line");
    }

    private bool SeafoamTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        OrdinaryTrainerHistoryOriginMatches(history, TrainerRegion.Seafoam);
}
