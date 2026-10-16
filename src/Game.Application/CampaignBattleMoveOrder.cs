using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.FirstBattle;
using Game.Domain.Pokemon;

namespace Game.Application;

/// <summary>A move-order edit is bound to the battle and occupant displayed when SELECT was pressed.</summary>
public sealed record CampaignBattleMoveSwap(string BattleId, long BattleRevision, int ActorSlot,
    OccupantToken? Actor, int FirstSlot, int SecondSlot);

public sealed record CampaignBattleMoveOrderContext(string BattleId, long BattleRevision, int ActorSlot,
    OccupantToken? Actor, long InstanceId, ImmutableArray<PokemonMoveSlot> Moves)
{
    public CampaignBattleMoveSwap Swap(int first, int second) => new(BattleId, BattleRevision, ActorSlot, Actor, first, second);
}

public sealed partial class CampaignSession
{
    public CampaignBattleMoveOrderContext? BattleMoveOrder => ReadBattleMoveOrder(State);

    private CampaignBattleMoveOrderContext? ReadBattleMoveOrder(CampaignState state)
    {
        if (state.DoubleBattle is { SelectingSlot: { } actor, LearningPending: false, ReplacementSlot: null } doubles)
            return Read(doubles.Id, doubles.Frame, actor);
        if (state.ForestTrainerBattle is { Stage: CampaignForestTrainerStage.Combat, CurrentTrainerEvent: null } trainer &&
            Ready(trainer.Presentation)) return Read(trainer.Id, trainer.Frame, 0);
        if (state.WildBattle is { } wild && Ready(wild)) return Read(wild.Id, wild.Frame, 0);
        if (state.Battle is { Phase: CampaignBattlePhase.ChooseMove } lab && !FirstBattleRules.RequiresStruggle(lab.State.Player))
            return new(lab.Id, lab.State.Revision, 0, null, state.Party[0].InstanceId, lab.State.Player.Pokemon.Moves);
        return null;

        static bool Ready(CampaignWildBattle battle) => battle.Phase == CampaignBattlePhase.ChooseMove &&
            battle.Replacement is null && battle.Capture is null && battle.EventIndex >= battle.Events.Length;
        CampaignBattleMoveOrderContext? Read(string id, SourceBattleFrame frame, int slot)
        {
            if (frame.FourTurn is not null || !frame.PendingSwitches.IsEmpty || frame.Rewards?.Pending is not null) return null;
            var occupant = frame.Get(new(slot));
            if (occupant.Occupant is not { } token || occupant.Body is null || token.Member.Index >= state.Party.Length ||
                occupant.Registers.Turn?.Lock is not null || occupant.Body.EncoreTurnsRemaining > 0 ||
                SourceFourActionTurns.RequiresStruggle(_binding.Ownership.Mechanics.Catalog, frame, new(slot))) return null;
            return new(id, frame.Revision, slot, token, state.Party[token.Member.Index].InstanceId, occupant.Body.Pokemon.Moves);
        }
    }

    private Decision<CampaignState> DecideBattleMoveSwap(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> Reject(string error) => new(CommandStatus.Rejected, state, error);
        if (command.BattleMoveSwap is not { } request || ReadBattleMoveOrder(state) is not { } current)
            return Reject("battle_move_order_not_available");
        if (request.BattleId != current.BattleId || request.BattleRevision != current.BattleRevision ||
            request.ActorSlot != current.ActorSlot || request.Actor != current.Actor || command.TargetInstanceId != current.InstanceId)
            return Reject("battle_move_order_stale_selection");
        if (state.Battle is { } lab)
        {
            var result = FirstBattleMoveOrderRules.Swap(BattlePokemon, lab.State, request.BattleRevision, request.FirstSlot, request.SecondSlot);
            if (!result.Succeeded) return Reject(result.Fault ?? "battle_move_order_rejected");
            return new(CommandStatus.Accepted, state with {
                Battle = lab with { State = result.State!, DisplayPlayer = result.State!.Player.Pokemon },
                Party = state.Party.SetItem(0, state.Party[0] with { Pokemon = result.State!.Player.Pokemon }) });
        }
        var frame = state.DoubleBattle?.Frame ?? state.ForestTrainerBattle?.Frame ?? state.WildBattle!.Frame;
        var changed = SourceBattleMoveOrderRules.Swap(_binding.Ownership.Mechanics.Catalog, frame, request.BattleRevision,
            current.Actor!.Value, request.FirstSlot, request.SecondSlot);
        if (!changed.Succeeded) return Reject(changed.Fault ?? "battle_move_order_rejected");
        var next = changed.State;
        if (state.DoubleBattle is { } doubles)
        {
            int member = current.Actor.Value.Member.Index;
            return new(CommandStatus.Accepted, state with {
                DoubleBattle = doubles with { Frame = next, DisplayedSlots = doubles.DisplayedSlots.SetItem(current.ActorSlot, next.Get(new(current.ActorSlot)).Body!) },
                Party = state.Party.SetItem(member, state.Party[member] with { Pokemon = next.Party(BattleSide.Player).Members[member].Pokemon }) });
        }
        if (state.ForestTrainerBattle is { } trainer)
            return new(CommandStatus.Accepted, ProjectSourceTrainerOwnedParty(state with {
                ForestTrainerBattle = trainer with { Presentation = trainer.Presentation with { Frame = next, DisplayPlayer = next.Get(new(0)).Body!.Pokemon } } }));
        return new(CommandStatus.Accepted, ProjectSourceWildOwnedParty(state with {
            WildBattle = state.WildBattle! with { Frame = next, DisplayPlayer = next.Get(new(0)).Body!.Pokemon } }));
    }
}
