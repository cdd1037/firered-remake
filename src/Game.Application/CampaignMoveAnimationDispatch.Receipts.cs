using Game.Domain.Battle;
using Game.Domain.WildBattle;
namespace Game.Application;

public static partial class CampaignMoveAnimationPresentation
{
    // Singles expose the historical two-side envelope. Normalize only the receipt
    // kinds consumed by invocation matching; unconsumed rows retain order/identity.
    private sealed record UninterpretedAnimationReceipt(long Sequence,int Turn):SourceBattleEvent(Sequence,Turn);
    internal static SourceBattleEvent AnimationReceipt(WildBattleEvent receipt)=>receipt switch
    {
        WildBattleActionBlockedEvent x => new SourceActionBlockedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Move,x.Status),
        WildBattleAbilityActivatedEvent x => new SourceAbilityActivatedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Ability),
        WildBattleAbilityPreventedEvent x => new SourceAbilityPreventedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.Ability),
        WildBattleAttackStringEvent x => new SourceAttackStringEvent(x.Sequence,x.Turn,new((int)x.Actor),x.ChosenMove,x.CurrentMove),
        WildBattleCallerSelectionEvent x => new SourceCallerSelectionEvent(x.Sequence,x.Turn,new((int)x.Actor),x.Caller,x.Result,x.CandidateSourceMoveIds,x.RngCursor),
        WildBattleCalledMoveTransitionEvent x => new SourceCalledMoveTransitionEvent(x.Sequence,x.Turn,
            new(new((int)x.Invocation.Actor),x.Invocation.ChosenSlot,x.Invocation.PpSlot,x.Invocation.OriginalChosenMove,
                x.Invocation.ChosenMove,x.Invocation.CurrentMove,x.Invocation.CalledMove,new((int)x.Invocation.Target),
                x.Invocation.CancellerPhase,x.Invocation.Flags) { PpOwner=new((int)x.Invocation.PpOwner) }),
        WildBattleSleepTalkEvent x => new SourceSleepTalkEvent(x.Sequence,x.Turn,new((int)x.Side),x.CalledSlot,x.CalledMove),
        WildBattleCalledPartyChangedEvent x => new SourceCalledPartyChangedEvent(x.Sequence,x.Turn,(BattleSide)x.Side,x.PartyIndex,x.Reason,x.Before,x.After),
        WildBattleCalledOverlayChangedEvent x => new SourceCalledOverlayChangedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Move,x.Before,x.After),
        WildBattleCalledDelayedEvent x => new SourceCalledDelayedEvent(x.Sequence,x.Turn,new((int)x.Target),x.Move,x.Before,x.After,x.Reason,x.SourcePartyIndex),
        WildBattleConfusionChangedEvent x => new SourceConfusionChangedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Before,x.After,x.Reason),
        WildBattleCriticalHitEvent x => new SourceCriticalHitEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target)),
        WildBattleDamageAdjustedEvent x => new SourceDamageAdjustedEvent(x.Sequence,x.Turn,new((int)x.Target),x.Move,x.Item,x.DamageBefore,x.DamageAfter,x.Endured,x.HungOn),
        WildBattleDisableChangedEvent x => new SourceDisableChangedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Move,x.Before,x.After,x.Reason),
        WildBattleEffectivenessEvent x => new SourceEffectivenessEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.Effectiveness),
        WildBattleFocusEnergyEvent x => new SourceFocusEnergyEvent(x.Sequence,x.Turn,new((int)x.Side)),
        WildBattleHeldItemConsumedEvent x => new SourceHeldItemConsumedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Item,x.HpBefore,x.HpAfter,x.HpRestored) { PpRecovery=x.PpRecovery,StatChanges=x.StatChanges,FocusEnergyRaised=x.FocusEnergyRaised,Flavor=x.Flavor },
        WildBattleHpChangedEvent x => new SourceHpChangedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.HpBefore,x.HpAfter,x.Damage,x.Critical,x.RandomPercent) { PreparedBasePower = x.PreparedBasePower, PreparedFuryCutterCounter = x.PreparedFuryCutterCounter },
        WildBattleInfatuationChangedEvent x => new SourceInfatuationChangedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Before is {} before?new((int)before):null,x.After is {} after?new((int)after):null,x.Reason),
        WildBattleLeechSeedChangedEvent x => new SourceLeechSeedChangedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Before is {} before?new((int)before):null,x.After is {} after?new((int)after):null,x.Reason),
        WildBattleMagnitudeEvent x => new SourceMagnitudeEvent(x.Sequence,x.Turn,new((int)x.Actor),x.Magnitude,x.Power),
        WildBattlePursuitInterceptedEvent x => new SourcePursuitInterceptedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.SwitchingSide),x.Slot),
        WildBattleMoveFailedEvent x => new SourceMoveFailedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.Reason),
        WildBattleMoveMissedEvent x => new SourceMoveMissedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move),
        WildBattleForcedOutEvent x => new SourceForcedOutEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.TrainerBattle),
        WildBattleEndedEvent x => new SourceEndedEvent(x.Sequence,x.Turn,x.Outcome),
        WildBattleMultiHitEvent x => new SourceMultiHitEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.PlannedHits,x.ActualHits),
        WildBattleCalledBeatUpMemberEvent x => new SourceCalledBeatUpMemberEvent(x.Sequence,x.Turn,(BattleSide)x.Side,x.PartyIndex,x.Species),
        WildBattleMoveCopiedEvent x => new SourceMoveCopiedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.CopiedMove,x.MoveSlot,x.Permanent,x.Replacement),
        WildBattleMoveUsedEvent x => new SourceMoveUsedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Slot,x.Move,x.PpBefore,x.PpAfter),
        WildBattleMtMoonVolatileEvent x => new SourceMtMoonVolatileEvent(x.Sequence,x.Turn,new((int)x.Side),x.Volatile,x.Before,x.After,x.Reason),
        WildBattleOrderEvent x => new SourceOrderEvent(x.Sequence,x.Turn,x.Phase,[new((int)x.First),new((int)x.Second)]),
        WildBattlePpCommandEvent x => new SourcePpCommandEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.PpSlot,x.ChosenMove,x.CurrentMove,x.Before,x.After,x.CommandCost,x.RngCursor) { PpOwner=new((int)x.PpOwner) },
        WildBattleProtectAttemptEvent x => new SourceProtectAttemptEvent(x.Sequence,x.Turn,new((int)x.Side),x.Move,x.Before,x.After,x.Threshold,x.Roll,x.NotLastAction,x.Succeeded),
        WildBattleRecoveredEvent x => new SourceRecoveredEvent(x.Sequence,x.Turn,new((int)x.Side),x.Move,x.HpBefore,x.HpAfter,x.Recovered),
        WildBattleRootsChangedEvent x => new SourceRootsChangedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Rooted,x.Reason),
        WildBattleSelfDestructEvent x => new SourceSelfDestructEvent(x.Sequence,x.Turn,new((int)x.Actor),x.Move,x.HpBefore),
        WildBattleSnatchReadyEvent x => new SourceSnatchReadyEvent(x.Sequence,x.Turn,new((int)x.Actor),x.ActionOrderIndex),
        WildBattleStatChangedEvent x => new SourceStatChangedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Stat,x.Before,x.After),
        WildBattleStatusChangedEvent x => new SourceStatusChangedEvent(x.Sequence,x.Turn,new((int)x.Side),x.Before,x.After,x.Reason),
        WildBattleSubstituteDamagedEvent x => new SourceSubstituteDamagedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.Move,x.HpBefore,x.HpAfter,x.Damage,x.Critical,x.RandomPercent) { PreparedBasePower = x.PreparedBasePower, PreparedFuryCutterCounter = x.PreparedFuryCutterCounter },
        WildBattleTowerVolatileEvent x => new SourceTowerVolatileEvent(x.Sequence,x.Turn,new((int)x.Side),x.Effect,x.Applied,x.Reason),
        WildBattleTransformedEvent x => new SourceTransformedEvent(x.Sequence,x.Turn,new((int)x.Actor),new((int)x.Target),x.OriginalSpecies,x.AppearanceSpecies,x.AppearancePersonality,x.AppearanceOriginalTrainerId,x.BattleView),
        WildBattleTurnReadyEvent x => new SourceTurnReadyEvent(x.Sequence,x.Turn,x.RandomTurnNumber),
        _ => new UninterpretedAnimationReceipt(receipt.Sequence,receipt.Turn)
    };
}
