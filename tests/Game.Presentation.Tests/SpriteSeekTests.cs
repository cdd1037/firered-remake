using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SpriteSeekTests
{
    private static SourceSpriteRecipe Air()=>new("test","test","test",false,[[new(0,3,false,false),new(0,3,true,false),new(0,3,false,true),new(0,3,true,true)]]){LoopStarts=[0]};
    private static void AssertSourceRows(string name,SourceSpriteFrameProgram player,bool paused=false)
    {
        using var fixture=SourceVectorFixture.Read("sprite-seek-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_sprite_seek_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/sprite-seek-source-vectors.json");
        foreach(var row in fixture.RootElement.GetProperty(name).EnumerateArray())
        {
            var v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();if(v[0]>0&&!paused)player.Step();Assert.AreEqual(v[2],player.Frame.Tile);Assert.AreEqual(v[3]!=0,player.Frame.HFlip);Assert.AreEqual(v[4]!=0,player.Frame.VFlip);Assert.AreEqual(v[5],player.DelayCounter);Assert.AreEqual(v[6]!=0,player.Ended);Assert.AreEqual(v[7],player.LoopCounter);
        }
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void DirectFrameSeekMatchesOriginalExtraDelayAndFrameFlips(int index)
    {var player=new SourceSpriteFrameProgram(Air());player.SeekFrame(index);AssertSourceRows("air"+index,player);}
    [TestMethod]
    public void PauseOwnershipRemainsWithTheCallingSprite()
    {var player=new SourceSpriteFrameProgram(Air());player.SeekFrame(2);AssertSourceRows("air-paused",player,true);}
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)]
    public void SeekPreservesSourceZeroOneAndMaximumDurationCounters(int index)
    {
        var recipe=Air() with{Animations=[[new(0,0,false,false),new(16,1,false,false),new(32,63,false,false)]],LoopStarts=[-1]};var player=new SourceSpriteFrameProgram(recipe);player.SeekFrame(index);AssertSourceRows("duration"+new[]{0,1,63}[index],player);
    }
    [TestMethod]
    public void SeekingDirectFramePreservesAnExistingFiniteLoopCounter()
    {
        var recipe=Air() with{Animations=[[new(0,2,false,false),new(16,1,true,false),new(32,0,false,true),new(48,2,false,false)]],FrameLoops=[[new(3,1,2)]],LoopStarts=[-1]};var player=new SourceSpriteFrameProgram(recipe);for(int tick=0;tick<4;tick++)player.Step();Assert.AreEqual(2,player.LoopCounter);player.SeekFrame(1);AssertSourceRows("marker-frame",player);
    }
    [TestMethod]
    public void SeekingFrameClearsEndedWithoutTreatingControlIndicesAsFrames()
    {
        var player=new SourceSpriteFrameProgram(Air() with{LoopStarts=[-1]});for(int tick=0;tick<16;tick++)player.Step();Assert.IsTrue(player.Ended);player.SeekFrame(0);Assert.IsFalse(player.Ended);Assert.AreEqual(3,player.DelayCounter);
        Assert.ThrowsExactly<InvalidDataException>(()=>player.SeekFrame(-1));Assert.ThrowsExactly<InvalidDataException>(()=>player.SeekFrame(4));
    }
}
