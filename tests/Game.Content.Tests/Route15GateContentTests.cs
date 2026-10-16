using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class Route15GateContentTests
{
    [TestMethod]
    public void SourceUpstairsUsesSharedAideAndReciprocalStairsWithoutChangingRoute16Threshold()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var id = new MapId("map.route-15-west-entrance-2f");
        var map = catalog.SourceMaps[id];
        Assert.AreEqual(13, map.Width);
        Assert.AreEqual(11, map.Height);
        var definition = catalog.MapDefinition(id);
        var actor = definition.Actors.Single();
        Assert.AreEqual("Route15_WestEntrance_2F_EventScript_Aide", actor.ScriptId);
        var aide = catalog.PokedexAides.ByActor[(id, actor.LocalId)];
        Assert.AreEqual(50, aide.RequiredCaught);
        Assert.AreEqual("pret:ITEM_EXP_SHARE", aide.Item.Id.Value);
        Assert.AreEqual("FLAG_GOT_EXP_SHARE_FROM_OAKS_AIDE", aide.RewardFlag);
        Assert.AreEqual(40, catalog.PokedexAides.ByActor.Values.Single(a => a.Map.Value == "map.route-16-north-entrance-2f").RequiredCaught);
        Assert.AreEqual(2, definition.BackgroundTriggers.Length);
        foreach (var script in definition.BackgroundTriggers.Select(trigger => trigger.ScriptId).Append(actor.ScriptId))
        {
            var program = catalog.FindProgram(script);
            Assert.IsNotNull(program);
            Assert.IsFalse(program.Instructions.Any(i => i.Op == StoryOp.Unsupported));
        }
        var stair = map.FieldWarps.Single();
        Assert.AreEqual(new MapId("map.route-15-west-entrance-1f"), stair.DestinationMapId);
        Assert.AreEqual(4, stair.DestinationWarpId);
        var lower = catalog.SourceMaps[stair.DestinationMapId].FieldWarps[4];
        Assert.AreEqual(id, lower.DestinationMapId);
        Assert.AreEqual(9, lower.X);
        Assert.AreEqual(10, lower.Y);
        Assert.IsFalse(map.Events.GetProperty("allow_cycling").GetBoolean());
        Assert.IsFalse(catalog.LandEncounters!.ContainsKey(id));
    }

    [TestMethod]
    public void ExpShareUsesSharedItemDefinitionIconAndExistingArticunoPortrait()
    {
        var shared = SharedItemCatalog.LoadProject(TestAssets.Root);
        var items = CurrentItemCatalog.Load(TestAssets.Root, shared);
        var share = items.Items.Single(item => item.Id.Value == "pret:ITEM_EXP_SHARE");
        Assert.AreSame(shared.Definition(share.Id), share.Item);
        Assert.AreEqual("HOLD_EFFECT_EXP_SHARE", share.Item.HoldEffect);
        Assert.AreEqual(EarlyShopUseSupport.NoFieldUse, share.UseSupport);
        Assert.IsTrue(share.Item.CanGive);
        var icon = PretEarlyShop.LoadPresentation(TestAssets.Root, items).Icons.Single(icon => icon.Item == share.Id);
        Assert.AreEqual(24, icon.Width);
        Assert.AreEqual(24, icon.Height);
        Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, icon.PngPath)));
        var presentation = PretSeafoamPresentation.Load(TestAssets.Root);
        var portrait = presentation.DexEntries[144].Portrait!;
        Assert.AreEqual(64, portrait.Width);
        Assert.AreEqual(64, portrait.Height);
        Assert.IsTrue(presentation.Files[portrait.TexturePath].Length > 0);
    }
}
