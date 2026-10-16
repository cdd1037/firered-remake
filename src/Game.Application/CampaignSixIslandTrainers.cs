using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsSixIslandDoubleScript(string script) => _content.SixIslandEnabled &&
        _content.SixIslandTrainers?.BattlesByScript.TryGetValue(script, out var source) == true &&
        source.Trainer == SixIslandTrainerPartyFactory.DoubleTrainer && source.Mode == 4;

    private int SixIslandTrainerSourceLine(string script)
    {
        bool rematch = script.EndsWith("Rematch", StringComparison.Ordinal);
        var entry = rematch ? script[..^7] : script;
        if (_content.SixIslandTrainers?.BattlesByScript.TryGetValue(entry, out var source) != true || source is null)
            throw new InvalidDataException("six_island_trainer_script_missing");
        SixIslandTrainerPartyFactory.ValidateBattle(source);
        string opcode = source.Mode == 4 ? rematch ? "trainerbattle_rematch_double" : "trainerbattle_double"
            : rematch ? "trainerbattle_rematch" : "trainerbattle_single";
        string[] args = source.Mode == 4
            ? [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText, source.NotEnoughMonsText!]
            : [source.Trainer.Value[5..], rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText, source.DefeatText];
        // Every authenticated first wrapper is followed by its own original rematch
        // wrapper. The campaign has no VS Seeker; those branches only show post text.
        return RequireTrainerInstruction(source, script, opcode, args, source.Source.StartLine + (rematch ? 8 : 1), "six_island_trainer_program_missing");
    }

    private void RequireSixIslandDoubleOccurrence(CampaignState state, StoryEffect effect, string script, bool rematch)
    {
        if (!IsSixIslandDoubleScript(script) || state.World.Player.MapId.Value != "map.six-island-water-path" ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.ScriptId != script ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            effect.Source.Line != SixIslandTrainerSourceLine(script + (rematch ? "Rematch" : "")))
            throw new InvalidDataException("six_island_double_source_occurrence");
    }
}
