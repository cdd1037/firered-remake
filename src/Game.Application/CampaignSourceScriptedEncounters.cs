using Game.Domain.World;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed record CampaignSourceScriptedPreparation(CampaignSourceTableReceipt Receipt, Route1WildPokemonResult Generated);
public sealed record CampaignSourceScriptedReturn(string Id, int Outcome);

public sealed partial class CampaignSession
{
    private static string SourceScriptPath(SourceScriptedEncounterDefinition source) =>
        "data/maps/" + source.ScriptId[..source.ScriptId.IndexOf("_EventScript_", StringComparison.Ordinal)] + "/scripts.inc";
    private static bool IsSourceScriptedBattle(CampaignWildBattle battle) =>
        SourceScriptedEncounterRegistry.Find(battle.SourceTableReceipt?.Origin) is not null;
    private void RequireSourceScriptedCaller(CampaignState state, StoryEffect effect, SourceScriptedEncounterDefinition source)
    {
        if (!_content.Maps.ContainsKey(source.Map) || state.World.Player.MapId.Value != source.Map.Value ||
            effect.Source.Path != SourceScriptPath(source) || effect.Source.Symbol != source.ScriptId ||
            state.LastTalked is not { } id || !state.Actors.TryGetValue(id, out var actor) ||
            actor.SourceLocalId != source.LocalId || actor.ScriptId != source.ScriptId ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != source.LocalId)
            throw new InvalidDataException("source_scripted_static_caller");
    }

