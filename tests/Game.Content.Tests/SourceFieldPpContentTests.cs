using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SourceFieldPpContentTests
{
    [TestMethod]
    public void SelectedPpAdmissionMatchesImportedCallbackAndEffectForBothContainers()
    {
        var catalog=CurrentItemCatalog.Load(TestAssets.Root);
        foreach(var symbol in new[]{"ETHER","MAX_ETHER","LEPPA_BERRY"})
        {
            var item=catalog.Items.Single(i=>i.Id.Value=="pret:ITEM_"+symbol);
            Assert.AreEqual(EarlyShopUseSupport.FieldEther,item.UseSupport);
            Assert.IsTrue(AuthenticatedFieldMedicine.IsSelectedPpEffect(item));
            Assert.AreEqual(FieldMedicineEffect.RestoreMovePp,AuthenticatedFieldMedicine.Resolve(catalog,item.Id).Effect);
        }
    }
    [TestMethod]
    [DataRow("amount")]
    [DataRow("shape")]
    [DataRow("callback")]
    public void SelectedPpCapabilityRejectsContradictoryImportedDefinition(string mutation)
    {
        var data=JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,"content/generated/data/available-items.json")))!;
        var item=data["items"]!.AsArray().Single(i=>i!["item"]!.GetValue<string>()=="pret:ITEM_ETHER")!;
        if(mutation=="amount")item["effectBytes"]![6]=11;
        else if(mutation=="shape")item["effectBytes"]![4]=8;
        else
        {
            item=data["items"]!.AsArray().Single(i=>i!["item"]!.GetValue<string>()=="pret:ITEM_POTION")!;
            item["useSupport"]="fieldEther";item["healHp"]=0;
            item["effectBytes"]=new JsonArray(0,0,0,0,24,0,10);
        }
        var exception=Assert.ThrowsExactly<InvalidDataException>(()=>CurrentItemCatalog.Parse(data.ToJsonString(),SharedItemCatalog.LoadProject(TestAssets.Root)));
        Assert.AreEqual("current_item_pp_effect",exception.Message);
    }
}
