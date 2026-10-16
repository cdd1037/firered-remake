using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class TrainerCardPhotoTests
{
    private static PretCampaignCatalog Content=>CurrentCampaignFixture.Catalog;
    private int commandId;
    private static void Set(CampaignSession session,CampaignState state)=>typeof(CampaignSession)
        .GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,Content.ContentVersion,
                Math.Max(session.Revision,CurrentCampaignFixture.Checkpoint.Generation)));
    private CampaignSession Visit(int money=1000,CampaignState? source=null)
    {
        var session=CampaignSession.CreateCurrent(Content);var state=source??CurrentCampaignFixture.Checkpoint.State;
        var map=new MapId("map.celadon-city-game-corner");
        state=state with{Money=money,World=state.World with{Player=new(map,16,13,Content.Maps[map].GetTile(16,13)!.Elevation,Direction.East)},
            Script=StoryState.Empty with{Flags=state.Script.Flags,Variables=state.Script.Variables,EffectSequence=state.Script.EffectSequence},
            Dialogue=null,ItemMenu=null,MonPicture=null,Motions=[],PresentationTicks=0,PresentationEffects=[],EntryPrograms=[],ArrivalFacing=null,
            IsSurfing=false,IsCycling=false,BicycleSlope=false,MapEdits=null};
        state=(CampaignState)typeof(CampaignSession).GetMethod("EnterMap",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(session,[state,null,Game.Domain.Roaming.RoamerTransition.Warp])!;
        Set(session,(CampaignState)typeof(CampaignSession).GetMethod("Pump",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,[state])!);
        Finish(session);return session;
    }
    private void Send(CampaignSession session,CampaignCommand command)
    {
        long revision=session.Revision;string id="card-photo:"+commandId++;
        var result=session.Execute(id,revision,command);Assert.AreEqual(CommandStatus.Accepted,result.Status,command+": "+result.Error);
        var stale=session.Execute(id,revision,command);Assert.AreEqual(CommandStatus.Rejected,stale.Status);Assert.AreEqual("stale_revision",stale.Error);
    }
    private void Finish(CampaignSession session,int? choice=null)
    {
        for(int n=0;n<100;n++)
        {
            var wait=session.State.Script.Wait?.Kind;
            if(choice is null && (wait==StoryWaitKind.MenuChoice || wait==StoryWaitKind.Choice &&
                session.State.Dialogue is {} dialog && dialog.Page+1==dialog.Pages.Length))return;
            if(session.State.Script.Completed && session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty)return;
            Send(session,wait==StoryWaitKind.ButtonPress?new("button_press",Text:"A"):new("ack",Choice:wait is StoryWaitKind.MenuChoice or StoryWaitKind.Choice?choice:null));
        }
        Assert.Fail("Printer script did not finish.");
    }
    private void Start(CampaignSession session)
    {
        Send(session,new("interact"));Finish(session);Assert.AreEqual(StoryWaitKind.Choice,session.State.Script.Wait?.Kind);
    }
    private void Buy(CampaignSession session,int tint)
    {
        Start(session);Send(session,new("ack",Choice:1));Finish(session);
        Assert.AreEqual(StoryWaitKind.MenuChoice,session.State.Script.Wait?.Kind);
        Assert.IsTrue(StoryMenuRules.Pending(session.State.Script,Content)!.IgnoreB);
        Send(session,new("ack",Choice:tint));Finish(session);
        Assert.IsTrue(session.State.Script.Completed);Assert.IsNull(session.State.MoneyBox);
    }
    private static int[] Photo(CampaignState state)=>Enumerable.Range(1,6)
        .Select(i=>state.Script.Variables.GetValueOrDefault(CampaignSession.TrainerCardPhotoPrefix+i)).ToArray();

    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void OriginalPrinterChargesOnceAndPersistsSnapshot(int tint)
    {
        var session=Visit();var original=session.State;var rng=original.World.Rng;
        Buy(session,tint);Assert.AreEqual(950,session.State.Money);Assert.AreEqual(rng,session.State.World.Rng);
        Assert.AreEqual(tint,session.State.Script.Variables[CampaignSession.TrainerCardPhotoTint]);
        var expected=Enumerable.Range(0,6).Select(i=>i<original.Party.Length?(int)SourceMailComposerRules.MailSpecies(Content.MailComposer!,
            NameRaterRules.SpeciesOrEgg(original.Party[i]),original.Party[i].Pokemon.Personality):0).ToArray();
        Assert.AreSequenceEqual(expected,Photo(session.State));
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var bytes=SaveCodec.Encode(session.State,session.Revision,session.RulesVersion,Content.ContentVersion);
        if(Environment.GetEnvironmentVariable("FIRERED_TRAINER_CARD_FIXTURE_DIR") is {Length:>0} fixtureDirectory)
        {
            Directory.CreateDirectory(fixtureDirectory);
            File.WriteAllBytes(Path.Combine(fixtureDirectory,$"trainer-card-photo-tint-{tint}.json"),bytes);
            File.WriteAllText(Path.Combine(fixtureDirectory,"README.txt"),
                "Controlled visual fixtures from the existing Application checkpoint fixture. Its lifetime hatch count is synthetic test input, not established player history. " +
                "Photos were purchased through the original printer transaction and saves passed full validation. No stars, HoF times or sticker awards were fabricated. " +
                "Player stands at (16,13) facing the original Game Corner printer. Never replace a user save with these fixtures.");
        }
        var decoded=SaveCodec.Decode<CampaignState>(bytes,session.RulesVersion,Content.ContentVersion,validate:session.ValidatePlayerSave);
        var restored=CampaignSession.RestoreCurrent(Content,decoded.State,decoded.Generation);
        Assert.AreSequenceEqual(expected,Photo(restored.State));
        Set(restored,restored.State with{Party=restored.State.Party.Reverse().ToImmutableArray()});
        Assert.AreSequenceEqual(expected,Photo(restored.State),"The photograph must not read the current party.");
    }
    [TestMethod]
    [DataRow(49,1)][DataRow(1000,0)]
    public void InsufficientMoneyAndDeclineKeepPreviousPhoto(int money,int yes)
    {
        var session=Visit(money);var before=session.State;Start(session);Send(session,new("ack",Choice:yes));Finish(session);
        Assert.AreEqual(money,session.State.Money);Assert.AreSequenceEqual(Photo(before),Photo(session.State));Assert.IsNull(session.State.MoneyBox);
    }
    [TestMethod]
    public void SnapshotUsesEggAndUnownMailEncodingAndClearsUnusedSlots()
    {
        var initial=CurrentCampaignFixture.Checkpoint.State;var mon=initial.Party[0];
        var unown=PokemonFactory.Create(Content.PokemonContent!.Catalog,new(new("pret:SPECIES_UNOWN"),5,initial.TrainerId,
            IvPolicy.Fixed(3),PersonalityPolicy.Fixed(3)),new Game.Domain.Randomness.RngState(0)).Pokemon;
        // Isolated source-transaction fixture; deliberately not asserted as a historical player save.
        var source=initial with{Party=[mon with{IsEgg=true},mon with{Pokemon=unown,InstanceId=initial.NextPokemonId}]};
        var session=Visit(source:source);Buy(session,2);
        Assert.AreSequenceEqual(new[]{412,30003,0,0,0,0},Photo(session.State));
        Set(session,session.State with{Party=[mon]});Buy(session,0);
        Assert.AreSequenceEqual(new[]{mon.Pokemon.SourceSpeciesId,0,0,0,0,0},Photo(session.State));
    }
    [TestMethod]
    public void PhotoVariablesRejectInvalidStoredTintsSpeciesAndHoles()
    {
        var session=Visit();var method=typeof(CampaignSession).GetMethod("ValidateTrainerCardPhoto",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(var pair in new[]{(CampaignSession.TrainerCardPhotoTint,4),(CampaignSession.TrainerCardPhotoPrefix+1,440),
            (CampaignSession.TrainerCardPhotoPrefix+1,201),(CampaignSession.TrainerCardPhotoPrefix+2,25)})
        {
            var state=session.State with{Script=session.State.Script with{Variables=ImmutableSortedDictionary<string,int>.Empty.SetItem(pair.Item1,pair.Item2)}};
            Assert.IsNotNull(method.Invoke(session,[state]));
        }
    }
    [TestMethod]
    public void SupportedStarsUseKnownDebutAndBothOriginalDexCompletions()
    {
        var session=Visit();var baseline=session.State;
        var all=Content.PokedexMenu!.Entries.Values.Where(e=>e.NationalNumber is >=1 and <=384 && e.NationalNumber is not (151 or 249 or 250 or 251))
            .Select(e=>e.SourceSpeciesId).ToImmutableSortedSet();
        Set(session,baseline with{CaughtSpecies=[],SeenSpecies=[],HallOfFame=null});Assert.AreEqual(0,session.TrainerCardStars);
        Set(session,session.State with{HallOfFame=new([],null)});Assert.AreEqual(0,session.TrainerCardStars);
        Set(session,session.State with{HallOfFame=new([],1)});Assert.AreEqual(1,session.TrainerCardStars);
        Set(session,session.State with{CaughtSpecies=Enumerable.Range(1,150).ToImmutableSortedSet(),SeenSpecies=Enumerable.Range(1,150).ToImmutableSortedSet()});Assert.AreEqual(2,session.TrainerCardStars);
        Set(session,session.State with{CaughtSpecies=all,SeenSpecies=all});Assert.AreEqual(3,session.TrainerCardStars);
        Set(session,session.State with{CaughtSpecies=all.Remove(150)});Assert.AreEqual(1,session.TrainerCardStars);
    }
}
