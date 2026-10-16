using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private sealed record DoubleTrainerOccurrence(TrainerDefinition Trainer, SourceTwinsBattleDefinition Interaction,
        ImmutableArray<TrainerActorLink> Actors, StoryInstruction FirstBattle, StoryInstruction? Rematch, StoryInstruction? RematchQuery);
    private sealed record DoubleTrainerCatalog(
        ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
        ImmutableDictionary<string, DoubleTrainerOccurrence> Scripts);
    private DoubleTrainerCatalog? _doubleTrainerCatalog;
    private DoubleTrainerCatalog DoubleTrainers => _doubleTrainerCatalog ??= BindDoubleTrainers();

    // Bind immutable definitions, compiled calls and map actors once at composition.
    // New rows in an existing catalog need no map-name or trainer-ID admission list.
    private DoubleTrainerCatalog BindDoubleTrainers()
    {
        var trainers = ImmutableDictionary.CreateBuilder<TrainerId, TrainerDefinition>();
        var scripts = ImmutableDictionary.CreateBuilder<string, DoubleTrainerOccurrence>(StringComparer.Ordinal);
        var actors = _content.Maps.Keys.SelectMany(map => _content.MapDefinition(map).Actors.Select(actor =>
            new TrainerActorLink(map, actor.LocalId, actor.SourceLocalId, actor.ScriptId)))
            .GroupBy(actor => actor.Script, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToImmutableArray(), StringComparer.Ordinal);

        StoryInstruction? Instruction(TrainerDefinition trainer, SourceTwinsBattleDefinition source, bool rematch)
        {
            string script = source.ScriptEntry + (rematch ? "Rematch" : "");
            var program = _content.FindProgram(script);
            if (program is null && rematch) return null;
            string[] args = [trainer.Id.Value[5..],
                rematch ? source.IntroText.Replace("Intro", "RematchIntro", StringComparison.Ordinal) : source.IntroText,
                source.DefeatText, source.NotEnoughMonsText];
            var matches = program?.Instructions.Where(instruction => instruction.Op == StoryOp.TrainerBattle &&
                instruction.Source.Path == source.Source.Path && instruction.Source.Symbol == script &&
                instruction.Opcode == (rematch ? "trainerbattle_rematch_double" : "trainerbattle_double") &&
                instruction.Args.SequenceEqual(args)).ToArray() ?? [];
            if (matches.Length != 1)
                throw new InvalidDataException("double_trainer_instruction_not_unique:" + script);
            return matches[0];
        }
        void Add(TrainerDefinition trainer, SourceTwinsBattleDefinition source)
        {
            if (!trainer.DoubleBattle || trainer.Party.Length < 2 || string.IsNullOrWhiteSpace(source.NotEnoughMonsText) ||
                !trainer.TrainerClass.StartsWith("TRAINER_CLASS_", StringComparison.Ordinal))
                throw new InvalidDataException("double_trainer_definition_mismatch:" + trainer.Id.Value);
            if (!actors.TryGetValue(source.ScriptEntry, out var links) || links.IsEmpty)
                throw new InvalidDataException("double_trainer_actor_missing:" + source.ScriptEntry);
            if (trainers.TryGetValue(trainer.Id, out var existing) && existing != trainer)
                throw new InvalidDataException("double_trainer_definition_conflict:" + trainer.Id.Value);
            trainers[trainer.Id] = trainer;
            var occurrence = new DoubleTrainerOccurrence(trainer, source, links,
                Instruction(trainer, source, false)!, Instruction(trainer, source, true),
                _content.FindProgram(source.ScriptEntry)!.Instructions.SingleOrDefault(instruction =>
                    instruction.Source.Path == source.Source.Path && instruction.Source.Symbol == source.ScriptEntry &&
                    instruction.Opcode == "specialvar" && instruction.Args.SequenceEqual(new[] { "VAR_RESULT", "ShouldTryRematchBattle" })));
            if (!scripts.TryAdd(source.ScriptEntry, occurrence))
                throw new InvalidDataException("double_trainer_script_duplicate:" + source.ScriptEntry);
        }
        void AddCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> definitions,
            IEnumerable<CeruleanTrainerBattleDefinition> sources)
        {
            foreach (var source in sources.Where(source => source.Mode == 4))
            {
                if (source.BattleTypeFlags != 9 || source.RivalFlags != 0 ||
                    source.EntryScript != "EventScript_TryDoDoubleTrainerBattle" || source.Callback != "CB2_EndTrainerBattle")
                    throw new InvalidDataException("double_trainer_battle_semantics:" + source.ScriptEntry);
                if (!definitions.TryGetValue(source.Trainer, out var trainer))
                    throw new InvalidDataException("double_trainer_definition_missing:" + source.Trainer.Value);
                Add(trainer, new(source.ScriptEntry, source.IntroText, source.DefeatText,
                    source.PostBattleText, source.NotEnoughMonsText!, source.Source));
            }
        }
        foreach (var source in TwinsDefinition.Interactions) Add(TwinsDefinition.Trainer, source);
        if (_content.CinnabarTrainerParties is { } cinnabar) AddCatalog(cinnabar.Trainers, cinnabar.BattlesByScript.Values);
        if (_content.FirstSeviiTrainerParties is { } firstSevii) AddCatalog(firstSevii.Trainers, firstSevii.BattlesBySource.Values);
        if (_content.VictoryRoadTrainerParties is { } victoryRoad) AddCatalog(victoryRoad.Trainers, victoryRoad.BattlesByScript.Values);
        if (_content.SeviiRubyTrainers is { } ruby) AddCatalog(ruby.Trainers, ruby.BattlesByScript.Values);
        if (_content.SixIslandTrainers is { } sixIsland) AddCatalog(sixIsland.Trainers, sixIsland.BattlesByScript.Values);
        if (_content.SevenIslandTrainers is { } sevenIsland) AddCatalog(sevenIsland.Trainers, sevenIsland.BattlesByScript.Values);
        if (_content.SeafoamTrainers is { } seafoam) AddCatalog(seafoam.Trainers, seafoam.BattlesByScript.Values);
        if (_content.Route16FlyTrainers is { } route16) AddCatalog(route16.Trainers, route16.BattlesByScript.Values);
        return new(trainers.ToImmutable(), scripts.ToImmutable());
    }

    private bool DoubleTrainerActorMap(MapId map, string script) =>
        DoubleTrainers.Scripts.TryGetValue(script, out var occurrence) && occurrence.Actors.Any(actor => actor.Map == map);

    private void RequireDoubleTrainerOccurrence(CampaignState state, StoryEffect effect, string script, bool rematch)
    {
        if (!DoubleTrainers.Scripts.TryGetValue(script, out var occurrence) ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle)
            throw new InvalidDataException("double_trainer_source_occurrence");
        RequireDoubleTrainerInstruction(state, effect, occurrence, rematch ? occurrence.Rematch : occurrence.FirstBattle);
    }

    private void RequireDoubleTrainerRematchQuery(CampaignState state, StoryEffect effect, string script)
    {
        var occurrence = DoubleTrainers.Scripts[script];
        RequireDoubleTrainerInstruction(state, effect, occurrence, occurrence.RematchQuery);
    }

    private static void RequireDoubleTrainerInstruction(CampaignState state, StoryEffect effect,
        DoubleTrainerOccurrence occurrence, StoryInstruction? instruction)
    {
        if (instruction is null || instruction.Source != effect.Source || instruction.Opcode != effect.Opcode ||
            !instruction.Args.SequenceEqual(effect.Args) || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            !occurrence.Actors.Any(link => link.Map == state.World.Player.MapId && link.LocalId == id &&
                link.SourceLocalId == actor.SourceLocalId && link.Script == actor.ScriptId))
            throw new InvalidDataException("double_trainer_source_occurrence");
    }

    private string DoubleTrainerClass(TrainerId trainer) =>
        DoubleTrainer(trainer).TrainerClass["TRAINER_CLASS_".Length..].Replace('_', ' ');
    private string DoubleEncounterMusic(TrainerId trainer) => (DoubleTrainer(trainer).EncounterMusicGender & 0x7f) switch
    {
        1 or 2 or 9 => "MUS_ENCOUNTER_GIRL",
        0 or 4 or 5 or 8 or 10 or 11 or 12 or 13 => "MUS_ENCOUNTER_BOY",
        _ => "MUS_ENCOUNTER_ROCKET"
    };
}
