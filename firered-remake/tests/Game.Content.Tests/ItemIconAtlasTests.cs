using Game.Content;
using System.Text.Json;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class ItemIconAtlasTests
{
    private static PretPewterMachineIcon[] MachineIcons(string root)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/pewter-machines/pewter-machines.json")));
        var icons = document.RootElement.GetProperty("icons").Deserialize<PretPewterMachineIcon[]>(
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        foreach (var icon in icons) PretPewterMachines.ValidateIcon(icon);
        return icons;
    }

    [TestMethod]
    public void TypedInventoryIconsLoadFromGeneratedMetadataAndOneNativePage()
    {
        string root = TestAssets.Output();
        try
        {
            foreach (string package in new[] { "early-shop", "pewter-machines", "silph-item-art", "cinnabar-item-art", "item-icons" })
            {
                string relative = "content/generated/" + package;
                Directory.CreateDirectory(Path.Combine(root, relative));
                foreach (string file in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, relative)))
                    File.Copy(file, Path.Combine(root, relative, Path.GetFileName(file)));
            }
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            var early = PretEarlyShop.Load(File.ReadAllText(Path.Combine(root, "content/generated/early-shop/early-shop.json")));
            var machines = MachineIcons(root);
            var silph = PretSilphItemArt.Load(root);
            var cinnabar = PretCinnabarItemArt.LoadProject(root);
            var paths = early.Icons.Select(i => (i.PngPath, i.Region))
                .Concat(machines.Concat(silph.Icons).Concat(cinnabar.Icons).Select(i => (i.PngPath, i.Region))).ToArray();
            Assert.AreEqual(37, paths.Length);
            Assert.AreEqual(1, paths.Select(i => i.PngPath).Distinct().Count());
            Assert.AreEqual("content/generated/item-icons/items.png", paths[0].PngPath);
            Assert.IsTrue(File.Exists(Path.Combine(root, paths[0].PngPath)));
            foreach (var (_, region) in paths)
            {
                Assert.IsNotNull(region);
                Assert.AreEqual((24, 24), (region.Width, region.Height));
                Assert.IsTrue(region.X >= 1 && region.Y >= 1);
            }
            var ice = cinnabar.Icons.Single(i => i.Item.Value == "pret:ITEM_TM14");
            var fire = cinnabar.Icons.Single(i => i.Item.Value == "pret:ITEM_TM38");
            Assert.AreEqual(ice.Source.Path, fire.Source.Path);
            Assert.AreNotEqual(ice.PaletteSource.Path, fire.PaletteSource.Path);
            Assert.AreNotEqual(ice.Region, fire.Region);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void InvalidLogicalRegionsAreRejectedBeforeTextureLoading()
    {
        var document = PretEarlyShop.Load(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/early-shop/early-shop.json")));
        Assert.ThrowsExactly<InvalidDataException>(() => PretEarlyShop.ValidateIcon(document.Icons[0] with { Region = new(1, 1, 23, 24) }));
        var machines = MachineIcons(TestAssets.Root);
        Assert.ThrowsExactly<InvalidDataException>(() => PretPewterMachines.ValidateIcon(machines[0] with { Region = new(-1, 1, 24, 24) }));
    }
}