    private bool TryApplySourceScriptedEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var source = SourceScriptedEncounterRegistry.Definitions.SingleOrDefault(d =>
            d.Map == state.World.Player.MapId && effect.Source.Path == SourceScriptPath(d) &&
            (effect.Source.Symbol == d.ScriptId || effect.Source.Symbol == d.OutcomeScript));
        if (source is null || !_content.Maps.ContainsKey(source.Map)) return false;
        var a = effect.Args;
        if (effect.Opcode == "specialvar" && a.SequenceEqual(new[] { "VAR_RESULT", "GetBattleOutcome" }))
        {
            if (state.SourceScriptedReturn is not { } result || result.Id != source.Id ||
                !(effect.Source.Symbol == source.OutcomeScript || effect.Source.Symbol == source.ScriptId))
                throw new InvalidDataException("source_scripted_outcome_context");
            bool caller = effect.Source.Symbol == source.ScriptId;
            next = state with { SourceScriptedReturn = caller ? null : result,
                Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.Outcome) } };
            return true;
        }
        if (effect.Source.Symbol != source.ScriptId) return false;
        if (effect.Opcode == "special" && a.SequenceEqual(new[] { "QuestLog_CutRecording" }))
        { RequireSourceScriptedCaller(state, effect, source); return true; }
        if (effect.Opcode is "playmoncry" or "waitbuttonpress")
        {
            RequireSourceScriptedCaller(state, effect, source);
            if (SourceScriptedEncounterRegistry.Find(state.SourceScriptedPreparation?.Receipt.Origin) != source ||
                (effect.Opcode == "playmoncry" ? !a.SequenceEqual(new[] { source.Species.Value[5..], "CRY_MODE_ENCOUNTER" }) : a.Length != 0))
                throw new InvalidDataException("source_scripted_encounter_presentation");
            return true;
        }
        if (effect.Opcode == "setwildbattle")
        {
            RequireSourceScriptedCaller(state, effect, source);
            if (!a.SequenceEqual(new[] { source.Species.Value[5..], source.Level.ToString(System.Globalization.CultureInfo.InvariantCulture) }) ||
                state.SourceScriptedPreparation is not null || state.SourceScriptedReturn is not null || !state.Script.Locked ||
                state.Script.Flags.GetValueOrDefault(source.FoughtFlag) || state.Script.Flags.GetValueOrDefault(source.HideFlag) ||
                state.Script.Flags.GetValueOrDefault(SpecialWildFlag)) throw new InvalidDataException("source_scripted_preparation_context");
            var made = SourceTableWildBattleContract.Generate(_binding.Ownership, SourceScriptedEncounterRegistry.Origin(source), source.Level, state.World.Rng, state.TrainerId);
            next = state with { SourceScriptedPreparation = new(new(SourceScriptedEncounterRegistry.Origin(source), state.World.Rng,
                made.GlobalRng, made.Pokemon), made), World = state.World with { Rng = made.GlobalRng } };
            return true;
        }
        if (effect.Opcode == "dowildbattle" || effect.Opcode == "special" && a.SequenceEqual(new[] { "StartLegendaryBattle" }))
        {
            RequireSourceScriptedCaller(state, effect, source);
            if ((source.Legendary ? effect.Opcode != "special" : effect.Opcode != "dowildbattle" || a.Length != 0) ||
                state.SourceScriptedPreparation is not { } pending || pending.Receipt.Origin != SourceScriptedEncounterRegistry.Origin(source) ||
                state.Script.Wait?.Kind != StoryWaitKind.Battle || !state.Script.Flags.GetValueOrDefault(SpecialWildFlag) ||
                state.World.Rng != pending.Generated.GlobalRng) throw new InvalidDataException("source_scripted_battle_start_context");
            next = BeginWildBattle(state with { SourceScriptedPreparation = null }, scriptedPokemon: pending.Generated,
                sourceTableOrigin: pending.Receipt.Origin, sourceTableGenerationRng: pending.Receipt.RngBefore);
            return true;
        }
        return false;
    }

    private bool SourceScriptedButtonWait(CampaignState state) => state.Script.Wait?.Kind == StoryWaitKind.ButtonPress &&
        !state.Script.Stack.IsEmpty && SourceScriptedEncounterRegistry.Definitions.Any(d =>
            d.Map == state.World.Player.MapId && state.Script.Stack[^1].ProgramId == d.ScriptId);
    private Decision<CampaignState> DecideSourceScriptedButton(CampaignState state, CampaignCommand command)
    {
        if (!SourceScriptedButtonWait(state)) return new(CommandStatus.Rejected, state, "source_scripted_button_not_pending");
        if (command.Text is not ("A" or "B")) return new(CommandStatus.Rejected, state, "button_press_requires_a_or_b");
        var next = state with { PresentationEffects = [], PresentationTicks = 0, Motions = [] };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, next.Script.Wait!.CorrelationId, _content))));
    }

    private CampaignState ReturnFromSourceScriptedBattle(CampaignState state, CampaignWildBattle battle)
    {
        var receipt = battle.SourceTableReceipt ?? throw new InvalidDataException("source_scripted_return_receipt");
        var source = SourceScriptedEncounterRegistry.Find(receipt.Origin) ?? throw new InvalidDataException("source_scripted_definition");
        if (state.World.Player.MapId.Value != source.Map.Value || state.Script.Wait is not { Kind: StoryWaitKind.Battle } wait ||
            !state.Script.Flags.GetValueOrDefault(SpecialWildFlag)) throw new InvalidDataException("source_scripted_return_context");
        var caller = state.Script;
        var next = state with { WildBattle = null, Dialogue = null, Music = FieldTravelMusic(state),
            Exploration = ResetEncounterModifiers(state.Exploration!), SourceScriptedReturn = new(source.Id, SnorlaxSourceOutcome(battle)),
            Script = caller with { Stack = [], Wait = null, Completed = true } };
        next = StartStory(next, source.ResumeScript);
        for (int i = 0; i < 8 && next.Script.Wait is { Kind: StoryWaitKind.NativeEffect } effect; i++)
            next = Advance(next, StoryRules.Continue(next.Script, effect.CorrelationId, _content));
        if (!next.Script.Completed || next.Script.Wait is not null || !next.Script.Stack.IsEmpty ||
            battle.Frame.Outcome == WildBattleOutcome.Caught && next.Actors.Values.Any(a => a.SourceLocalId == source.LocalId && a.Active))
            throw new InvalidDataException("source_scripted_resume_incomplete");
        next = next with { Script = next.Script with { Stack = caller.Stack, Wait = caller.Wait, Completed = caller.Completed, Locked = caller.Locked } };
        return Pump(Advance(next, StoryRules.Continue(next.Script, wait.CorrelationId, _content)));
    }

    private static void ValidateSourceScriptedCatalog(ICampaignCatalog content)
    {
        foreach (var source in SourceScriptedEncounterRegistry.Definitions.Where(d => content.Maps.ContainsKey(d.Map)))
        {
            var definition = content.MapDefinition(source.Map);
            var program = content.FindProgram(source.ScriptId);
            if (definition.Programs.OnResume != source.ResumeScript ||
                !definition.Actors.Any(a => a.SourceLocalId == source.LocalId && a.ScriptId == source.ScriptId && a.HideFlag == source.HideFlag) ||
                program is null || program.Instructions.Count(i => i.Source.Path == SourceScriptPath(source) &&
                    i.Opcode == "setwildbattle" && i.Op == StoryOp.Special && i.Args.SequenceEqual(new[] {
                        source.Species.Value[5..], source.Level.ToString(System.Globalization.CultureInfo.InvariantCulture) })) != 1 ||
                program.Instructions.Count(i => i.Op == StoryOp.TrainerBattle && (source.Legendary
                    ? i.Opcode == "special" && i.Args.SequenceEqual(new[] { "StartLegendaryBattle" }) : i.Opcode == "dowildbattle")) != 1)
                throw new InvalidDataException("source_scripted_catalog_binding");
        }
    }

    private string? ValidateSourceScriptedProgress(CampaignState state)
    {
        if (state.SourceScriptedPreparation is not null || state.SourceScriptedReturn is not null)
            return "save_source_scripted_active";
        foreach (var source in SourceScriptedEncounterRegistry.Definitions.Where(d => _content.Maps.ContainsKey(d.Map)))
        {
            var attempts = (state.Exploration?.Battles ?? []).Where(b =>
                SourceScriptedEncounterRegistry.Find(b.SourceTableReceipt?.Origin) == source).ToArray();
            bool fought = state.Script.Flags.GetValueOrDefault(source.FoughtFlag);
            bool hidden = state.Script.Flags.GetValueOrDefault(source.HideFlag);
            var terminal = attempts.Where(b => b.Outcome is WildBattleOutcome.Won or WildBattleOutcome.Caught).ToArray();
            if (terminal.Length > 1 || fought != (terminal.Length == 1) || fought && (!hidden || attempts[^1] != terminal[0]))
                return "save_source_scripted_completion";
            if (hidden && !fought && attempts.LastOrDefault()?.Outcome != WildBattleOutcome.Escaped)
                return "save_source_scripted_hide_receipt";
            if (state.World.Player.MapId == source.Map && state.Actors.Values.Any(a => a.SourceLocalId == source.LocalId && hidden && a.Active))
                return "save_source_scripted_actor";
        }
        return null;
    }

}
