using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignMapViewTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Call<T>(CampaignSession session, string method, params object[] arguments) =>
        (T)typeof(CampaignSession).GetMethod(method, Hidden)!.Invoke(session, arguments)!;
    private static IReadOnlyDictionary<MapId,T> View<T>(IReadOnlyDictionary<MapId,T> maps, Func<MapId,T,T> project)
    {
        var type=typeof(CampaignSession).GetNestedType("CampaignMapView`1", BindingFlags.NonPublic)!.MakeGenericType(typeof(T));
        return (IReadOnlyDictionary<MapId,T>)Activator.CreateInstance(type, BindingFlags.Public | Hidden,
            binder:null, args:[maps,project], culture:null)!;
    }
    private sealed class ObservedMaps<T>(IReadOnlyDictionary<MapId,T> maps) : IReadOnlyDictionary<MapId,T>
    {
        public readonly List<MapId> Reads=[];
        public int Enumerations;
        public int Count=>maps.Count;
        public IEnumerable<MapId> Keys=>maps.Keys;
        public IEnumerable<T> Values=>Keys.Select(key=>this[key]);
        public T this[MapId key] { get { Reads.Add(key); return maps[key]; } }
        public bool ContainsKey(MapId key)=>maps.ContainsKey(key);
        public bool TryGetValue(MapId key,out T value){Reads.Add(key);return maps.TryGetValue(key,out value!);}
        public IEnumerator<KeyValuePair<MapId,T>> GetEnumerator(){Enumerations++;return maps.GetEnumerator();}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    [TestMethod]
    public void ViewPreservesEagerLayerOrderDictionaryShapeAndOneProjectionPerRequestedKey()
    {
        MapId first=new("map.first"),second=new("map.second"),third=new("map.third"),missing=new("map.missing");
        var source=new Dictionary<MapId,int>{{first,2},{second,3},{third,4}};
        var reads=new ObservedMaps<int>(source);var innerCalls=new List<MapId>();var outerCalls=new List<MapId>();
        int Inner(MapId id,int value){innerCalls.Add(id);return value+7;}
        int Outer(MapId id,int value){outerCalls.Add(id);return value*3;}
        var view=View(View(reads,Inner),Outer);
        Assert.AreEqual(3,view.Count);CollectionAssert.AreEqual(source.Keys.ToArray(),view.Keys.ToArray());
        Assert.IsTrue(view.ContainsKey(first));Assert.AreEqual(0,reads.Reads.Count);Assert.AreEqual(0,innerCalls.Count);
        Assert.AreEqual(30,view[second]);Assert.IsTrue(view.TryGetValue(second,out int again));Assert.AreEqual(30,again);
        CollectionAssert.AreEqual(new[]{second},innerCalls);CollectionAssert.AreEqual(new[]{second},outerCalls);
        Assert.IsFalse(view.TryGetValue(missing,out _));Assert.ThrowsExactly<KeyNotFoundException>(()=>_ = view[missing]);
        Assert.AreEqual(1,innerCalls.Count,"Absent keys never invoke a projection.");
        var eager=source.ToDictionary(p=>p.Key,p=>(p.Value+7)*3);
        CollectionAssert.AreEqual(eager.ToArray(),view.ToArray());CollectionAssert.AreEqual(eager.Values.ToArray(),view.Values.ToArray());
        Assert.AreEqual(3,innerCalls.Count);Assert.AreEqual(3,outerCalls.Count);
        Assert.AreEqual(0,reads.Enumerations,"The view enumerates stable keys without forcing prior dictionary enumeration.");
    }
    private static readonly string[] ConnectedLayers=["SafariConnectedMaps","VictoryRoadWalkingMaps","CinnabarWalkingMaps",
        "SeviiRubyWalkingMaps","FourIslandWalkingMaps","SixIslandWalkingMaps","PostgameWalkingMaps","SeafoamWalkingMaps"];
    private static IReadOnlyDictionary<MapId,ConnectedFieldMap> Walking(CampaignSession session,IReadOnlyDictionary<MapId,ConnectedFieldMap> maps)
    {
        foreach(string layer in ConnectedLayers)maps=Call<IReadOnlyDictionary<MapId,ConnectedFieldMap>>(session,layer,maps);
        return maps;
    }
    [TestMethod]
    public void RegionalMovementViewsDoNotReadOrTransformOffscreenMaps()
    {
        var content=CurrentCampaignFixture.Catalog;var session=CampaignSession.CreateCurrent(content);
        MapId id=new("map.pallet-town");var state=session.State with{World=session.State.World with{Player=session.State.World.Player with{MapId=id}},MapEdits=null};
        var connectedReads=new ObservedMaps<ConnectedFieldMap>(content.ConnectedFieldMaps);
        var connected=Walking(session,connectedReads);
        Assert.AreEqual(0,connectedReads.Reads.Count);Assert.AreEqual(0,connectedReads.Enumerations);
        Assert.AreSame(content.ConnectedFieldMaps[id],connected[id]);Assert.AreSame(connected[id],connected[id]);
        CollectionAssert.AreEqual(new[]{id},connectedReads.Reads);
        var rawReads=new ObservedMaps<IFieldMap>(content.Maps);IReadOnlyDictionary<MapId,IFieldMap> raw=rawReads;
        raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"SilphMovementMaps",raw,state);
        raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"SafariMovementMaps",raw);
        raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"CinnabarMovementMaps",raw,state);
        raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"PokemonLeagueMovementMaps",raw,state,new CampaignCommand("move",Direction.East));
        foreach(string layer in new[]{"SeviiRubyMovementMaps","FourIslandMovementMaps","SixIslandMovementMaps","FiveIslandMovementMaps"})
            raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,layer,raw,state);
        raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"PostgameMovementMaps",raw);
        Assert.AreEqual(0,rawReads.Reads.Count);Assert.AreEqual(0,rawReads.Enumerations);
        Assert.AreSame(content.Maps[id],raw[id]);Assert.AreSame(raw[id],raw[id]);CollectionAssert.AreEqual(new[]{id},rawReads.Reads);
    }
    [TestMethod]
    public void TowerCourseMapsReuseBoundObjectsAndKeepAllNonTowerMaps()
    {
        var content=CurrentCampaignFixture.Catalog;var session=CampaignSession.CreateCurrent(content);
        foreach(var course in Enum.GetValues<TrainerTowerChallengeKind>())
        {
            var state=session.State with{TrainerTower=TrainerTowerProgressRules.Create() with{SelectedCourse=course}};
            var raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"TrainerTowerMovementMaps",state);
            var connected=Call<IReadOnlyDictionary<MapId,ConnectedFieldMap>>(session,"TrainerTowerConnectedMaps",state);
            Assert.AreSame(raw,Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"TrainerTowerMovementMaps",state));
            Assert.AreSame(connected,Call<IReadOnlyDictionary<MapId,ConnectedFieldMap>>(session,"TrainerTowerConnectedMaps",state));
            Assert.AreEqual(content.Maps.Count,raw.Count);Assert.AreEqual(content.ConnectedFieldMaps.Count,connected.Count);
            var floors=content.TrainerTowerCatalog.Course(course).Floors.ToDictionary(f=>new MapId($"map.trainer-tower-{f.Index+1}f"));
            foreach(var id in content.Maps.Keys)
            {
                Assert.AreSame(floors.TryGetValue(id,out var floor)?content.TrainerTowerMap(id,floor.Format):content.Maps[id],raw[id]);
                Assert.AreSame(floor is not null?content.TrainerTowerConnectedMap(id,floor.Format):content.ConnectedFieldMaps[id],connected[id]);
            }
        }
    }
    [TestMethod]
    public void CompleteWalkingEnumerationMatchesEagerCellRulesIncludingAdjacentOccupancyAndEdits()
    {
        var content=CurrentCampaignFixture.Catalog;var session=CampaignSession.CreateCurrent(content);MapId id=new("map.pallet-town");
        var state=session.State with{World=session.State.World with{Player=new(id,6,8,3,Direction.North)},
            Actors=ImmutableSortedDictionary<string,CampaignActor>.Empty.Add("test-actor",new("test-actor",1,"test",7,8,0,Direction.South,"test","test","0",true,false,7,8)),
            MapLayout=null,MapEdits=null,TrainerTower=TrainerTowerProgressRules.Create() with{SelectedCourse=TrainerTowerChallengeKind.Mixed}};
        // Exercise the same virtual-border edit translation used by connected maps.
        var source=content.ConnectedFieldMaps[id];
        var border=Enumerable.Range(-7,source.Height+14).SelectMany(y=>Enumerable.Range(-7,source.Width+15).Select(x=>(X:x,Y:y)))
            .Select(p=>(p.X,p.Y,Cell:ConnectedFieldCoordinates.Resolve(content.ConnectedFieldMaps,id,p.X,p.Y)))
            .First(p=>p.Cell is {} cell&&cell.Map!=id);
        var destination=border.Cell!.Value;var sourceCell=content.SourceMaps[destination.Map].GetSourceCell(destination.X,destination.Y)!;
        state=state with{MapEdits=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty.Add(border.X+":"+border.Y,
            new(border.X,border.Y,sourceCell.MetatileId,sourceCell.Collision==0?3:0,7))};
        var actual=Call<IReadOnlyDictionary<MapId,ConnectedFieldMap>>(session,"OccupiedFieldMaps",state);
        var expected=EagerConnected(state);
        Assert.AreEqual(expected.Count,actual.Count);int cells=0,occupied=0,changed=0;
        foreach(var (mapId,wanted) in expected)
        {
            var got=actual[mapId];Assert.AreEqual(wanted.Id,got.Id);Assert.AreEqual(wanted.Width,got.Width);Assert.AreEqual(wanted.Height,got.Height);
            CollectionAssert.AreEqual(wanted.Connections.ToArray(),got.Connections.ToArray(),mapId.Value);
            CollectionAssert.AreEqual(wanted.Cells.ToArray(),got.Cells.ToArray(),mapId.Value);
            cells+=got.Cells.Length;occupied+=got.Cells.Count(c=>c.Occupied);
            changed+=got.Cells.Where((c,index)=>c!=content.ConnectedFieldMaps[mapId].Cells[index]).Count();
        }
        Assert.IsTrue(cells>0&&occupied>0&&changed>0);Assert.AreEqual(expected.Count,actual.ToArray().Length);
        Assert.AreEqual(7,actual[destination.Map].CellAt(destination.X,destination.Y)!.Elevation);
    }
    // Independent eager reference: retain source collision/elevation and apply the
    // established movement-only behavior rules in their documented layer order.
    private static Dictionary<MapId,ConnectedFieldMap> EagerConnected(CampaignState state)
    {
        var content=CurrentCampaignFixture.Catalog;var maps=content.ConnectedFieldMaps.ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var floor in content.TrainerTowerCatalog.Course(state.TrainerTower!.SelectedCourse).Floors)
        {MapId id=new($"map.trainer-tower-{floor.Index+1}f");maps[id]=content.TrainerTowerConnectedMap(id,floor.Format)!;}
        foreach(var edit in state.MapEdits!.Values)
        {
            var target=ConnectedFieldCoordinates.Resolve(maps,state.World.Player.MapId,edit.X,edit.Y)!.Value;var map=maps[target.Map];
            int behavior=content.MetatileDefinition(target.Map,edit.SourceMetatileId)!.Behavior;
            maps[target.Map]=map with{Cells=map.Cells.SetItem(target.Y*map.Width+target.X,new(behavior,edit.Collision,edit.Elevation))};
        }
        var nearby=maps[state.World.Player.MapId].Connections.Select(c=>c.DestinationMapId).Append(state.World.Player.MapId).ToHashSet();
        foreach(var id in maps.Keys.ToArray())
        {
            var map=maps[id];var cells=map.Cells.ToBuilder();
            if(nearby.Contains(id))
            {
                var actors=id==state.World.Player.MapId?state.Actors.Values.Where(a=>a.Active).Select(a=>(a.X,a.Y,a.Elevation)):
                    content.MapDefinition(id).Actors.Where(a=>a.HideFlag=="0"||!state.Script.Flags.GetValueOrDefault(a.HideFlag)).Select(a=>(a.X,a.Y,a.Elevation));
                foreach(var a in actors)if(a.X>=0&&a.Y>=0&&a.X<map.Width&&a.Y<map.Height)
                {int at=a.Y*map.Width+a.X;var cell=cells[at];if(a.Elevation==0||cell.Elevation==0||a.Elevation==cell.Elevation)cells[at]=cell with{Occupied=true};}
            }
            map=SafariFieldMovementRules.ForConnectedMovement(map with{Cells=cells.ToImmutable()},EarlyLandEncounterRules.SafariRulesVersion);
            maps[id]=map with{Cells=map.Cells.Select(c=>ExpectedWalkingCell(id.Value,c)).ToImmutableArray()};
        }
        return maps;
    }
    private static ConnectedFieldCell ExpectedWalkingCell(string id,ConnectedFieldCell cell)
    {
        int b=cell.SourceBehavior;
        if(id is "map.victory-road-1f" or "map.victory-road-2f" or "map.victory-road-3f"&&b==0x20)b=8;
        if(id=="map.route-23"&&b==0x17&&cell.Collision==0&&cell.Elevation==3)b=0;
        if(id is "map.pokemon-mansion-3f" or "map.victory-road-3f"&&b==0x66)b=8;
        if(id is "map.one-island-kindle-road" or "map.one-island-kindle-road-ember-spa" or "map.mt-ember-exterior"&&cell.Collision==0&&b is 0x0c or 0x17 or 0x28)b=0;
        if((id=="map.four-island"||id.StartsWith("map.four-island-",StringComparison.Ordinal))&&cell.Collision==0&&b is 0x16 or 0x23 or 0x26 or 0x27)b=0;
        if(id is "map.six-island-water-path" or "map.six-island-ruin-valley"&&cell.Collision==0&&b==0x0c)b=0;
        if(cell.Collision==0)
        {
            if((id,b) is ("map.seven-island-sevault-canyon",0x0c) or ("map.seven-island-sevault-canyon-entrance",0x0c) or
                ("map.seven-island-tanoby-ruins",0x0c or 0x17) or ("map.seven-island-trainer-tower",0x17) or
                ("map.birth-island-exterior",0x0c) or ("map.two-island-cape-brink",0x0c))b=0;
            if(id=="map.seven-island-sevault-canyon-tanoby-key"&&b==0x20)b=8;
            if(id is "map.seafoam-islands-b3f" or "map.seafoam-islands-b4f"&&
                (SeafoamFieldRules.CurrentDirection(b) is not null||b is 0x11 or 0x12 or 0x17 or 0x1a or 0x1b))b=0x10;
            if(id=="map.route-20"&&b==0x17&&cell.Elevation==3)b=0;
        }
        return cell with{SourceBehavior=b};
    }
    [TestMethod]
    public void MapLayoutReplacementKeepsRawAndConnectedVariantIdentity()
    {
        var content=CurrentCampaignFixture.Catalog;var session=CampaignSession.CreateCurrent(content);MapId id=new("map.three-island-dunsparce-tunnel");
        const string layout="LAYOUT_THREE_ISLAND_DUNSPARCE_TUNNEL_DUG_OUT";
        var state=session.State with{World=session.State.World with{Player=session.State.World.Player with{MapId=id}},MapLayout=layout};
        var raw=Call<IReadOnlyDictionary<MapId,IFieldMap>>(session,"MapLayoutRawMaps",state);
        var connected=Call<IReadOnlyDictionary<MapId,ConnectedFieldMap>>(session,"MapLayoutConnectedMaps",state);
        Assert.AreSame(content.MapLayout(id,layout),raw[id]);Assert.AreSame(content.ConnectedMapLayout(id,layout),connected[id]);
        Assert.AreSame(content.Maps[new("map.pallet-town")],raw[new("map.pallet-town")]);
        Assert.AreEqual(content.Maps.Count,raw.Count);Assert.AreEqual(content.ConnectedFieldMaps.Count,connected.Count);
    }
}
