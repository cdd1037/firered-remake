using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignSelphyRequestRouteTests
{
    [TestMethod]
    public void ExistingRequestRepeatsExpiresByWalkingAndRefreshesThroughContinue()
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var basis=CurrentCampaignFixture.Checkpoint.State;
        var session=CampaignSession.CreateCurrent(catalog);
        var map=catalog.Maps[new("map.five-island-resort-gorgeous-house")];
        // Only place the already rescued player at Selphy. Keep every earned flag,
        // actual battle receipt, owned Pokemon, request, counter and RNG value.
        var state=basis with
        {
            World=basis.World with{Player=new(map.Id,4,5,map.GetTile(4,5)!.Elevation,Direction.North)},
            Script=basis.Script with{Completed=true,Locked=false,Wait=null,Stack=[]},
            EntryPrograms=[],ArrivalFacing=null,Dialogue=null,Motions=[],PresentationTicks=0,PresentationEffects=[],
            MapLayout=null,MapEdits=null,PlayerVisible=true,IsCycling=false,IsSurfing=false,FieldWeather=null,
            FlashLevel=0,FieldTransition=null,QuestLog=null,Music=catalog.MapDefinition(map.Id).Music
        };
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        state=(CampaignState)typeof(CampaignSession).GetMethod("EnterMap",flags)!.Invoke(session,[state,null,Game.Domain.Roaming.RoamerTransition.Warp])!;
        state=(CampaignState)typeof(CampaignSession).GetMethod("Pump",flags)!.Invoke(session,[state])!;
        Assert.IsNull(session.ValidatePlayerSave(state));
        Assert.IsTrue(state.BattledTrainers.Contains("pret:TRAINER_LADY_SELPHY"));
        Assert.IsTrue(state.ForestTrainerHistory!.Value.Any(h=>h.TrainerId=="pret:TRAINER_LADY_SELPHY"&&h.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won));
        Assert.AreEqual(133,Request(state).RequestedSpecies);
        Assert.AreEqual(190,Request(state).StepCounter);
        Assert.IsFalse(SelphyRequestRules.PartyContainsRequestedSpecies(state.Party,133));
        session=CampaignSession.RestoreCurrent(catalog,state,CurrentCampaignFixture.Checkpoint.Generation);
        var inventory=JsonSerializer.SerializeToElement(state.Inventory);
        var history=JsonSerializer.SerializeToElement(state.ForestTrainerHistory);
        var party=JsonSerializer.SerializeToElement(state.Party);
        int commands=0;
        void Send(CampaignCommand command)
        {
            var result=session.Execute("selphy-route:"+(++commands),session.Revision,command);
            Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        }
        void Talk()
        {
            Send(new("interact"));
            for(int i=0;session.State.Script.Wait is not null;i++)
            {Assert.IsTrue(i<20,"Selphy's source request dialogue must finish.");Send(new("ack"));}
            Assert.IsTrue(session.State.Script.Completed);Assert.IsFalse(session.State.Script.Locked);
        }
        void Continue()
        {
            var saved=session.PrepareFieldSave();Assert.IsNull(session.ValidatePlayerSave(saved));
            var bytes=SaveCodec.Encode(saved,session.Revision,session.RulesVersion,catalog.ContentVersion);
            var decoded=SaveCodec.Decode<CampaignState>(bytes,session.RulesVersion,catalog.ContentVersion,validate:session.ValidatePlayerSave);
            session=CampaignSession.RestoreCurrent(catalog,decoded.State,decoded.Generation);
            Assert.AreEqual(Request(saved),Request(session.State));
            Assert.AreEqual(saved.World.Rng,session.State.World.Rng);
            Assert.AreEqual(saved.World.Player,session.State.World.Player);
            Assert.IsNull(session.State.Script.Wait);Assert.IsNull(session.State.FieldTransition);
        }
        var originalRequest=Request(state);var originalRng=state.World.Rng;
        Talk();Assert.AreEqual(originalRequest,Request(session.State));
        Assert.AreEqual(originalRng,session.State.World.Rng);
        Assert.IsTrue(JsonElement.DeepEquals(party,JsonSerializer.SerializeToElement(session.State.Party)));
        Continue();
        for(int i=0;i<60;i++)
        {
            var previous=session.State;var expected=SelphyRequestRules.AdvanceEligibleStep(Request(previous));
            Send(new("move",i%2==0?Direction.South:Direction.North));
            Assert.AreEqual(previous.World.StepCount+1,session.State.World.StepCount);
            Assert.AreEqual(expected,Request(session.State));
            Assert.AreEqual(map.Id,session.State.World.Player.MapId);
            Assert.AreEqual((4,i%2==0?6:5),(session.State.World.Player.X,session.State.World.Player.Y));
        }
        Assert.AreEqual(SelphyRequestRules.ExpiredSpecies,Request(session.State).RequestedSpecies);
        Assert.AreEqual(0,Request(session.State).StepCounter);
        // Isolate request effects from ordinary walking updates to the party.
        party=JsonSerializer.SerializeToElement(session.State.Party);
        var expired=Request(session.State);
        var sample=SelphyRequestRules.SampleRequest(expired,session.State.SeenSpecies.ToImmutableHashSet(),session.State.World.Rng);
        Assert.IsFalse(sample.Trace.IsEmpty);
        Talk();Assert.AreEqual(sample.State,Request(session.State));Assert.AreEqual(sample.Rng,session.State.World.Rng);
        Assert.IsTrue(session.State.SeenSpecies.Contains(sample.State.RequestedSpecies));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(SelphyRequestRules.ShownRequestedMonFlag));
        Continue();
        Assert.IsTrue(JsonElement.DeepEquals(inventory,JsonSerializer.SerializeToElement(session.State.Inventory)));
        Assert.IsTrue(JsonElement.DeepEquals(history,JsonSerializer.SerializeToElement(session.State.ForestTrainerHistory)));
        Assert.IsTrue(JsonElement.DeepEquals(party,JsonSerializer.SerializeToElement(session.State.Party)));
    }

    private static SelphyRequestState Request(CampaignState state)=>new(
        checked((ushort)state.Script.Variables.GetValueOrDefault(SelphyRequestRules.RequestedSpeciesVariable)),
        checked((ushort)state.Script.Variables.GetValueOrDefault(SelphyRequestRules.StepCounterVariable)),
        SelphyRequestRules.DeluxeRewards.Add("ITEM_LUXURY_BALL").Single(symbol=>
            CurrentCampaignFixture.Catalog.TryConstant(symbol,out int value)&&value==state.Script.Variables.GetValueOrDefault(SelphyRequestRules.RewardVariable)));
}
