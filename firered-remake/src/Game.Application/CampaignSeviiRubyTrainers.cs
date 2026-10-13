using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsSeviiRubyTrainer(TrainerId trainer) => _content.SeviiRubyEnabled &&
        _content.SeviiRubyTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    private ForestTrainerBattleDefinition SeviiRubyTrainerScript(TrainerId trainer, string? script)
    {
        var source = script is null
            ? _content.SeviiRubyTrainers!.BattlesByScript.Values.Single(b => b.Trainer == trainer && b.Mode != 4)
            : _content.SeviiRubyTrainers!.BattlesByScript.GetValueOrDefault(script) ?? throw new InvalidDataException("sevii_ruby_trainer_script_missing");
        if (source.Trainer != trainer || source.Mode is not (0 or 3)) throw new InvalidDataException("sevii_ruby_single_script_mismatch");
        return new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, source.IntroText,
            source.DefeatText, source.PostBattleText, source.EntryScript, source.Callback, source.Source);
    }

    private int SeviiRubyTrainerSourceLine(string script) => _content.FindProgram(script)?.Instructions
        .Single(i => i.Op == StoryOp.TrainerBattle).Source.Line ?? throw new InvalidDataException("sevii_ruby_trainer_program_missing");

    private static string SeviiRubyNoIntroActorScript(TrainerId trainer) => trainer.Value switch
    {
        "pret:TRAINER_TEAM_ROCKET_GRUNT_43" => "MtEmber_Exterior_EventScript_Grunt1",
        "pret:TRAINER_TEAM_ROCKET_GRUNT_44" => "MtEmber_Exterior_EventScript_Grunt2",
        _ => throw new InvalidDataException("sevii_ruby_no_intro_trainer")
    };

    private void RequireSeviiRubyTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro)
    {
        if (!IsSeviiRubyTrainer(trainer) || (source.Mode == 3) != noIntro ||
            state.World.Player.MapId != OrdinaryTrainerMap(trainer)) throw new InvalidDataException("sevii_ruby_trainer_occurrence");
        if (!noIntro) return;
        string actorId = trainer.Value == "pret:TRAINER_TEAM_ROCKET_GRUNT_43" ? "LOCALID_MT_EMBER_GRUNT1" : "LOCALID_MT_EMBER_GRUNT2";
        if (state.World.Player.MapId.Value != RubyExteriorMap || state.Script.Variables.GetValueOrDefault(RubyCenterScene) != 4 ||
            !NationalDexEnabledIn(state) || state.LastTalked != actorId ||
            !state.Actors.TryGetValue(actorId, out var actor) || !actor.Active || actor.ScriptId != SeviiRubyNoIntroActorScript(trainer) ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            throw new InvalidDataException("sevii_ruby_grunt_source_occurrence");
    }

    private bool SeviiRubyTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        history.SourceScriptEntry is { } script && history.SourceScriptLine is { } line &&
        _content.SeviiRubyTrainers!.BattlesByScript.TryGetValue(script, out var source) &&
        source.Trainer.Value == history.TrainerId && source.Mode is 0 or 3 && line == SeviiRubyTrainerSourceLine(script);
}
