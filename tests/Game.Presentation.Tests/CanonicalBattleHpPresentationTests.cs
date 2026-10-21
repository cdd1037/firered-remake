using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Ids;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class CanonicalBattleHpPresentationTests
{
    private static readonly MoveId Move = new("pret:MOVE_TACKLE");

    private static PokemonCatalog Mechanics => TestAssets.Cached(() =>
        SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root)).Catalog;

    [TestMethod]
    [DataRow("SUNKERN", 10, 560L, 0)]
    [DataRow("SUNKERN", 10, 650L, 31)]
    [DataRow("SUNFLORA", 10, 651L, 32)]
    [DataRow("SUNFLORA", 10, 741L, 63)]
    [DataRow("SUNFLORA", 10, 742L, 64)]
    [DataRow("BULBASAUR", 10, 652L, 32)]
    [DataRow("CHARIZARD", 10, 652L, 32)]
    [DataRow("PIDGEY", 10, 652L, 32)]
    [DataRow("CATERPIE", 10, 1166L, 32)]
    [DataRow("ARCANINE", 10, 1457L, 32)]
    [DataRow("MEWTWO", 10, 1457L, 32)]
    [DataRow("SUNKERN", 10, 100L, 0)]
    [DataRow("SUNFLORA", 10, 900L, 64)]
    [DataRow("SUNFLORA", 100, 1059860L, 0)]
    public void HealthboxExperienceUsesAuthoritativeSpeciesAndExactPixelWidths(string species, int level, long experience, int pixels)
    {
        Assert.AreEqual(pixels, SourceBattleHpPresentation.ExperienceBar(Mechanics,
            new("pret:SPECIES_" + species), level, experience));
    }

    // pret/pokefirered community decompilation 037335f4c725d7c9aecdac87066f2002b4bd7e14:
    // species_info.h:5570,5599 selects Medium Slow; experience_tables.h:7 gives thresholds
    // 560 and 742. battle_interface.c:2057-2060 raises positive subpixel progress to one.
    [TestMethod]
    [DataRow("SUNKERN", 560L, 0)]
    [DataRow("SUNKERN", 561L, 1)]
    [DataRow("SUNKERN", 562L, 1)]
    [DataRow("SUNKERN", 563L, 1)]
    [DataRow("SUNKERN", 566L, 2)]
    [DataRow("SUNFLORA", 561L, 1)]
    [DataRow("SUNFLORA", 562L, 1)]
    [DataRow("SUNFLORA", 741L, 63)]
    [DataRow("SUNFLORA", 742L, 64)]
    public void SettledExperienceRetainsSourceMinimumVisiblePixel(string species, long experience, int pixels)
    {
        Assert.AreEqual(pixels, SourceBattleHpPresentation.ExperienceBar(Mechanics,
            new("pret:SPECIES_" + species), 10, experience));
    }

    // Defensive helper boundaries, not claims about upstream malformed-input arithmetic.
    // The source explicitly empties level-100 EXP tiles in battle_interface.c:1934-1938.
    [TestMethod]
    [DataRow(10, -1L, 0)]
    [DataRow(10, 559L, 0)]
    [DataRow(10, 743L, 64)]
    [DataRow(10, long.MinValue, 0)]
    [DataRow(10, long.MaxValue, 64)]
    [DataRow(100, 0L, 0)]
    [DataRow(100, 1059861L, 0)]
    [DataRow(100, long.MinValue, 0)]
    [DataRow(100, long.MaxValue, 0)]
    public void SettledExperienceClampsBeforeArithmeticAndEmptiesMaximumLevel(int level, long experience, int pixels)
    {
        Assert.AreEqual(pixels, SourceBattleHpPresentation.ExperienceBar(Mechanics,
            new("pret:SPECIES_SUNFLORA"), level, experience));
    }

    [TestMethod]
    public void MatchingSlotsUseTheSameDamageAndRecoverySpansInEitherLayout()
    {
        for (int position = 0; position < 4; position++)
        {
            var slot = new BattlerSlot(position); var member = new PartyMemberKey(slot.Side, slot.Position);
            SourceBattleEvent[] receipts = [
                new SourceHpChangedEvent(1, 1, slot.Opposite, slot, Move, 30, 20, 10, false, 100),
                new SourceAbilityDamageEvent(2, 1, slot.Opposite, slot, Move, "ABILITY_ROUGH_SKIN", 30, 20, 10),
                new SourceRecoveredEvent(3, 1, slot, Move, 20, 30, 10),
                new SourceHeldItemConsumedEvent(4, 1, slot, "ITEM_ORAN_BERRY", 20, 30, 10),
                new SourceAbilityRecoveredEvent(5, 1, slot, "ABILITY_RAIN_DISH", 20, 30, 10)];
            for (int index = 0; index < receipts.Length; index++)
            {
                var receipt = receipts[index];
                Assert.IsTrue(SourceBattleHpPresentation.ChangesHp(receipt));
                Assert.AreEqual(index < 2 ? (30, 20) : (20, 30), SourceBattleHpPresentation.Span(receipt, slot, member));
                Assert.IsNull(SourceBattleHpPresentation.Span(receipt, slot.Opposite, new(slot.Opposite.Side, slot.Opposite.Position)));
                if (position < 2)
                {
                    // Singles and doubles controllers both read this exact canonical receipt and event-time member.
                    SourceBattleEvent[] singles = [receipt]; SourceBattleEvent[] doubles = [receipt];
                    Assert.AreSame(singles[0], doubles[0]);
                    Assert.AreEqual(SourceBattleHpPresentation.Span(singles[0], slot, member),
                        SourceBattleHpPresentation.Span(doubles[0], slot, member));
                }
            }
        }
    }

    [TestMethod]
    public void MedicineUsesTheDisplayedPartyMemberRatherThanOnlyTheActorOrFinalSlot()
    {
        var receipt = new SourceMedicineAppliedEvent(1, 1, new("pret:ITEM_POTION"), 101, 2, false, 10, 30, 0, 0) {
            Actor = new(0), TargetSide = BattleSide.Player };
        Assert.AreEqual((10, 30), SourceBattleHpPresentation.Span(receipt, new(0), new(BattleSide.Player, 2)));
        Assert.AreEqual((10, 30), SourceBattleHpPresentation.Span(receipt, new(2), new(BattleSide.Player, 2)));
        Assert.IsNull(SourceBattleHpPresentation.Span(receipt, new(0), new(BattleSide.Player, 0)));
        Assert.IsNull(SourceBattleHpPresentation.Span(receipt, new(1), new(BattleSide.Opponent, 2)));
    }

    [TestMethod]
    public void LeechSeedOwnsBothHpSpansAndSelfDestructEndsAtZero()
    {
        var drain = new SourceLeechSeedDrainEvent(1, 1, new(3), new(0), 40, 35, 12, 17, 5, 5);
        Assert.AreEqual((40, 35), SourceBattleHpPresentation.Span(drain, new(3), new(BattleSide.Opponent, 1)));
        Assert.AreEqual((12, 17), SourceBattleHpPresentation.Span(drain, new(0), new(BattleSide.Player, 0)));
        Assert.IsNull(SourceBattleHpPresentation.Span(drain, new(1), new(BattleSide.Opponent, 0)));
        var selfDestruct = new SourceSelfDestructEvent(2, 1, new(2), new("pret:MOVE_SELF_DESTRUCT"), 87);
        Assert.AreEqual((87, 0), SourceBattleHpPresentation.Span(selfDestruct, new(2), new(BattleSide.Player, 1)));
        Assert.IsTrue(SourceBattleHpPresentation.ChangesHp(selfDestruct));
        Assert.IsFalse(SourceBattleHpPresentation.ChangesHp(new SourceTurnReadyEvent(3, 1, 0)));
        Assert.IsNull(SourceBattleHpPresentation.Span(null, new(0), new(BattleSide.Player, 0)));
    }
}
