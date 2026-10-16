using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class SlotMachineVisualPresentationTests
{
    [TestMethod]
    public void WinningTaskUsesPinnedSineAndNineFrameLightCadence()
    {
        var layout = PretSlotMachinePresentation.Load(TestAssets.Root).Layout;
        // Independent Task_FlashWinningLine + gSineTable vector. State 0 performs no palette pulse.
        Assert.AreEqual(new SlotMachineVisualPresentation.FlashState(null, 0), SlotMachineVisualPresentation.Flash(layout, 0));
        int[] source = [1, 3, 4, 5, 6, 7, 7, 8, 7, 7, 6, 5, 4, 3, 1, 0];
        for (int i = 0; i < source.Length * 3; i++)
        {
            var frame = SlotMachineVisualPresentation.Flash(layout, i + 1);
            Assert.AreEqual(source[i % source.Length], frame.LineDarken);
            Assert.AreEqual(new[] { 0, 1, 2, 1 }[i / 9 % 4], frame.LightFrame);
        }
        var art = PretSlotMachinePresentation.Load(TestAssets.Root);
        foreach (var line in layout.GetProperty("matchLines").EnumerateArray())
        foreach (int shade in source.Append(0).Distinct())
            Assert.IsTrue(art.Textures.ContainsKey($"win-{line.GetProperty("id").GetInt32()}-{shade}"));
    }

    [TestMethod]
    public void SoftwarePaletteBlendKeepsSignedShiftRounding()
    {
        // blend_palette.c: r + (((0-r)*coeff)>>4), deliberately different from hardware BLDY.
        for(int value=0;value<32;value++)
        for(int coefficient=0;coefficient<=16;coefficient++)
        {
            int expected=value+((-value*coefficient)>>4);
            byte input=(byte)((value<<3)|(value>>2));
            Assert.AreEqual((byte)((expected<<3)|(expected>>2)),SlotMachineVisualPresentation.FadeChannel(input,coefficient));
        }
        Assert.AreEqual((byte)239,SlotMachineVisualPresentation.FadeChannel(255,1));
    }

    [TestMethod]
    public void PaletteYRepresentsNextTransferUntilBothHalvesHaveBeenUpdated()
    {
        // palette.c first transfers BG at Y, then OBJ at Y, then advances Y by 3.
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(16, 16), SlotMachineVisualPresentation.Fade(16, 0, true, -1));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(16, 16), SlotMachineVisualPresentation.Fade(13, 0, false, -1));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(13, 16), SlotMachineVisualPresentation.Fade(13, 0, true, -1));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(1, 1), SlotMachineVisualPresentation.Fade(0, 0, false, -1));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(0, 1), SlotMachineVisualPresentation.Fade(0, 0, true, -1));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(0, 0), SlotMachineVisualPresentation.Fade(0, 0, false, 0));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(3, 0), SlotMachineVisualPresentation.Fade(3, 16, true, -1));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(16, 15), SlotMachineVisualPresentation.Fade(16, 16, true, -1));
        Assert.AreEqual(new SlotMachineVisualPresentation.FadeState(16, 16), SlotMachineVisualPresentation.Fade(16, 16, false, 0));
    }

    [TestMethod]
    [DataRow("spinning", 24, 0, 16)]
    [DataRow("payout", 28, 32, 48)]
    [DataRow("lose", 12, 64, 80)]
    public void ClefairyUsesSourceDurationMinusOneCounterAndExactLoopBoundary(string name, int duration, int first, int second)
    {
        var animation = PretSlotMachinePresentation.Load(TestAssets.Root).Layout.GetProperty("clefairy").GetProperty("animations").GetProperty(name);
        Assert.AreEqual(first, SlotMachineVisualPresentation.AnimationTile(animation, 0));
        Assert.AreEqual(first, SlotMachineVisualPresentation.AnimationTile(animation, duration - 1));
        Assert.AreEqual(second, SlotMachineVisualPresentation.AnimationTile(animation, duration));
        Assert.AreEqual(second, SlotMachineVisualPresentation.AnimationTile(animation, duration * 2 - 1));
        Assert.AreEqual(first, SlotMachineVisualPresentation.AnimationTile(animation, duration * 2));
    }
}
