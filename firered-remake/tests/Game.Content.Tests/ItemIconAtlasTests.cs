using Game.Content;
using System.Text.Json;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class ItemIconAtlasTests
{
    private static PretPewterMachineIcon[] MachineIcons(string root)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "content/generated/data/machine-learning/pewter.json")));
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
            foreach (string relative in new[]
            {
                "content/generated/data/services/early-shop/early-shop.json",
                "content/generated/data/items.json",
                "content/generated/data/machine-learning/pewter.json",
                "content/generated/data/services/silph-item-art.json",
                "content/generated/data/services/cinnabar-item-art/icons.json",
                "content/generated/data/services/cinnabar-item-art/manifest.json",
                "content/generated/sprites/items.png"
            })
            {
                string destination = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(TestAssets.Root, relative), destination);
            }
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            var early = PretEarlyShop.Load(File.ReadAllText(Path.Combine(root, "content/generated/data/services/early-shop/early-shop.json")), SharedItemCatalog.LoadProject(root));
            var machines = MachineIcons(root);
            var silph = PretSilphItemArt.Load(root);
            var cinnabar = PretCinnabarItemArt.LoadProject(root);
            var paths = early.Icons.Select(i => (i.PngPath, i.Region))
                .Concat(machines.Concat(silph.Icons).Concat(cinnabar.Icons).Select(i => (i.PngPath, i.Region))).ToArray();
            Assert.AreEqual(37, paths.Length);
            Assert.AreEqual(1, paths.Select(i => i.PngPath).Distinct().Count());
            Assert.AreEqual("content/generated/sprites/items.png", paths[0].PngPath);
            Assert.IsTrue(File.Exists(Path.Combine(root, paths[0].PngPath)));
            foreach (var (_, region) in paths)
            {
                Assert.IsNotNull(region);
                Assert.AreEqual((24, 24), (region.Width, region.Height));
                Assert.IsTrue(region.X >= 1 && region.Y >= 1);
            }
            var ice = cinnabar.Icons.Single(i => i.Item.Value == "pret:ITEM_TM14");
            var fire = cinnabar.Icons.Single(i => i.Item.Value == "pret:ITEM_TM38");
            Assert.AreEqual(ice.PngPath, fire.PngPath);
            Assert.AreNotEqual(ice.Region, fire.Region);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void EarlyMachineViewsLoadFromDirectGeneratedFilesOnly()
    {
        string root = TestAssets.Output();
        try
        {
            string[] resources = ["data/items.json", "data/machines.json",
                "data/machine-learning/pewter.json", "data/machine-learning/bill.json",
                "data/machine-learning/cerulean.json", "data/machine-learning/ssanne.json",
                "data/machine-learning/route11.json"];
            foreach (string relative in resources)
            {
                string destination = Path.Combine(root, "content/generated", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(TestAssets.Root, "content/generated", relative), destination);
            }
            var shared = SharedMachineCatalog.LoadProject(root);
            var pewter = PretPewterMachines.LoadDocument(File.ReadAllText(
                Path.Combine(root, "content/generated/data/machine-learning/pewter.json")), shared);
            Assert.AreEqual(2, pewter.Icons.Length);
            Assert.AreEqual(8, PretBillMachines.LoadProject(root).Machines.Length);
            Assert.AreEqual(5, PretCeruleanMachines.LoadProject(root).Machines.Length);
            Assert.AreEqual(11, PretSsAnneMachines.LoadProject(root).Machines.Length);
            Assert.AreEqual(13, PretRoute11Machines.LoadProject(root).Machines.Length);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void InvalidLogicalRegionsAreRejectedBeforeTextureLoading()
    {
        var document = PretEarlyShop.Load(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/services/early-shop/early-shop.json")), SharedItemCatalog.LoadProject(TestAssets.Root));
        Assert.ThrowsExactly<InvalidDataException>(() => PretEarlyShop.ValidateIcon(document.Icons[0] with { Region = new(1, 1, 23, 24) }));
        var machines = MachineIcons(TestAssets.Root);
        Assert.ThrowsExactly<InvalidDataException>(() => PretPewterMachines.ValidateIcon(machines[0] with { Region = new(-1, 1, 24, 24) }));
    }
}
