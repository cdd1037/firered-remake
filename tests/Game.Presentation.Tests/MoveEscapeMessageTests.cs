using Game.Domain.Battle;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class MoveEscapeMessageTests
{
    [TestMethod, DataRow(WildBattleSide.Player), DataRow(WildBattleSide.Opponent)]
    public void MoveEscapeOwnsOneSourceFledMessageWithTheEscapingBattler(WildBattleSide actor)
    {
        var slot = new BattlerSlot((int)actor);
        var receipt = new SourceEndedEvent(14, 3, WildBattleOutcome.Escaped) {
            MoveEscape = new(slot, new("pret:MOVE_TELEPORT")) };
        string before = JsonSerializer.Serialize(receipt);
        var display = Describe(receipt);
        Assert.IsNotNull(display); Assert.IsFalse(display.Silent); Assert.IsFalse(display.Unsupported);
        var text = display.Texts.Single();
        Assert.AreEqual("sText_PkmnFledFromBattle", text.Symbol);
        Assert.AreEqual(("B_ATK_NAME_WITH_PREFIX", slot.Side == BattleSide.Player ? "ABRA" : "Wild ABRA"), text.Values.Single());
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/battle/text.json")));
        Assert.AreEqual("{B_ATK_NAME_WITH_PREFIX} fled from\\nbattle!", source.RootElement.GetProperty(text.Symbol).GetString());
        Assert.AreEqual(before, JsonSerializer.Serialize(receipt));
    }

    [TestMethod]
    public void GenericEndAndRunCannotBorrowMoveEscapeTextAndMalformedReceiptIsRejected()
    {
        foreach (var outcome in Enum.GetValues<WildBattleOutcome>())
            Assert.IsNull(Describe(new SourceEndedEvent(14, 3, outcome)));
        var run = new SourceRunAttemptEvent(13, 3, 0, 1, 80, 20, null, null, true);
        Assert.IsNull(Describe(run), "The existing Run view continues to select Got Away Safely.");
        var ended = new SourceEndedEvent(14, 3, WildBattleOutcome.Escaped) {
            MoveEscape = new(new BattlerSlot(0), new("pret:MOVE_TELEPORT")) };
        Assert.IsTrue(Describe(ended with { Outcome = WildBattleOutcome.Won })!.Unsupported);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BattlerSlot(4));
        Assert.IsTrue(Describe(ended with { MoveEscape = ended.MoveEscape! with { Move = default(MoveId) } })!.Unsupported);
    }

    [TestMethod, DataRow(WildBattleSide.Player), DataRow(WildBattleSide.Opponent)]
    public void TrailingTeleportPpSynchronizationIsSilent(WildBattleSide actor)
    {
        var slot = new BattlerSlot((int)actor);
        var mechanics = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        var pokemon = SourcePokemonFactory.Create(mechanics,
            new(new("pret:SPECIES_ABRA"), 5, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var move = pokemon.Moves.Single();
        Assert.AreEqual("pret:MOVE_TELEPORT", move.Move.Value);
        var before = new WildBattleCalledPartyMember(pokemon, "ITEM_NONE", 0);
        var after = before with { Pokemon = pokemon with { Moves = [move with { CurrentPp = move.CurrentPp - 1 }] } };
        var write = new SourceCalledPartyChangedEvent(15, 3, slot.Side, 0, "active-sync", before, after) { SourceSlot = slot };
        var display = Describe(write);
        Assert.IsNotNull(display); Assert.IsTrue(display.Silent); Assert.IsFalse(display.Unsupported);
        Assert.AreEqual(0, display.Texts.Length);
    }

    private static SourceTrainerEventDisplay? Describe(SourceBattleEvent receipt) =>
        SourceTrainerPresentationModel.Describe([receipt], 0,
            side => side.Side == BattleSide.Player ? "ABRA" : "Wild ABRA", move => move.Value, (_, _) => "party");
}
