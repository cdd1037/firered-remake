using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private static bool DoubleEventChangesHp(SourceBattleEvent? current)=>current is SourceHpChangedEvent or SourceRecoilEvent or SourceConfusionSelfHitEvent or
        SourceStatusDamageEvent or SourceWeatherDamageEvent or SourceAbilityDamageEvent or SourceRecoveredEvent or SourceDrainEvent or SourceMedicineAppliedEvent or
        SourcePotionRecoveredEvent or SourceHeldItemHealEvent or SourceHeldItemConsumedEvent or SourceAbilityRecoveredEvent or SourceBindingDamageEvent or
        SourceLeechSeedDrainEvent or SourceIngrainHealEvent or SourceTowerHpEvent or SourceSelfDestructEvent;
    private int DoubleEventHp(int slot,WildBattleCombatant body)
    {
        var battle=_session.State.DoubleBattle!;
        (int Before,int After)? span=battle.CurrentEvent switch {
            SourceHpChangedEvent x when x.Target.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceRecoilEvent x when x.Actor.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceConfusionSelfHitEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceStatusDamageEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceWeatherDamageEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceAbilityDamageEvent x when x.Target.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceRecoveredEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceDrainEvent x when x.Actor.Value==slot=>(x.HpBefore,x.HpAfter),
            SourcePotionRecoveredEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceHeldItemHealEvent x when x.Actor.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceHeldItemConsumedEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceAbilityRecoveredEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceBindingDamageEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceLeechSeedDrainEvent x when x.DrainedSide.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceLeechSeedDrainEvent x when x.ReceivingSide.Value==slot=>(x.ReceiverHpBefore,x.ReceiverHpAfter),
            SourceIngrainHealEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceTowerHpEvent x when x.Side.Value==slot=>(x.HpBefore,x.HpAfter),
            SourceSelfDestructEvent x when x.Actor.Value==slot=>(x.HpBefore,0),
            SourceMedicineAppliedEvent x when battle.DisplayedMembers[slot]==new PartyMemberKey(x.TargetSide??BattleSide.Player,x.PartyIndex)=>(x.HpBefore,x.HpAfter),
            _=>null};
        return span is {} hp?(int)Math.Round(Mathf.Lerp(hp.Before,hp.After,(float)Math.Clamp(_doubleElapsed/.4,0,1))):body.Pokemon.CurrentHp;
    }
    private uint DoubleEventStatus(int slot,WildBattleCombatant body)=>_session.State.DoubleBattle!.CurrentEvent is SourceStatusChangedEvent x&&x.Side.Value==slot
        ?(_doubleElapsed<.12?x.Before:x.After):body.Status;
    private void DrawDoubleSubstitute(int slot)
    {
        EnsureSourceMoveAssets();bool player=(slot&1)==0;
        DrawTexture(_sourceSubstituteTextures[player?"substitute-back":"substitute-front"],(player?new Vector2(40,65):new Vector2(144,24))+DoubleSpriteOffsets[slot]);
    }
}
