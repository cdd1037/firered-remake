using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Presentation-only event-time bodies. Never converted into a gameplay frame.</summary>
public sealed record CampaignBattleDisplayBodies(
    ImmutableArray<WildBattleCombatant> Bodies, ImmutableArray<PartyMemberKey> Members)
{
    public static CampaignBattleDisplayBodies FromSlots(ImmutableArray<SourceBattleSlot> slots)
    {
        if (slots.IsDefaultOrEmpty) throw new InvalidDataException("display_body_seed_missing");
        if (slots.Length != 4 || slots.Where((s, i) => s.Slot.Value != i).Any())
            throw new InvalidDataException("display_body_seed_shape");
        bool singles = slots[2].Body is null && slots[2].Member is null && slots[3].Body is null && slots[3].Member is null;
        var present = (singles ? slots.Take(2) : slots).ToArray();
        if (present.Any(s => s.Body is null || s.Member is null))
            throw new InvalidDataException("display_body_seed_sparse");
        return new(present.Select(s => s.Body!).ToImmutableArray(), present.Select(s => s.Member!.Value).ToImmutableArray());
    }

    public CampaignBattleDisplayBodies Apply(SourceBattleEvent e)
    {
        var bodies = Bodies; var members = Members;
        void Change(BattlerSlot slot,Func<WildBattleCombatant,WildBattleCombatant> update)=>bodies=bodies.SetItem(slot.Value,update(bodies[slot.Value]));
        void Hp(BattlerSlot slot,int hp)=>Change(slot,m=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}});
        void Pp(BattlerSlot slot,int position,int pp)
        {
            Change(slot,m=>position<0||position>=m.Pokemon.Moves.Length?m:m with{Pokemon=m.Pokemon with{
                Moves=m.Pokemon.Moves.SetItem(position,m.Pokemon.Moves[position] with{CurrentPp=pp})}});
        }
        void PersistentPp(BattlerSlot slot,Game.Domain.Items.SourceItemPpRecovery recovery)=>Change(slot,m=>{
            var permanent=WildBattleTransformationRules.PersistentPokemon(m);
            if(recovery.Slot<0||recovery.Slot>=permanent.Moves.Length||permanent.Moves[recovery.Slot].Move!=recovery.Move)
                throw new InvalidDataException("preset_double_item_pp_receipt");
            return WildBattleTransformationRules.ApplyPersistentPokemon(m,permanent with{Moves=permanent.Moves.SetItem(recovery.Slot,
                permanent.Moves[recovery.Slot] with{CurrentPp=recovery.After})});
        });
        switch(e)
        {
            case SourceHpChangedEvent x:Hp(x.Target,x.HpAfter);break;
            case SourceRecoilEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourceConfusionSelfHitEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceStatusDamageEvent x:Hp(x.Side,x.HpAfter);Change(x.Side,m=>m with{Status=x.Status});break;
            case SourceWeatherDamageEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceAbilityDamageEvent x:Hp(x.Target,x.HpAfter);break;
            case SourceRecoveredEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceDrainEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourcePotionRecoveredEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceHeldItemHealEvent x:Hp(x.Actor,x.HpAfter);break;
            case SourceAbilityRecoveredEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceBindingDamageEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceIngrainHealEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceTowerHpEvent x:Hp(x.Side,x.HpAfter);break;
            case SourceLeechSeedDrainEvent x:Hp(x.DrainedSide,x.HpAfter);Hp(x.ReceivingSide,x.ReceiverHpAfter);break;
            case SourceSelfDestructEvent x:Hp(x.Actor,0);break;
            case SourceMedicineAppliedEvent x:
                for(int i=0;i<members.Length;i++)
                    if(members[i]==new PartyMemberKey(x.TargetSide??BattleSide.Player,x.PartyIndex))
                    {
                        Hp(new(i),x.HpAfter);
                        if(x.StatusBefore!=x.StatusAfter)Change(new(i),m=>m with{Status=x.StatusAfter});
                        if(x.PpRecovery is {} recoveries)foreach(var itemRecovery in recoveries)PersistentPp(new(i),itemRecovery);
                        if(x.FriendshipAfter is {} friendship)Change(new(i),m=>WildBattleTransformationRules.ApplyPersistentPokemon(m,
                            WildBattleTransformationRules.PersistentPokemon(m) with{Friendship=friendship}));
                    }
                break;
            case SourceFaintedEvent x:Hp(x.Side,0);break;
            case SourceHeldItemConsumedEvent x:
                Hp(x.Side,x.HpAfter);Change(x.Side,m=>m with{HeldItem="ITEM_NONE"});
                if(x.PpRecovery is {} heldRecovery)PersistentPp(x.Side,heldRecovery);break;
            case SourceHeldItemKnockedOffEvent x:Change(x.Target,m=>m with{HeldItem="ITEM_NONE",KnockedOffItem=x.Item});break;
            case SourceHeldItemStolenEvent x:Change(x.Target,m=>m with{HeldItem="ITEM_NONE"});Change(x.Actor,m=>m with{HeldItem=x.Item});break;
            case SourceStatusChangedEvent x:Change(x.Side,m=>m with{Status=x.After});break;
            case SourceConfusionChangedEvent x:Change(x.Side,m=>m with{ConfusionTurnsRemaining=x.After});break;
            case SourceFocusEnergyEvent x:Change(x.Side,m=>m with{FocusEnergy=true});break;
            case SourceCalledOverlayChangedEvent x:Change(x.Side,m=>m with{CalledOverlay=x.After});break;
            case SourceTransformedEvent x:
                var actor=bodies[x.Actor.Value];var target=bodies[x.Target.Value];
                var donor=target.Pokemon with{Species=x.AppearanceSpecies,SourceSpeciesId=x.BattleView.SourceSpeciesId,
                    Personality=x.AppearancePersonality,OriginalTrainerId=x.AppearanceOriginalTrainerId};
                var form=new WildBattleTransformation(WildBattleTransformationRules.PersistentPokemon(actor),donor,
                    Enumerable.Range(0,4).Select(i=>i<actor.Pokemon.Moves.Length?actor.Pokemon.Moves[i].PpUps:0).ToImmutableArray());
                Change(x.Actor,m=>m with{Pokemon=x.BattleView,Transform=form,CopyMoves=null,CalledOverlay=target.CalledOverlay,
                    AttackStage=target.AttackStage,DefenseStage=target.DefenseStage,SpeedStage=target.SpeedStage,
                    AccuracyStage=target.AccuracyStage,EvasionStage=target.EvasionStage,SpecialAttackStageDelta=target.SpecialAttackStageDelta,
                    SpecialDefenseStageDelta=target.SpecialDefenseStageDelta,DisabledMove=null,DisableTurnsRemaining=0});break;
            case SourceMoveCopiedEvent x:
                Change(x.Actor,m=>{
                    var copies=m.CopyMoves?.Slots??[];
                    if(!x.Permanent&&!copies.Any(copy=>copy.Slot==x.MoveSlot))copies=copies.Add(new(x.MoveSlot,m.Pokemon.Moves[x.MoveSlot]));
                    var moves=m.Pokemon.Moves.SetItem(x.MoveSlot,x.Replacement);
                    if(x.Permanent)copies=copies.Select(copy=>copy with{PersistentSlot=moves[copy.Slot]}).ToImmutableArray();
                    return m with{Pokemon=m.Pokemon with{Moves=moves},CopyMoves=copies.IsEmpty?null:new(copies)};
                });break;
            case SourcePpCommandEvent x:Pp(x.PpOwner,x.PpSlot,x.After);break;
            case SourceMoveUsedEvent x:Pp(x.Actor,x.Slot,x.PpAfter);break;
            case SourceSpiteEvent x:Pp(x.Target,x.Slot,x.PpAfter);break;
            case SourceGrudgePpEvent x:Pp(x.Attacker,x.ChosenSlot,0);break;
            case SourceMagicCoatPressurePpEvent x:Pp(x.Reflector,x.Slot,x.After);break;
            case SourceSnatchPressurePpEvent x:Pp(x.Snatcher,x.Slot,x.After);break;
            case SourceRewardAppliedEvent x:
                for(int i=0;i<members.Length;i++)if(members[i]==x.Recipient)
                    Change(new(i),m=>WildBattleTransformationRules.ApplyPersistentPokemon(m,x.Reward.Pokemon));
                break;
            case SourceCalledPartyChangedEvent x:
                for(int i=0;i<members.Length;i++)if(members[i]==new PartyMemberKey(x.Side,x.PartyIndex))
                    Change(new(i),m=>WildBattleTransformationRules.ApplyPersistentPokemon(m,x.After.Pokemon) with{
                        Status=x.After.Status,HeldItem=x.After.HeldItem,KnockedOffItem=x.After.KnockedOffItem});
                break;
            case SourceCalledPartySwitchedEvent x:
                bodies=bodies.SetItem(x.Side.Value,(x.Replacement??throw new InvalidDataException("preset_double_switch_snapshot_missing")).Body);
                members=members.SetItem(x.Side.Value,new(x.Side.Side,x.IncomingIndex));break;
            case SourcePlayerReplacedEvent x:bodies=bodies.SetItem(x.Slot.Value,x.Replacement.Body);break;
            case SourcePlayerSwitchedEvent x:bodies=bodies.SetItem(x.Slot.Value,x.Replacement.Body);break;
            case SourceSubstituteDamagedEvent x:Change(x.Target,m=>m with{CalledVolatile=(m.CalledVolatile??new()) with{SubstituteHp=x.HpAfter}});break;
            case SourceMtMoonVolatileEvent {Volatile:"substitute"} x:Change(x.Side,m=>m with{CalledVolatile=(m.CalledVolatile??new()) with{SubstituteHp=x.After}});break;
            case SourceMtMoonVolatileEvent {Volatile:"stockpile"} x:Change(x.Side,m=>m with{CalledVolatile=(m.CalledVolatile??new()) with{StockpileCount=x.After}});break;
            case SourceStatChangedEvent x:
                Change(x.Target,m=>x.Stat switch {
                    WildBattleStat.Attack=>m with{AttackStage=x.After},WildBattleStat.Defense=>m with{DefenseStage=x.After},
                    WildBattleStat.Speed=>m with{SpeedStage=x.After},WildBattleStat.Accuracy=>m with{AccuracyStage=x.After},
                    WildBattleStat.Evasion=>m with{EvasionStage=x.After},WildBattleStat.SpecialAttack=>m with{SpecialAttackStageDelta=x.After-6},
                    WildBattleStat.SpecialDefense=>m with{SpecialDefenseStageDelta=x.After-6},_=>m});break;
        }
        return new(bodies, members);
    }
}

/// <summary>Transient cursor over an owned canonical event batch. Same-sequence legacy mirrors are idempotent.</summary>
public sealed record CampaignBattleDisplayBatch(CampaignBattleDisplayBodies Display, int NextEventIndex = 0)
{
    public CampaignBattleDisplayBatch AdvanceThrough(ImmutableArray<SourceBattleEvent> events, long sequence)
    {
        if (events.IsDefault || NextEventIndex < 0 || NextEventIndex > events.Length)
            throw new InvalidDataException("display_body_event_cursor");
        var display = Display; int index = NextEventIndex;
        while (index < events.Length && events[index].Sequence <= sequence)
        {
            if (index > 0 && events[index].Sequence <= events[index - 1].Sequence)
                throw new InvalidDataException("display_body_event_order");
            display = display.Apply(events[index++]);
        }
        return new(display, index);
    }
}
