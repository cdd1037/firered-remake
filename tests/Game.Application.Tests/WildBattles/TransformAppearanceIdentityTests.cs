using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class TransformAppearanceIdentityTests
{
    [TestMethod]
    [DataRow("WAILORD","SPLASH",false,true)]
    [DataRow("WAILORD","SPLASH",true,false)]
    [DataRow("UNOWN","HIDDEN_POWER",true,false)]
    [DataRow("DEOXYS","PSYCHO_BOOST",false,true)]
    [DataRow("CASTFORM","TACKLE",true,false)]
    [DataRow("SPINDA","TACKLE",false,true)]
    public void OrdinaryAndSpecialPicturesShareActorPaletteAndDonorBitmapIdentity(string species,string move,bool actorShiny,bool donorShiny)
    {
        var actor=Kernel.Mon("DITTO","TRANSFORM",100,personality:actorShiny?42u:0u).Pokemon;
        var donor=Kernel.Mon(species,move,100,personality:donorShiny?42u:3u).Pokemon;
        Assert.AreEqual(actorShiny,actor.IsShiny);Assert.AreEqual(donorShiny,donor.IsShiny);
        // The battle view retains actor identity fields; drawing selects the
        // explicitly captured donor picture, never BattleView.Personality.
        var battle=actor with{Species=donor.Species,SourceSpeciesId=donor.SourceSpeciesId};
        var body=new WildBattleCombatant(battle){Transform=new(actor,donor,[0,0,0,0]),
            CalledOverlay=species=="CASTFORM"?new(){CastformForm=SourceCastformForm.Rainy}:null};
        var appearance=CampaignBattleFormAppearance.From(body);
        Assert.AreSame(donor,appearance.RenderPokemon);Assert.AreEqual(actorShiny,appearance.Shiny);Assert.IsTrue(appearance.Transformed);
        Assert.AreEqual(species=="CASTFORM"?SourceCastformForm.Rainy:SourceCastformForm.Normal,appearance.Form);
        Assert.AreEqual(UnownFormRules.ResolveRenderSpeciesId(donor.SourceSpeciesId,donor.Personality),
            UnownFormRules.ResolveRenderSpeciesId(appearance.RenderPokemon.SourceSpeciesId,appearance.RenderPokemon.Personality));
        // Selection is pure. Pixel availability, including front Spinda's absent
        // PID-specific spots input, remains the material resolver's boundary.
        Assert.AreSame(body.Transform.CopiedPokemon,appearance.RenderPokemon);
    }
}
