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
    [DataRow("RAZOR_LEAF",75)][DataRow("SWIFT",129)][DataRow("DYNAMIC_PUNCH",223)][DataRow("CHARM",204)][DataRow("COVET",343)][DataRow("IRON_TAIL",231)][DataRow("POISON_TAIL",342)][DataRow("DRAGON_RAGE",82)][DataRow("FIRE_BLAST",126)][DataRow("FLAME_WHEEL",172)][DataRow("DRAGON_BREATH",225)][DataRow("ICE_PUNCH",8)][DataRow("ICE_BEAM",58)][DataRow("POWDER_SNOW",181)][DataRow("FOCUS_ENERGY",116)][DataRow("ENDURE",203)][DataRow("BIND",20)][DataRow("WRAP",35)][DataRow("BITE",44)][DataRow("CLAMP",128)][DataRow("FIRE_SPIN",83)][DataRow("WHIRLPOOL",250)][DataRow("SAND_TOMB",328)][DataRow("ACID",51)][DataRow("BUBBLE_BEAM",61)][DataRow("TOXIC",92)][DataRow("LEECH_LIFE",141)][DataRow("BUBBLE",145)][DataRow("SUPERSONIC",48)][DataRow("SCREECH",103)][DataRow("METAL_SOUND",319)][DataRow("ABSORB",71)][DataRow("MEGA_DRAIN",72)][DataRow("GIGA_DRAIN",202)][DataRow("RECOVER",105)][DataRow("THUNDER_PUNCH",9)][DataRow("WATER_GUN",55)][DataRow("THUNDERBOLT",85)][DataRow("EGG_BOMB",121)][DataRow("CRABHAMMER",152)][DataRow("ZAP_CANNON",192)][DataRow("KARATE_CHOP",2)][DataRow("SUBMISSION",66)][DataRow("THUNDER_SHOCK",84)][DataRow("THUNDER_WAVE",86)][DataRow("HYDRO_PUMP",56)][DataRow("OCTAZOOKA",190)][DataRow("SIGNAL_BEAM",324)][DataRow("MUD_SHOT",341)][DataRow("FIRE_PUNCH",7)][DataRow("EMBER",52)][DataRow("FLAMETHROWER",53)][DataRow("POISON_POWDER",77)][DataRow("STUN_SPORE",78)][DataRow("SLEEP_POWDER",79)][DataRow("SNATCH",289)][DataRow("HEADBUTT",29)][DataRow("PECK",64)][DataRow("DOUBLE_EDGE",38)][DataRow("SLAM",21)][DataRow("VINE_WHIP",22)][DataRow("VICE_GRIP",11)][DataRow("STOMP",23)][DataRow("JUMP_KICK",26)][DataRow("ROLLING_KICK",27)]
    [DataRow("BODY_SLAM",34)][DataRow("TAKE_DOWN",36)][DataRow("LOW_KICK",67)]
    [DataRow("STRENGTH",70)][DataRow("HI_JUMP_KICK",136)]
    public void PhysicalRootRequiresCommittedSuccessfulSourceInvocation(string name,int id)
    {
        var session=CampaignSession.RestoreCurrent(content,FieldFixture(),checkpoint.Generation);
        Send(session,new("turn",Direction:Direction.North));Drain(session);
        var move=new MoveId("pret:MOVE_"+name);var actor=WildBattleSide.Player;var target=id is 105 or 116 or 203?actor:WildBattleSide.Opponent;
        var battle=session.State.WildBattle! with
        {
            Phase=CampaignBattlePhase.Events,EventIndex=1,
            Events=[new WildBattleAttackStringEvent(1,1,actor,move,move),
                new WildBattleMoveUsedEvent(2,1,actor,target,0,move,10,9),
                new WildBattlePpCommandEvent(3,1,actor,target,0,move,move,10,9,1,0),
                id==116?new WildBattleFocusEnergyEvent(4,1,actor):id==203?new WildBattleProtectAttemptEvent(4,1,actor,move,0,1,65535,0,true,true):id==48?new WildBattleConfusionChangedEvent(4,1,target,0,3,"move"):id==105?new WildBattleRecoveredEvent(4,1,actor,move,5,10,5):id is 103 or 319 or 204?new WildBattleStatChangedEvent(4,1,actor,target,id==204?WildBattleStat.Attack:id==103?WildBattleStat.Defense:WildBattleStat.SpecialDefense,0,-2):id is 77 or 78 or 79 or 86 or 92?new WildBattleStatusChangedEvent(4,1,target,0,id==92?WildBattleSourceFieldEffects.ToxicPoison:id==77?WildBattleStatus.Poison:id is 78 or 86?WildBattleStatus.Paralysis:3,"move"):id==289?new WildBattleSnatchReadyEvent(4,1,actor,0):new WildBattleHpChangedEvent(4,1,actor,target,move,10,5,5,false,100)]
        };
        string hash=CanonicalJson.Hash(battle);
        var result=CampaignMoveAnimationPresentation.AtAttackText(battle,true,SourceBattleScene.On);
        Assert.IsNotNull(result.Dispatch,result.Reason);Assert.AreEqual(id,result.Dispatch.SourceMoveId);Assert.AreEqual(4L,result.Dispatch.ResultSequence);
        Assert.AreEqual(hash,CanonicalJson.Hash(battle));
        if(id==203)
        {var failed=battle with{Events=battle.Events.SetItem(3,new WildBattleProtectAttemptEvent(4,1,actor,move,0,1,65535,0,true,false))};Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(failed,true,SourceBattleScene.On).Dispatch);}

        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle,true,SourceBattleScene.Off).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle,false,SourceBattleScene.On).Dispatch);
        var missed=battle with{Events=battle.Events.SetItem(3,new WildBattleMoveMissedEvent(4,1,actor,target,move))};
        Assert.AreEqual("source-no-animation-result",CampaignMoveAnimationPresentation.AtAttackText(missed,true,SourceBattleScene.On).Reason);
        var withoutPp=battle with{Events=battle.Events.RemoveAt(2)};
        Assert.AreEqual("unmatched-source-pp-command",CampaignMoveAnimationPresentation.AtAttackText(withoutPp,true,SourceBattleScene.On).Reason);
    }
    [TestMethod]
    [DataRow("RAZOR_LEAF",75)][DataRow("SWIFT",129)][DataRow("DYNAMIC_PUNCH",223)][DataRow("CHARM",204)][DataRow("COVET",343)][DataRow("IRON_TAIL",231)][DataRow("POISON_TAIL",342)][DataRow("DRAGON_RAGE",82)][DataRow("FIRE_BLAST",126)][DataRow("FLAME_WHEEL",172)][DataRow("DRAGON_BREATH",225)][DataRow("ICE_PUNCH",8)][DataRow("ICE_BEAM",58)][DataRow("POWDER_SNOW",181)][DataRow("FOCUS_ENERGY",116)][DataRow("ENDURE",203)][DataRow("BIND",20)][DataRow("WRAP",35)][DataRow("BITE",44)][DataRow("CLAMP",128)][DataRow("FIRE_SPIN",83)][DataRow("WHIRLPOOL",250)][DataRow("SAND_TOMB",328)][DataRow("ACID",51)][DataRow("BUBBLE_BEAM",61)][DataRow("TOXIC",92)][DataRow("LEECH_LIFE",141)][DataRow("BUBBLE",145)][DataRow("SUPERSONIC",48)][DataRow("SCREECH",103)][DataRow("METAL_SOUND",319)]
    [DataRow("ABSORB",71)][DataRow("MEGA_DRAIN",72)][DataRow("GIGA_DRAIN",202)][DataRow("RECOVER",105)]
    public void SharedRingDrainAndRecoveryDispatchAllFourCommittedSlots(string name,int id)
    {
        var move=new MoveId("pret:MOVE_"+name);
        for(int slot=0;slot<4;slot++)
        {
            var actor=new BattlerSlot(slot);var target=id is 105 or 116 or 203?actor:new BattlerSlot(slot^1);
            SourceBattleEvent result=id switch
            {
                116=>new SourceFocusEnergyEvent(4,1,actor),
                203=>new SourceProtectAttemptEvent(4,1,actor,move,0,1,65535,0,true,true),
                92=>new SourceStatusChangedEvent(4,1,target,0,WildBattleSourceFieldEffects.ToxicPoison,"move"),
                48=>new SourceConfusionChangedEvent(4,1,target,0,3,"move"),
                103 or 319 or 204=>new SourceStatChangedEvent(4,1,actor,target,id==204?WildBattleStat.Attack:id==103?WildBattleStat.Defense:WildBattleStat.SpecialDefense,0,-2),
                105=>new SourceRecoveredEvent(4,1,actor,move,5,10,5),
                _=>new SourceHpChangedEvent(4,1,actor,target,move,10,5,5,false,100)
            };
            var battle=new CampaignDoubleBattle("projection",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,
                [new SourceAttackStringEvent(1,1,actor,move,move),new SourceMoveUsedEvent(2,1,actor,target,0,move,10,9),new SourcePpCommandEvent(3,1,actor,target,0,move,move,10,9,1,0),result],1,[],[],0,0,[],[],[],[],0);
            string before=CanonicalJson.Hash(battle);var decision=CampaignMoveAnimationPresentation.AtAttackText(battle,SourceBattleScene.On);
            Assert.IsNotNull(decision.Dispatch,decision.Reason);Assert.AreEqual(id,decision.Dispatch.SourceMoveId);Assert.AreEqual(slot,decision.Dispatch.Actor.Value);Assert.AreEqual(target.Value,decision.Dispatch.Target.Value);Assert.AreEqual(before,CanonicalJson.Hash(battle));
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

}
