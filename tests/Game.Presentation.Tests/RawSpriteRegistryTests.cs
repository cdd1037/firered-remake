using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RawSpriteRegistryTests
{
    private sealed class Sprite(int slot)
    {
        public int Slot{get;}=slot;
        public bool Counted{get;init;}
        public bool Visible{get;init;}=true;
        public int Kind{get;init;}=1;
        public Action<Sprite> Callback{get;set;}=_=>{};
    }
    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)][DataRow(4)]
    [DataRow(5)][DataRow(6)][DataRow(7)][DataRow(8)][DataRow(9)]
    public void LiveRegistryTraversalMatchesExtractedSourceAllocationAndDeletion(int scenario)
    {
        using var source=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/raw-sprite-registry-source-vectors.json")));
        var registry=new SourceSpriteRegistry<Sprite>(s=>s.Slot);var actual=new List<string>();int tick=0,once=0;
        void Log(string op,int slot)=>actual.Add($"{tick},{op},{slot}");
        void Frame(Sprite sprite){if(sprite.Kind!=0)Log("F",sprite.Slot);}
        void Callback(Sprite sprite)=>sprite.Callback(sprite);
        Sprite Child(int slot){Log("A",slot);return new(slot){Callback=s=>Log("C",s.Slot)};}
        void Remove(int slot){Log("D",slot);registry.RemoveAt(slot);}
        void Parent(Sprite sprite)
        {
            Log("P",sprite.Slot);if(once++!=0)return;
            if(scenario==4)Remove(2);
            if(scenario==5)Remove(sprite.Slot);
            if(scenario==6){Remove(2);return;}
            int slot=scenario is 2 or 3?registry.CreateAndAnimate(Child,Callback,Frame):registry.Create(Child,atEnd:scenario==8);
            if(slot==SourceSpriteRegistry<Sprite>.Capacity)Log("X",slot);
        }
        void Reserve(int slot)=>registry.Add(new(slot){Kind=0});
        void PlaceParent(int slot)=>registry.Add(new(slot){Callback=Parent});
        if(scenario is 1 or 3){Reserve(1);PlaceParent(2);}
        else if(scenario==5){Reserve(0);Reserve(1);PlaceParent(2);}
        else if(scenario==7){for(int i=0;i<64;i++){if(i==1)PlaceParent(i);else Reserve(i);}}
        else if(scenario!=9){Reserve(0);PlaceParent(1);if(scenario is 4 or 6)registry.Add(new(2){Callback=s=>Log("O",s.Slot)});}
        for(tick=0;tick<2;tick++){registry.Advance(Callback,Frame);if(scenario==9&&tick==0)registry.Create(Child);}
        var expected=source.RootElement.GetProperty(scenario.ToString(CultureInfo.InvariantCulture)).EnumerateArray()
            .Select(r=>$"{r[0].GetInt32()},{r[1].GetString()},{r[2].GetInt32()}").ToArray();
        CollectionAssert.AreEqual(expected,actual.ToArray());
        Assert.AreEqual(registry.Count,registry.Select(s=>s.Slot).Distinct().Count());
    }
    [TestMethod]
    public void RawOwnershipContinuesAfterCountedOwnerEndsAndCancelHasNoCallbacks()
    {
        var registry=new SourceSpriteRegistry<Sprite>(s=>s.Slot);int callbacks=0,frames=0;
        registry.Add(new(0){Counted=true});registry.Add(new(1){Callback=_=>callbacks++});
        Assert.AreEqual(1,registry.Count(s=>s.Counted));Assert.AreEqual(2,registry.Count);
        registry.RemoveAt(0);Assert.AreEqual(0,registry.Count(s=>s.Counted));
        registry.Advance(s=>s.Callback(s),_=>frames++);
        Assert.AreEqual(1,callbacks);Assert.AreEqual(1,frames);
        var first=registry.ToArray();var second=registry.ToArray();CollectionAssert.AreEqual(first,second);
        Assert.AreEqual(1,callbacks);Assert.AreEqual(1,frames);
        registry.Clear();registry.Clear();registry.Advance(s=>s.Callback(s),_=>frames++);
        Assert.AreEqual(0,registry.Count);Assert.AreEqual(1,callbacks);Assert.AreEqual(1,frames);
    }
    [TestMethod]
    public void InvisibleSpritesAndExplicitReservationsStayOccupied()
    {
        var registry=new SourceSpriteRegistry<Sprite>(s=>s.Slot);int callbacks=0;
        registry.Add(new(2){Visible=false,Callback=_=>callbacks++});
        Assert.AreEqual(3,registry.FindFreeSlot(slot=>slot<2));
        Assert.AreEqual(3,registry.Create(slot=>new(slot),slot=>slot<2));
        registry.Advance(s=>s.Callback(s),_=>{});Assert.AreEqual(1,callbacks);
        Assert.IsFalse(registry[2]!.Visible);
    }
    [TestMethod]
    public void FullRegistryDoesNotRunFactoryOrImmediateCallbackAndSentinelNeverBecomesLive()
    {
        var registry=new SourceSpriteRegistry<Sprite>(s=>s.Slot);for(int i=0;i<64;i++)registry.Add(new(i));
        int calls=0;Assert.AreEqual(64,registry.CreateAndAnimate(slot=>{calls++;return new(slot);},_=>calls++,_=>calls++));
        Assert.AreEqual(0,calls);Assert.AreEqual(64,registry.Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>registry.Add(new(64)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>registry.AdvanceSlot(64,_=>calls++,_=>calls++));
    }
    [TestMethod]
    public void ImmediateDestructionSkipsFrameAdvanceButReturnsItsAllocatedSourceSlot()
    {
        var registry=new SourceSpriteRegistry<Sprite>(s=>s.Slot);int callbacks=0,frames=0;
        Assert.AreEqual(0,registry.CreateAndAnimate(slot=>new(slot),sprite=>{callbacks++;registry.RemoveAt(sprite.Slot);},_=>frames++));
        Assert.AreEqual(1,callbacks);Assert.AreEqual(0,frames);Assert.AreEqual(0,registry.Count);
    }
    [TestMethod]
    public void FrameCompletionIsObservedByNextCallbackBeforeAnyFurtherPlayerStep()
    {
        var registry=new SourceSpriteRegistry<Sprite>(s=>s.Slot);var trace=new List<string>();
        var player=new SourceSpriteFrameProgram(new("frame","frame","test",false,[[new(0,1,false,false)]]));
        registry.Add(new(0));
        void Callback(Sprite sprite){trace.Add(player.Ended?"callback-ended":"callback-active");if(player.Ended)registry.Remove(sprite);}
        void Frame(Sprite sprite){trace.Add("frame");player.Step();}
        registry.Advance(Callback,Frame);registry.Advance(Callback,Frame);registry.Advance(Callback,Frame);
        CollectionAssert.AreEqual(new[]{"callback-active","frame","callback-ended"},trace.ToArray());
        Assert.AreEqual(0,registry.Count);
    }
    [TestMethod]
    public void StaleObjectRemovalCannotDeleteAReplacementOrDuplicateAnOccupiedSlot()
    {
        var registry=new SourceSpriteRegistry<Sprite>(s=>s.Slot);var original=new Sprite(0);registry.Add(original);
        registry.RemoveAt(0);var replacement=new Sprite(0);registry.Add(replacement);
        Assert.IsFalse(registry.Remove(original));Assert.AreSame(replacement,registry[0]);
        Assert.ThrowsExactly<InvalidOperationException>(()=>registry.Add(new(0)));
        Assert.ThrowsExactly<InvalidOperationException>(()=>registry.Create(_=>new(2)));
        Assert.AreEqual(1,registry.Count);Assert.AreSame(replacement,registry[0]);
    }
}
