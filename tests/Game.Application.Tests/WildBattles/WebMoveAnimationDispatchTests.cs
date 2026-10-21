using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class WebMoveAnimationDispatchTests
{
    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static string Name(int id)=>id switch {43=>"LEER",81=>"STRING_SHOT",169=>"SPIDER_WEB",313=>"FAKE_TEARS",_=>throw new ArgumentOutOfRangeException(nameof(id))};
    private static MoveId Move(int id)=>new("pret:MOVE_"+Name(id));
    private static bool Reduction(int id)=>id!=169;
    private static bool Spread(int id)=>id is 43 or 81;
    private static WildBattleStat ReducedStat(int id)=>id switch {43=>WildBattleStat.Defense,313=>WildBattleStat.SpecialDefense,_=>WildBattleStat.Speed};
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> e,int i)=>new("web",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,e,i,[],[],0,0,[],[],[],[],0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> e,int i)=>new("web",null!,new(),e,i,null!,null!,CampaignBattlePhase.Events,[],0,[]);
    private static WildBattleCombatant Mon(string species,params string[] names)
    {
        var p=SourcePokemonFactory.Create(Mechanics,new(new("pret:SPECIES_"+species),50,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
        var moves=names.Select(name=>{var m=new MoveId("pret:MOVE_"+name);Assert.IsTrue(Mechanics.Catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==m&&x.Level<=50));int pp=Mechanics.Catalog.Moves[m].Pp;return new PokemonMoveSlot(m,pp,pp);}).ToImmutableArray();
        return new(p with{Moves=moves});
    }
    private static WildBattleCombatant[] Bodies(int id,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","TACKLE")).ToArray();
        bodies[actor]=Mon(id is 43 or 313?"TEDDIURSA":"SPINARAK",Name(id)) with {AccuracyStage=12};
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, single ? [Member(bodies[0])] : [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(0) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }


    private static SourceStatChangedEvent[] Reductions(SourceKernelResult r,int actor,int id=81) =>
        r.Events.OfType<SourceStatChangedEvent>().Where(s=>s.Actor.Value==actor&&s.Target.Value!=actor&&s.Stat==ReducedStat(id)&&s.After<s.Before).ToArray();
    private static void Check(SourceKernelResult result,int id,int actor,bool single=false)
    {
        string snapshot=JsonSerializer.Serialize(result);var e=result.Events;var used=e.OfType<SourceMoveUsedEvent>().Single(x=>x.Move==Move(id));
        SourceBattleEvent[] outcomes=Reduction(id)?Reductions(result,actor,id).Cast<SourceBattleEvent>().ToArray():e.OfType<SourceTowerVolatileEvent>().Where(v=>v.Effect=="mean-look"&&v.Applied).Cast<SourceBattleEvent>().ToArray();
        int roots=0,waves=0;
        for(int i=0;i<e.Length;i++)
        {
            CampaignMoveAnimationDecision At(SourceBattleScene scene)=>single?AnimationDispatchFixture.AtAttackText(Single(e,i),true,scene):AnimationDispatchFixture.AtAttackText(Double(e,i),scene);
            var decision=At(SourceBattleScene.On);Assert.AreEqual(decision,At(SourceBattleScene.On));Assert.IsNull(At(SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Single(e,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Double(e,i),SourceBattleScene.On).Dispatch);
            if(e[i]==used&&outcomes.Length>0)
            {
                var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);int target=outcomes[0] switch {SourceStatChangedEvent s=>s.Target.Value,SourceTowerVolatileEvent v=>v.Side.Value,_=>throw new InvalidOperationException()};
                var pp=e.OfType<SourcePpCommandEvent>().Single(p=>p.Target.Value==target);
                Assert.AreEqual(id,cue.SourceMoveId);Assert.AreEqual(actor,cue.Actor.Value);Assert.AreEqual(target,cue.Target.Value);
                Assert.AreEqual(outcomes[0].Sequence,cue.ResultSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(pp.Sequence+1,cue.ResultSequence);roots++;
            }
            else Assert.IsNull(decision.Dispatch);
            var wave=single?AnimationDispatchFixture.StatAtResult(Single(e,i),true):AnimationDispatchFixture.StatAtResult(Double(e,i));
            if(Reduction(id)&&outcomes.Contains(e[i]))
            {
                Assert.IsNotNull(wave);var stat=(SourceStatChangedEvent)e[i];Assert.AreEqual(id,wave.SourceMoveId);Assert.AreEqual(stat.Target,wave.Affected);Assert.AreEqual(id==43?23:id==313?50:24,wave.AnimationArgument);
                Assert.AreEqual(stat.Sequence,wave.Sequence);Assert.AreEqual(e.OfType<SourcePpCommandEvent>().Single(p=>p.Target==stat.Target).Sequence,wave.PpSequence);waves++;
            }
            else Assert.IsNull(wave);
        }
        Assert.AreEqual(outcomes.Length>0?1:0,roots);Assert.AreEqual(Reduction(id)?outcomes.Length:0,waves);
        Assert.AreEqual(snapshot,JsonSerializer.Serialize(result),"Presentation must not mutate state, RNG, trace or receipts.");
    }
    private static void Pp(SourceKernelResult r,int actor,int visits,int cost)
    {
        var rows=r.Events.OfType<SourcePpCommandEvent>().ToArray();Assert.AreEqual(visits,rows.Length);
        var used=r.Events.OfType<SourceMoveUsedEvent>().Single();Assert.AreEqual(used.PpBefore,rows[0].Before);Assert.AreEqual(used.PpAfter,rows[0].After);
        foreach(var row in rows){Assert.AreEqual(actor,row.Actor.Value);Assert.AreEqual(actor,row.PpOwner.Value);Assert.AreEqual(cost,row.CommandCost);Assert.AreEqual(used.Move,row.ChosenMove);Assert.AreEqual(used.Move,row.CurrentMove);Assert.AreEqual(0,row.PpSlot);}
        Assert.AreEqual(Math.Max(0,rows[0].Before-cost),rows[0].After);
        foreach(var row in rows.Skip(1)){Assert.AreEqual(rows[0].After,row.Before);Assert.AreEqual(row.Before,row.After);}
        Assert.AreEqual(rows[^1].After,r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
    }
    [TestMethod][DataRow(43)][DataRow(81)][DataRow(169)][DataRow(313)]
    public void NaturalPrimaryStatusLearnersAllFourPositionsAndBothSinglesSidesProjectCommittedResults(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var r=Resolve(Frame(Bodies(id,actor),single),actor,actor^1);
            Assert.IsFalse(r.Events.OfType<SourceMoveFailedEvent>().Any());
            Assert.AreEqual(Spread(id)&&!single?2:1,Reduction(id)?Reductions(r,actor,id).Length:r.Events.OfType<SourceTowerVolatileEvent>().Count(x=>x.Effect=="mean-look"));
            Pp(r,actor,Spread(id)&&!single?2:1,1);Check(r,id,actor,single);
        }
    }
    [TestMethod][DataRow(43)][DataRow(313)]
    public void PrimaryReductionStageCapsAndHerbRestorationKeepRequestedWave(int id)
    {
        foreach(bool single in new[]{false,true})foreach(int stage in new[]{0,1,6})
        {
            var b=Bodies(id);
            for(int slot=1;slot<4;slot+=2)b[slot]=id==43?b[slot] with{DefenseStage=stage}:b[slot] with{SpecialDefenseStageDelta=stage-6};
            var r=Resolve(Frame(b,single));var reductions=Reductions(r,0,id);
            Assert.AreEqual(stage==0?0:Spread(id)&&!single?2:1,reductions.Length);
            foreach(var result in reductions){Assert.AreEqual(stage,result.Before);Assert.AreEqual(Math.Max(0,stage-(id==313?2:1)),result.After);}
            Check(r,id,0,single);
        }
        var bodies=Bodies(id);bodies[1]=bodies[1] with{HeldItem="ITEM_WHITE_HERB"};
        var herb=Resolve(Frame(bodies));Assert.AreEqual(1,herb.Events.OfType<SourceHeldItemConsumedEvent>().Count());
        Assert.IsTrue(herb.Events.OfType<SourceStatChangedEvent>().Any(e=>e.Actor.Value==1&&e.Target.Value==1&&e.Stat==ReducedStat(id)&&e.After==6));
        Check(herb,id,0);
    }
    [TestMethod][DataRow(43,"protect")][DataRow(43,"substitute")][DataRow(43,"clear-body")]
    [DataRow(313,"protect")][DataRow(313,"substitute")][DataRow(313,"clear-body")]
    public void PrimaryReductionPreventionKeepsSuccessfulSpreadPartnerOnly(int id,string reason)
    {
        foreach(bool single in new[]{false,true})
        {
            var b=Bodies(id);b[1]=reason switch {"protect"=>b[1] with{Protected=true},
                "substitute"=>b[1] with{CalledVolatile=new(){SubstituteHp=1}},
                _=>Mon("TENTACOOL","ACID")};
            var r=Resolve(Frame(b,single));var rows=Reductions(r,0,id);
            Assert.AreEqual(Spread(id)&&!single?1:0,rows.Length);if(rows.Length>0)Assert.AreEqual(3,rows[0].Target.Value);
            Assert.IsTrue(reason=="clear-body"?r.Events.OfType<SourceAbilityPreventedEvent>().Any():r.Events.OfType<SourceMoveFailedEvent>().Any());
            Check(r,id,0,single);
        }
    }
    [TestMethod]
    public void FakeTearsSelectedAllyAndMalformedResultStayReceiptOwned()
    {
        for(int actor=0;actor<4;actor++)
        {
            var result=Resolve(Frame(Bodies(313,actor)),actor,actor^2);
            Assert.AreEqual(actor^2,Reductions(result,actor,313).Single().Target.Value);Check(result,313,actor);
        }
        foreach(bool single in new[]{false,true})
        {
            var e=Resolve(Frame(Bodies(313),single)).Events;int at=Array.FindIndex(e.ToArray(),x=>x is SourceStatChangedEvent);var row=(SourceStatChangedEvent)e[at];
            foreach(var bad in new[]{row with{Actor=new(1)},row with{Target=new(0)},row with{Stat=WildBattleStat.Defense},row with{After=row.Before-1},row with{After=row.Before},row with{Before=13}})
                Reject(e.SetItem(at,bad),single);
            Reject(e.RemoveAt(at),single);Reject(e.Insert(at+1,row with{Sequence=row.Sequence+1}),single);
        }
    }
    [TestMethod]
    public void SpiderWebSelectedAllyIsTheActualRecipientAtEveryPosition()
    {
        for(int actor=0;actor<4;actor++)
        {
            var r=Resolve(Frame(Bodies(169,actor)),actor,actor^2);
            Assert.AreEqual(actor^2,r.Events.OfType<SourceTowerVolatileEvent>().Single(x=>x.Effect=="mean-look").Side.Value);
            Assert.AreEqual(new BattlerSlot(actor),r.State.Slots[actor^2].Registers.MeanLookSource);Pp(r,actor,1,1);Check(r,169,actor);
        }
    }
    [TestMethod][DataRow("cap")][DataRow("protect")][DataRow("substitute")][DataRow("clear-body")][DataRow("white-smoke")]
    public void StringShotBlockedFirstPassStillOwnsOneRootAndOneWaveOnSecond(string branch)
    {
        var b=Bodies(81);b[1]=branch switch {
            "cap"=>b[1] with{SpeedStage=0},"protect"=>b[1] with{Protected=true},
            "substitute"=>b[1] with{CalledVolatile=new(){SubstituteHp=1}},
            "clear-body"=>Mon("TENTACOOL","ACID"),"white-smoke"=>Mon("TORKOAL","EMBER"),_=>throw new InvalidOperationException()};
        if(branch=="clear-body")Assert.AreEqual("ABILITY_CLEAR_BODY",WildBattleEffective.Ability(b[1]));
        if(branch=="white-smoke")Assert.AreEqual("ABILITY_WHITE_SMOKE",WildBattleEffective.Ability(b[1]));
        var r=Resolve(Frame(b));Assert.AreEqual(3,Reductions(r,0).Single().Target.Value);
        if(branch=="cap")Assert.AreEqual(1,r.Events.OfType<SourceStatAtMinimumEvent>().Count());
        else if(branch is "clear-body" or "white-smoke")Assert.AreEqual(1,r.Events.OfType<SourceAbilityPreventedEvent>().Count());
        else Assert.IsTrue(r.Events.OfType<SourceMoveFailedEvent>().Any(f=>f.Target.Value==1&&f.Reason==(branch=="protect"?"protected":"substitute")));
        Pp(r,0,2,1);Check(r,81,0);
    }
    [TestMethod]
    public void StringShotActualAccuracyMissThenSuccessfulPartnerOwnsSecondReceipt()
    {
        var b=Bodies(81);b[0]=b[0] with{AccuracyStage=0};b[1]=b[1] with{EvasionStage=12};var start=Frame(b);
        for(uint seed=0;seed<4096;seed++)
        {
            var r=Resolve(start with{Rng=new(seed)});
            if(!r.Events.OfType<SourceMoveMissedEvent>().Any(m=>m.Target.Value==1)||Reductions(r,0).Length!=1||Reductions(r,0)[0].Target.Value!=3)continue;
            Pp(r,0,2,1);Check(r,81,0);return;
        }
        Assert.Fail("No actual first-miss/second-hit branch found in bounded deterministic seed search.");
    }
    [TestMethod]
    public void StringShotWhiteHerbRestorationIsNotAnotherMoveResultOrWave()
    {
        var b=Bodies(81);b[1]=b[1] with{HeldItem="ITEM_WHITE_HERB"};var r=Resolve(Frame(b));
        Assert.AreEqual(2,Reductions(r,0).Length);Assert.IsTrue(r.Events.OfType<SourceStatChangedEvent>().Any(s=>s.Actor.Value==1&&s.Target.Value==1&&s.Stat==WildBattleStat.Speed&&s.Before==5&&s.After==6));
        Assert.AreEqual(1,r.Events.OfType<SourceHeldItemConsumedEvent>().Count());Assert.AreEqual(6,r.State.Slots[1].Body!.SpeedStage);Check(r,81,0);
    }
    [TestMethod][DataRow(81)][DataRow(169)]
    public void PressureCostsAndLastPpClampKeepExactPerPassReceipts(int id)
    {
        foreach(int remaining in new[]{1,2,5})
        {
            var b=Bodies(id);b[1]=Mon("DUSCLOPS","ASTONISH");b[3]=Mon("DUSCLOPS","ASTONISH");
            Assert.AreEqual("ABILITY_PRESSURE",WildBattleEffective.Ability(b[1]));
            b[0]=b[0] with{Pokemon=b[0].Pokemon with{Moves=b[0].Pokemon.Moves.SetItem(0,b[0].Pokemon.Moves[0] with{CurrentPp=remaining})}};
            var r=Resolve(Frame(b));Pp(r,0,id==81?2:1,id==81?3:2);Check(r,id,0);
        }
    }
    [TestMethod][DataRow("protect")][DataRow("substitute")][DataRow("already-trapped")][DataRow("semi-invulnerable")]
    public void SpiderWebFailuresHaveNoRootOrWave(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            var b=Bodies(169);if(branch=="protect")b[1]=b[1] with{Protected=true};
            if(branch=="substitute")b[1]=b[1] with{CalledVolatile=new(){SubstituteHp=1}};
            // A real Dig command creates the authoritative charge/underground registers.
            if(branch=="semi-invulnerable")b[1]=Mon("DIGLETT","DIG");
            var frame=Frame(b,single);
            if(branch=="already-trapped")frame=Resolve(frame).State;
            if(branch=="semi-invulnerable")frame=Resolve(frame,1,0).State;
            var r=Resolve(frame);Assert.IsFalse(r.Events.OfType<SourceTowerVolatileEvent>().Any(v=>v.Effect=="mean-look"&&v.Applied));
            Assert.IsTrue(r.Events.OfType<SourceMoveFailedEvent>().Any(f=>f.Reason==(branch=="protect"?"protected":branch)));
            Pp(r,0,1,1);Check(r,169,0,single);
        }
    }
    [TestMethod]
    public void BugStatusHasNoTypeImmunityAndSpiderWebDoesNotUseAccuracy()
    {
        foreach(int id in new[]{81,169})
        {
            var b=Bodies(id);b[1]=Mon("GASTLY","LICK");b[3]=Mon("GASTLY","LICK");
            if(id==169)b[0]=b[0] with{AccuracyStage=0};var r=Resolve(Frame(b));
            Assert.IsFalse(r.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));
            Assert.IsTrue(id==81?Reductions(r,0).Length==2:r.Events.OfType<SourceTowerVolatileEvent>().Any(v=>v.Effect=="mean-look"));Check(r,id,0);
        }
    }
    [TestMethod]
    public void StringShotBothFailedTargetsNeverInventRootOrStatWave()
    {
        foreach(bool single in new[]{false,true})
        {
            var b=Bodies(81);b[1]=b[1] with{SpeedStage=0};b[3]=b[3] with{Protected=true};
            var r=Resolve(Frame(b,single));Assert.AreEqual(0,Reductions(r,0).Length);Pp(r,0,single?1:2,1);Check(r,81,0,single);
        }
    }
    [TestMethod]
    public void StringShotOneRemainingSpeedStageReachesZeroWithOneStageWave()
    {
        var b=Bodies(81);b[1]=b[1] with{SpeedStage=1};b[3]=b[3] with{SpeedStage=1};var r=Resolve(Frame(b));
        foreach(var stat in Reductions(r,0)){Assert.AreEqual(1,stat.Before);Assert.AreEqual(0,stat.After);}
        Assert.AreEqual(2,Reductions(r,0).Length);Check(r,81,0);
    }
    [TestMethod]
    public void SpiderWebSelectedPressureAllyCostsTwoAndKeepsItsPosition()
    {
        var b=Bodies(169);b[2]=Mon("DUSCLOPS","ASTONISH");var r=Resolve(Frame(b),0,2);
        Assert.AreEqual(2,r.Events.OfType<SourceTowerVolatileEvent>().Single(v=>v.Effect=="mean-look").Side.Value);
        Pp(r,0,1,2);Check(r,169,0);
    }
    private static void Reject(ImmutableArray<SourceBattleEvent> events,bool single=false)
    {
        int used=Array.FindIndex(events.ToArray(),e=>e is SourceMoveUsedEvent);
        Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Single(events,used),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtAttackText(Double(events,used),SourceBattleScene.On).Dispatch);
    }
    [TestMethod][DataRow(43)][DataRow(81)][DataRow(169)][DataRow(313)]
    public void MalformedPpOwnershipMoveSlotAmountAndOrderingFailClosed(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var r=Resolve(Frame(Bodies(id),single));Check(r,id,0,single);var e=r.Events;int at=Array.FindIndex(e.ToArray(),x=>x is SourcePpCommandEvent);var p=(SourcePpCommandEvent)e[at];
            SourcePpCommandEvent[] bad=[p with{Actor=new(1)},p with{PpOwner=new(1)},p with{Target=new(0)},p with{ChosenMove=new("pret:MOVE_TACKLE")},p with{CurrentMove=new("pret:MOVE_TACKLE")},p with{PpSlot=1},p with{Turn=p.Turn+1},p with{Sequence=p.Sequence+1},p with{Before=p.Before+1},p with{After=p.After-1},p with{CommandCost=0},p with{CommandCost=4}];
            foreach(var row in bad)Reject(e.SetItem(at,row),single);
            Reject(e.RemoveAt(at),single);Reject(e.Insert(at+1,p),single);Reject(e.SetItem(at,e[at+1]).SetItem(at+1,p),single);
            if(Spread(id)&&!single)
            {
                int second=Array.FindLastIndex(e.ToArray(),x=>x is SourcePpCommandEvent);var q=(SourcePpCommandEvent)e[second];
                foreach(var row in new[]{q with{PpOwner=new(1)},q with{Target=p.Target},q with{Before=q.Before+1},q with{After=q.After-1},q with{CommandCost=0}})Reject(e.SetItem(second,row));
            }
        }
    }
    [TestMethod]
    public void SpiderWebVolatileMustBeExactImmediateMoveOwnedReceipt()
    {
        foreach(bool single in new[]{false,true})
        {
            var r=Resolve(Frame(Bodies(169),single));var e=r.Events;int at=Array.FindIndex(e.ToArray(),x=>x is SourceTowerVolatileEvent);var v=(SourceTowerVolatileEvent)e[at];
            SourceTowerVolatileEvent[] bad=[v with{Side=new(0)},v with{Effect="other"},v with{Applied=false},v with{Reason="ability"},v with{Turn=v.Turn+1},v with{Sequence=v.Sequence+1}];
            foreach(var row in bad)Reject(e.SetItem(at,row),single);
            Reject(e.RemoveAt(at),single);Reject(e.Insert(at+1,v with{Sequence=v.Sequence+1}),single);
            Reject(e.Insert(at,new SourceMoveFailedEvent(v.Sequence,v.Turn,new(0),new(1),Move(169),"substitute")),single);
        }
    }
    [TestMethod]
    public void StringShotStatOwnershipBoundsAndRepeatedTargetsFailClosed()
    {
        foreach(bool single in new[]{false,true})
        {
            var r=Resolve(Frame(Bodies(81),single));var e=r.Events;int at=Array.FindIndex(e.ToArray(),x=>x is SourceStatChangedEvent);var s=(SourceStatChangedEvent)e[at];
            SourceStatChangedEvent[] bad=[s with{Actor=new(1)},s with{Target=new(0)},s with{Stat=WildBattleStat.Defense},s with{Before=13},s with{After=-1},s with{After=s.Before},s with{After=s.Before-2},s with{Turn=s.Turn+1},s with{Sequence=s.Sequence+1}];
            foreach(var row in bad)Reject(e.SetItem(at,row),single);
            Reject(e.Insert(at+1,s with{Sequence=s.Sequence+1}),single);
        }
    }
    [TestMethod][DataRow(43)][DataRow(81)][DataRow(169)][DataRow(313)]
    public void MissingMetadataOrMissingResolvedRootCannotAdmitAnimation(int id)
    {
        var r=Resolve(Frame(Bodies(id)));int used=Array.FindIndex(r.Events.ToArray(),e=>e is SourceMoveUsedEvent);var b=Double(r.Events,used);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(b,SourceBattleScene.On,null).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(b,SourceBattleScene.On,Mechanics.Catalog.Moves,(_,_)=>false).Dispatch);
    }
}
