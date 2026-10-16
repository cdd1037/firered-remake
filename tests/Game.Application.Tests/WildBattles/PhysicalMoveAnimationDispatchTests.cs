using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Game.Domain.World;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class PhysicalMoveAnimationDispatchTests:WildBattleFixture
{
    [TestMethod]
    [DataRow("SPORE",147)][DataRow("COTTON_SPORE",178)][DataRow("SYNTHESIS",235)][DataRow("DREAM_EATER",138)][DataRow("BARRIER",112)][DataRow("LIGHT_SCREEN",113)][DataRow("REFLECT",115)][DataRow("MIRROR_COAT",243)][DataRow("CONFUSION",93)][DataRow("PSYCHIC",94)][DataRow("DEFENSE_CURL",111)][DataRow("PSYBEAM",60)][DataRow("HYPNOSIS",95)][DataRow("PSYWAVE",149)][DataRow("SLACK_OFF",303)][DataRow("BULK_UP",339)][DataRow("SPLASH",150)][DataRow("MUD_SPORT",300)][DataRow("THIEF",168)][DataRow("OUTRAGE",200)][DataRow("CRUNCH",242)][DataRow("RAZOR_LEAF",75)][DataRow("SWIFT",129)][DataRow("DYNAMIC_PUNCH",223)][DataRow("CHARM",204)][DataRow("COVET",343)][DataRow("IRON_TAIL",231)][DataRow("POISON_TAIL",342)][DataRow("DRAGON_RAGE",82)][DataRow("FIRE_BLAST",126)][DataRow("FLAME_WHEEL",172)][DataRow("DRAGON_BREATH",225)][DataRow("ICE_PUNCH",8)][DataRow("ICE_BEAM",58)][DataRow("POWDER_SNOW",181)][DataRow("FOCUS_ENERGY",116)][DataRow("ENDURE",203)][DataRow("BIND",20)][DataRow("WRAP",35)][DataRow("BITE",44)][DataRow("CLAMP",128)][DataRow("FIRE_SPIN",83)][DataRow("WHIRLPOOL",250)][DataRow("SAND_TOMB",328)][DataRow("ACID",51)][DataRow("BUBBLE_BEAM",61)][DataRow("TOXIC",92)][DataRow("LEECH_LIFE",141)][DataRow("BUBBLE",145)][DataRow("SUPERSONIC",48)][DataRow("SCREECH",103)][DataRow("METAL_SOUND",319)][DataRow("ABSORB",71)][DataRow("MEGA_DRAIN",72)][DataRow("GIGA_DRAIN",202)][DataRow("RECOVER",105)][DataRow("THUNDER_PUNCH",9)][DataRow("WATER_GUN",55)][DataRow("THUNDERBOLT",85)][DataRow("EGG_BOMB",121)][DataRow("CRABHAMMER",152)][DataRow("ZAP_CANNON",192)][DataRow("KARATE_CHOP",2)][DataRow("SUBMISSION",66)][DataRow("THUNDER_SHOCK",84)][DataRow("THUNDER_WAVE",86)][DataRow("HYDRO_PUMP",56)][DataRow("OCTAZOOKA",190)][DataRow("SIGNAL_BEAM",324)][DataRow("MUD_SHOT",341)][DataRow("FIRE_PUNCH",7)][DataRow("EMBER",52)][DataRow("FLAMETHROWER",53)][DataRow("POISON_POWDER",77)][DataRow("STUN_SPORE",78)][DataRow("SLEEP_POWDER",79)][DataRow("SNATCH",289)][DataRow("HEADBUTT",29)][DataRow("PECK",64)][DataRow("DOUBLE_EDGE",38)][DataRow("SLAM",21)][DataRow("VINE_WHIP",22)][DataRow("VICE_GRIP",11)][DataRow("STOMP",23)][DataRow("JUMP_KICK",26)][DataRow("ROLLING_KICK",27)]
    [DataRow("BODY_SLAM",34)][DataRow("TAKE_DOWN",36)][DataRow("LOW_KICK",67)]
    [DataRow("STRENGTH",70)][DataRow("HI_JUMP_KICK",136)]
    public void PhysicalRootRequiresCommittedSuccessfulSourceInvocation(string name,int id)
    {
        var session=CampaignSession.RestoreCurrent(content,FieldFixture(),checkpoint.Generation);
        Send(session,new("turn",Direction:Direction.North));Drain(session);
        var move=new MoveId("pret:MOVE_"+name);var actor=WildBattleSide.Player;var target=id is 105 or 235 or 116 or 150 or 203 or 300 or 303 or 339 or 111 or 112 or 113 or 115?actor:WildBattleSide.Opponent;
        var battle=session.State.WildBattle! with
        {
            Phase=CampaignBattlePhase.Events,EventIndex=1,
            Events=[new WildBattleAttackStringEvent(1,1,actor,move,move),
                new WildBattleMoveUsedEvent(2,1,actor,target,0,move,10,9),
                new WildBattlePpCommandEvent(3,1,actor,target,0,move,move,10,9,1,0),
                id is 113 or 115?new WildBattleMtMoonVolatileEvent(4,1,actor,id==113?"light-screen":"reflect",0,5,"move"):id==112?new WildBattleStatChangedEvent(4,1,actor,actor,WildBattleStat.Defense,6,8):id==111?new WildBattleStatChangedEvent(4,1,actor,actor,WildBattleStat.Defense,6,7):id==339?new WildBattleStatChangedEvent(4,1,actor,actor,WildBattleStat.Attack,6,7):id==150?new WildBattleMoveFailedEvent(4,1,actor,actor,move,"nothing-happened"):id==300?new WildBattleMtMoonVolatileEvent(4,1,actor,"mud-sport",0,1,"move"):id==116?new WildBattleFocusEnergyEvent(4,1,actor):id==203?new WildBattleProtectAttemptEvent(4,1,actor,move,0,1,65535,0,true,true):id==48?new WildBattleConfusionChangedEvent(4,1,target,0,3,"move"):id is 105 or 235 or 303?new WildBattleRecoveredEvent(4,1,actor,move,5,10,5):id is 103 or 178 or 319 or 204?new WildBattleStatChangedEvent(4,1,actor,target,id==178?WildBattleStat.Speed:id==204?WildBattleStat.Attack:id==103?WildBattleStat.Defense:WildBattleStat.SpecialDefense,6,4):id is 77 or 78 or 79 or 86 or 92 or 95 or 147?new WildBattleStatusChangedEvent(4,1,target,0,id==92?WildBattleSourceFieldEffects.ToxicPoison:id==77?WildBattleStatus.Poison:id is 78 or 86?WildBattleStatus.Paralysis:3,"move"):id==289?new WildBattleSnatchReadyEvent(4,1,actor,0):new WildBattleHpChangedEvent(4,1,actor,target,move,10,5,5,false,100)]
        };
        if(id==111)battle=battle with{Events=battle.Events.SetItem(3,new WildBattleStatChangedEvent(5,1,actor,actor,WildBattleStat.Defense,6,7)).Insert(3,new WildBattleDefenseCurledEvent(4,1,actor))};
        if(id==339)battle=battle with{Events=battle.Events.Add(new WildBattleStatChangedEvent(5,1,actor,actor,WildBattleStat.Defense,6,7))};
        string hash=CanonicalJson.Hash(battle);
        var result=CampaignMoveAnimationPresentation.AtAttackText(battle,true,SourceBattleScene.On);
        Assert.IsNotNull(result.Dispatch,result.Reason);Assert.AreEqual(id,result.Dispatch.SourceMoveId);Assert.AreEqual(id==111?5L:4L,result.Dispatch.ResultSequence);
        Assert.AreEqual(hash,CanonicalJson.Hash(battle));
        if(id is 103 or 178 or 204 or 319)
        {
            int argument=id switch{103=>47,178=>48,204=>46,_=>50};
            var statResult=CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=3},true);
            Assert.IsNotNull(statResult);Assert.AreEqual(argument,statResult.AnimationArgument);Assert.AreEqual(4L,statResult.Sequence);
            var recorded=(WildBattleStatChangedEvent)battle.Events[3];
            var clamped=battle with{EventIndex=3,Events=battle.Events.SetItem(3,recorded with{Before=1,After=0})};
            Assert.AreEqual(argument,CampaignStatAnimationPresentation.AtResult(clamped,true)!.AnimationArgument);
            Assert.IsNull(CampaignStatAnimationPresentation.AtResult(clamped with{Events=clamped.Events.SetItem(3,recorded with{Before=0,After=0})},true));
            Assert.IsNull(CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=3},true,true));
        }
        if(id==235)
        {
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(3,new WildBattleRecoveredEvent(4,1,actor,move,10,10,0))},true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(3,new WildBattleRecoveredEvent(4,1,actor,move,5,10,4))},true,SourceBattleScene.On).Dispatch);
        }
        if(id==138)
        {
            var drained=battle with{Events=battle.Events.Add(new WildBattleRecoveredEvent(5,1,actor,move,5,7,2))};
            Assert.AreEqual(4L,CampaignMoveAnimationPresentation.AtAttackText(drained,true,SourceBattleScene.On).Dispatch!.ResultSequence);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(drained with{Events=drained.Events.RemoveAt(3)},true,SourceBattleScene.On).Dispatch);
        }
        if(id==111)
        {
            Assert.IsNotNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.RemoveAt(3)},true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(4,new WildBattleStatAtMaximumEvent(5,1,actor,actor,WildBattleStat.Defense))},true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(3,new WildBattleDefenseCurledEvent(4,1,target==WildBattleSide.Player?WildBattleSide.Opponent:WildBattleSide.Player))},true,SourceBattleScene.On).Dispatch);
        }
        if(id==339)
        {
            var capped=battle with{Events=battle.Events.SetItem(4,new WildBattleStatAtMaximumEvent(5,1,actor,actor,WildBattleStat.Defense))};
            Assert.IsNotNull(CampaignMoveAnimationPresentation.AtAttackText(capped,true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.RemoveAt(4)},true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(4,new WildBattleStatChangedEvent(5,1,actor,actor,WildBattleStat.Attack,6,7))},true,SourceBattleScene.On).Dispatch);
        }
        if(id==203)
        {var failed=battle with{Events=battle.Events.SetItem(3,new WildBattleProtectAttemptEvent(4,1,actor,move,0,1,65535,0,true,false))};Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(failed,true,SourceBattleScene.On).Dispatch);}

        if(id is 150 or 300)
        {
            WildBattleEvent invalid=id==339?new WildBattleStatChangedEvent(4,1,actor,actor,WildBattleStat.Attack,6,7):id==150?new WildBattleMoveFailedEvent(4,1,actor,actor,move,"protected"):new WildBattleMtMoonVolatileEvent(4,1,actor,"mud-sport",1,1,"move");
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(3,invalid)},true,SourceBattleScene.On).Dispatch);
        }
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle,true,SourceBattleScene.Off).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle,false,SourceBattleScene.On).Dispatch);
        var missed=battle with{Events=battle.Events.SetItem(3,new WildBattleMoveMissedEvent(4,1,actor,target,move))};
        Assert.AreEqual("source-no-animation-result",CampaignMoveAnimationPresentation.AtAttackText(missed,true,SourceBattleScene.On).Reason);
        var withoutPp=battle with{Events=battle.Events.RemoveAt(2)};
        Assert.AreEqual("unmatched-source-pp-command",CampaignMoveAnimationPresentation.AtAttackText(withoutPp,true,SourceBattleScene.On).Reason);
    }
    [TestMethod]
    [DataRow("SPORE",147)][DataRow("COTTON_SPORE",178)][DataRow("SYNTHESIS",235)][DataRow("DREAM_EATER",138)][DataRow("BARRIER",112)][DataRow("LIGHT_SCREEN",113)][DataRow("REFLECT",115)][DataRow("MIRROR_COAT",243)][DataRow("CONFUSION",93)][DataRow("PSYCHIC",94)][DataRow("DEFENSE_CURL",111)][DataRow("PSYBEAM",60)][DataRow("HYPNOSIS",95)][DataRow("PSYWAVE",149)][DataRow("SLACK_OFF",303)][DataRow("BULK_UP",339)][DataRow("SPLASH",150)][DataRow("MUD_SPORT",300)][DataRow("THIEF",168)][DataRow("OUTRAGE",200)][DataRow("CRUNCH",242)][DataRow("RAZOR_LEAF",75)][DataRow("SWIFT",129)][DataRow("DYNAMIC_PUNCH",223)][DataRow("CHARM",204)][DataRow("COVET",343)][DataRow("IRON_TAIL",231)][DataRow("POISON_TAIL",342)][DataRow("DRAGON_RAGE",82)][DataRow("FIRE_BLAST",126)][DataRow("FLAME_WHEEL",172)][DataRow("DRAGON_BREATH",225)][DataRow("ICE_PUNCH",8)][DataRow("ICE_BEAM",58)][DataRow("POWDER_SNOW",181)][DataRow("FOCUS_ENERGY",116)][DataRow("ENDURE",203)][DataRow("BIND",20)][DataRow("WRAP",35)][DataRow("BITE",44)][DataRow("CLAMP",128)][DataRow("FIRE_SPIN",83)][DataRow("WHIRLPOOL",250)][DataRow("SAND_TOMB",328)][DataRow("ACID",51)][DataRow("BUBBLE_BEAM",61)][DataRow("TOXIC",92)][DataRow("LEECH_LIFE",141)][DataRow("BUBBLE",145)][DataRow("SUPERSONIC",48)][DataRow("SCREECH",103)][DataRow("METAL_SOUND",319)]
    [DataRow("ABSORB",71)][DataRow("MEGA_DRAIN",72)][DataRow("GIGA_DRAIN",202)][DataRow("RECOVER",105)]
    public void SharedRingDrainAndRecoveryDispatchAllFourCommittedSlots(string name,int id)
    {
        var move=new MoveId("pret:MOVE_"+name);
        for(int slot=0;slot<4;slot++)
        {
            var actor=new BattlerSlot(slot);var target=id is 105 or 235 or 116 or 150 or 203 or 300 or 303 or 339 or 111 or 112 or 113 or 115?actor:new BattlerSlot(slot^1);
            SourceBattleEvent result=id switch
            {
                113 or 115=>new SourceMtMoonVolatileEvent(4,1,actor,id==113?"light-screen":"reflect",0,5,"move"),
                112=>new SourceStatChangedEvent(4,1,actor,actor,WildBattleStat.Defense,6,8),
                111=>new SourceStatChangedEvent(4,1,actor,actor,WildBattleStat.Defense,6,7),
                339=>new SourceStatChangedEvent(4,1,actor,actor,WildBattleStat.Attack,6,7),
                95 or 147=>new SourceStatusChangedEvent(4,1,target,0,3,"move"),
                150=>new SourceMoveFailedEvent(4,1,actor,actor,move,"nothing-happened"),
                300=>new SourceMtMoonVolatileEvent(4,1,actor,"mud-sport",0,1,"move"),
                116=>new SourceFocusEnergyEvent(4,1,actor),
                203=>new SourceProtectAttemptEvent(4,1,actor,move,0,1,65535,0,true,true),
                92=>new SourceStatusChangedEvent(4,1,target,0,WildBattleSourceFieldEffects.ToxicPoison,"move"),
                48=>new SourceConfusionChangedEvent(4,1,target,0,3,"move"),
                103 or 178 or 319 or 204=>new SourceStatChangedEvent(4,1,actor,target,id==178?WildBattleStat.Speed:id==204?WildBattleStat.Attack:id==103?WildBattleStat.Defense:WildBattleStat.SpecialDefense,6,4),
                105 or 235 or 303=>new SourceRecoveredEvent(4,1,actor,move,5,10,5),
                _=>new SourceHpChangedEvent(4,1,actor,target,move,10,5,5,false,100)
            };
            var battle=new CampaignDoubleBattle("projection",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,
                [new SourceAttackStringEvent(1,1,actor,move,move),new SourceMoveUsedEvent(2,1,actor,target,0,move,10,9),new SourcePpCommandEvent(3,1,actor,target,0,move,move,10,9,1,0),result],1,[],[],0,0,[],[],[],[],0);
            if(id==111)battle=battle with{Events=battle.Events.SetItem(3,new SourceStatChangedEvent(5,1,actor,actor,WildBattleStat.Defense,6,7)).Insert(3,new SourceDefenseCurledEvent(4,1,actor))};
            if(id==339)battle=battle with{Events=battle.Events.Add(new SourceStatChangedEvent(5,1,actor,actor,WildBattleStat.Defense,6,7))};
            string before=CanonicalJson.Hash(battle);var decision=CampaignMoveAnimationPresentation.AtAttackText(battle,SourceBattleScene.On);
            Assert.IsNotNull(decision.Dispatch,decision.Reason);Assert.AreEqual(id,decision.Dispatch.SourceMoveId);Assert.AreEqual(slot,decision.Dispatch.Actor.Value);Assert.AreEqual(target.Value,decision.Dispatch.Target.Value);Assert.AreEqual(before,CanonicalJson.Hash(battle));
            if(id==339)
            {
                Assert.IsNotNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(4,new SourceStatAtMaximumEvent(5,1,actor,actor,WildBattleStat.Defense))},SourceBattleScene.On).Dispatch);
                Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(4,new SourceStatChangedEvent(5,1,actor,actor,WildBattleStat.Speed,6,7))},SourceBattleScene.On).Dispatch);
            }
            if(id is 103 or 178 or 204 or 319)
            {
                int argument=id switch{103=>47,178=>48,204=>46,_=>50};
                var statResult=CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=3});
                Assert.IsNotNull(statResult);Assert.AreEqual(argument,statResult.AnimationArgument);Assert.AreEqual(target,statResult.Affected);
                var recorded=(SourceStatChangedEvent)battle.Events[3];
                var clamped=battle with{EventIndex=3,Events=battle.Events.SetItem(3,recorded with{Before=1,After=0})};
                Assert.AreEqual(argument,CampaignStatAnimationPresentation.AtResult(clamped)!.AnimationArgument);
                Assert.IsNull(CampaignStatAnimationPresentation.AtResult(clamped with{Events=clamped.Events.SetItem(3,recorded with{Before=0,After=0})}));
                Assert.IsNull(CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=3},true));
            }
            if(id is 147 or 178)
            {
                SourceBattleEvent blocked=id==147?new SourceMoveFailedEvent(4,1,actor,target,move,"safeguard"):new SourceStatAtMinimumEvent(4,1,actor,target,WildBattleStat.Speed);
                Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(3,blocked)},SourceBattleScene.On).Dispatch);
                Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(3,new SourceMoveFailedEvent(4,1,actor,target,move,"substitute"))},SourceBattleScene.On).Dispatch);
            }
            var wrong=battle with{Events=battle.Events.SetItem(3,new SourceConfusionChangedEvent(4,1,target,0,3,"item"))};
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(wrong,SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod][DataRow("ACID",51)][DataRow("BUBBLE",145)][DataRow("POWDER_SNOW",181)][DataRow("RAZOR_LEAF",75)][DataRow("SWIFT",129)]
    public void SpreadHitSelectsFirstSuccessfulPassWithoutSpendingPpAgain(string name,int id)
    {
        var move=new MoveId("pret:MOVE_"+name);var actor=new BattlerSlot(0);var target=new BattlerSlot(1);var partner=new BattlerSlot(3);
        foreach(bool firstMisses in new[]{false,true})
        {
            SourceBattleEvent first=firstMisses?new SourceMoveMissedEvent(4,1,actor,target,move):new SourceHpChangedEvent(4,1,actor,target,move,10,5,5,false,100);
            var battle=new CampaignDoubleBattle("spread",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,
                [new SourceAttackStringEvent(1,1,actor,move,move),new SourceMoveUsedEvent(2,1,actor,target,0,move,10,9),new SourcePpCommandEvent(3,1,actor,target,0,move,move,10,9,1,0),first,
                 new SourcePpCommandEvent(5,1,actor,partner,0,move,move,9,9,0,0),new SourceHpChangedEvent(6,1,actor,partner,move,10,5,5,false,100)],1,[],[],0,0,[],[],[],[],0);
            var decision=CampaignMoveAnimationPresentation.AtAttackText(battle,SourceBattleScene.On);Assert.IsNotNull(decision.Dispatch,decision.Reason);Assert.AreEqual(id,decision.Dispatch.SourceMoveId);Assert.AreEqual(firstMisses?3:1,decision.Dispatch.Target.Value);Assert.AreEqual(firstMisses?6L:4L,decision.Dispatch.ResultSequence);
            var extraPp=battle with{Events=battle.Events.SetItem(4,new SourcePpCommandEvent(5,1,actor,partner,0,move,move,9,8,1,0))};
            Assert.AreEqual("unmatched-source-spread-pp",CampaignMoveAnimationPresentation.AtAttackText(extraPp,SourceBattleScene.On).Reason);
            if(firstMisses)
            {var bothMiss=battle with{Events=battle.Events.SetItem(5,new SourceMoveMissedEvent(6,1,actor,partner,move))};Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(bothMiss,SourceBattleScene.On).Dispatch);}
        }
    }

    [TestMethod][DataRow(107)][DataRow(111)]
    public void SelfStatStatePreludePreservesRecordedKernelOrderAndCannotBorrowAnotherActor(int id)
    {
        var actor=new BattlerSlot(0);var move=new MoveId(id==107?"pret:MOVE_MINIMIZE":"pret:MOVE_DEFENSE_CURL");var stat=id==107?WildBattleStat.Evasion:WildBattleStat.Defense;
        SourceBattleEvent marker=id==107?new SourceMtMoonVolatileEvent(3,1,actor,"minimized",0,1,"move"):new SourceDefenseCurledEvent(4,1,actor);
        var pp=new SourcePpCommandEvent(id==107?4:3,1,actor,actor,0,move,move,10,9,1,0);
        SourceBattleEvent[] events=[new SourceAttackStringEvent(1,1,actor,move,move),new SourceMoveUsedEvent(2,1,actor,actor,0,move,10,9),id==107?marker:pp,id==107?pp:marker,new SourceStatChangedEvent(5,1,actor,actor,stat,6,7)];
        var battle=new CampaignDoubleBattle("self-state",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,[..events],1,[],[],0,0,[],[],[],[],0);
        Assert.IsNotNull(CampaignMoveAnimationPresentation.AtAttackText(battle,SourceBattleScene.On).Dispatch);
        Assert.IsNotNull(CampaignStatAnimationPresentation.AtResult(battle with{EventIndex=4}));
        int markerIndex=id==107?2:3;
        SourceBattleEvent wrong=id==107?new SourceMtMoonVolatileEvent(3,1,new(1),"minimized",0,1,"move"):new SourceDefenseCurledEvent(4,1,new(1));
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(markerIndex,wrong)},SourceBattleScene.On).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(4,new SourceStatAtMaximumEvent(5,1,actor,actor,stat))},SourceBattleScene.On).Dispatch);
    }

    [TestMethod]
    public void MagicCoatSetupBeforeAttackStringBelongsOnlyToItsCommittedInvocation()
    {
        var move=new MoveId("pret:MOVE_MAGIC_COAT");
        for(int slot=0;slot<4;slot++)
        {
            var actor=new BattlerSlot(slot);
            var battle=new CampaignDoubleBattle("coat",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,
                [new SourceMtMoonVolatileEvent(1,1,actor,"magic-coat",0,1,"move"),new SourceAttackStringEvent(2,1,actor,move,move),new SourceMoveUsedEvent(3,1,actor,actor,0,move,10,9),new SourcePpCommandEvent(4,1,actor,actor,0,move,move,10,9,1,0)],2,[],[],0,0,[],[],[],[],0);
            var decision=CampaignMoveAnimationPresentation.AtAttackText(battle,SourceBattleScene.On);
            Assert.IsNotNull(decision.Dispatch,decision.Reason);Assert.AreEqual(277,decision.Dispatch.SourceMoveId);Assert.AreEqual(1L,decision.Dispatch.ResultSequence);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(0,new SourceMtMoonVolatileEvent(1,1,new(slot^1),"magic-coat",0,1,"move"))},SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.SetItem(0,new SourceMtMoonVolatileEvent(1,0,actor,"magic-coat",0,1,"move"))},SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle with{Events=battle.Events.Add(new SourceMoveFailedEvent(5,1,actor,actor,move,"magic-coat-last-action"))},SourceBattleScene.On).Dispatch);
        }
        var session=CampaignSession.RestoreCurrent(content,FieldFixture(),checkpoint.Generation);Send(session,new("turn",Direction:Direction.North));Drain(session);
        var wild=session.State.WildBattle! with{Phase=CampaignBattlePhase.Events,EventIndex=2,Events=[new WildBattleMtMoonVolatileEvent(1,1,WildBattleSide.Player,"magic-coat",0,1,"move"),new WildBattleAttackStringEvent(2,1,WildBattleSide.Player,move,move),new WildBattleMoveUsedEvent(3,1,WildBattleSide.Player,WildBattleSide.Player,0,move,10,9),new WildBattlePpCommandEvent(4,1,WildBattleSide.Player,WildBattleSide.Player,0,move,move,10,9,1,0)]};
        Assert.IsNotNull(CampaignMoveAnimationPresentation.AtAttackText(wild,true,SourceBattleScene.On).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(wild with{Events=wild.Events.RemoveAt(0),EventIndex=1},true,SourceBattleScene.On).Dispatch);
    }

}
