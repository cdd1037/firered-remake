using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.FieldItems;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class FieldGrassAndPowderTests:WildBattleFixture
{
    private CampaignState At(string mapId,int x,int y,Direction facing)
    {
        var s=FieldFixture();var map=content.Maps[new(mapId)];var definition=content.MapDefinition(map.Id);
        s=s with{World=s.World with{Player=new(map.Id,x,y,map.GetTile(x,y)!.Elevation,facing)},
            Actors=definition.Actors.ToImmutableSortedDictionary(a=>a.LocalId,a=>new CampaignActor(a.LocalId,a.SourceLocalId,a.GraphicsId,a.X,a.Y,a.Elevation,Direction.South,a.MovementType,a.ScriptId,a.HideFlag,false,false,a.X,a.Y),StringComparer.Ordinal),
            ObjectSlots=ImmutableSortedDictionary<int,string>.Empty.Add(0,"LOCALID_PLAYER"),SelectedObjectSlot=0,LastTalked=null,MapEdits=null,MapLayout=null,
            FlashLevel=0,IsSurfing=false,IsCycling=false,Music=definition.Music,EntryPrograms=[],ArrivalFacing=null,Dialogue=null,Motions=[],PresentationEffects=[],PresentationTicks=0};
        return (CampaignState)typeof(CampaignSession).GetMethod("RefreshActors",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(validator,[s])!;
    }
    private CampaignSession Start(CampaignState state)
    {Assert.IsNull(validator.ValidatePlayerSave(state));return CampaignSession.RestoreCurrent(content,state,checkpoint.Generation);}
    private CampaignState Cutter(CampaignState state)
    {
        state=CurrentFixturePokemon.BringToLead(content,state,45);var owner=state.Party[0];var move=catalog.Moves[new("pret:MOVE_CUT")];
        return state with{Party=state.Party.SetItem(0,owner with{Pokemon=owner.Pokemon with{Moves=[new(move.Id,0,move.Pp)]}})};
    }
    private void Cut(CampaignSession session)
    {
        Send(session,new("field_cut"));Assert.IsTrue(session.State.CutEffect!.Grass);
        Send(session,new("field_cut_grass_ack",Text:session.State.CutEffect.WaitId));
        Send(session,new("field_cut_grass_ack",Text:session.State.CutEffect!.WaitId));
    }
    private List<string> Until(CampaignSession session,Func<bool> done)
    {
        List<string> seen=[];
        for(int i=0;i<80;i++){if(session.State.Dialogue is {} d)seen.Add(d.TextSymbol);if(done())return seen;Send(session,new("ack"));}
        Assert.Fail("Source field conversation failed to reach callback");return seen;
    }
    private void Finish(CampaignSession session)=>Until(session,()=>session.State.Script.Completed&&session.State.Script.Wait is null&&session.State.Dialogue is null);
    private void Interest(CampaignSession session,int answer)
    {Send(session,new("interact"));Until(session,()=>session.State.Script.Wait?.Kind==StoryWaitKind.Choice&&(session.State.Dialogue is not {} choice||choice.Page+1==choice.Pages.Length));Send(session,new("ack",Choice:answer));}
    [TestMethod]
    public void Route1CutUsesSourceNineCellElevationAndKeepsPartyRngThenBlocksEncounters()
    {
        var session=Start(Cutter(At("map.route-1",11,6,Direction.South)));var before=session.State;
        Assert.IsTrue(session.CanUseFieldCut(0));Cut(session);
        var edits=session.State.MapEdits!;Assert.IsTrue(edits.Count>0);
        foreach(var e in edits.Values)
        {
            Assert.IsTrue(Math.Abs(e.X-11)<=1&&Math.Abs(e.Y-6)<=1);Assert.AreEqual(before.World.Player.Elevation,e.Elevation);
            Assert.AreEqual(0,e.Collision);Assert.AreEqual(0,session.FieldEncounterTypeAt(e.X,e.Y));
            Assert.AreEqual(content.CutGrassMetatiles[content.SourceMetatileAt(before.World.Player.MapId,e.X,e.Y)!.SourceMetatileId],e.SourceMetatileId);
        }
        Assert.AreEqual(CanonicalJson.Hash(before.Party),CanonicalJson.Hash(session.State.Party));Assert.AreEqual(before.World.Rng,session.State.World.Rng);
        Assert.AreEqual((before.GameStats?.GetValueOrDefault("GAME_STAT_USED_CUT")??0)+1,session.State.GameStats!["GAME_STAT_USED_CUT"]);
        Assert.IsFalse(session.CanUseFieldCut(0));Assert.IsNull(session.ValidatePlayerSave(session.State));
        var restored=CampaignSession.RestoreCurrent(content,session.PrepareFieldSave(),session.Revision);Assert.AreEqual(CanonicalJson.Hash(edits),CanonicalJson.Hash(restored.State.MapEdits));
        var result=session.Execute("repeat-cut",session.Revision,new("field_cut"));Assert.AreEqual("field_cut_nothing_to_cut",result.Error);
    }
    [TestMethod]
    public void SourceGrassOnlyChangesSameElevationAndSaveViewDropsDistantEditsWithoutChangingLiveMap()
    {
        var state=Cutter(At("map.route-1",11,6,Direction.South));var session=Start(state);Cut(session);
        var map=content.SourceMaps[new("map.route-1")];
        var distant=map.Cells.First(c=>c.Collision==0&&c.Elevation==state.World.Player.Elevation&&c.Y>20&&
            !content.MapDefinition(map.Id).Actors.Any(a=>a.X==c.X&&a.Y==c.Y));
        var other=session.State with{World=session.State.World with{Player=session.State.World.Player with{X=distant.X,Y=distant.Y}}};
        other=(CampaignState)typeof(CampaignSession).GetMethod("RefreshActors",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(validator,[other])!;
        var far=Start(other);Assert.IsTrue(far.State.MapEdits!.Count>0);Assert.AreEqual(0,far.PrepareFieldSave().MapEdits!.Count);
        Assert.IsTrue(far.State.MapEdits.Count>0);
        // A different-height neighboring tile must remain byte-for-byte unchanged.
        var candidates=map.Cells.Where(c=>c.Terrain==1&&c.Elevation!=state.World.Player.Elevation).ToArray();
        foreach(var cell in candidates.Where(c=>Math.Abs(c.X-11)<=1&&Math.Abs(c.Y-6)<=1))Assert.IsFalse(session.State.MapEdits!.ContainsKey(cell.X+":"+cell.Y));
    }
    [TestMethod]
    public void PowderManGiftThenEveryZeroBalanceVendorChoiceAndCancelRemainSafe()
    {
        var state=At("map.cerulean-city-house-5",7,5,Direction.North);
        state=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_GOT_POWDER_JAR",false)}};
        var session=Start(state);Interest(session,1);Finish(session);
        Assert.AreEqual(1,OpeningInventoryRules.Quantity(session.State.Inventory!.KeyItems??[],new("pret:ITEM_POWDER_JAR")));
        Assert.IsTrue(session.State.Script.Flags["FLAG_GOT_POWDER_JAR"]);Assert.AreEqual(0,session.State.BerryPowder);
        Send(session,new("interact"));Until(session,()=>session.SourceMenu is not null);
        Assert.AreEqual("LISTMENU_BERRY_POWDER",session.SourceMenu!.Definition.Id);Assert.IsTrue(session.State.BerryPowderVendor);
        var inventory=CanonicalJson.Hash(session.State.Inventory);
        for(int i=0;i<11;i++)
        {
            Send(session,new("ack",Choice:i));Until(session,()=>session.State.Script.Wait?.Kind==StoryWaitKind.Choice&&(session.State.Dialogue is not {} choice||choice.Page+1==choice.Pages.Length));
            Send(session,new("ack",Choice:1));var seen=Until(session,()=>session.SourceMenu is not null);
            CollectionAssert.Contains(seen,"CeruleanCity_House1_Text_DontHaveEnoughBerryPowder");
            Assert.AreEqual(inventory,CanonicalJson.Hash(session.State.Inventory));Assert.AreEqual(0,session.State.BerryPowder);
        }
        Send(session,new("ack",Choice:127));Finish(session);Assert.IsFalse(session.State.BerryPowderVendor);Assert.IsNull(session.ValidatePlayerSave(session.State));
        Send(session,new("bag_open"));Send(session,new("field_powder_jar",Text:"pret:ITEM_POWDER_JAR"));
        Assert.AreEqual("gText_PowderQty",session.State.ItemMenu!.MessageSymbol);Assert.AreEqual("0",session.State.TextBuffers["STR_VAR_1"]);
        Send(session,new("menu_ack"));Assert.AreEqual("bag",session.State.ItemMenu!.View);
        Assert.IsTrue(session.SupportsRegisteredKeyItem("pret:ITEM_POWDER_JAR"));
        Send(session,new("item_register",Text:"pret:ITEM_POWDER_JAR"));Send(session,new("menu_back"));
        Send(session,new("use_registered"));Assert.AreEqual("registered",session.State.ItemMenu!.Origin);
        Assert.AreEqual("0",session.State.TextBuffers["STR_VAR_1"]);Send(session,new("menu_ack"));Assert.IsNull(session.State.ItemMenu);
        Assert.IsNull(session.ValidatePlayerSave(session.State));
    }
    [TestMethod]
    public void PowderManDeclineAndNoBerryPouchDoNotGiveJar()
    {
        var state=At("map.cerulean-city-house-5",7,5,Direction.North);
        var session=Start(state);Interest(session,0);Finish(session);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_GOT_POWDER_JAR"));
        state=state with{Inventory=state.Inventory! with{Berries=[],KeyItems=state.Inventory.KeyItems!.Value.RemoveAll(s=>s.Item==SsAnneItemRules.BerryPouchId)},
            Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_SYS_GOT_BERRY_POUCH",false)}};
        session=Start(state);Interest(session,1);var seen=Until(session,()=>session.State.Script.Completed&&session.State.Script.Wait is null);
        CollectionAssert.Contains(seen,"CeruleanCity_House1_Text_WhyMustYouLieNoBerries");Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_GOT_POWDER_JAR"));
    }
    [TestMethod]
    public void GrassCutAcknowledgementsAreCorrelatedAndTreeStillWins()
    {
        var grass=Start(Cutter(At("map.route-1",11,6,Direction.South)));Send(grass,new("field_cut"));
        var before=grass.State;var wrong=grass.Execute("wrong-cut-ack",grass.Revision,new("field_cut_grass_ack",Text:"stale"));
        Assert.AreEqual("field_cut_callback_mismatch",wrong.Error);Assert.AreSame(before,grass.State);
        var locked=grass.Execute("cut-walk",grass.Revision,new("move",Direction.South));Assert.AreEqual("field_cut_event_active",locked.Error);
        var tree=content.SourceMaps.Values.SelectMany(m=>content.MapDefinition(m.Id).Actors.Select(a=>(m,a)))
            .First(v=>v.a.GraphicsId=="OBJ_EVENT_GFX_CUT_TREE"&&v.m.GetTile(v.a.X,v.a.Y+1) is {Blocked:false});
        var s=Cutter(At(tree.m.Id.Value,tree.a.X,tree.a.Y+1,Direction.North));
        s=s with{Script=s.Script with{Flags=s.Script.Flags.SetItem(tree.a.HideFlag,false)}};
        s=(CampaignState)typeof(CampaignSession).GetMethod("RefreshActors",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(validator,[s])!;
        var session=Start(s);Send(session,new("field_cut"));
        Assert.IsFalse(session.State.CutEffect!.Grass);Assert.AreEqual(tree.a.LocalId,session.State.CutEffect.ActorId);
    }
    [TestMethod]
    [DataRow("map.viridian-forest")]
    [DataRow("map.route-16")]
    [DataRow("map.route-25")]
    public void GrassCutWorksAcrossSourceTilesetsWithoutMapAdmission(string id)
    {
        var map=content.SourceMaps[new(id)];
        var cell=map.Cells.First(c=>c.Terrain==1&&c.Collision==0&&content.CutGrassMetatiles.ContainsKey(c.MetatileId)&&
            !content.MapDefinition(map.Id).Actors.Any(a=>a.X==c.X&&a.Y==c.Y));
        var session=Start(Cutter(At(id,cell.X,cell.Y,Direction.South)));Cut(session);
        Assert.AreEqual(content.CutGrassMetatiles[cell.MetatileId],session.State.MapEdits![cell.X+":"+cell.Y].SourceMetatileId);
        Assert.IsNull(session.ValidatePlayerSave(session.State));
    }
    [TestMethod]
    public void CameraConnectionsCarryOnlyTheSourceViewportAndWarpEntryClearsGrass()
    {
        var session=Start(Cutter(At("map.route-25",1,3,Direction.West)));Cut(session);
        Send(session,new("move",Direction.West));Send(session,new("move",Direction.West));
        Assert.AreEqual("map.route-24",session.State.World.Player.MapId.Value);
        Assert.IsTrue(session.State.MapEdits!.Values.Any(e=>e.X>=content.Maps[session.State.World.Player.MapId].Width));
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var saved=CampaignSession.RestoreCurrent(content,session.PrepareFieldSave(),session.Revision);
        Send(saved,new("move",Direction.East));Send(saved,new("move",Direction.East));
        Assert.AreEqual("map.route-25",saved.State.World.Player.MapId.Value);
        Assert.AreEqual(1,saved.State.MapEdits!["1:3"].SourceMetatileId);
        var reloaded=(CampaignState)typeof(CampaignSession).GetMethod("EnterMap",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(saved,[saved.State,Direction.South,Game.Domain.Roaming.RoamerTransition.Warp])!;
        Assert.IsNull(reloaded.MapEdits);
    }
    [TestMethod]
    public void PowderSavedAmountIsBoundedAndNoDeclinedGiftOrVendorVisitMintsIt()
    {
        var state=At("map.cerulean-city-house-5",7,5,Direction.North);
        Assert.AreEqual("save_berry_powder_range",validator.ValidatePlayerSave(state with{BerryPowder=-1}));
        Assert.AreEqual("save_berry_powder_range",validator.ValidatePlayerSave(state with{BerryPowder=100000}));
        var session=Start(state with{BerryPowder=99999});Interest(session,0);Finish(session);
        Assert.AreEqual(99999,session.State.BerryPowder);
    }
    [TestMethod]
    [DataRow("map.seafoam-islands-b3f")]
    [DataRow("map.four-island-icefall-cave-1f")]
    [DataRow("map.rocket-hideout-b2f")]
    [DataRow("map.route-2")]
    public void SharedMetatileDefinitionsPreserveExistingFieldAttributes(string id)
    {
        var map=content.SourceMaps[new(id)];
        foreach(var cell in map.Cells.DistinctBy(c=>c.MetatileId))
        {
            var definition=content.MetatileDefinition(map.Id,cell.MetatileId)!;Assert.IsNotNull(definition);
            Assert.AreEqual((cell.BehaviorId,cell.Terrain,cell.EncounterType,cell.LayerType),
                (definition.Behavior,definition.Terrain,definition.EncounterType,definition.LayerType));
        }
    }
}
