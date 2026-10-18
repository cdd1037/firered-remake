using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ScanlineStateSourceTests
{
    private static int[] Numbers(JsonElement value)=>value.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("scanline-state-source-vectors.json",
        "python tests/Game.Presentation.Tests/Reference/generate_scanline_state_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");

    [TestMethod]
    public void EveryNativeOperationMatchesRetainedBanksLifecycleAndConditionalFirstRowWrites()
    {
        using var document=Vectors();var root=document.RootElement;int cases=0,operations=0,captures=0;
        foreach(var entry in root.GetProperty("cases").EnumerateArray())
        {
            var state=new SourceScanlineState();int ordinal=0;
            foreach(var operation in entry.GetProperty("ops").EnumerateArray())
            {
                string op=operation.GetProperty("op").GetString()!;int[] args=Numbers(operation.GetProperty("args"));
                var expected=operation.GetProperty("expected");int[] overrides=Numbers(expected.GetProperty("overrides"));
                string where=entry.GetProperty("name").GetString()+" operation "+ordinal++ +" "+op;
                SourceScanlineFrame? frame=null;
                switch(op)
                {
                    case "reset":state=new();break;
                    case "clear":state.Clear();break;
                    case "fill":
                        for(int bank=0;bank<2;bank++)for(int index=0;index<SourceScanlineState.BankWords;index++)
                            state.Write(bank,index,unchecked((ushort)(args[0]+bank*17011+index*977)));
                        break;
                    case "write":state.Write(args[0],args[1],(ushort)args[2]);break;
                    case "set":state.SetParams((SourceScanlineDestination)args[0],args[1]!=0);break;
                    case "stop":state.Stop();break;
                    case "request":state.RequestStop();break;
                    case "gpu":case "flush":break; // The native queue owns these outputs; model receives only its selected dirty halfwords.
                    case "capture":frame=state.CaptureVBlank(overrides[0]<0?null:(ushort)overrides[0],overrides[1]<0?null:(ushort)overrides[1]);break;
                    default:Assert.Fail("Unknown source operation: "+op);break;
                }
                CollectionAssert.AreEqual(Numbers(expected.GetProperty("state")),new[]{state.SourceBuffer,state.State,(int)state.Destination,state.Word32?1:0},where);
                int[] banks=Enumerable.Range(0,2).SelectMany(bank=>Enumerable.Range(0,SourceScanlineState.BankWords).Select(index=>(int)state.Read(bank,index))).ToArray();
                CollectionAssert.AreEqual(Numbers(expected.GetProperty("buffers")),banks,where+" all retained words");
                var samples=expected.GetProperty("frame");
                if(samples.ValueKind==JsonValueKind.Null)Assert.IsNull(frame,where);
                else
                {
                    Assert.IsNotNull(frame,where);frame.Validate();Assert.AreEqual(state.Destination,frame.Destination,where);Assert.AreEqual(state.Word32,frame.Word32,where);
                    CollectionAssert.AreEqual(Numbers(samples),frame.Words.Select(x=>(int)x).ToArray(),where+" every raster/terminal word");captures++;
                }
                operations++;
            }
            cases++;
        }
        var coverage=root.GetProperty("coverage");Assert.AreEqual(coverage.GetProperty("cases").GetInt32(),cases);Assert.AreEqual(coverage.GetProperty("operations").GetInt32(),operations);Assert.AreEqual(coverage.GetProperty("captures").GetInt32(),captures);
        Assert.AreEqual(1920,coverage.GetProperty("retainedWordsPerOperation").GetInt32());Assert.IsTrue(cases>0&&operations>cases&&captures>cases);
    }

    [TestMethod]
    public void CapturesAreDetachedAndOverridesNeitherMutateBanksNorLeakIntoLaterFrames()
    {
        var state=new SourceScanlineState();state.Write(0,0,7);state.Write(0,1,9);state.Write(0,320,31);state.Write(0,321,37);state.Write(1,0,11);state.Write(1,1,13);
        state.SetParams(SourceScanlineDestination.Bg2Horizontal,true);var first=state.CaptureVBlank(101,103)!;
        Assert.AreEqual(322,first.Words.Length);Assert.AreEqual((ushort)101,first.Words[0]);Assert.AreEqual((ushort)103,first.Words[1]);Assert.AreEqual((ushort)31,first.Words[320]);Assert.AreEqual((ushort)37,first.Words[321]);
        Assert.AreEqual((ushort)7,state.Read(0,0));Assert.AreEqual((ushort)9,state.Read(0,1));
        var detached=first.Detach();first.Words[0]=200;Assert.AreEqual((ushort)101,detached.Words[0]);Assert.AreEqual((ushort)7,state.Read(0,0));
        var second=state.CaptureVBlank()!;Assert.AreEqual((ushort)11,second.Words[0]);Assert.AreEqual((ushort)13,second.Words[1]);
        state.Clear();Assert.AreEqual((ushort)11,second.Words[0]);Assert.AreEqual((ushort)103,detached.Words[1]);
        state.Write(0,0,17);state.Write(0,1,19);state.SetParams(SourceScanlineDestination.BlendAlpha,false);var halfword=state.CaptureVBlank(null,65535)!;
        Assert.AreEqual(161,halfword.Words.Length);Assert.AreEqual((ushort)17,halfword.Words[0]);Assert.AreEqual((ushort)19,halfword.Words[1]);
    }

    [TestMethod]
    public void StopAndDeferredStopRetainBothBanksAndSourceCursorUntilExplicitClear()
    {
        var state=new SourceScanlineState();state.Write(0,959,65535);state.Write(1,959,123);state.SetParams(SourceScanlineDestination.Bg1Horizontal,false);
        Assert.IsNotNull(state.CaptureVBlank());Assert.AreEqual(1,state.SourceBuffer);
        state.RequestStop();Assert.AreEqual(3,state.State);Assert.IsNull(state.CaptureVBlank(1,2));Assert.AreEqual(0,state.State);Assert.AreEqual(1,state.SourceBuffer);
        Assert.AreEqual((ushort)65535,state.Read(0,959));Assert.AreEqual((ushort)123,state.Read(1,959));
        state.SetParams(SourceScanlineDestination.Bg2Horizontal,true);Assert.AreEqual(1,state.SourceBuffer);state.Stop();Assert.IsNull(state.CaptureVBlank());Assert.AreEqual(1,state.SourceBuffer);
        state.SetParams(SourceScanlineDestination.BlendAlpha,false);Assert.IsNotNull(state.CaptureVBlank());Assert.AreEqual(0,state.SourceBuffer);
        state.Clear();Assert.AreEqual(0,state.State);Assert.AreEqual(0,state.SourceBuffer);Assert.AreEqual(SourceScanlineDestination.Bg1Horizontal,state.Destination);Assert.IsFalse(state.Word32);
        for(int bank=0;bank<2;bank++)for(int i=0;i<SourceScanlineState.BankWords;i++)Assert.AreEqual((ushort)0,state.Read(bank,i));
    }

    [TestMethod]
    public void InvalidDestinationsWidthsSampleCountsAndMemoryBoundsFailClosed()
    {
        var state=new SourceScanlineState();
        foreach(var destination in new[]{(SourceScanlineDestination)(-1),(SourceScanlineDestination)5})
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.SetParams(destination,false));
            Assert.Throws<ArgumentException>(()=>new SourceScanlineFrame(destination,false,new ushort[161]).Validate());
        }
        Assert.Throws<ArgumentOutOfRangeException>(()=>state.SetParams(SourceScanlineDestination.BlendAlpha,true));
        Assert.Throws<ArgumentException>(()=>new SourceScanlineFrame(SourceScanlineDestination.BlendAlpha,true,new ushort[322]).Validate());
        foreach(int count in new[]{0,160,162,320,321,323})foreach(bool paired in new[]{false,true})
            Assert.Throws<ArgumentException>(()=>new SourceScanlineFrame(SourceScanlineDestination.Bg1Horizontal,paired,new ushort[count]).Validate());
        Assert.Throws<ArgumentException>(()=>new SourceScanlineFrame(SourceScanlineDestination.Bg1Horizontal,false,null!).Validate());
        foreach(int bank in new[]{-1,2})
        {Assert.Throws<ArgumentOutOfRangeException>(()=>state.Read(bank,0));Assert.Throws<ArgumentOutOfRangeException>(()=>state.Write(bank,0,0));}
        foreach(int index in new[]{-1,960})
        {Assert.Throws<ArgumentOutOfRangeException>(()=>state.Read(0,index));Assert.Throws<ArgumentOutOfRangeException>(()=>state.Write(0,index,0));}
        Assert.AreEqual(0,state.State);Assert.AreEqual(0,state.SourceBuffer);
    }
}
