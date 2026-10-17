using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Original rematch occurrence and entry facts, independent of the selected upgraded party.
/// Prior victories are replayed from the ordinary history, never trusted from this receipt.</summary>
public sealed record CampaignVsSeekerBattleReceipt(string BaseTrainer, string SourceMap, int LocalId,
    string ActorScript, string ScriptEntry, int SourceLine, byte RequestedRank, int ProgressFlags, int MoneyBefore);

public sealed partial class CampaignSession
{
    private bool IsVsSeekerDouble(TrainerId id) => VsSeekerEnabled &&
        _content.VsSeekerTrainers?.Trainers.TryGetValue(id, out var trainer) == true && trainer.DoubleBattle;

    private int VsSeekerProgressFlags(CampaignState state)
    {
        int mask = 0;
        for (int i = 1; i < _content.VsSeeker!.ProgressFlags.Length; i++)
            if (state.Script.Flags.GetValueOrDefault(_content.VsSeeker.ProgressFlags[i])) mask |= 1 << i;
        return mask;
    }

    private HashSet<string> VsSeekerFlags(int mask) => _content.VsSeeker!.ProgressFlags
        .Where((flag, i) => i > 0 && (mask & (1 << i)) != 0).ToHashSet(StringComparer.Ordinal);

    private VsSeekerBattleOccurrence VsSeekerOccurrence(CampaignVsSeekerBattleReceipt receipt) =>
        _content.VsSeekerBattleOccurrences.Single(o => o.IsRematch && o.ActorScript == receipt.ActorScript &&
            o.ScriptEntry == receipt.ScriptEntry && o.Source.StartLine == receipt.SourceLine && o.Trainer.Value == receipt.BaseTrainer);

    private (CampaignActor Actor, VsSeekerActorBinding Binding) RequireVsSeekerActor(CampaignState state)
    {
        if (state.Script.QuestLogState != 0 || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            state.ObjectSlots?.GetValueOrDefault(state.SelectedObjectSlot) != id)
            throw new InvalidDataException("vs_seeker_battle_actor");
        string map = _content.MapDefinition(state.World.Player.MapId).SourceMapSymbol;
        var binding = _content.VsSeeker!.ActorBindings.SingleOrDefault(a => a.SourceMap == map &&
            a.LocalId == actor.SourceLocalId && a.ScriptLabel == actor.ScriptId)
            ?? throw new InvalidDataException("vs_seeker_battle_binding");
        return (actor, binding);
    }

    private bool TryApplyVsSeekerBattleEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!VsSeekerEnabled) return false;
        bool query = effect.Opcode == "specialvar" && effect.Args.Length == 2 && effect.Args[1] == "ShouldTryRematchBattle";
        bool rematch = effect.Opcode is "trainerbattle_rematch" or "trainerbattle_rematch_double";
        if (!query && !rematch) return false;
        var (actor, binding) = RequireVsSeekerActor(state);
        var occurrence = _content.VsSeekerBattleOccurrences.SingleOrDefault(o => o.ActorScript == actor.ScriptId && o.IsRematch == rematch)
            ?? throw new InvalidDataException("vs_seeker_battle_occurrence_missing");
        if (occurrence.Trainer.Value != "pret:" + binding.TrainerSymbol || effect.Source.Path != occurrence.Source.Path ||
            effect.Source.Symbol != occurrence.ScriptEntry || effect.Source.Line != occurrence.Source.StartLine + (query ? 1 : 0) ||
            (query ? !effect.Args.SequenceEqual(new[] { "VAR_RESULT", "ShouldTryRematchBattle" }) :
                effect.Opcode != occurrence.Opcode || !effect.Args.SequenceEqual(occurrence.Arguments)))
            throw new InvalidDataException("vs_seeker_battle_source_mismatch");
        if (query)
        {
            bool shouldTry = VsSeekerRules.ShouldTryRematch(_content.VsSeeker!, VsProgress(state).State,
                binding.TrainerSymbol, actor.SourceLocalId, VsFought(state));
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", shouldTry ? 1 : 0) } };
            return true;
        }
        if (state.Script.Wait?.Kind != StoryWaitKind.Battle) throw new InvalidDataException("vs_seeker_battle_wait");
        if (!VsSeekerRules.IsReady(_content.VsSeeker!, VsProgress(state).State, binding.TrainerSymbol, actor.SourceLocalId))
        {
            var unchanged = state with { Script = state.Script with { Locked = true, Variables = state.Script.Variables.SetItem("VAR_RESULT", 0) } };
            next = Advance(unchanged, StoryRules.Continue(unchanged.Script, unchanged.Script.Wait!.CorrelationId, _content));
            return true;
        }
        if (!OwnsVsSeeker(state) || !state.BattledTrainers.Contains(occurrence.Trainer.Value))
            throw new InvalidDataException("vs_seeker_battle_unearned");
        bool doubles = effect.Opcode == "trainerbattle_rematch_double";
        if (doubles && state.Party.Count(p => !p.IsEgg && p.Pokemon.CurrentHp > 0) < 2)
        {
            string text = occurrence.Arguments[3];
            next = state with { Dialogue = new(text, _content.TextPages(text), 0, false, true),
                Script = state.Script with { Stack = [], Locked = false, Wait = state.Script.Wait! with { Kind = StoryWaitKind.Dialog } } };
            return true;
        }
        int flags = VsSeekerProgressFlags(state);
        var selected = new TrainerId("pret:" + VsSeekerRules.RematchTrainer(_content.VsSeeker!, binding.TrainerSymbol,
            VsFought(state), VsSeekerFlags(flags)));
        if (_content.VsSeekerTrainers is not { } teams || !teams.Trainers.TryGetValue(selected, out var trainer) || trainer.DoubleBattle != doubles)
            throw new InvalidDataException("vs_seeker_battle_party_missing");
        var receipt = new CampaignVsSeekerBattleReceipt(occurrence.Trainer.Value, binding.SourceMap, binding.LocalId,
            actor.ScriptId, occurrence.ScriptEntry, occurrence.Source.StartLine,
            VsProgress(state).State.RematchRanks[actor.SourceLocalId], flags, state.Money);
        next = doubles ? BeginVsSeekerDoubleBattle(state, actor, occurrence, selected, receipt)
            : BeginVsSeekerSingleBattle(state, actor, occurrence, selected, receipt);
        return true;
    }

    private SourceFourRewardState VsSeekerRewards(CampaignState state) => new() {
        Context = new(SourceRewardBattleKind.OrdinaryTrainer, state.TrainerId, state.PlayerName,
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection),
        Metadata = state.Party.Select((p, i) => new SourceRewardMetadata(new(BattleSide.Player, i),
            p.OriginalTrainerName, p.MetLocation, p.Ball, SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()
    };

    private void RequireVsSeekerBattleEntry(CampaignState state)
    {
        if (state.Battle is not null || state.WildBattle is not null || state.ForestTrainerBattle is not null || state.DoubleBattle is not null ||
            state.Party.IsDefaultOrEmpty || state.Party.Length > 6 || state.Party.All(p => p.IsEgg || p.Pokemon.CurrentHp == 0) ||
            state.Party.Any(p => !HasSupportedCampaignOriginalTrainer(state, p)))
            throw new InvalidDataException("vs_seeker_battle_entry");
        if (long.MaxValue - Revision < 4096 || long.MaxValue - state.Script.Revision < 4096 ||
            long.MaxValue - state.Script.EffectSequence < 4096 || ulong.MaxValue - state.World.Rng.Cursor < (1UL << 32))
            throw new InvalidDataException("vs_seeker_battle_counter_headroom");
    }

    private CampaignState BeginVsSeekerSingleBattle(CampaignState state, CampaignActor actor, VsSeekerBattleOccurrence occurrence,
        TrainerId trainer, CampaignVsSeekerBattleReceipt receipt)
    {
        RequireVsSeekerBattleEntry(state);
        var party = SourceTrainerEntryParty(state);
        int active = Enumerable.Range(0, party.Length).First(i => !party[i].IsEgg && party[i].Pokemon.CurrentHp > 0);
        var setup = SourceTrainerEncounterEntry.Prepare(state.World.Rng, "vs-seeker-trainer");
        if (!setup.Succeeded) throw new InvalidDataException(setup.Fault);
        var made = VsSeekerTrainerPartyFactory.Create(_content.VsSeekerTrainers!, _binding.Ownership.Mechanics, trainer, setup.Rng);
        var prepared = SourceTrainerEncounterEntry.Begin(CanonicalTrainerPokemon, setup, made, party, active,
            EffectiveBattleStyle(state), CampaignBattleTerrain(state), _binding.Ownership, VsSeekerRewards(state));
        if (!prepared.Succeeded) throw new InvalidDataException(prepared.Fault);
        var frame = prepared.State! with { Obedience = CampaignObedienceContext(state) };
        string id = "forest-trainer-battle:" + ((state.ForestTrainerHistory?.Length ?? 0) + 1);
        var presentation = new CampaignWildBattle(id, state.Party[active], frame, ProjectTrainerWildEvents(prepared.Events), 0,
            frame.Get(new(0)).Body!.Pokemon, frame.Get(new(1)).Body!.Pokemon, CampaignBattlePhase.Events, [], 0, prepared.Trace) {
            DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(prepared.EntrySlots)), SourceEvents = prepared.Events, ActivePartyIndex = active,
            FaintedMembers = state.Party.Where(p => p.Pokemon.CurrentHp == 0).Select(p => p.InstanceId).ToImmutableSortedSet(),
            OpponentHpAtLastSwitch = frame.Get(new(1)).Body!.Pokemon.CurrentHp };
        var battle = new CampaignForestTrainerBattle(id, trainer, actor.LocalId, presentation, CampaignForestTrainerStage.Intro,
            state.Party.Select(p => p.Pokemon.Level).ToImmutableArray(), ProjectTrainerEvents(frame, prepared.Events), 0, 0, [0]) {
            SourceScriptEntry = occurrence.ScriptEntry, SourceScriptLine = occurrence.Source.StartLine, VsSeeker = receipt };
        string intro = occurrence.Arguments[1];
        return ProjectSourceTrainerOwnedParty(state with { ForestTrainerBattle = battle,
            Dialogue = new(intro, _content.TextPages(intro), 0, false, false),
            Script = state.Script with { Locked = true, Variables = state.Script.Variables.SetItem("VAR_RESULT", 0) },
            Actors = state.Actors.SetItem(actor.LocalId, actor with { MovementType = ForestTrainerFacingType(actor.Facing) }),
            Music = OrdinaryEncounterMusic(trainer) });
    }

    private CampaignState BeginVsSeekerDoubleBattle(CampaignState state, CampaignActor actor, VsSeekerBattleOccurrence occurrence,
        TrainerId trainer, CampaignVsSeekerBattleReceipt receipt)
    {
        RequireVsSeekerBattleEntry(state);
        var setup = SourceTrainerEncounterEntry.Prepare(state.World.Rng, "vs-seeker-double");
        if (!setup.Succeeded) throw new InvalidDataException(setup.Fault);
        var made = VsSeekerTrainerPartyFactory.Create(_content.VsSeekerTrainers!, _binding.Ownership.Mechanics, trainer, setup.Rng);
        var prepared = SourcePresetTrainerEncounterEntry.Begin(_binding.Ownership.Mechanics, setup, made,
            state.Party.Select(p => new WildBattleCalledPartyMember(p.Pokemon, p.HeldItem, p.Status, p.IsEgg)).ToImmutableArray(),
            _binding.Ownership, state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"),
            state.Script.Flags.GetValueOrDefault("FLAG_BADGE05_GET"), state.Script.Flags.GetValueOrDefault("FLAG_BADGE03_GET"),
            state.Script.Flags.GetValueOrDefault("FLAG_BADGE07_GET"), CampaignBattleTerrain(state));
        if (!prepared.Succeeded) throw new InvalidDataException(prepared.Fault);
        var frame = prepared.State! with { Obedience = CampaignObedienceContext(state), Rewards = VsSeekerRewards(state) };
        string id = "double-trainer-battle:" + ((state.DoubleBattleHistory?.Length ?? 0) + 1), intro = occurrence.Arguments[1];
        return state with { DoubleBattle = new(id, trainer, actor.LocalId, frame, CampaignDoubleBattleStage.Intro,
            prepared.Events, 0, [], state.Party.Select(p => p.Pokemon.Level).ToImmutableArray(), 0, 0,
            prepared.EntrySlots.Select(c=>c.Body!).ToImmutableArray(), prepared.EntrySlots.Select(c=>c.Member!.Value).ToImmutableArray(),
            prepared.Trace, [], 0) { VsSeeker = receipt, VsSeekerParty = made, VsSeekerInventory = SourceTrainerItemInventory.Create(made.Trainer),
                VsSeekerCurrentMove = SourceTrainerEncounterPolicy.NoMove },
            Dialogue = new(intro, _content.TextPages(intro), 0, false, false),
            Script = state.Script with { Locked = true, Variables = state.Script.Variables.SetItem("VAR_RESULT", 0) },
            Actors = state.Actors.SetItem(actor.LocalId, actor with { MovementType = ForestTrainerFacingType(actor.Facing) }),
            Music = VsSeekerEncounterMusic(trainer) };
    }

    private string VsSeekerEncounterMusic(TrainerId trainer) => (_content.VsSeekerTrainers!.Trainers[trainer].EncounterMusicGender & 0x7f) switch {
        1 or 2 or 9 => "MUS_ENCOUNTER_GIRL", 0 or 4 or 5 or 8 or 10 or 11 or 12 or 13 => "MUS_ENCOUNTER_BOY", _ => "MUS_ENCOUNTER_ROCKET" };

    private CampaignState ClearVsSeekerLastTalked(CampaignState state, CampaignVsSeekerBattleReceipt? receipt)
    {
        if (receipt is null) return state;
        var progress = VsProgress(state);
        return state with { VsSeekerProgress = progress with { State = VsSeekerRules.ClearLastTalked(progress.State, receipt.LocalId) } };
    }

    private bool ValidVsSeekerHistory(CampaignState state, CampaignVsSeekerBattleReceipt receipt, string actualTrainer,
        WildBattleOutcome outcome, int moneyGained, int? multiplier, IReadOnlySet<string> previousWinners, bool doubles)
    {
        if (!VsSeekerEnabled || outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw) ||
            !OwnsVsSeeker(state) || receipt.BaseTrainer is null || receipt.ActorScript is null ||
            receipt.ScriptEntry is null || receipt.SourceMap is null || receipt.RequestedRank is < 1 or > 5 ||
            receipt.ProgressFlags is < 0 or > 62 || (receipt.ProgressFlags & 3) != 2 ||
            (receipt.ProgressFlags & ~VsSeekerProgressFlags(state)) != 0 || receipt.MoneyBefore is < 0 or > 999999 ||
            !previousWinners.Contains(receipt.BaseTrainer)) return false;
        var occurrence = _content.VsSeekerBattleOccurrences.SingleOrDefault(o => o.IsRematch && o.ActorScript == receipt.ActorScript &&
            o.ScriptEntry == receipt.ScriptEntry && o.Source.StartLine == receipt.SourceLine && o.Trainer.Value == receipt.BaseTrainer);
        if (occurrence is null || (occurrence.Mode == 7) != doubles ||
            !_content.VsSeeker!.ActorBindings.Any(a => a.SourceMap == receipt.SourceMap && a.LocalId == receipt.LocalId &&
                a.ScriptLabel == receipt.ActorScript && "pret:" + a.TrainerSymbol == receipt.BaseTrainer)) return false;
        var fought = previousWinners.Select(t => t[5..]).ToHashSet(StringComparer.Ordinal);
        if (VsSeekerRules.NextRank(_content.VsSeeker, receipt.BaseTrainer[5..], fought) != receipt.RequestedRank ||
            "pret:" + VsSeekerRules.RematchTrainer(_content.VsSeeker, receipt.BaseTrainer[5..], fought, VsSeekerFlags(receipt.ProgressFlags)) != actualTrainer)
            return false;
        return SourceBattleMoneyRules.ValidMultiplier(multiplier) && moneyGained == SourceBattleMoneyRules.SettleTrainerPrize(receipt.MoneyBefore,
            VsSeekerTrainerPartyFactory.BasePrize(_content.VsSeekerTrainers!, new(actualTrainer)), outcome, multiplier).Awarded;
    }
}
