using Game.Presentation;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class QuestLogPalettePresentationTests
{
    [TestMethod]
    public void EveryRgb555ColorMatchesOriginalCAtEveryRestorationTone()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile("tests/Reference/quest-palette-source-vectors.json", "python tests/Reference/generate_quest_palette_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Reference/quest-palette-source-vectors.json")));
        foreach(var row in document.RootElement.GetProperty("tones").EnumerateArray())
        {
            int tone=row.GetProperty("tone").GetInt32();var bytes=new byte[32768*2];
            for(int color=0;color<32768;color++)BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(color*2,2),QuestLogPalettePresentation.Transform((ushort)color,tone));
            Assert.AreEqual(row.GetProperty("sha256").GetString(),Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),$"tone {tone}");
        }
    }
    [TestMethod]
    public void RgbaProjectionPreservesAlphaAndRestoresOriginalWithoutQuantization()
    {
        byte[] original=[255,0,0,255,0,255,0,127,9,17,33,0,3,127,249,255];
        var unchanged=original.ToArray();QuestLogPalettePresentation.TransformRgba(unchanged,0);
        CollectionAssert.AreEqual(original,unchanged);
        var gray=original.ToArray();QuestLogPalettePresentation.TransformRgba(gray,16);
        CollectionAssert.AreEqual(new byte[]{74,74,74,255,148,148,148,127,9,17,33,0},gray[..12]);
        for(int i=0;i<original.Length;i+=4)Assert.AreEqual(original[i+3],gray[i+3]);
        // Restoration samples the original field pixels each frame, never a prior tinted result.
        for(int tone=16;tone>=0;tone--)
        {
            var frame=original.ToArray();QuestLogPalettePresentation.TransformRgba(frame,tone);
            if(tone==0)CollectionAssert.AreEqual(original,frame);
        }
    }
    [TestMethod]
    public void OpacityBitAndInvalidInputsHaveExplicitContracts()
    {
        for(int tone=0;tone<=16;tone++)Assert.AreEqual((ushort)(QuestLogPalettePresentation.Transform(0x1234,tone)|0x8000),QuestLogPalettePresentation.Transform(0x9234,tone));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>QuestLogPalettePresentation.Transform(0,-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>QuestLogPalettePresentation.Transform(0,17));
        Assert.ThrowsExactly<ArgumentException>(()=>QuestLogPalettePresentation.TransformRgba(new byte[3],1));
    }
}
