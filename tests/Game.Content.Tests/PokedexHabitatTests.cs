using Game.Content;

namespace FireRed.Tests.Content;

[TestClass,TestCategory("Game")]
public sealed class PokedexHabitatTests
{
    private static readonly Lazy<PretPokedexMenu> Catalog=new(()=>PretPokedexMenu.Load(TestAssets.Root));
    [TestMethod]
    public void HabitatPartitionAndSourceGroupOrderArePreserved()
    {
        var menu=Catalog.Value;
        CollectionAssert.AreEqual(new[]{"GrasslandPkmn","ForestPkmn","WatersEdgePkmn","SeaPkmn","CavePkmn","MountainPkmn","RoughTerrainPkmn","UrbanPkmn","RarePkmn"},menu.Document.Habitats.Select(h=>h.Id).ToArray());
        CollectionAssert.AreEqual(new[]{19,20,161,162},menu.Document.Habitats[0].Pages[0].ToArray());
        var species=menu.Document.Habitats.SelectMany(h=>h.Pages).SelectMany(p=>p).ToArray();
        Assert.AreEqual(386,species.Length);Assert.AreEqual(386,species.Distinct().Count());
    }
    [TestMethod]
    public void UnseenPagesAreSkippedAndNationalGatesFilterWithinSharedPages()
    {
        var menu=Catalog.Value;
        var regional=menu.BuildHabitats([19,20,161,162],[20],new(true));
        var first=regional[0].Pages.Single();
        Assert.AreEqual(0,first.SourcePage);
        CollectionAssert.AreEqual(new[]{19,20},first.Rows.Select(r=>r.SourceSpeciesId).ToArray());
        Assert.IsFalse(first.Rows[0].Caught);Assert.IsTrue(first.Rows[1].Caught);
        Assert.IsTrue(regional.Skip(1).All(h=>h.Pages.IsEmpty));
        var national=menu.BuildHabitats([19,161,162],[],new(true,true,0xB9,0x6258));
        CollectionAssert.AreEqual(new[]{19,161,162},national[0].Pages[0].Rows.Select(r=>r.SourceSpeciesId).ToArray());
        Assert.ThrowsExactly<InvalidDataException>(()=>menu.BuildHabitats([19],[20],new(true)));
        Assert.ThrowsExactly<InvalidDataException>(()=>menu.BuildHabitats([19],[],new(false)));
    }
    [TestMethod]
    public void SizeComparisonIsCaughtOnlyAndUsesExactSignedSourceTransforms()
    {
        var menu=Catalog.Value;
        Assert.IsNull(menu.BuildSizeComparison(1,[1],[],new(true)));
        Assert.AreEqual(new PokedexSizeTransform(356,16,256,-2),menu.BuildSizeComparison(1,[1],[1],new(true)));
        Assert.ThrowsExactly<InvalidDataException>(()=>menu.BuildSizeComparison(1,[],[],new(true)));
        Assert.AreEqual(new PokedexHabitatSlot(88,24,88,88),menu.Document.HabitatLayouts[0][0]);
        Assert.AreEqual(new PokedexHabitatSlot(176,16,120,32),menu.Document.HabitatLayouts[3][3]);
    }
    [TestMethod]
    public void MalformedPartitionsAndZeroAffineCoefficientsRejectAtLoad()
    {
        string source=File.ReadAllText(Path.Combine(TestAssets.Root,"content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json"));
        foreach(bool duplicate in new[]{true,false})
        {
            var root=Path.Combine(Path.GetTempPath(),"dex-habitat-"+Guid.NewGuid().ToString("N"));
            var directory=Path.Combine(root,"content/generated/sprites/pokemon/pokedex-menu");
            Directory.CreateDirectory(directory);
            try
            {
                var json=System.Text.Json.Nodes.JsonNode.Parse(source)!;
                if(duplicate)json["habitats"]![0]!["pages"]![0]![0]=20;
                else json["sizes"]!["1"]!["pokemonScale"]=0;
                File.WriteAllText(Path.Combine(directory,"pokedex-menu.json"),json.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(()=>PretPokedexMenu.Load(root));
            }
            finally{Directory.Delete(root,true);}
        }
    }

}
