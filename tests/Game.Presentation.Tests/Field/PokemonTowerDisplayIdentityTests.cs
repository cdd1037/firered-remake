using Game.Domain.WildBattle;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class PokemonTowerDisplayIdentityTests
{
    [TestMethod]
    [DataRow(PokemonTowerBattleKind.DisguisedMarowak, (int)PokemonTowerPresentationModel.RevealPhase.NotApplicable, true)]
    [DataRow(PokemonTowerBattleKind.RevealedMarowak, (int)PokemonTowerPresentationModel.RevealPhase.BeforeReveal, true)]
    [DataRow(PokemonTowerBattleKind.RevealedMarowak, (int)PokemonTowerPresentationModel.RevealPhase.AfterReveal, false)]
    public void DisplayNamesDoNotReplaceScriptedSpeciesIdentity(PokemonTowerBattleKind kind,
        int phaseValue, bool ghost)
    {
        var phase = (PokemonTowerPresentationModel.RevealPhase)phaseValue;
        var appearance = PokemonTowerPresentationModel.Opponent(kind, phase, 105, "Bone Parent", "Spirit");
        Assert.AreEqual(ghost ? "Spirit" : "Bone Parent", appearance.Name);
        Assert.AreEqual(ghost, appearance.GhostSprite);
        Assert.AreEqual(ghost ? null : (int?)105, appearance.SpeciesSprite);
        Assert.ThrowsExactly<ArgumentException>(() =>
            PokemonTowerPresentationModel.Opponent(kind, phase, 104, "MAROWAK", "GHOST"));
    }
}
