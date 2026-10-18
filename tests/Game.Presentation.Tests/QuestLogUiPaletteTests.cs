using Game.Presentation;
using Game.Presentation.BattleAnimations;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class QuestLogUiPaletteTests
{
    [TestMethod]
    public void ReservedUiPaletteAndTexturedPixelsHaveIdenticalBlackFadeAtEveryStep()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(
            "content/generated/data/quest-log.json", "python scripts/imports/presentation/import-quest-log.py")));
        var colors = document.RootElement.GetProperty("windowPalette").EnumerateArray()
            .Select(color => color.EnumerateArray().Select(channel => channel.GetByte()).ToArray()).ToArray();
        Assert.AreEqual(16, colors.Length);
        foreach (var rgb in colors)
        {
            Assert.AreEqual(3, rgb.Length);
            ushort original = (ushort)((rgb[0] >> 3) | ((rgb[1] >> 3) << 5) | ((rgb[2] >> 3) << 10));
            for (int fade = 0; fade <= 16; fade++)
            {
                byte[] pixels = [rgb[0], rgb[1], rgb[2], 255, 9, 17, 33, 0];
                QuestLogPalettePresentation.TransformRgba(pixels, 0, fade);
                ushort faded = SourcePaletteMath.Blend(original, fade, 0);
                for (int channel = 0; channel < 3; channel++)
                {
                    int five = (faded >> (channel * 5)) & 31;
                    byte expected = fade == 0 ? rgb[channel] : (byte)((five << 3) | (five >> 2));
                    Assert.AreEqual(expected, pixels[channel], $"palette {original:x4}, fade {fade}, channel {channel}");
                }
                Assert.AreEqual((byte)255, pixels[3]);
                CollectionAssert.AreEqual(new byte[] { 9, 17, 33, 0 }, pixels[4..]);
            }
        }
    }

    [TestMethod]
    public void BlueGrayRecapBarRetainsHueAndUsesFiveBitBlackRounding()
    {
        byte[] bar = [66, 90, 107, 255];
        QuestLogPalettePresentation.TransformRgba(bar, 0, 8);
        CollectionAssert.AreEqual(new byte[] { 33, 41, 49, 255 }, bar);
        byte[] black = [66, 90, 107, 255];
        QuestLogPalettePresentation.TransformRgba(black, 0, 16);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 255 }, black);
    }
}
