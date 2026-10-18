using Game.Presentation;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class FieldTransitionPaletteTests
{
    [TestMethod]
    public void RecordedTonePrecedesFieldFadeWithIndependentIntegerRounding()
    {
        ushort[] colors = [0,1,31,32,0x3e0,0x400,0x7c00,0x7fff,0x1234,0x4210,0x2bb9];
        foreach (int tone in Enumerable.Range(0,17))
        foreach (int fade in Enumerable.Range(0,17))
        foreach (ushort color in colors)
        {
            int[] channels=[color&31,(color>>5)&31,(color>>10)&31];
            byte[] pixels=[Expand(channels[0]),Expand(channels[1]),Expand(channels[2]),127,9,17,33,0];
            QuestLogPalettePresentation.TransformRgba(pixels,tone,fade);
            int gray=(76*channels[0]+151*channels[1]+29*channels[2])/256;
            for(int c=0;c<3;c++)
            {
                int tinted=channels[c]+(int)Math.Floor(tone*(gray-channels[c])/16d);
                int faded=tinted+(int)Math.Floor(-fade*tinted/16d);
                Assert.AreEqual(Expand(faded),pixels[c],$"{color:x4}:tone{tone}:fade{fade}:channel{c}");
            }
            Assert.AreEqual((byte)127,pixels[3]);
            CollectionAssert.AreEqual(new byte[]{9,17,33,0},pixels[4..]);
        }
        static byte Expand(int value)=>(byte)((value<<3)|(value>>2));
    }

    [TestMethod]
    public void NoEffectsRestoresOriginalBytesAndInvalidFadeFails()
    {
        byte[] pixels=[3,127,249,255];var original=pixels.ToArray();
        QuestLogPalettePresentation.TransformRgba(pixels,0,0);
        CollectionAssert.AreEqual(original,pixels);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>QuestLogPalettePresentation.TransformRgba(pixels,0,-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>QuestLogPalettePresentation.TransformRgba(pixels,0,17));
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(4)]
    public void SharedMusicFadeUsesRequestedSourceTickSpeedAndStopsOnce(int speed)
    {
        var fade=new SourceMusicFade(speed);int changes=0;
        for(int tick=1;tick<=16*speed;tick++)
        {
            bool changed=fade.Advance();
            Assert.AreEqual(tick%speed==0,changed);
            if(changed)changes++;
            Assert.AreEqual(64-4*(tick/speed),fade.Volume);
        }
        Assert.AreEqual(16,changes);Assert.AreEqual(0,fade.Volume);
        for(int tick=0;tick<10;tick++)Assert.IsFalse(fade.Advance());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new SourceMusicFade(0));
    }
}
