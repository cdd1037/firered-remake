using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.FirstBattle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public enum CampaignForestTrainerStage { Intro, Combat, Rewards, Finished }
public sealed record CampaignForestTrainerHistory(string Id, string TrainerId, WildBattleOutcome Outcome,
    int ExperienceGained, int MoneyGained, ImmutableArray<int> OpponentsSent)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? PokemonLeagueChallenge { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MoneyMultiplier { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVsSeekerBattleReceipt? VsSeeker { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? SourceScriptEntry { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? SourceScriptLine { get; init; }
}

/// <summary>Campaign scene continuation and display state around the single canonical battle owner.</summary>
public sealed record CampaignForestTrainerBattle(string Id, TrainerId Trainer, string ActorId,
    CampaignWildBattle Presentation, CampaignForestTrainerStage Stage,
    ImmutableArray<int> LevelsBeforeBattle, ImmutableArray<ForestTrainerCombatEvent> TrainerEvents,
    int TrainerEventIndex, int ExperienceGained, ImmutableArray<int> OpponentsSent)
{
    [JsonIgnore] public SourceBattleFrame Frame { get => Presentation.Frame ?? throw new InvalidDataException("canonical_trainer_frame_missing"); init => Presentation = Presentation with { Frame = value }; }
    [JsonIgnore] public CampaignTrainerCombatView Combat => CampaignTrainerReadProjection.From(Frame);
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public CampaignVsSeekerBattleReceipt? VsSeeker { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? SourceScriptEntry { get; init; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? SourceScriptLine { get; init; }
    [JsonIgnore] public ForestTrainerCombatEvent? CurrentTrainerEvent => TrainerEventIndex < TrainerEvents.Length &&
        (Presentation.EventIndex >= Presentation.Events.Length ||
         TrainerEvents[TrainerEventIndex].Sequence < Presentation.Events[Presentation.EventIndex].Sequence)
        ? TrainerEvents[TrainerEventIndex] : null;
}

public sealed partial class CampaignSession
{
    private SourceTrainerEncounterEntryResult PrepareForestTrainerBattle(CampaignState state, TrainerId trainer)
    {
        ValidateForestTrainerEntryContext(state, trainer);
        return PrepareCanonicalTrainerBattle(state, trainer);
    }

    private void ValidateForestTrainerEntryContext(CampaignState state, TrainerId trainer)
    {
        if (state.Party.Length is < 1 or > 6 || state.Battle is not null ||
            state.WildBattle is not null || state.ForestTrainerBattle is not null ||
            state.Script.QuestLogState != 0 || state.Party.All(p => p.IsEgg || p.Pokemon.CurrentHp == 0))
            throw new InvalidDataException("forest_trainer_entry_context");
        if (long.MaxValue - Revision < 4096 || long.MaxValue - state.Script.Revision < 4096 ||
            long.MaxValue - state.Script.EffectSequence < 4096 || ulong.MaxValue - state.World.Rng.Cursor < (1UL << 32))
            throw new InvalidDataException("forest_trainer_counter_headroom_required");
        var definition = OrdinaryTrainerScript(trainer);
        if (definition.Mode is not (0 or 1 or 2 or 3 or 9))
            throw new InvalidDataException("forest_trainer_mode_unsupported");
        if(!string.IsNullOrEmpty(definition.IntroText))_ = _content.TextPages(definition.IntroText);
        _ = _content.TextPages(definition.DefeatText);
        if(!string.IsNullOrEmpty(definition.PostBattleText))_ = _content.TextPages(definition.PostBattleText);
        _ = SourceTrainerEntryParty(state);
    }

    private CampaignState BeginForestTrainerBattle(CampaignState state, StoryEffect effect)
    {
        bool noIntro=effect.Opcode=="trainerbattle_no_intro";
        if ((noIntro?effect.Args.Length!=2:effect.Args.Length is <3 or >5) || state.Script.Wait?.Kind != StoryWaitKind.Battle)
            throw new InvalidDataException("forest_trainer_source_wait_invalid");
        var trainer = new TrainerId("pret:" + effect.Args[0]);
        var occurrence = RequireTrainerOccurrence(trainer, effect);
        var definition = occurrence.Source.Definition;
        string? battleActorId = state.LastTalked;
        if(noIntro)
        {
            if (definition.Mode != 3)
                throw new InvalidDataException("trainer_no_intro_mode_mismatch");
            // Quiz consoles have no talking actor. Source no-intro battle setup does not
            // call SetMapVarsToTrainer, so its stale VAR_LAST_TALKED is not the opponent.
            // Resolve only the authenticated Cinnabar trainer actor for presentation;
            // preserve the source object-selection globals through the quiz branch.
            var selected=IsLostCaveTrainer(trainer)
                ? state.Actors.Values.SingleOrDefault(a=>a.Active&&a.LocalId==LostCaveSelphyActor)
                : IsSeviiRubyTrainer(trainer)
                ? state.Actors.Values.SingleOrDefault(a=>a.Active&&a.ScriptId==SeviiRubyNoIntroActorScript(trainer))
                : IsPokemonLeagueTrainer(trainer)
                ? state.Actors.Values.SingleOrDefault(a=>a.Active&&a.LocalId==PokemonLeagueTrainerActor(trainer))
                : IsCinnabarTrainer(trainer)
                ? state.Actors.Values.SingleOrDefault(a=>a.Active&&a.ScriptId==OrdinaryNoIntroActorScript(trainer))
                : state.Actors.Values.SingleOrDefault(a=>a.Active&&a.SourceLocalId==state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED"));
            if(selected is null)throw new InvalidDataException("mtmoon_no_intro_actor_missing");
            battleActorId=selected.LocalId;
            if(!IsCinnabarTrainer(trainer)&&!IsPokemonLeagueChampion(trainer))state=SelectInteractedObject(state,selected.LocalId) with{LastTalked=selected.LocalId};
        }
        if (battleActorId is not { } actorId || !state.Actors.TryGetValue(actorId, out var actor) ||
            !actor.Active || state.World.Player.MapId != occurrence.Map)
            throw new InvalidDataException("forest_trainer_source_definition_mismatch");
        if (noIntro)
        {
            if (actor.ScriptId != OrdinaryNoIntroActorScript(trainer) ||
                IsCeruleanRival(trainer) && actor.LocalId != "LOCALID_CERULEAN_RIVAL")
                throw new InvalidDataException("trainer_scene_actor_mismatch");
            // Scene-driven battles retain their actual story prerequisites and sequencing.
            if (IsLostCaveTrainer(trainer)) RequireLostCaveTrainerOccurrence(state, trainer, definition, true);
            if (IsFourIslandTrainer(trainer)) RequireFourIslandTrainerOccurrence(state, trainer, definition, true);
            if (IsSeviiRubyTrainer(trainer)) RequireSeviiRubyTrainerOccurrence(state, trainer, definition, true);
            if (IsFirstSeviiTrainer(trainer)) RequireFirstSeviiBikerOccurrence(state, trainer, definition);
            if (IsPokemonLeagueTrainer(trainer)) RequirePokemonLeagueOccurrence(state, trainer, definition, true);
            if (IsLeagueRivalTrainer(trainer)) RequireLeagueRivalOccurrence(state, trainer, definition, true);
        }
        else
        {
            RequireTrainerActorOccurrence(state, occurrence);
            if (!_forestTrainerApproaching) state = FaceSelectedObject(state);
        }
        if (state.BattledTrainers.Contains(trainer.Value)&&!IsPokemonLeagueTrainer(trainer)&&!(IsFirstSeviiTrainer(trainer)&&_content.FirstSeviiTrainerParties!.ReplaysDefeatedTrainer(definition.ScriptEntry,definition.Source.StartLine)))
        {
            // Script_HasTrainerBeenFought -> gotopostbattlescript: resume at the instruction
            // immediately after trainerbattle_single, preserving source MSGBOX_AUTOCLOSE.
            state = state with { Script = state.Script with { Locked = true,
                Variables = state.Script.Variables.SetItem("VAR_RESULT", 1) } };
            return Advance(state, StoryRules.Continue(state.Script, state.Script.Wait!.CorrelationId, _content));
        }
        var prepared = PrepareForestTrainerBattle(state, trainer);
        int active = Enumerable.Range(0, state.Party.Length).First(i => !state.Party[i].IsEgg && state.Party[i].Pokemon.CurrentHp > 0);
        string id = "forest-trainer-battle:" + ((state.ForestTrainerHistory?.Length ?? 0) + 1);
        var combat = prepared.State!;
        var owned = state.Party[active];
        var presentation = new CampaignWildBattle(id, owned, combat, ProjectTrainerWildEvents(prepared.Events), 0,
            combat.Get(new(0)).Body!.Pokemon, combat.Get(new(1)).Body!.Pokemon, CampaignBattlePhase.Events,
            [], 0, prepared.Trace)
        { DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(prepared.EntrySlots)), SourceEvents = prepared.Events, ActivePartyIndex = active,
            OpponentHpAtLastSwitch = combat.Get(new(1)).Body!.Pokemon.CurrentHp };
        var battle = new CampaignForestTrainerBattle(id, trainer, actorId, presentation,
            CampaignForestTrainerStage.Intro, state.Party.Select(p => p.Pokemon.Level).ToImmutableArray(),
            ProjectTrainerEvents(combat, prepared.Events), 0, 0, [0])
        {
            // The live scene needs its exact call to resume the right continuation.
            SourceScriptEntry = occurrence.Instruction.Source.Symbol,
            SourceScriptLine = occurrence.Instruction.Source.Line
        };
        var fixedActor = state.Actors[actorId];
        state = state with { Actors = state.Actors.SetItem(actorId, fixedActor with { MovementType = ForestTrainerFacingType(fixedActor.Facing) }) };
        var next=ProjectSourceTrainerOwnedParty(state with { ForestTrainerBattle = battle,
            Dialogue = noIntro?null:new(definition.IntroText, _content.TextPages(definition.IntroText), 0, false, false),
            Script = state.Script with { Locked = true, Variables = _forestTrainerApproaching ? state.Script.Variables : state.Script.Variables.SetItem("VAR_RESULT", 0) },
            Music = noIntro || occurrence.Source.MusicOption == "NO_MUSIC" ? state.Music : OrdinaryEncounterMusic(trainer) });
        return noIntro?PrepareForestTrainerPresentation(ResetFieldPoisonCounterForBattle(next) with{ForestTrainerBattle=battle with{Stage=CampaignForestTrainerStage.Combat},Dialogue=null,Motions=[],PresentationTicks=0,World=next.World with{Rng=combat.Rng},SeenSpecies=next.SeenSpecies.Add(combat.Get(new(1)).Body!.Pokemon.SourceSpeciesId)}):next;
    }

    private Decision<CampaignState> DecideForestTrainerBattle(CampaignState state, CampaignCommand command)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        if (state.Script.Wait?.Kind != StoryWaitKind.Battle)
            return new(CommandStatus.Fault, state, "forest_trainer_script_context");
        if (trainer.Stage == CampaignForestTrainerStage.Intro)
        {
            if (command.Kind != "ack") return new(CommandStatus.Rejected, state, "forest_trainer_intro_pending");
            if (state.Dialogue is { } dialogue && dialogue.Page + 1 < dialogue.Pages.Length)
                return new(CommandStatus.Accepted, state with { Dialogue = dialogue with { Page = dialogue.Page + 1 } });
            return new(CommandStatus.Accepted, PrepareForestTrainerPresentation(ResetFieldPoisonCounterForBattle(state) with
            { ForestTrainerBattle = trainer with { Stage = CampaignForestTrainerStage.Combat }, Dialogue = null,
                Motions = [], PresentationTicks = 0, World = state.World with { Rng = trainer.Frame.Rng },
                SeenSpecies = state.SeenSpecies.Add(trainer.Frame.Get(new(1)).Body!.Pokemon.SourceSpeciesId) }));
        }
        if (command.Kind == "ack")
        {
            if (b.Phase == CampaignBattlePhase.Events)
            {
                trainer = trainer.CurrentTrainerEvent is null
                    ? trainer with { Presentation = b with { EventIndex = b.EventIndex + 1 } }
                    : trainer with { TrainerEventIndex = trainer.TrainerEventIndex + 1 };
                return new(CommandStatus.Accepted, PrepareForestTrainerPresentation(state with { ForestTrainerBattle = trainer }));
            }
            if (b.Phase == CampaignBattlePhase.Results)
            {
                if (b.PageIndex + 1 < b.Pages.Length)
                {
                    int index = b.PageIndex + 1;
                    return new(CommandStatus.Accepted, state with { ForestTrainerBattle = trainer with { Presentation = b with
                    { PageIndex = index, DisplayPlayer = b.Pages[index].Reward is WildRewardLevelEvent && b.RewardPartyIndex == b.ActivePartyIndex
                        ? state.Party[b.ActivePartyIndex].Pokemon : b.DisplayPlayer } } });
                }
                if (!b.PendingMoves.IsEmpty) return new(CommandStatus.Rejected, state, "trainer_move_learning_required");
                return new(CommandStatus.Accepted, trainer.Stage == CampaignForestTrainerStage.Finished
                    ? BeginForestTrainerEvolution(state) : ContinueForestTrainerAfterRewards(state));
            }
            return new(CommandStatus.Rejected, state, "battle_move_required");
        }
        if (command.Kind == "wild_learn") return DecideForestTrainerLearning(state, command);
        if (b.Phase != CampaignBattlePhase.ChooseMove) return new(CommandStatus.Rejected, state, "battle_presentation_pending");
        if(SourceTrainerBatonPassPending(trainer.Frame))
            return DecideSourceTrainerPartyChoice(state with{ForestTrainerBattle=trainer},command);
        if (command.Kind == "battle_ball") return new(CommandStatus.Rejected, state, "trainer_battle_cannot_capture");
        if (command.Kind == "battle_menu" && command.Text == "run") return new(CommandStatus.Rejected, state, "trainer_battle_cannot_run");
        if (trainer.Frame.FourTurn?.Phase == SourceFourActionPhase.ShiftPending || trainer.Frame.PendingSwitches.Any(p => p.Slot.Value == 0))
            return DecideForestTrainerReplacement(state, command);
        if (command.Kind == "battle_menu" && command.Text is "bag" or "party") return new(CommandStatus.Accepted, state);
        if (command.Kind == "battle_switch") return DecideForestTrainerReplacement(state, command);
        if (command.Kind == "battle_poke_flute") return DecideTrainerPokeFlute(state, command);
        if (command.Kind == "battle_medicine") return DecideTrainerMedicine(state, command);
        if (command.Kind == "battle_potion")
            return DecideTrainerMedicine(state, command with { Kind = "battle_medicine", Text = "pret:ITEM_POTION",
                TargetInstanceId = command.PartyIndex >= 0 && command.PartyIndex < state.Party.Length ? state.Party[command.PartyIndex].InstanceId : null });
        if (command.Kind != "battle_move") return new(CommandStatus.Rejected, state, "forest_trainer_battle_active");
        var frame = trainer.Frame;
        if (SourceWildBattlePolicy.ValidateMoveSelection(CanonicalTrainerPokemon, frame, command.MoveSlot) is { } invalid)
            return new(CommandStatus.Rejected, state, invalid);
        return BeginCanonicalTrainerAction(state, new(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Move, command.MoveSlot, new(1)));
    }

    private Decision<CampaignState> DecideForestTrainerReplacement(CampaignState state, CampaignCommand command)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation; var frame = trainer.Frame;
        bool shift = frame.FourTurn?.Phase == SourceFourActionPhase.ShiftPending;
        if (shift && command.Kind == "trainer_shift" && command.Choice == 0)
            return CommitCanonicalTrainerResult(state, SourceFourActionTurns.ChooseShift(CanonicalTrainerPokemon, frame, TrainerContinuation(frame), null));
        if (shift && command.Kind == "trainer_shift" && command.Choice == 1)
            return new(CommandStatus.Accepted, state with { ForestTrainerBattle = trainer with { Presentation = b with { Replacement = CampaignReplacementStage.Select } } });
        if (command.Kind != "battle_switch") return new(CommandStatus.Rejected, state, shift ? "trainer_shift_choice_required" : "trainer_replacement_required");
        int index = command.PartyIndex;
        if (index < 0 || index >= state.Party.Length || index == b.ActivePartyIndex || state.Party[index].IsEgg || state.Party[index].Pokemon.CurrentHp == 0)
            return new(CommandStatus.Rejected, state, "replacement_member_invalid");
        var incoming = new PartyMemberKey(BattleSide.Player, index);

        if (!shift && frame.PendingSwitches.Any(p => p.Slot.Value == 0))
            return CommitCanonicalTrainerResult(state, SourceFourActionTurns.ChooseReplacement(CanonicalTrainerPokemon, frame, TrainerContinuation(frame), new(0), incoming));
        var outgoing = state.Party[b.ActivePartyIndex]; int hp = frame.Get(new(1)).Body!.Pokemon.CurrentHp;
        int snapshot = b.OpponentHpAtLastSwitch ?? hp; int loss = hp >= snapshot || snapshot == 0 ? 0 : (snapshot - hp) * 100 / snapshot;
        string withdrawal = hp >= snapshot ? "sText_PkmnThatsEnough" : loss < 30 ? "sText_PkmnComeBack" : loss < 70 ? "sText_PkmnOkComeBack" : "sText_PkmnGoodComeBack";
        int percent = Math.Max(1, hp * 100 / frame.Get(new(1)).Body!.Pokemon.Stats.MaxHp);
        string send = hp == 0 || percent >= 70 ? "sText_GoPkmn2" : percent >= 40 ? "sText_DoItPkmn" : percent >= 10 ? "sText_GoForItPkmn" : "sText_YourFoesWeakGetEmPkmn";
        var selected = state with { ForestTrainerBattle = trainer with { Presentation = b with {
            LastSwitch = new(outgoing, withdrawal, send), OpponentHpAtLastSwitch = hp } } };
        return shift ? CommitCanonicalTrainerResult(selected, SourceFourActionTurns.ChooseShift(CanonicalTrainerPokemon, frame, TrainerContinuation(frame), incoming))
            : BeginCanonicalTrainerAction(selected, new(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Switch, Incoming: incoming));
    }

    private CampaignState PrepareForestTrainerPresentation(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        while (b.EventIndex < b.Events.Length || trainer.TrainerEventIndex < trainer.TrainerEvents.Length)
        {
            trainer = trainer with { Presentation = b };
            b=AdvanceDisplayThrough(b,trainer.CurrentTrainerEvent?.Sequence ?? b.Events[b.EventIndex].Sequence);
            trainer = trainer with { Presentation = b };
            if (trainer.CurrentTrainerEvent is { } te)
            {
                if(te is SsAnneTrainerPlayerForcedOutEvent forced)return state with{ForestTrainerBattle=trainer with{Presentation=b with{DisplayPlayer=BattleDisplayPokemon(forced.Replacement),DisplayPlayerAppearance=null}}};
                if(te is CeruleanTrainerItemUsedEvent)return state with{ForestTrainerBattle=trainer};
                if (te is ForestTrainerOpponentReplacedEvent replacement)
                    return state with { ForestTrainerBattle = trainer with { Presentation = b with { DisplayOpponent = BattleDisplayPokemon(replacement.Replacement),DisplayOpponentAppearance=null } } };
                // Started is paired with the ordinary started event. AI/EXP/selection/SHIFT are
                // typed diagnostics; SHIFT receives its own decision screen after event draining.
                trainer = trainer with { TrainerEventIndex = trainer.TrainerEventIndex + 1 }; continue;
            }
            var e = b.Events[b.EventIndex];
            if(HiddenEarlyPartyEvent(e)){b=b with{EventIndex=b.EventIndex+1};continue;}
            if(e is WildBattleStatusChangedEvent {Reason:"sleep-countdown"} or WildBattleLeechSeedChangedEvent {Reason:not "move"}){b=b with{EventIndex=b.EventIndex+1};continue;}
            if (e is WildBattleOrderEvent or WildBattleTurnReadyEvent or WildBattleEndedEvent or WildBattleReplacementRequiredEvent or WildBattleDefenseCurledEvent || e is WildBattleBindingChangedEvent binding && binding.Reason is not ("bind" or "wrap" or "expired"))
            { b = b with { EventIndex = b.EventIndex + 1 }; continue; }
            void Update(WildBattleSide side, Func<PokemonInstance, PokemonInstance> update) =>
                b = side == WildBattleSide.Player ? b with { DisplayPlayer = update(b.DisplayPlayer) } : b with { DisplayOpponent = update(b.DisplayOpponent) };
            switch (e)
            {
                case WildBattlePpCommandEvent pp when pp.PpSlot>=0:Update(pp.PpOwner,p=>p with{Moves=p.Moves.SetItem(pp.PpSlot,p.Moves[pp.PpSlot] with{CurrentPp=pp.After})});break;
                case WildBattleSnatchPressurePpEvent pp:Update(pp.Snatcher,p=>p with{Moves=p.Moves.SetItem(pp.Slot,p.Moves[pp.Slot] with{CurrentPp=pp.After})});break;
                case WildBattleGrudgePpEvent grudge:Update(grudge.Attacker,p=>p with{Moves=p.Moves.SetItem(grudge.ChosenSlot,p.Moves[grudge.ChosenSlot] with{CurrentPp=0})});break;
                case WildBattleWeatherDamageEvent weather:Update(weather.Side,p=>p with{CurrentHp=weather.HpAfter});break;
                case WildBattleDrainEvent drain:Update(drain.Actor,p=>p with{CurrentHp=drain.HpAfter});break;
                case WildBattleMoveCopiedEvent copied:Update(copied.Actor,p=>p with{Moves=p.Moves.SetItem(copied.MoveSlot,copied.Replacement)});break;
                case WildBattleTransformedEvent transformed:b=ApplyTransformDisplay(b,transformed);break;
                case WildBattlePlayerSwitchedEvent change: b = b with { DisplayPlayer = change.Replacement.Pokemon }; break;
                case WildBattlePlayerReplacedEvent change: b = b with { DisplayPlayer = change.Replacement.Pokemon }; break;
                case WildBattleMoveUsedEvent used when used.Slot >= 0: Update(used.Actor, p => p with { Moves = p.Moves.SetItem(used.Slot, p.Moves[used.Slot] with { CurrentPp = used.PpAfter }) }); break;
                case WildBattleHpChangedEvent hp: Update(hp.Target, p => p with { CurrentHp = hp.HpAfter }); break;
                case WildBattleHeldItemConsumedEvent item:Update(item.Side,p=>p with{CurrentHp=item.HpAfter});break;
                case WildBattleIngrainHealEvent roots: Update(roots.Side, p => p with { CurrentHp = roots.HpAfter }); break;
                case WildBattleRecoveredEvent recovered: Update(recovered.Side, p => p with { CurrentHp = recovered.HpAfter }); break;
                case WildBattlePotionRecoveredEvent potion: Update(potion.Side, p => p with { CurrentHp = potion.HpAfter }); break;
                case WildBattleMedicineAppliedEvent {ActiveTarget:true} medicine:Update(medicine.TargetSide??WildBattleSide.Player,p=>p with{CurrentHp=medicine.HpAfter});break;
                case WildBattleRecoilEvent recoil: Update(recoil.Actor, p => p with { CurrentHp = recoil.HpAfter }); break;
                case WildBattleAbilityDamageEvent ability: Update(ability.Target, p => p with { CurrentHp = ability.HpAfter }); break;
                case WildBattleConfusionSelfHitEvent self: Update(self.Side, p => p with { CurrentHp = self.HpAfter }); break;
                case WildBattleLeechSeedDrainEvent seed:Update(seed.DrainedSide,p=>p with{CurrentHp=seed.HpAfter});Update(seed.ReceivingSide,p=>p with{CurrentHp=seed.ReceiverHpAfter});break;
                case WildBattleBindingDamageEvent bind:Update(bind.Side,p=>p with{CurrentHp=bind.HpAfter});break;
                case WildBattleStatusDamageEvent status: Update(status.Side, p => p with { CurrentHp = status.HpAfter }); break;
            }
            return state with { ForestTrainerBattle = trainer with { Presentation = b } };
        }
        b=AdvanceDisplayThrough(b,long.MaxValue);
        state = state with { ForestTrainerBattle = trainer with { Presentation = b with
        { DisplayPlayer = BattleDisplayPokemon(trainer.Combat.Battle.Player), DisplayOpponent = BattleDisplayPokemon(trainer.Combat.Battle.Opponent),DisplayPlayerAppearance=BattleAppearance(trainer.Combat.Battle.Player),DisplayOpponentAppearance=BattleAppearance(trainer.Combat.Battle.Opponent) } } };
        return ContinueCanonicalTrainer(state);
    }

    private CampaignState ContinueForestTrainerAfterRewards(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!;
        return ContinueCanonicalTrainer(state with { ForestTrainerBattle = trainer with {
            Stage = CampaignForestTrainerStage.Combat, Presentation = trainer.Presentation with { Pages = [], PageIndex = 0 } } });
    }

    private Decision<CampaignState> DecideForestTrainerLearning(CampaignState state, CampaignCommand command)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation; var frame = trainer.Frame;
        if (b.Phase != CampaignBattlePhase.Results || b.PageIndex + 1 < b.Pages.Length || frame.Rewards?.Pending is not { } learning)
            return new(CommandStatus.Rejected, state, "trainer_learning_not_pending");
        var owned = state.Party[learning.Recipient.Index];
        if (command.MoveSlot < -1 || command.MoveSlot >= owned.Pokemon.Moves.Length)
            return new(CommandStatus.Rejected, state, "trainer_learning_slot_invalid");
        if (command.MoveSlot >= 0 && PokemonMoveRules.IsHiddenMachineMove(owned.Pokemon.Moves[command.MoveSlot].Move))
            return new(CommandStatus.Rejected, state, "hm_move_cannot_be_forgotten");
        var move = learning.Moves[0];
        return CommitCanonicalTrainerResult(state, SourceFourRewards.Learn(CanonicalTrainerPokemon, frame, TrainerContinuation(frame),
            new(learning.Recipient, move.Level, move.Move, command.MoveSlot < 0 ? null : command.MoveSlot)));
    }

    private CampaignState SettleForestTrainerBattle(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!; var b = trainer.Presentation;
        if(trainer.Frame.TrainerTowerEncounter is not null)return SettleTrainerTowerBattle(state,trainer.Frame,trainer.Id,trainer.OpponentsSent);
        if ((state.ForestTrainerHistory ?? []).Any(h => h.Id == trainer.Id)) throw new InvalidDataException("forest_trainer_settlement_duplicate");
        bool won = trainer.Frame.Outcome == WildBattleOutcome.Won;
        var prize = SourceBattleMoneyRules.SettleTrainerPrize(state.Money,
            4 * OrdinaryTrainer(trainer.Trainer).Party[^1].Level * OrdinaryTrainer(trainer.Trainer).BaseMoney,
            trainer.Frame.Outcome,trainer.Frame.Field.MoneyMultiplier);
        int award=prize.DisplayAmount,money=prize.Money;
        var pages = ImmutableArray.CreateBuilder<CampaignWildPage>();
        if (won)
        {
            pages.Add(new($"{state.PlayerName} defeated\n{OrdinaryTrainerClass(trainer.Trainer)} {(IsPokemonLeagueChampion(trainer.Trainer)||IsLeagueRivalTrainer(trainer.Trainer)||IsSilphRival(trainer.Trainer)||IsPokemonTowerRival(trainer.Trainer)||IsRoute22Trainer(trainer.Trainer)||IsCeruleanRival(trainer.Trainer)||IsSsAnneRival(trainer.Trainer)?state.RivalName:OrdinaryTrainer(trainer.Trainer).SourceTrainerName)}!"));
            foreach (var text in _content.TextPages(trainer.VsSeeker is {} vsReceipt ? VsSeekerOccurrence(vsReceipt).Arguments[2] : OrdinaryTrainerScript(trainer.Trainer,trainer.SourceScriptEntry,trainer.SourceScriptLine).DefeatText)) pages.Add(new(CampaignSourceText.DisplayPage(text, state)));
            pages.Add(new($"{state.PlayerName} got {award}\nfor winning!"));
        }
        else
        {
            if(IsRoute22Trainer(trainer.Trainer))foreach(var text in _content.TextPages(_content.Route22Trainers!.Battles[trainer.Trainer].VictoryText))pages.Add(new(CampaignSourceText.DisplayPage(text, state)));
            pages.Add(new($"{state.PlayerName} is out of usable\nPOKéMON!"));
            int lost = WhiteOutLoss(state);
            pages.Add(new(state.Money == 0 ? $"{state.PlayerName} whited out!" : $"{state.PlayerName} panicked and lost\n{lost}...\n{state.PlayerName} whited out!"));
            foreach (var page in _content.TextPages(HasCenterRespawn(state) ? "gText_PlayerScurriedToCenter" : "gText_PlayerScurriedBackHome"))
                pages.Add(new(CampaignSourceText.DisplayPage(page, state)));
        }
        var history = new CampaignForestTrainerHistory(trainer.Id, trainer.Trainer.Value, trainer.Frame.Outcome,
            trainer.ExperienceGained, prize.Awarded, trainer.OpponentsSent)
        {
            VsSeeker = trainer.VsSeeker,
            MoneyMultiplier = trainer.Frame.Field.MoneyMultiplier,
            PokemonLeagueChallenge = IsPokemonLeagueTrainer(trainer.Trainer) ? PokemonLeagueChallenge(state) : null,
            // Ordinary settled history records a battle, not the source-code location.
            SourceScriptEntry = trainer.VsSeeker?.ScriptEntry,
            SourceScriptLine = trainer.VsSeeker?.SourceLine
        };
        if (won)
        {
            var payday = SourceBattleMoneyRules.SettlePayDay(money, trainer.Frame.Field.PayDayMoney, WildBattleOutcome.Won,trainer.Frame.Field.MoneyMultiplier);
            var pickup = VermilionPickupRules.Apply(_binding.Ownership, state.Party, state.World.Rng, WildBattleOutcome.Won);
            money = payday.Money;
            if (payday.Amount != 0) pages.Add(new($"{state.PlayerName} picked up\n¥{payday.DisplayAmount}!"));
            state = state with { Party = pickup.Party, World = state.World with { Rng = pickup.Rng } };
            trainer = trainer with { Frame = SyncSourceTrainerPickup(trainer.Frame with { Rng = pickup.Rng }, state.Party) };
            b = trainer.Presentation;
            b = b with { RandomTrace = pickup.Trace };
        }
        var settled = state with { Money = money, ForestTrainerHistory = (state.ForestTrainerHistory ?? []).Add(history),
            ForestTrainerBattle = trainer with { Stage = CampaignForestTrainerStage.Finished,
                Presentation = b with { Phase = CampaignBattlePhase.Results, Pages = pages.ToImmutable(), PageIndex = 0 } } };
        // Original battle_main clears the matching rematch state before evolution and
        // the win/loss callback, including its intentionally unused direction draws.
        settled = ResetVsSeekerAfterBattle(settled, trainer.Trainer);
        return settled with { ForestTrainerBattle = settled.ForestTrainerBattle! with {
            Frame = settled.ForestTrainerBattle.Frame with { Rng = settled.World.Rng } } };
    }

    private CampaignState BeginForestTrainerEvolution(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!;
        if (trainer.Stage != CampaignForestTrainerStage.Finished || !trainer.Presentation.PendingMoves.IsEmpty || state.Evolution is not null)
            throw new InvalidDataException("forest_trainer_evolution_context");
        if (trainer.Frame.Outcome != WildBattleOutcome.Won) return ReturnFromForestTrainerBattle(state);
        var proposals = EligibleLevelEvolutions(state,trainer.LevelsBeforeBattle);
        return proposals.IsEmpty ? ReturnFromForestTrainerBattle(state) : state with
        { Evolution = new(trainer.Id, proposals, 0, CampaignEvolutionStage.Proposal, [], 0, []) };
    }

    private CampaignState ReturnFromForestTrainerBattle(CampaignState state)
    {
        var trainer = state.ForestTrainerBattle!;
        if(trainer.Frame.TrainerTowerEncounter is not null)return ReturnFromTrainerTowerBattle(state,trainer.Frame,trainer.Id);
        if (!(state.ForestTrainerHistory ?? []).Any(h => h.Id == trainer.Id) || !trainer.Presentation.PendingMoves.IsEmpty)
            throw new InvalidDataException("forest_trainer_not_settled");
        if(IsRoute22Trainer(trainer.Trainer))return ReturnFromRoute22Rival(state);
        var next = state with { ForestTrainerBattle = null, Dialogue = null, Motions = [], PresentationTicks = 0,
            Script = state.Script with { Stack = [], Wait = null, Completed = true, Locked = false } };
        if (trainer.Frame.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw) return WhiteOutFromRoute1(next);
        // Normal callback sets the fought flag, then common single-battle script sets mode 0,
        // releases and ends. The map post-battle speech is reached on a later interaction.
        next=next with { BattledTrainers = next.BattledTrainers.Add(trainer.Trainer.Value),
            Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", 0) },
            Music = FieldTravelMusic(next), Exploration = ResetEncounterModifiers(next.Exploration!) };
        if (trainer.VsSeeker is not null) return ClearVsSeekerLastTalked(next, trainer.VsSeeker);
        var occurrence = ResolveTrainerOccurrence(trainer.Trainer, trainer.SourceScriptEntry, trainer.SourceScriptLine);
        if (occurrence.Source.Opcode == "trainerbattle_no_intro")
        {
            var continuation = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", 0) };
            return Pump(Advance(next with { Script = continuation },
                StoryRules.Continue(continuation, continuation.Wait!.CorrelationId, _content)));
        }
        if (occurrence.Source.PostBattleScript is { } callback)
            return Pump(StartStory(next with { Script = next.Script with { Locked = true } }, callback));
        return next;
    }
}
