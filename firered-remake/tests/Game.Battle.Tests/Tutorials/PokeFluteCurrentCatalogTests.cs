using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Battle.Tutorials;

[TestClass, TestCategory("Game")]
public sealed class PokeFluteCurrentCatalogTests
{
    [TestMethod]
    public void CurrentCatalogFieldFluteWakesPartyWithoutConsumption()
    {
        var campaign = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var content = campaign.PokemonContent!;
        var shop = campaign.EarlyShop!;
        var pokemon = SourcePokemonFactory.Create(content.Pokemon.Mechanics,
            new(new("pret:SPECIES_CHARMANDER"), 5, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var member = new PartyPokemon(1, pokemon, "CHARMANDER", "RED", 0, 2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        ImmutableArray<PartyPokemon> party = [member with { Status = 3 }, member with { InstanceId = 2, Status = 8 }];
        var inventory = new InventoryState([], [], shop.Limits) { KeyItems = [new(PokemonTowerItemRules.PokeFluteId, 1)] };
        var result = PokeFluteRules.UseField(shop, content, inventory, party);
        Assert.IsTrue(result.Succeeded, result.Error);
        CollectionAssert.AreEqual(new[] { 0 }, result.AwakenedIndices.ToArray());
        Assert.AreEqual(0u, result.Party[0].Status);
        Assert.AreEqual(party[1], result.Party[1]);
        Assert.AreEqual(1, inventory.KeyItems.Value.Single().Quantity);
        Assert.AreEqual(3u, party[0].Status);
        Assert.AreEqual("poke_flute_item_missing", PokeFluteRules.UseField(shop, content, inventory with { KeyItems = [] }, party).Error);
        var missingDefinition = shop with { Items = shop.Items.Where(item => item.Id != PokemonTowerItemRules.PokeFluteId).ToImmutableArray() };
        Assert.AreEqual("poke_flute_profile_not_enabled", PokeFluteRules.UseField(missingDefinition, content, inventory, party).Error);
    }
}
