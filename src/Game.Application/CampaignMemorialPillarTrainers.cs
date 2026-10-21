using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsMemorialPillarTrainer(TrainerId trainer) =>
        TrainerRegisteredOnMap(trainer, "map.five-island-memorial-pillar");
    private bool IsMemorialPillarTrainerMap(MapId map) =>
        map.Value == "map.five-island-memorial-pillar" && TrainerOccurrences.ActorMaps.Contains(map);

    private void RequireMemorialPillarTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro) =>
        RequireOrdinaryTrainerOccurrence(state, trainer, source, noIntro,
            "memorial_pillar_trainer_source_occurrence");

    // The existing no-VS-Seeker post-battle branch still authenticates the actual source query.
    private void RequireMemorialPillarRematchQuery(CampaignState state, StoryEffect effect)
    {
        if (!IsMemorialPillarTrainerMap(state.World.Player.MapId) || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            !TrainerOccurrences.ByActor.TryGetValue((state.World.Player.MapId, id), out var occurrence) ||
            occurrence.Source.Definition.ScriptEntry != actor.ScriptId)
            throw new InvalidDataException("memorial_pillar_rematch_query_context");
        var source = occurrence.Source.Definition;
        if (effect.Source.Path != source.Source.Path || effect.Source.Symbol != source.ScriptEntry ||
            effect.Source.Line != source.Source.StartLine + 2 || effect.Opcode != "specialvar" ||
            !effect.Args.SequenceEqual(new[] { "VAR_RESULT", "ShouldTryRematchBattle" }))
            throw new InvalidDataException("memorial_pillar_rematch_query_source");
    }
    private void RequireMemorialPillarRematchContinuation(CampaignState state, StoryEffect effect,
        ForestTrainerBattleDefinition source)
    {
        RequireMemorialPillarTrainerOccurrence(state, source.Trainer, source, false);
        var program = _content.FindProgram(source.ScriptEntry + "Rematch");
        var instruction = program?.Instructions.SingleOrDefault(i => i.Op == StoryOp.TrainerBattle);
        if (instruction is null || instruction.Opcode != "trainerbattle_rematch" ||
            instruction.Source.Path != source.Source.Path || instruction.Source.Symbol != effect.Source.Symbol ||
            instruction.Source.Line != source.Source.EndLine + 1 || effect.Source.Line != instruction.Source.Line ||
            !instruction.Args.SequenceEqual(effect.Args))
            throw new InvalidDataException("memorial_pillar_rematch_continuation_source");
    }
}
