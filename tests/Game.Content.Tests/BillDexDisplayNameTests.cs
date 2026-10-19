using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class BillDexDisplayNameTests
{
    private const string RelativePath = "content/generated/data/items/bill/bill-items.json";
    private static SharedMachineCatalog Machines => TestAssets.Cached(() => SharedMachineCatalog.LoadProject(TestAssets.Root));
    private static PretBillItemsDocument Original => TestAssets.Cached(() => PretBillItems.LoadProject(TestAssets.Root, Machines));

    [TestMethod]
    [DataRow("Eevee")]
    [DataRow("同じ表示名")]
    [DataRow("A shared display name longer than ten characters")]
    public void ProjectLoaderBindsPicturesByIdentityRatherThanDisplayName(string name)
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        TestAssets.EditJson(output.Path, RelativePath, document =>
        {
            foreach (var view in document["dexViews"]!.AsArray()) view!["name"] = name;
        });

        var edited = PretBillItems.LoadProject(output.Path, Machines);
        CollectionAssert.AreEqual(Original.DexViews.Select(view => view with { Name = name }).ToArray(),
            edited.DexViews.ToArray());
        for (int i = 0; i < Original.Items.Length; i++)
        {
            Assert.AreSame(Original.Items[i].Metadata.Item, edited.Items[i].Metadata.Item);
            Assert.AreEqual(Original.Items[i].Metadata.Description, edited.Items[i].Metadata.Description);
        }
    }

    [TestMethod]
    [DataRow("empty-name")]
    [DataRow("blank-name")]
    [DataRow("species")]
    [DataRow("sourceSpeciesId")]
    [DataRow("nationalDex")]
    [DataRow("x")]
    [DataRow("y")]
    [DataRow("width")]
    [DataRow("height")]
    [DataRow("marksSeen")]
    [DataRow("marksOwned")]
    public void EditedNamesDoNotRelaxBillPictureIdentityOrLayout(string change)
    {
        using var output = TestAssets.TemporaryOutput();
        Prepare(output.Path);
        TestAssets.EditJson(output.Path, RelativePath, document =>
        {
            var view = document["dexViews"]![0]!;
            view["name"] = "Edited display label";
            switch (change)
            {
                case "empty-name": view["name"] = ""; break;
                case "blank-name": view["name"] = " \t\n"; break;
                case "species": view["species"]!["value"] = "pret:SPECIES_FLAREON"; break;
                case "marksSeen": view[change] = false; break;
                case "marksOwned": view[change] = true; break;
                default: view[change] = view[change]!.GetValue<int>() + 1; break;
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => PretBillItems.LoadProject(output.Path, Machines));
    }

    private static void Prepare(string root) => TestAssets.CopyFiles(root,
        new[] { RelativePath }.Concat(Original.Icons.Select(icon => icon.PngPath))
            .Concat(Original.DexViews.Select(view => "content/generated/data/items/bill/" + view.PngPath)).Distinct());
}
