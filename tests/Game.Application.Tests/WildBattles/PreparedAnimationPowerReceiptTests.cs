using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Rules;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Change=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Legal=FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class PreparedAnimationPowerReceiptTests
{
    private static SourceBattleFrame StarterAbilityFrame(WildBattleCombatant actor,WildBattleCombatant target,bool single,bool water)
    {
        if(single)
        {
            // The acquisition proof is an actual two-Skill-Swap doubles flow.
            // Singles uses its already-produced body as controlled post-entry
            // state, not a claim of natural singles acquisition or new entry.
            var acquiredBody=StarterAbilityFrame(actor,target,false,water).Slots[0].Body!;
            var initial=Kernel.Frame([actor,target,actor,target],true);
            return Change.Body(initial,0,_=>acquiredBody);
        }
        // Stay in one initialized battle: Abra takes the starter's ability,
        // the starter switches to the natural attacker, and Abra swaps again.
        // A second real switch restores the chosen target without reinjecting
        // an acquired overlay through the initial-entry boundary.
        var abra=Legal.MachineMon("ABRA","TM48","SKILL_SWAP","TELEPORT");
        var donor=Kernel.Mon(water?"SQUIRTLE":"CHARMANDER",water?"TACKLE":"SCRATCH");
        var filler=Kernel.Mon(water?"WAILORD":"CAMERUPT",water?"SPLASH":"GROWL").Pokemon.Moves[0];
        actor=actor with{Pokemon=actor.Pokemon with{Moves=actor.Pokemon.Moves.Add(filler)}};
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100)).ToArray();
        bodies[0]=donor;bodies[1]=abra;
        var frame=Kernel.Frame(bodies,single) with{BattleKind=WildBattleCalledBattleKind.Trainer};
        int reserve=single?1:2;
        frame=frame with{Parties=frame.Parties.SetItem(0,frame.Parties[0] with{Members=frame.Parties[0].Members.Add(new(actor.Pokemon))})
            .SetItem(1,frame.Parties[1] with{Members=frame.Parties[1].Members.Add(new(target.Pokemon))})};
        var acquired=Envelope.Resolve(frame,1,0);frame=acquired.State;
        Assert.AreEqual(water?"ABILITY_TORRENT":"ABILITY_BLAZE",WildBattleEffective.Ability(frame.Slots[1].Body!));
        frame=SwitchTurn(frame,0,reserve);
        Assert.AreEqual(water?"ABILITY_TORRENT":"ABILITY_BLAZE",WildBattleEffective.Ability(frame.Slots[0].Body!));
        frame=SwitchTurn(frame,1,reserve);
        Assert.AreEqual(target.Pokemon.Species,frame.Slots[1].Body!.Pokemon.Species);
        Assert.AreEqual(water?"ABILITY_TORRENT":"ABILITY_BLAZE",WildBattleEffective.Ability(frame.Slots[0].Body!));
        return frame;
    }
    private static SourceBattleFrame SwitchTurn(SourceBattleFrame frame,int switching,int reserve)
    {
        var choices=frame.Slots.Take(frame.BattlerCount).Select(cell=>
        {
            if(cell.Slot.Value==switching)return new SourceActionSelection(cell.Occupant!.Value,SourceActionKind.Switch,
                Incoming:new(cell.Slot.Side,reserve));
            int moveSlot=switching==1&&cell.Slot.Value==0?1:0;
            var move=Kernel.Mechanics.Catalog.Moves[cell.Body!.Pokemon.Moves[moveSlot].Move];
            var target=move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?cell.Slot:cell.Slot.Opposite;
            return new SourceActionSelection(cell.Occupant!.Value,SourceActionKind.Move,moveSlot,target);
        }).ToImmutableArray();
        var started=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);
        Assert.IsTrue(started.Succeeded,started.Fault);return Change.CompleteTurn(started);
    }
    private static SourceBattleFrame Frame(int id,bool single,bool sport,bool boost,bool substitute,bool minimum)
    {
        var actor=Kernel.Mon(id==323?"WAILORD":"CAMERUPT",id==323?"WATER_SPOUT":"ERUPTION");
        var target=sport?Kernel.Mon("PSYDUCK","WATER_SPORT",100):Kernel.Mon("WAILORD","SPLASH",100);
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100)).ToArray();bodies[0]=actor;bodies[1]=target;
        var frame=boost?StarterAbilityFrame(actor,target,single,id==323):Kernel.Frame(bodies,single);
        for(int slot=0;slot<frame.BattlerCount;slot++)frame=Change.Body(frame,slot,b=>b with{SpeedStage=0});
        frame=Change.Body(frame,0,b=>b with{AccuracyStage=12,SpeedStage=12,Pokemon=b.Pokemon with{CurrentHp=minimum?1:b.Pokemon.Stats.MaxHp/3}});
        if(sport){frame=Envelope.Resolve(frame,1,1).State;Assert.IsTrue(frame.Slots[1].Body!.WaterSport);}
        if(substitute)foreach(int slot in single?new[]{1}:new[]{1,3})frame=Change.Body(frame,slot,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
        return frame;
    }
    private static (int? Base,int? Animation,int Damage,bool Critical,int Percent,int Target) Receipt(SourceBattleEvent row)=>row switch
    {
        SourceHpChangedEvent h=>(h.PreparedBasePower,h.PreparedAnimationPower,h.Damage,h.Critical,h.RandomPercent,h.Target.Value),
        SourceSubstituteDamagedEvent h=>(h.PreparedBasePower,h.PreparedAnimationPower,h.Damage,h.Critical,h.RandomPercent,h.Target.Value),
        _=>throw new InvalidOperationException()
    };
    private static SourceBattleEvent AnimationPower(SourceBattleEvent row,int? value)=>row switch
    {
        SourceHpChangedEvent h=>h with{PreparedAnimationPower=value},SourceSubstituteDamagedEvent h=>h with{PreparedAnimationPower=value},
        _=>throw new InvalidOperationException()
    };
    private static CampaignMoveAnimationDispatch Cue(ImmutableArray<SourceBattleEvent> events,bool single)
    {
        int at=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
        // Both layouts consume the same canonical receipt identities through their envelopes.
        var decision=single?AnimationDispatchFixture.AtAttackText(Change.Single(events,at),true,SourceBattleScene.On):
            AnimationDispatchFixture.AtAttackText(Envelope.Double(events,at),SourceBattleScene.On);
        Assert.IsNotNull(decision.Dispatch,decision.Reason);return decision.Dispatch;
    }

    [TestMethod,DataRow(284),DataRow(323)]
    public void ActualAnimationPowerPreservesDynamicPowerDamageAndDrawsThroughSportAndStarterAbilities(int id)
    {
        foreach(bool single in new[]{false,true})foreach(bool substitute in new[]{false,true})
        foreach(bool sport in new[]{false,true})foreach(bool boost in new[]{false,true})foreach(bool minimum in new[]{false,true})
        {
            var frame=Frame(id,single,sport,boost,substitute,minimum);string before=JsonSerializer.Serialize(frame);
            var actor=frame.Slots[0].Body!;int prepared=Math.Max(1,actor.Pokemon.CurrentHp*150/actor.Pokemon.Stats.MaxHp);
            int expectedAnimationPower=prepared;if(sport&&id==284)expectedAnimationPower/=2;if(boost)expectedAnimationPower=expectedAnimationPower*150/100;
            var result=Envelope.Resolve(frame);var hits=Envelope.Hits(result.Events);Assert.AreEqual(single?1:2,hits.Length);
            foreach(var hit in hits)
            {
                var h=Receipt(hit);Assert.AreEqual(prepared,h.Base);Assert.AreEqual(expectedAnimationPower,h.Animation);Assert.AreEqual(substitute,hit is SourceSubstituteDamagedEvent);
                var target=frame.Slots[h.Target].Body!;var move=Kernel.Mechanics.Catalog.Moves[actor.Pokemon.Moves[0].Move];
                var calculated=SourceBattleDamage.Calculate(Kernel.Mechanics.Catalog,actor,target,move,h.Critical,h.Percent,
                    dynamicPower:prepared,sourceDoubleBattle:!single,sourceDefenderCount:single?1:2,sourceWaterSport:sport);
                Assert.AreEqual(expectedAnimationPower,calculated.SourceMovePower);
                Assert.AreEqual(actor.Pokemon.Stats.SpecialAttack*expectedAnimationPower,calculated.Steps.Single(s=>s.Operation=="attack-times-power").Value);
                // Independent pinned integer order for these special, same-type
                // attackers and Water-type targets; no weather/held/stat boosts.
                int damage=actor.Pokemon.Stats.SpecialAttack*expectedAnimationPower*(2*actor.Pokemon.Level/5+2)/target.Pokemon.Stats.SpecialDefense/50;
                if(!single)damage/=2;damage+=2;if(h.Critical)damage*=2;damage=damage*15/10;damage/=2;
                damage=Math.Max(1,damage*h.Percent/100);Assert.AreEqual(damage,h.Damage);Assert.AreEqual(damage,calculated.Damage);
                if(single)
                {
                    var exported=OverlayHitAnimationKernelFixture.Single([hit], 0).Events[0];Assert.AreEqual(expectedAnimationPower,exported switch{
                        SourceHpChangedEvent e=>e.PreparedAnimationPower,SourceSubstituteDamagedEvent e=>e.PreparedAnimationPower,_=>null});
                }
            }
            Assert.AreEqual((ushort)expectedAnimationPower,Cue(result.Events,single).MovePower);
            string[] suffixes=[".accuracy",".critical",".damage",".secondary-effect"];int[] bounds=[100,16,16,100];
            Assert.AreEqual(hits.Length*4,result.Trace.Length);uint seed=frame.Rng.Seed;ulong cursor=frame.Rng.Cursor;
            for(int i=0;i<result.Trace.Length;i++)
            {
                var draw=result.Trace[i];Assert.IsTrue(draw.Purpose.EndsWith(suffixes[i%4],StringComparison.Ordinal));Assert.AreEqual(bounds[i%4],draw.Bound);
                Assert.AreEqual(seed,draw.Before);seed=unchecked(seed*0x41C64E6Du+0x6073u);ushort raw=(ushort)(seed>>16);
                Assert.AreEqual(++cursor,draw.Cursor);Assert.AreEqual(seed,draw.After);Assert.AreEqual(raw,draw.Raw);Assert.AreEqual(raw%draw.Bound,draw.Result);
            }
            Assert.AreEqual(seed,result.State.Rng.Seed);Assert.AreEqual(cursor,result.State.Rng.Cursor);
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Envelope.Resolve(frame)));
        }
    }

    [TestMethod]
    public void MissingNegativeAndOverflowAnimationPowerFailClosedForBothReceiptKindsAndEnvelopes()
    {
        foreach(bool single in new[]{false,true})foreach(bool substitute in new[]{false,true})
        {
            var events=Envelope.Resolve(Frame(323,single,false,true,substitute,false)).Events;var first=Envelope.Hits(events)[0];
            foreach(int? bad in new int?[]{null,-1,65536})Envelope.Deny(events.SetItem(events.IndexOf(first),AnimationPower(first,bad)),single,"Invalid source animation power");
            if(!single)
            {
                // Frozen command power remains equal across passes. The actual
                // calculation register is pass-local and need not be equal.
                var second=Envelope.Hits(events)[1];var changed=events.SetItem(events.IndexOf(second),AnimationPower(second,1));
                Assert.AreEqual(Cue(events,false).MovePower,Cue(changed,false).MovePower);
            }
        }
    }

    [TestMethod]
    public void FaintingWaterSportOwnerChangesLaterCalculationWithoutChangingTheFrozenCommand()
    {
        var frame=Frame(284,false,true,false,false,false);
        frame=Change.Body(frame,1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1}});
        var result=Kernel.Ordered(frame);var hits=Envelope.Hits(result.Events);Assert.AreEqual(2,hits.Length);
        var first=Receipt(hits[0]);var second=Receipt(hits[1]);Assert.AreEqual(first.Base,second.Base);
        Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side==new BattlerSlot(1)));
        Assert.AreEqual(first.Base/2,first.Animation);Assert.AreEqual(second.Base,second.Animation);
        Assert.AreEqual((ushort)first.Animation!.Value,Cue(result.Events,false).MovePower);
    }
}
