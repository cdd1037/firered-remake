using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class EvolutionScenePixelTests
{
    [TestMethod]
    public void EveryRgb555ColorAndWhiteningStepMatchesIndependentIntegerArithmetic()
    {
        // Both opacity states, every 15-bit color, every coefficient including endpoints.
        ushort[] colors = Enumerable.Range(0, 65536).Select(value => (ushort)value).ToArray();
        var source = new SourcePixelAsset(256, 256, colors);
        int checkedColors = 0;
        for (int coefficient = 0; coefficient <= 16; coefficient++)
        {
            var actual = EvolutionScenePixels.Whiten(source, coefficient);
            Assert.AreNotSame(source.Pixels, actual.Pixels);
            for (int value = 0; value < colors.Length; value++)
            {
                int expected = value & 0x8000;
                for (int shift = 0; shift <= 10; shift += 5)
                {
                    int channel = (value >> shift) & 31;
                    expected |= (channel + (31 - channel) * coefficient / 16) << shift;
                }
                if (actual.Pixels[value] != expected)
                    Assert.Fail($"Color {value:X4}, coefficient {coefficient}: expected {expected:X4}, actual {actual.Pixels[value]:X4}.");
                if (colors[value] != value) Assert.Fail($"Whitening changed source color {value:X4}.");
                checkedColors++;
            }
        }
        Assert.AreEqual(32768 * 17 * 2, checkedColors);
    }

    [TestMethod]
    public void AlphaBlendingRoundsFiveBitChannelsAndSaturatesBeforeDisplayConversion()
    {
        for (int first = 0; first < 32; first++)
        for (int second = 0; second < 32; second++)
        for (int shift = 0; shift <= 10; shift += 5)
        {
            ushort a = (ushort)(0x8000 | first << shift), b = (ushort)(0x8000 | second << shift);
            Assert.AreEqual((ushort)(0x8000 | (first + second) / 2 << shift), SourcePaletteMath.AlphaBlend(a, b, 8, 8));
            Assert.AreEqual((ushort)(0x8000 | Math.Min(31, first + second) << shift), SourcePaletteMath.AlphaBlend(a, b, 16, 16));
            Assert.AreEqual(a, SourcePaletteMath.AlphaBlend(a, b, 31, -1));
            Assert.AreEqual(b, SourcePaletteMath.AlphaBlend(a, b, -1, 31));
        }
        Assert.AreEqual((ushort)0xffff, SourcePaletteMath.AlphaBlend(0xffff, 0xffff, 16, 16));
    }

    [TestMethod]
    public void WhiteningEndpointsPreserveSourceOpacityAndDetachedStorage()
    {
        ushort[] colors = [0, 0x0123, 0x8000, 0x9234, 0xffff];
        var source = new SourcePixelAsset(5, 1, colors);
        var unchanged = EvolutionScenePixels.Whiten(source, 0);
        CollectionAssert.AreEqual(colors, unchanged.Pixels);
        unchanged.Pixels[3] = 0; Assert.AreEqual((ushort)0x9234, source.Pixels[3]);
        var white = EvolutionScenePixels.Whiten(source, 16);
        CollectionAssert.AreEqual(new ushort[] { 0x7fff, 0x7fff, 0xffff, 0xffff, 0xffff }, white.Pixels);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EvolutionScenePixels.Whiten(source, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EvolutionScenePixels.Whiten(source, 17));
        Assert.ThrowsExactly<ArgumentException>(() => EvolutionScenePixels.Whiten(new(2, 2, [0]), 4));
    }

    [TestMethod]
    public void SourceColorEndpointsAreDistinctFromOriginalRgb8TextureRestoration()
    {
        // Original portraits can use RGB8 value 132 for source channel 16. Source
        // pixels encode that as 16, whose canonical display expansion is 131.
        // Thus the view must reuse its original texture at zero, while active
        // fades use the source endpoint. A zero-fade reupload would alter RGB8.
        const byte originalRgb8 = 132;
        int channel = originalRgb8 >> 3;
        var source = new SourcePixelAsset(1, 1, [(ushort)(0x8000 | channel | channel << 5 | channel << 10)]);
        var zero = EvolutionScenePixels.Whiten(source, 0);
        Assert.AreEqual(16, zero.Pixels[0] & 31);
        Assert.AreEqual(131, (zero.Pixels[0] & 31) * 255 / 31);
        Assert.AreNotEqual((int)originalRgb8, (zero.Pixels[0] & 31) * 255 / 31);
        Assert.AreEqual((ushort)0xffff, EvolutionScenePixels.Whiten(source, 16).Pixels[0]);
        for (int coefficient = 0; coefficient <= 16; coefficient++)
        {
            int expectedGray = 31 * coefficient / 16;
            Assert.AreEqual((ushort)(0x8000 | expectedGray | expectedGray << 5 | expectedGray << 10),
                EvolutionScenePixels.Whiten(new(1, 1, [0x8000]), coefficient).Pixels[0]);
        }
    }

    [TestMethod]
    public void MovingFieldSamplesBothScrolledCropsAndLeavesInputsUnchanged()
    {
        ushort[] inner = new ushort[256 * 256], outer = new ushort[256 * 256];
        for (int y = 0; y < 256; y++)
        for (int x = 0; x < 256; x++)
        {
            inner[y * 256 + x] = (ushort)(0x8000 | x % 32 | y % 32 << 5 | (x + y) % 32 << 10);
            outer[y * 256 + x] = (ushort)(0x8000 | y % 32 | (x + y) % 32 << 5 | x % 32 << 10);
        }
        var innerBefore = inner.ToArray(); var outerBefore = outer.ToArray();
        // Includes positive/negative 256 wrapping, and the real scene's scroll envelope.
        foreach (var offsets in new[] { (8, 16, 8, 16), (4, 12, 12, 20), (12, 20, 4, 12), (-4, -2, 255, 255), (260, 272, 264, 270) })
        {
            var actual = EvolutionScenePixels.MovingBackground(new(256, 256, inner), new(256, 256, outer),
                offsets.Item1, offsets.Item2, offsets.Item3, offsets.Item4);
            Assert.AreEqual(240, actual.Width); Assert.AreEqual(112, actual.Height);
            for (int y = 0; y < actual.Height; y++)
            for (int x = 0; x < actual.Width; x++)
            {
                static int Wrap(int value) => (value % 256 + 256) % 256;
                int a = inner[Wrap(y + offsets.Item2) * 256 + Wrap(x + offsets.Item1)];
                int b = outer[Wrap(y + offsets.Item4) * 256 + Wrap(x + offsets.Item3)];
                int expected = 0x8000;
                for (int shift = 0; shift <= 10; shift += 5)
                    expected |= (((a >> shift) & 31) + ((b >> shift) & 31)) / 2 << shift;
                Assert.AreEqual((ushort)expected, actual.Pixels[y * 240 + x]);
            }
        }
        CollectionAssert.AreEqual(innerBefore, inner); CollectionAssert.AreEqual(outerBefore, outer);
    }

    [TestMethod]
    public void BackgroundTransparencyUsesVisibleLayersAndAnOpaqueBlackBackdrop()
    {
        ushort[] inner = new ushort[256 * 256], outer = new ushort[256 * 256];
        inner[0] = 0x001f; outer[0] = 0x03e0; // Hidden RGB must not color the backdrop.
        inner[1] = 0x801f; outer[2] = 0x83e0; inner[3] = 0x801f; outer[3] = 0x83e0;
        var actual = EvolutionScenePixels.MovingBackground(new(256, 256, inner), new(256, 256, outer), 0, 0, 0, 0);
        CollectionAssert.AreEqual(new ushort[] { 0x8000, 0x801f, 0x83e0, 0x81ef }, actual.Pixels.Take(4).ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => EvolutionScenePixels.MovingBackground(new(255, 256, inner), new(256, 256, outer), 0, 0, 0, 0));
        Assert.ThrowsExactly<ArgumentException>(() => EvolutionScenePixels.MovingBackground(new(256, 256, inner), new(256, 256, [0]), 0, 0, 0, 0));
    }
}
