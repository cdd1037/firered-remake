using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
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
        var map=catalog.Maps[new("map.five-island-resort-gorgeous-house")];
        var session=Place(basis,map.Id.Value,4,5,CurrentCampaignFixture.Checkpoint.Generation);
        var state=session.State;
        Assert.IsTrue(state.BattledTrainers.Contains("pret:TRAINER_LADY_SELPHY"));
        Assert.IsTrue(state.ForestTrainerHistory!.Value.Any(h=>h.TrainerId=="pret:TRAINER_LADY_SELPHY"&&h.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won));
        Assert.AreEqual(133,Request(state).RequestedSpecies);
        Assert.AreEqual(190,Request(state).StepCounter);
        Assert.IsFalse(SelphyRequestRules.PartyContainsRequestedSpecies(state.Party,133));
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

    [TestMethod]
    public void ExistingBoxedPersianFulfillsNaturallyRefreshedRequestOnceThroughContinue()
    {
        const string house="map.five-island-resort-gorgeous-house";
        const string text="FiveIsland_ResortGorgeous_House_Text_";
        const string butler="LOCALID_RESORT_GORGEOUS_HOUSE_BUTLER";
        var catalog=CurrentCampaignFixture.Catalog;
        var basis=CurrentCampaignFixture.Checkpoint.State;
        Assert.AreEqual(new SelphyRequestState(133,190,"ITEM_LUXURY_BALL"),Request(basis));
        Assert.AreEqual(new RngState(2014338387u,60802),basis.World.Rng);
        Assert.IsTrue(basis.BattledTrainers.Contains("pret:TRAINER_LADY_SELPHY"));
        Assert.IsTrue(basis.ForestTrainerHistory!.Value.Any(h=>h.TrainerId=="pret:TRAINER_LADY_SELPHY"&&
            h.Outcome==Game.Domain.WildBattle.WildBattleOutcome.Won));
        Assert.IsFalse(basis.Script.Flags.GetValueOrDefault(SelphyRequestRules.HiddenInsideFlag));
        var persian=basis.Storage!.Boxes[0].Slots[26];
        Assert.IsNotNull(persian);
        Assert.AreEqual(33L,persian.InstanceId);
        Assert.AreEqual("pret:SPECIES_PERSIAN",persian.Pokemon.Species.Value);
        Assert.AreEqual(53,persian.Pokemon.SourceSpeciesId);
        Assert.IsFalse(persian.IsEgg);
        Assert.AreEqual(6,basis.Party.Length);
        Assert.AreEqual(68L,basis.Party[5].InstanceId);
        Assert.IsTrue(basis.Party[5].IsEgg);
        Assert.IsNull(basis.Storage.Boxes[0].Slots[14]);
        PartyPokemon[] Owned(CampaignState state)=>state.Party
            .Concat(state.Storage!.Boxes.SelectMany(box=>box.Slots).OfType<PartyPokemon>())
            .OrderBy(mon=>mon.InstanceId).ToArray();
        var owned=Owned(basis);
        var session=Place(basis,"map.viridian-pokemon-center-1f",11,2,CurrentCampaignFixture.Checkpoint.Generation);
        int commands=0;
        void Send(CampaignCommand command)
        {
            var result=session.Execute("selphy-reward:"+(++commands),session.Revision,command);
            Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        }
        bool Stable()=>session.State.Script.Completed&&!session.State.Script.Locked&&
            session.State.Script.Wait is null&&session.State.Dialogue is null&&session.State.Motions.IsEmpty&&
            session.State.Pc is null&&session.State.EntryPrograms.IsEmpty;
        void DrainUntil(Func<bool> complete)
        {
            for(int i=0;!complete();i++)
            {
                Assert.IsTrue(i<80,"The public PC/dialogue route must reach its bounded endpoint.");
                Assert.IsNotNull(session.State.Script.Wait);
                Send(new("ack"));
            }
        }
        void Continue()
        {
            Assert.IsTrue(Stable());
            var saved=session.PrepareFieldSave();
            Assert.IsNull(session.ValidatePlayerSave(saved));
            var decoded=SaveCodec.Decode<CampaignState>(SaveCodec.Encode(saved,session.Revision,
                session.RulesVersion,catalog.ContentVersion),session.RulesVersion,catalog.ContentVersion,
                validate:session.ValidatePlayerSave);
            long revision=session.Revision;
            session=CampaignSession.RestoreCurrent(catalog,decoded.State,decoded.Generation);
            Assert.AreEqual(revision,session.Revision);
            AssertStructure(saved,session.State,"Strict Continue preserves the entire saved reward-route payload.");
            Assert.IsTrue(Stable());
            Assert.IsNull(session.State.FieldTransition);
        }
        List<string> Talk(List<CampaignMotion>? motions=null)
        {
            Send(new("interact"));
            var dialogue=new List<string>();
            for(int i=0;session.State.Script.Wait is not null;i++)
            {
                Assert.IsTrue(i<80,"Selphy's reward scene must finish through ordinary acknowledgements.");
                Assert.IsNotNull(session.ValidatePlayerSave(session.PrepareFieldSave()),
                    "A suspended dialogue or movement cannot be saved as a stable reward endpoint.");
                if(session.State.Dialogue is {} current&&dialogue.LastOrDefault()!=current.TextSymbol)
                    dialogue.Add(current.TextSymbol);
                if(motions is not null)
                    foreach(var motion in session.State.Motions)
                        if(!motions.Contains(motion))motions.Add(motion);
                Send(new("ack"));
            }
            Assert.IsTrue(Stable());
            return dialogue;
        }

        // Open the actual PC tile. Deposit the already owned egg to avoid a hatch
        // interrupting this route, then withdraw the actual Persian without creating one.
        Send(new("interact"));
        DrainUntil(()=>session.State.Pc?.View==CampaignPcView.Main);
        Assert.IsNotNull(session.ValidatePlayerSave(session.State));
        Send(new("pc_select",Choice:0));
        DrainUntil(()=>session.State.Pc?.View==CampaignPcView.StorageMenu);
        Send(new("storage_select",Choice:1));
        Send(new("storage_box",Choice:0));
        Send(new("storage_deposit",PartyIndex:5));
        Assert.AreEqual(5,session.State.Party.Length);
        Assert.AreEqual(68L,session.State.Storage!.Boxes[0].Slots[14]!.InstanceId);
        Assert.IsTrue(session.State.Storage.Boxes[0].Slots[14]!.IsEgg);
        AssertStructure(persian,session.State.Storage.Boxes[0].Slots[26],"Deposit preserves the existing Persian.");
        Send(new("pc_back"));
        Send(new("storage_select",Choice:0));
        Send(new("storage_withdraw",MoveSlot:26));
        AssertStructure(persian,session.State.Party[^1],"Withdrawal preserves the same Persian and all its ownership data.");
        Assert.IsNull(session.State.Storage!.Boxes[0].Slots[26]);
        Send(new("pc_back"));
        Send(new("pc_back"));
        DrainUntil(()=>session.State.Pc?.View==CampaignPcView.Main);
        Send(new("pc_back"));
        DrainUntil(Stable);
        Assert.AreEqual(6,session.State.Party.Length);
        Assert.IsFalse(session.State.Party.Any(mon=>mon.IsEgg));
        // This checkpoint's egg is already healthy and has current calculated stats.
        AssertStructure(owned,Owned(session.State),"Public PC transfers preserve every existing owner, including the boxed egg.");
        Assert.AreEqual(owned.Length,Owned(session.State).Select(mon=>mon.InstanceId).Distinct().Count());
        AssertStructure(basis.Inventory,session.State.Inventory,"The PC route neither earns nor consumes items.");
        Assert.AreEqual(Request(basis),Request(session.State));
        Assert.AreEqual(basis.World.Rng,session.State.World.Rng);
        Assert.AreEqual(basis.World.StepCount,session.State.World.StepCount);
        Continue();

        session=Place(session.State,house,4,5,session.Revision);
        Assert.AreEqual(Request(basis),Request(session.State));
        Assert.AreEqual(basis.World.Rng,session.State.World.Rng);
        AssertStructure(owned,Owned(session.State),"Map entry preserves the public PC ownership result.");
        Assert.IsFalse(session.State.Actors[butler].Visible);
        for(int i=0;i<60;i++)
        {
            var previous=session.State;
            Send(new("move",i%2==0?Direction.South:Direction.North));
            Assert.AreEqual(previous.World.StepCount+1,session.State.World.StepCount);
            Assert.AreEqual(SelphyRequestRules.AdvanceEligibleStep(Request(previous)),Request(session.State));
            Assert.AreEqual(house,session.State.World.Player.MapId.Value);
            Assert.AreEqual((4,i%2==0?6:5),(session.State.World.Player.X,session.State.World.Player.Y));
            Assert.IsTrue(Stable());
        }
        Assert.AreEqual(SelphyRequestRules.ExpiredSpecies,Request(session.State).RequestedSpecies);
        Assert.AreEqual(0,Request(session.State).StepCounter);
        Assert.AreEqual(basis.World.Rng,session.State.World.Rng);
        var sample=SelphyRequestRules.SampleRequest(Request(session.State),session.State.SeenSpecies.ToImmutableHashSet(),session.State.World.Rng);
        Assert.AreEqual(new SelphyRequestState(53,0,"ITEM_RARE_CANDY"),sample.State);
        Assert.AreEqual(10,sample.Trace.Length);
        Assert.AreEqual(new RngState(3829882909u,60812),sample.Rng);
        CollectionAssert.AreEqual(new[]{text+"OhImTiredOfWaiting",text+"WishToSeeMonPlease"},Talk());
        Assert.AreEqual(sample.State,Request(session.State));
        Assert.AreEqual(sample.Rng,session.State.World.Rng);
        Assert.IsTrue(SelphyRequestRules.PartyContainsRequestedSpecies(session.State.Party,53));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(SelphyRequestRules.ShownRequestedMonFlag));
        Continue();

        // Capture after the real walking/friendship updates. Showing a Pokemon is
        // neither a trade nor a fresh random request, and the Butler's steps are not player steps.
        var beforeReward=session.State;
        var reward=new Game.Domain.Ids.ItemId("pret:ITEM_RARE_CANDY");
        int quantity=OpeningInventoryRules.Quantity(beforeReward.Inventory!.BagItems,reward);
        var motions=new List<CampaignMotion>();
        var rewardDialogue=Talk(motions);
        CollectionAssert.AreEqual(new[]{text+"MonYouCaughtForMeThankYou",text+"ButlerYesMyLady",
            text+"SelphyGiveTokenOfAppreciation",text+"ButlerIShallDoAsYouBid",text+"ButlerPleaseAcceptThisAsHerThanks"},
            rewardDialogue.Where(symbol=>symbol.StartsWith(text,StringComparison.Ordinal)).ToArray());
        var butlerMotions=motions.Where(motion=>motion.ActorId==butler).ToArray();
        Assert.AreEqual(2,butlerMotions.Length);
        CollectionAssert.AreEqual(new[]{"walk_in_place_down","step_end"},butlerMotions[0].Actions.ToArray());
        CollectionAssert.AreEqual(new[]{"walk_left","walk_left","walk_left","walk_down","walk_down","walk_down",
            "walk_in_place_faster_right","step_end"},butlerMotions[1].Actions.ToArray());
        CollectionAssert.AreEqual(new[]{(6,2),(5,2),(4,2),(3,2),(3,3),(3,4),(3,5),(3,5)},
            butlerMotions[1].Path.Select(position=>(position.X,position.Y)).ToArray());
        var actor=session.State.Actors[butler];
        Assert.IsTrue(actor.Visible);
        Assert.AreEqual((3,5,3,5,Direction.East),(actor.X,actor.Y,actor.TemplateX,actor.TemplateY,actor.Facing));
        Assert.AreEqual(beforeReward.World.Player with{Facing=Direction.West},session.State.World.Player);
        Assert.AreEqual(beforeReward.World.StepCount,session.State.World.StepCount);
        Assert.AreEqual(beforeReward.World.Rng,session.State.World.Rng);
        Assert.AreEqual(sample.State with{RequestedSpecies=0},Request(session.State));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(SelphyRequestRules.ShownRequestedMonFlag));
        Assert.AreEqual(quantity+1,OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems,reward));
        AssertStructure(beforeReward.Inventory with
            {BagItems=OpeningInventoryRules.SetQuantity(beforeReward.Inventory.BagItems,reward,quantity+1)},
            session.State.Inventory,"Exactly one Rare Candy is added; every other inventory pocket and stack is unchanged.");
        AssertStructure(beforeReward.Party,session.State.Party,"Showing Persian leaves the entire party unchanged.");
        AssertStructure(beforeReward.Storage,session.State.Storage,"Showing Persian leaves every boxed owner unchanged.");
        AssertStructure(basis.BattledTrainers,session.State.BattledTrainers,"The route never invents a trainer win.");
        AssertStructure(basis.ForestTrainerHistory,session.State.ForestTrainerHistory,"The actual earned rescue receipt is retained.");
        AssertStructure(basis.HallOfFame,session.State.HallOfFame,"Prior earned Hall records are retained.");
        AssertStructure(basis.SeenSpecies,session.State.SeenSpecies,"Showing an owned Pokemon does not invent a seen species.");
        AssertStructure(basis.CaughtSpecies,session.State.CaughtSpecies,"Showing an owned Pokemon does not create a capture.");
        Continue();

        var fulfilled=session.State;
        Send(new("turn",Direction.North));
        CollectionAssert.AreEqual(new[]{text+"SelphyWishYouBestOfLuck"},Talk());
        Assert.AreEqual(Request(fulfilled),Request(session.State));
        Assert.AreEqual(fulfilled.World.Rng,session.State.World.Rng);
        Assert.AreEqual(fulfilled.World.StepCount,session.State.World.StepCount);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(SelphyRequestRules.ShownRequestedMonFlag));
        AssertStructure(fulfilled.Inventory,session.State.Inventory,"The restored visit guard prevents a duplicate reward.");
        AssertStructure(fulfilled.Party,session.State.Party,"Repeat dialogue leaves the same owned party intact.");
        AssertStructure(fulfilled.Storage,session.State.Storage,"Repeat dialogue leaves storage intact.");
        Assert.AreEqual(actor,session.State.Actors[butler],"The Butler keeps his committed position through Continue and repeat dialogue.");
        Continue();
    }

    private static CampaignSession Place(CampaignState basis,string mapName,int x,int y,long generation)
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var session=CampaignSession.CreateCurrent(catalog);
        var map=catalog.Maps[new(mapName)];
        // Only placement is controlled. Actual map entry owns temporary flags and
        // actors; earned history, owners, request variables, step count and RNG survive.
        var state=basis with
        {
            World=basis.World with{Player=new(map.Id,x,y,map.GetTile(x,y)!.Elevation,Direction.North)},
            Script=basis.Script with{Completed=true,Locked=false,Wait=null,Stack=[]},
            EntryPrograms=[],ArrivalFacing=null,Dialogue=null,Motions=[],PresentationTicks=0,PresentationEffects=[],
            MapLayout=null,MapEdits=null,PlayerVisible=true,IsCycling=false,IsSurfing=false,FieldWeather=null,
            FlashLevel=0,FieldTransition=null,QuestLog=null,Music=catalog.MapDefinition(map.Id).Music
        };
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        state=(CampaignState)typeof(CampaignSession).GetMethod("EnterMap",flags)!.Invoke(session,
            [state,null,Game.Domain.Roaming.RoamerTransition.Warp])!;
        state=(CampaignState)typeof(CampaignSession).GetMethod("Pump",flags)!.Invoke(session,[state])!;
        Assert.IsNull(session.ValidatePlayerSave(state));
        return CampaignSession.RestoreCurrent(catalog,state,generation);
    }

    private static void AssertStructure(object? expected,object? actual,string message)=>
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected),
            JsonSerializer.SerializeToElement(actual)),message);

    private static SelphyRequestState Request(CampaignState state)=>new(
        checked((ushort)state.Script.Variables.GetValueOrDefault(SelphyRequestRules.RequestedSpeciesVariable)),
        checked((ushort)state.Script.Variables.GetValueOrDefault(SelphyRequestRules.StepCounterVariable)),
        SelphyRequestRules.DeluxeRewards.Add("ITEM_LUXURY_BALL").Single(symbol=>
            CurrentCampaignFixture.Catalog.TryConstant(symbol,out int value)&&value==state.Script.Variables.GetValueOrDefault(SelphyRequestRules.RewardVariable)));
}
