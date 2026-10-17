using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class CampaignBattleFormAppearanceTests
{
    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static PokemonInstance Pokemon(string species,bool shiny)=>SourcePokemonFactory.Create(Mechanics,
        new(new("pret:SPECIES_"+species),30,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(shiny?42u:0u)),new(0)).Pokemon;
    [TestMethod,DataRow(true,false),DataRow(false,true)]
    public void TransformUsesActorPaletteIdentityAndDonorForm(bool actorShiny,bool donorShiny)
    {
        var actor=Pokemon("DITTO",actorShiny);var donor=Pokemon("CASTFORM",donorShiny);
        Assert.AreEqual(actorShiny,actor.IsShiny);Assert.AreEqual(donorShiny,donor.IsShiny);
        var body=new WildBattleCombatant(donor){Transform=new(actor,donor,[0,0,0,0]),CalledOverlay=new(){CastformForm=SourceCastformForm.Rainy}};
        var appearance=CampaignBattleFormAppearance.From(body);
        Assert.AreEqual(donor,appearance.RenderPokemon);Assert.AreEqual(SourceCastformForm.Rainy,appearance.Form);
        Assert.AreEqual(actorShiny,appearance.Shiny);Assert.IsTrue(appearance.Transformed);
    }
    [TestMethod]
    public void UntransformedIdentityAndExplicitNormalFormAreStable()
    {
        var mon=Pokemon("CASTFORM",true);var body=new WildBattleCombatant(mon);
        var normal=CampaignBattleFormAppearance.From(body);
        Assert.AreEqual(SourceCastformForm.Normal,normal.Form);Assert.IsTrue(normal.Shiny);Assert.IsFalse(normal.Transformed);
        foreach(var form in Enum.GetValues<SourceCastformForm>())
            Assert.AreEqual(form,CampaignBattleFormAppearance.From(body with{CalledOverlay=new(){CastformForm=form}}).Form);
    }
}
