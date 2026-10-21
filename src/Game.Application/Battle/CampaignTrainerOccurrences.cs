using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private sealed record RegisteredTrainerSource(ForestTrainerBattleDefinition Definition, string Opcode,
        ImmutableArray<string> Arguments, string? PostBattleScript = null, string? MusicOption = null);
    private sealed record TrainerActorLink(MapId Map, string LocalId, int SourceLocalId, string Script);
    private sealed record RegisteredTrainerOccurrence(RegisteredTrainerSource Source, StoryInstruction Instruction,
        ImmutableArray<TrainerActorLink> Actors, MapId Map);
    private sealed record TrainerOccurrenceCatalog(
        ImmutableDictionary<(TrainerId Trainer, string Script, int Line), RegisteredTrainerOccurrence> ByIdentity,
        ImmutableDictionary<TrainerId, ImmutableArray<RegisteredTrainerOccurrence>> ByTrainer,
        ImmutableDictionary<TrainerId, RegisteredTrainerOccurrence> Primary,
        ImmutableDictionary<(MapId Map, string Actor), RegisteredTrainerOccurrence> ByActor,
        ImmutableHashSet<TrainerId> HistoryIdentities,
        ImmutableHashSet<MapId> ActorMaps);

    private IEnumerable<TrainerId> TrainersOnMap(MapId map) => TrainerOccurrences.ByTrainer
        .Where(entry => entry.Value.Any(occurrence => occurrence.Map == map)).Select(entry => entry.Key);

    private TrainerOccurrenceCatalog? _trainerOccurrences;
    private TrainerOccurrenceCatalog TrainerOccurrences => _trainerOccurrences ??= BindTrainerOccurrences();

    /// <summary>Source rows, exact compiled calls and actor links are joined once, never rescanned per battle.</summary>
    private TrainerOccurrenceCatalog BindTrainerOccurrences()
    {
        var actorLinks = _content.Maps.Keys.SelectMany(map => _content.MapDefinition(map).Actors.Select(actor =>
            new TrainerActorLink(map, actor.LocalId, actor.SourceLocalId, actor.ScriptId)))
            .GroupBy(actor => actor.Script, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToImmutableArray(), StringComparer.Ordinal);
        static string MapName(string name) => name.Replace("_", "", StringComparison.Ordinal).ToUpperInvariant();
        var sourceMaps = _content.Maps.Keys.ToDictionary(
            map => MapName(_content.MapDefinition(map).SourceMapSymbol[4..]), map => map, StringComparer.Ordinal);
        var programs = new Dictionary<string, StoryProgram>(StringComparer.Ordinal);
        var identities = ImmutableDictionary.CreateBuilder<(TrainerId, string, int), RegisteredTrainerOccurrence>();
        var byTrainer = ImmutableDictionary.CreateBuilder<TrainerId, ImmutableArray<RegisteredTrainerOccurrence>>();
        var primary = ImmutableDictionary.CreateBuilder<TrainerId, RegisteredTrainerOccurrence>();
        var maps = ImmutableHashSet.CreateBuilder<MapId>();
        var histories = ImmutableHashSet.CreateBuilder<TrainerId>();
        foreach (var (trainer, binding) in OrdinaryTrainerBindings)
        {
            var occurrences = ImmutableArray.CreateBuilder<RegisteredTrainerOccurrence>();
            foreach (var source in binding.Scripts.Sources.Where(source => source.Definition.Trainer == trainer))
            {
                var definition = source.Definition;
                if (!programs.TryGetValue(definition.ScriptEntry, out var program))
                {
                    program = _content.FindProgram(definition.ScriptEntry)
                        ?? throw new InvalidDataException("trainer_occurrence_program_missing:" + definition.ScriptEntry);
                    programs.Add(definition.ScriptEntry, program);
                }
                var matches = program.Instructions.Where(instruction => instruction.Op == StoryOp.TrainerBattle &&
                    instruction.Source.Path == definition.Source.Path && instruction.Source.Symbol == definition.ScriptEntry &&
                    instruction.Opcode == source.Opcode && instruction.Args.SequenceEqual(source.Arguments)).ToArray();
                if (matches.Length > 1)
                    matches = matches.Where(instruction => instruction.Source.Line >= definition.Source.StartLine &&
                        instruction.Source.Line <= definition.Source.EndLine).ToArray();
                if (matches.Length != 1)
                    throw new InvalidDataException("trainer_occurrence_not_unique:" + trainer.Value + ":" + definition.ScriptEntry);
                var instruction = matches[0];
                ValidateTrainerOccurrenceSemantics(definition, instruction);
                ValidateTrainerPostDialogue(definition, program, instruction);
                var links = actorLinks.GetValueOrDefault(definition.ScriptEntry, []);
                var linkedMaps = links.Select(link => link.Map).Distinct().ToArray();
                string sourceMap = definition.Source.Path.StartsWith("data/maps/", StringComparison.Ordinal)
                    ? definition.Source.Path.Split('/')[2]
                    : definition.ScriptEntry.Split("_EventScript_", StringSplitOptions.None)[0];
                var map = linkedMaps.Length == 1 ? linkedMaps[0]
                    : sourceMaps.TryGetValue(MapName(sourceMap), out var declaredMap) ? declaredMap
                    : throw new InvalidDataException("trainer_occurrence_map_missing:" + trainer.Value);
                var occurrence = new RegisteredTrainerOccurrence(source, instruction, links, map);
                if (!identities.TryAdd((trainer, instruction.Source.Symbol, instruction.Source.Line), occurrence))
                    throw new InvalidDataException("trainer_occurrence_duplicate:" + trainer.Value);
                occurrences.Add(occurrence);
                if (source.Opcode == "trainerbattle_single") maps.UnionWith(links.Select(link => link.Map));
            }
            if (occurrences.Count == 0)
            {
                if (binding.Scripts.Sources.IsEmpty) continue; // Rematch-only rosters have their own typed receipt.
                throw new InvalidDataException("trainer_occurrence_missing:" + trainer.Value);
            }
            var all = occurrences.ToImmutable();
            byTrainer.Add(trainer, all);
            if (all.Select(occurrence => occurrence.Source.Definition.BattleTypeFlags).Distinct().Count() == 1)
                histories.Add(trainer);
            var ordinary = all.Where(occurrence => occurrence.Source.Opcode == "trainerbattle_single" &&
                !occurrence.Actors.IsEmpty).ToArray();
            var linked = all.Where(occurrence => !occurrence.Actors.IsEmpty).ToArray();
            var candidates = ordinary.Length != 0 ? ordinary : linked.Length != 0 ? linked : all.ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException("trainer_primary_occurrence_ambiguous:" + trainer.Value);
            primary.Add(trainer, candidates[0]);
        }
        var byActor = ImmutableDictionary.CreateBuilder<(MapId, string), RegisteredTrainerOccurrence>();
        foreach (var occurrence in primary.Values)
        foreach (var actor in occurrence.Actors)
            if (!byActor.TryAdd((actor.Map, actor.LocalId), occurrence))
                throw new InvalidDataException("trainer_actor_binding_ambiguous:" + actor.Map.Value + ":" + actor.LocalId);
        return new(identities.ToImmutable(), byTrainer.ToImmutable(), primary.ToImmutable(),
            byActor.ToImmutable(), histories.ToImmutable(), maps.ToImmutable());
    }

    // event.inc selects the battle mode from the macro arguments. StartTrainerBattle
    // supplies TRAINER and adds FIRST_BATTLE only for the early-rival tutorial flags.
    private void ValidateTrainerPostDialogue(ForestTrainerBattleDefinition definition,
        StoryProgram program, StoryInstruction battle)
    {
        if (definition.Mode != 0) return;
        var dialogs = program.Instructions.Where(instruction => instruction.Opcode == "msgbox").ToArray();
        if (dialogs.Length != 1 || dialogs[0].Op != StoryOp.Dialog ||
            !dialogs[0].Args.SequenceEqual([definition.PostBattleText, "MSGBOX_AUTOCLOSE"]) ||
            program.Instructions.IndexOf(dialogs[0]) <= program.Instructions.IndexOf(battle) ||
            string.IsNullOrWhiteSpace(definition.PostBattleText) || _content.TextPages(definition.PostBattleText).IsDefaultOrEmpty)
            throw new InvalidDataException("trainer_post_dialogue_binding:" + definition.Trainer.Value);
    }

    private static void ValidateTrainerOccurrenceSemantics(ForestTrainerBattleDefinition definition,
        StoryInstruction instruction)
    {
        var args = instruction.Args;
        int mode;
        uint flags = 1u << 3;
        switch (instruction.Opcode)
        {
            case "trainerbattle_single" when args.Length is >= 3 and <= 5:
                if (args.Length == 5 && args[4] is not ("TRUE" or "FALSE" or "1" or "0" or "NO_MUSIC"))
                    throw new InvalidDataException("trainer_occurrence_music_argument");
                bool continuation = args.Length >= 4 && args[3] is not ("FALSE" or "0");
                bool music = args.Length < 5 || args[4] is "TRUE" or "1";
                mode = !continuation ? 0 : music ? 2 : 1;
                break;
            case "trainerbattle_no_intro" when args.Length == 2:
                mode = 3;
                break;
            case "trainerbattle_earlyrival" when args.Length == 4 &&
                ushort.TryParse(args[1], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var rivalFlags):
                mode = 9;
                if ((rivalFlags & 3) != 0) flags |= 1u << 4;
                break;
            default:
                throw new InvalidDataException("trainer_occurrence_opcode_arguments");
        }
        if (definition.Mode != mode || definition.BattleTypeFlags != flags)
            throw new InvalidDataException("trainer_occurrence_semantics_mismatch:" + definition.Trainer.Value);
    }

    private RegisteredTrainerOccurrence ResolveTrainerOccurrence(TrainerId trainer, string? script = null, int? line = null)
    {
        if (script is null && line is null && TrainerOccurrences.Primary.TryGetValue(trainer, out var primary))
            return primary;
        if (script is not null && line is { } sourceLine &&
            TrainerOccurrences.ByIdentity.TryGetValue((trainer, script, sourceLine), out var exact))
            return exact;
        if (script is not null && line is null && TrainerOccurrences.ByTrainer.TryGetValue(trainer, out var occurrences))
        {
            var matches = occurrences.Where(occurrence => occurrence.Instruction.Source.Symbol == script).ToArray();
            if (matches.Length == 1) return matches[0];
        }
        throw new InvalidDataException("trainer_occurrence_unknown:" + trainer.Value + ":" + script + ":" + line);
    }

    private RegisteredTrainerOccurrence RequireTrainerOccurrence(TrainerId trainer, StoryEffect effect)
    {
        var occurrence = ResolveTrainerOccurrence(trainer, effect.Source.Symbol, effect.Source.Line);
        if (occurrence.Instruction.Source != effect.Source || occurrence.Instruction.Opcode != effect.Opcode ||
            !occurrence.Instruction.Args.SequenceEqual(effect.Args))
            throw new InvalidDataException("trainer_occurrence_effect_mismatch");
        return occurrence;
    }

    private void RequireTrainerActorOccurrence(CampaignState state, RegisteredTrainerOccurrence occurrence)
    {
        if (state.Script.Wait?.Kind != StoryWaitKind.Battle || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            !occurrence.Actors.Any(link => link.Map == state.World.Player.MapId && link.LocalId == id &&
                link.SourceLocalId == actor.SourceLocalId && link.Script == actor.ScriptId))
            throw new InvalidDataException("trainer_actor_occurrence_mismatch");
    }

    // Every occurrence uses the same bound roster. Presentation modes and source
    // coordinates do not distinguish a settled battle; real battle-flag variants do.
    private bool RegisteredTrainerHistoryMatches(CampaignForestTrainerHistory history) =>
        TrainerOccurrences.HistoryIdentities.Contains(new(history.TrainerId));
}
