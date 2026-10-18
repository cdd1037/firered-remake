using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceAnimationBackgroundTests
{
    private static string Folder => Path.Combine(TestAssets.Root, "content/generated/battle/animations/backgrounds");
    private static JsonDocument Vectors() => SourceVectorFixture.Read("source-background-vectors.json", "python tests/Reference/generate_background_vectors.py content/raw/pokefirered/037335f4 tests/Game.Presentation.Tests/Fixtures/source-background-vectors.json");
    private static string Hash(ushort[] pixels)
    {
        var bytes = new byte[pixels.Length * 2];
        for (int i = 0; i < pixels.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), pixels[i]);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
    [TestMethod]
    public void EverySourceTerrainMatchesIndependentRawTilePaletteVectors()
    {
        using var catalog = new SourceAnimationBackgroundCatalog(Folder);
        using var vectors = Vectors(); var pixels = new ushort[240 * 112];
        var rows = vectors.RootElement.GetProperty("terrains").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(catalog.TerrainIds.ToArray(), rows.Select(x => x.GetProperty("id").GetInt32()).ToArray());
        foreach (var row in rows)
        {
            int id = row.GetProperty("id").GetInt32();
            catalog.CreateSurface(id).Render(pixels, 240, 112);
            Assert.AreEqual(row.GetProperty("sha256").GetString(), Hash(pixels), $"Source terrain {id}");
        }
    }
    [TestMethod]
    public void PartialMovesRetainTilemapAndTilesAndRestoreBothScreenblocks()
    {
        using var catalog = new SourceAnimationBackgroundCatalog(Folder);
        using var vectors = Vectors(); var surface = catalog.CreateSurface(0);
        var pixels = new ushort[512 * 256]; var viewport = new ushort[240 * 112];
        foreach (var row in vectors.RootElement.GetProperty("states").EnumerateArray())
        {
            string name = row.GetProperty("name").GetString()!;
            switch (name)
            {
                case "terrain": break;
                case "dark": surface.Apply(catalog.Move(1)); break;
                case "psychic-after-dark": case "psychic-after-restore": surface.Apply(catalog.Move(3)); break;
                case "restore": surface.Restore(); break;
                default: Assert.Fail("Unexpected reference state " + name); break;
            }
            surface.Render(pixels, 512, 256);
            Assert.AreEqual(row.GetProperty("sha256").GetString(), Hash(pixels), name);
            foreach (var point in row.GetProperty("samples").EnumerateArray())
                Assert.AreEqual(new SourceBackgroundPixel(point.GetProperty("bank").GetInt32(), point.GetProperty("index").GetInt32()),
                    surface.Sample(point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32()), name);
            foreach (int screen in new[] {0, 1})
            {
                surface.Render(viewport, 240, 112, 249, -13, screen);
                Assert.AreEqual(row.GetProperty(screen == 0 ? "scroll256" : "scroll512").GetString(), Hash(viewport), name);
            }
        }
        surface.Reset(); surface.Render(viewport, 240, 112);
        Assert.AreEqual(vectors.RootElement.GetProperty("terrains")[0].GetProperty("sha256").GetString(), Hash(viewport));
    }
    [TestMethod]
    public void MovePaletteOwnsOnlyBankTwoAndTransparentIndicesUseBackdrop()
    {
        using var catalog = new SourceAnimationBackgroundCatalog(Folder); var surface = catalog.CreateSurface(0);
        var colors = Enumerable.Range(0, 80).Select(i => surface.PaletteColor(i / 16, i % 16)).ToArray();
        surface.Apply(catalog.Move(1));
        for (int i = 0; i < 80; i++) if (i < 32 || i >= 48) Assert.AreEqual(colors[i], surface.PaletteColor(i / 16, i % 16));
        Assert.AreNotEqual(surface.PaletteColor(0, 0), surface.PaletteColor(2, 0), "Dark palette zero is green, not the backdrop.");
        var pixel = new ushort[1]; bool transparent = false;
        for (int y = 0; y < 256 && !transparent; y++) for (int x = 0; x < 256; x++)
            if (surface.Sample(x, y) == new SourceBackgroundPixel(0, 0))
            {
                surface.Render(pixel, 1, 1, x, y);
                Assert.AreEqual((ushort)(0x8000 | colors[0]), pixel[0]); transparent = true; break;
            }
        Assert.IsTrue(transparent);
        surface.Apply(catalog.Move(3));
        Assert.AreEqual(new SourceBackgroundPixel(0, 1), surface.Sample(240, 0), "Psychic's bank-0 map columns reference nonblank tile zero.");
        surface.Render(pixel, 1, 1, 240, 0);
        Assert.AreEqual((ushort)(0x8000 | colors[1]), pixel[0]);
        surface.Restore();
        for (int i = 0; i < 80; i++) Assert.AreEqual(colors[i], surface.PaletteColor(i / 16, i % 16));
    }
    [TestMethod]
    public void PaletteBlendsUseActualBanksAndFadedOverrideHasLastWriteOwnership()
    {
        var tiles = new byte[64]; Array.Fill(tiles, (byte)0x11, 32, 32);
        var map = new byte[4096];
        for (int bank = 2; bank <= 4; bank++) BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan((bank - 2) * 2), (ushort)((bank << 12) | 1));
        var palette = Enumerable.Repeat((ushort)0x421, 48).ToArray();
        var surface = new SourceAnimationBackgroundSurface(new(tiles, map, palette), new ushort[32]);
        var pixels = new ushort[24];
        SourcePaletteBlend[] blends = [new(2, 16, 31), new(3, 16, 31 << 10), new(4, 16, 31 << 5)];
        surface.Render(pixels, 24, 1, blends: blends);
        Assert.AreEqual((ushort)0x801f, pixels[0]); Assert.AreEqual((ushort)0xfc00, pixels[8]); Assert.AreEqual((ushort)0x83e0, pixels[16]);
        var faded = Enumerable.Repeat((ushort)0x421, 16).ToArray();
        surface.Render(pixels, 24, 1, blends: blends, inverted: new[] {2}, fadedBank2: faded);
        Assert.AreEqual((ushort)0x8421, pixels[0]); Assert.AreEqual((ushort)0xfc00, pixels[8]);
        surface.Render(pixels, 24, 1, darkening: 8, blends: blends, fadedBank2: faded);
        Assert.AreEqual((ushort)0x8421, pixels[0], "Hardware retains channel 1 at EVY 8; software palette blackening would truncate it.");
        Assert.AreEqual((ushort)0xc000, pixels[8]);
    }
    [TestMethod]
    public void DirectFadedEntriesKeepIndexZeroAndUnselectedBanksAndPrecedeHardwareDarkening()
    {
        var tiles=new byte[64];Array.Fill(tiles,(byte)0x11,32,32);
        var map=new byte[4096];
        for(int bank=2;bank<=4;bank++)BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan((bank-2)*2),(ushort)((bank<<12)|1));
        var surface=new SourceAnimationBackgroundSurface(new(tiles,map,Enumerable.Repeat((ushort)0x421,48).ToArray()),
            Enumerable.Repeat((ushort)0x123,32).ToArray());
        var entries=Enumerable.Range(1,15).Select(index=>new SourcePaletteEntry(32+index,0x7fff)).ToArray();
        var pixels=new ushort[32];
        surface.Render(pixels,32,1,blends:[new(2,16,31)],inverted:[2],fadedEntries:entries);
        Assert.AreEqual((ushort)0xffff,pixels[0]);
        Assert.AreEqual((ushort)0x8421,pixels[8]);Assert.AreEqual((ushort)0x8421,pixels[16]);
        Assert.AreEqual((ushort)0x8123,pixels[24],"Index zero retains the backdrop palette.");
        surface.Render(pixels,32,1,darkening:8,fadedEntries:entries);
        Assert.AreEqual((ushort)(0x8000|SourceAnimationBackgroundSurface.Darken(0x7fff,8)),pixels[0]);
        Assert.AreEqual((ushort)0x421,surface.PaletteColor(2,1),"Direct faded writes never mutate unfaded source colors.");
    }
    [TestMethod]
    public void OverlayDirectWritesUseIndexedBankMembershipAndKeepTransparentPixels()
    {
        var tiles=new byte[32];Array.Fill(tiles,(byte)0x11);tiles[0]=0x10;
        var map=new byte[2048];BinaryPrimitives.WriteUInt16LittleEndian(map,(ushort)(8<<12));
        var overlay=new SourceOverlayAsset(tiles,map,Enumerable.Repeat((ushort)0x421,16).ToArray());
        var pixels=new ushort[8];
        overlay.Render(pixels,8,1,0,0,null,[new(8,16,31)],[],[new(129,0x3e0)]);
        Assert.AreEqual((ushort)0,pixels[0]);
        for(int i=1;i<8;i++)Assert.AreEqual((ushort)0x83e0,pixels[i]);
    }
    [TestMethod]
    public void HardwareDarkeningRoundsBeforeSubtractingAndPreservesOpacity()
    {
        Assert.AreEqual((ushort)0x421, SourceAnimationBackgroundSurface.Darken(0x421, 8));
        Assert.AreEqual((ushort)0xc210, SourceAnimationBackgroundSurface.Darken(0xffff, 8));
        Assert.AreEqual((ushort)0x8000, SourceAnimationBackgroundSurface.Darken(0xffff, 16));
        Assert.AreEqual((ushort)0x7fff, SourceAnimationBackgroundSurface.Darken(0x7fff, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SourceAnimationBackgroundSurface.Darken(1, 17));
    }
    [TestMethod]
    public void CatalogIsLazyDeduplicatesAliasesAndSurfaceOutlivesClosedAtlas()
    {
        var catalog = new SourceAnimationBackgroundCatalog(Folder); Assert.AreEqual(0, catalog.CachedAssetCount);
        var surface = catalog.CreateSurface(0); Assert.AreEqual(1, catalog.CachedAssetCount);
        Assert.AreSame(catalog.Move(0), catalog.Move(1)); Assert.AreEqual(2, catalog.CachedAssetCount);
        catalog.Dispose(); Assert.Throws<ObjectDisposedException>(() => catalog.Move(1));
        var pixels = new ushort[240 * 112]; surface.Render(pixels, 240, 112); surface.Reset(); surface.Render(pixels, 240, 112);
        Assert.IsTrue(pixels.All(p => (p & 0x8000) != 0));
    }
    [TestMethod]
    public void RepeatedSurfaceRenderingAllocatesNoManagedFrameBuffers()
    {
        using var catalog = new SourceAnimationBackgroundCatalog(Folder); var surface = catalog.CreateSurface(0);
        surface.Apply(catalog.Move(3)); var pixels = new ushort[240 * 112];
        surface.Render(pixels, 240, 112);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 16; i++) surface.Render(pixels, 240, 112, i, -i, i & 1, i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(0L, allocated);
    }
    [TestMethod]
    public void EqualVisibleGymPixelsDoNotEraseDistinctHiddenPaletteOwnership()
    {
        using var catalog = new SourceAnimationBackgroundCatalog(Folder);
        var gym = catalog.CreateSurface(11); var leader = catalog.CreateSurface(12);
        var first = new ushort[240 * 112]; var second = new ushort[first.Length];
        gym.Render(first, 240, 112); leader.Render(second, 240, 112);
        CollectionAssert.AreEqual(first, second);
        Assert.AreNotEqual(gym.PaletteColor(4, 1), leader.PaletteColor(4, 1));
        gym.Apply(catalog.Move(1)); leader.Apply(catalog.Move(1));
        Assert.AreNotEqual(gym.PaletteColor(4, 1), leader.PaletteColor(4, 1));
    }
    [TestMethod]
    public void InvalidPaletteOwnershipTileReferencesAndRenderShapesFailClosed()
    {
        var tiles = new byte[32]; var map = new byte[4096]; var palette = new ushort[48];
        BinaryPrimitives.WriteUInt16LittleEndian(map, 0x1000);
        Assert.Throws<InvalidDataException>(() => new SourceBackgroundAsset(tiles, map, palette));
        BinaryPrimitives.WriteUInt16LittleEndian(map, 1);
        Assert.Throws<InvalidDataException>(() => new SourceBackgroundAsset(tiles, map, palette));
        BinaryPrimitives.WriteUInt16LittleEndian(map, 0);
        palette[0] = 0x8000;
        Assert.Throws<InvalidDataException>(() => new SourceBackgroundAsset(tiles, map, palette));
        palette[0] = 0;
        var surface = new SourceAnimationBackgroundSurface(new(tiles, map, palette), new ushort[32]);
        Assert.Throws<ArgumentException>(() => surface.Render(new ushort[1], 240, 112));
        Assert.Throws<ArgumentException>(() => surface.Render(new ushort[1], 1, 1, fadedBank2: new ushort[15]));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Sample(0, 0, 2));
    }
    [TestMethod]
    public void CatalogRejectsInvalidAtlasRangeAndEmptyTerrainSet()
    {
        string folder = Path.Combine(Path.GetTempPath(), "background-invalid-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            File.Copy(Path.Combine(Folder, "atlas.bin"), Path.Combine(folder, "atlas.bin"));
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, "manifest.json")))!;
            json["basePalette"]![0] = int.MaxValue; File.WriteAllText(Path.Combine(folder, "manifest.json"), json.ToJsonString());
            Assert.Throws<InvalidDataException>(() => new SourceAnimationBackgroundCatalog(folder));
            json = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, "manifest.json")))!; json["terrains"] = new JsonArray();
            File.WriteAllText(Path.Combine(folder, "manifest.json"), json.ToJsonString());
            Assert.Throws<InvalidDataException>(() => new SourceAnimationBackgroundCatalog(folder));
            // Constructor failures must release the stream so a replacement can open it exclusively.
            using var exclusive = new FileStream(Path.Combine(folder, "atlas.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(folder, true); }
    }
}
