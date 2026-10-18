using System.Reflection;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BackgroundPaletteWriteTests
{
    private delegate void WriteRange(int bank,int start,ReadOnlySpan<ushort> colors);
    private sealed class Audio:ISourceAnimationAudio
    {
        public void Play(SourceAnimationSound cue){}
        public bool IsPlaying=>false;
        public void StopAnimationEffects(){}
    }
    private static SourceBattleAnimation Runtime()=>new(new(156,"palette-registers",[new("delay",["60"],1),new("end",[],2)]),
        new("palette",1,156,new(0),new(0)),Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)),false,new Audio(),Enumerable.Repeat((ushort)0x421,16).ToArray());
    private static T Method<T>(SourceBattleAnimation runtime,string name)where T:Delegate=>
        typeof(SourceBattleAnimation).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.CreateDelegate<T>(runtime);
    [TestMethod]
    public void SourceFadedWritesSnapshotDetachAndWholeBankFadesReplaceOnlySelectedEntries()
    {
        var runtime=Runtime();var write=Method<WriteRange>(runtime,"WriteBackgroundPaletteRange");
        ushort[] colors=Enumerable.Repeat((ushort)0x7fff,15).ToArray();
        write(1,1,colors);write(2,1,colors);write(3,1,colors);
        var saved=runtime.Snapshot();Array.Fill(colors,(ushort)0);
        Assert.AreEqual(45,saved.BackgroundPaletteEntries.Length);
        Assert.IsTrue(saved.BackgroundPaletteEntries.All(p=>p.Index%16!=0&&p.Color==0x7fff));
        Method<Action<uint,int,ushort>>(runtime,"BlendPalettes")(1u<<2,8,0);
        var faded=runtime.Snapshot();Assert.AreEqual(30,faded.BackgroundPaletteEntries.Length);
        Assert.IsFalse(faded.BackgroundPaletteEntries.Any(p=>p.Index/16==2));
        Assert.IsTrue(saved.BackgroundPaletteEntries.All(p=>p.Color==0x7fff));
        Method<Action<int,int,int>>(runtime,"InvertSelectedPalettes")(256,0,0);
        Assert.IsTrue(runtime.Snapshot().BackgroundPaletteEntries.All(p=>p.Color==0));
        runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().BackgroundPaletteEntries.Length);
        Assert.AreEqual(45,saved.BackgroundPaletteEntries.Length);
    }
    [TestMethod]
    public void SourceBackgroundLoadReplacesItsOwnedBanksAndRangeValidationIsBounded()
    {
        var runtime=Runtime();var write=Method<WriteRange>(runtime,"WriteBackgroundPaletteRange");
        write(1,1,[1]);write(2,1,[2]);write(3,1,[3]);write(4,1,[4]);
        Method<Action<int?>>(runtime,"LoadBackgroundPalette")(null);
        CollectionAssert.AreEqual(new[]{new SourcePaletteEntry(17,1)},runtime.Snapshot().BackgroundPaletteEntries);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>write(16,0,[1]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>write(2,15,[1,2]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>write(2,-1,[1]));
    }
}
