using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;

namespace FireRed.Tests.Battle.Lifecycle;

[TestClass, TestCategory("Game")]
public sealed class SharedTrainerGenerationTests
{
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static VsSeekerTrainerCatalog Trainers => TestAssets.Cached(() => PretVsSeekerParties.Load(TestAssets.Root));

    // Fixed vectors from raw CreateNPCTrainerParty, charmap and Random32: name sums
    // accumulate across members; the low PID byte is male 0x88, female 0x78 or double 0x80.
    [TestMethod]
    [DataRow("YOUNGSTER_BEN", "RATTATA", "EKANS", 11, 0, "ITEM_NONE", 0x7AA88u, 0xDC188u)]
    [DataRow("LASS_JANICE", "PIDGEY", "PIDGEY", 9, 0, "ITEM_NONE", 0x92478u, 0x124878u)]
    [DataRow("CRUSH_KIN_RON_MYA", "HITMONCHAN", "HITMONLEE", 29, 6, "ITEM_BLACK_BELT", 0xC8C80u, 0x185A80u)]
    public void NamePersonalityPartyOrderItemsAndOtDrawsMatchSourceCases(string trainer, string first, string second,
        int level, int iv, string heldItem, uint firstPersonality, uint secondPersonality)
    {
        var made = VsSeekerTrainerPartyFactory.Create(Trainers, Mechanics, new("pret:TRAINER_" + trainer), new(42));
        CollectionAssert.AreEqual(new[] { "pret:SPECIES_" + first, "pret:SPECIES_" + second }, made.Party.Select(p => p.Pokemon.Species.Value).ToArray());
        CollectionAssert.AreEqual(new[] { firstPersonality, secondPersonality }, made.Party.Select(p => p.Pokemon.Personality).ToArray());
        CollectionAssert.AreEqual(new[] { 3626748553u, 1052862226u }, made.Party.Select(p => p.Pokemon.OriginalTrainerId).ToArray());
        foreach (var member in made.Party)
        {
            Assert.AreEqual(level, member.Pokemon.Level);
            Assert.AreEqual(new SixStats(iv, iv, iv, iv, iv, iv), member.Pokemon.Ivs);
            Assert.AreEqual(heldItem, member.HeldItem);
        }
        Assert.AreEqual(new RngState(1052882606, 4), made.Rng);
        CollectionAssert.AreEqual(new ushort[] { 51849, 55339, 26386, 16065 }, made.Trace.Select(draw => draw.Raw).ToArray());
    }

    // sPidgeyLevelUpLearnset: Gust at 9, Quick Attack at 13, Whirlwind at 19.
    // The fifth learned move drops Tackle rather than keeping a cached earlier moveset.
    [TestMethod]
    [DataRow(8, new[] { "TACKLE", "SAND_ATTACK" })]
    [DataRow(9, new[] { "TACKLE", "SAND_ATTACK", "GUST" })]
    [DataRow(19, new[] { "SAND_ATTACK", "GUST", "QUICK_ATTACK", "WHIRLWIND" })]
    public void NaturalMovesUseCurrentLevelAndKeepTheLastFourLearnedMoves(int level, string[] moves)
    {
        var trainer = Trainers.Trainers[new("pret:TRAINER_LASS_JANICE")];
        var made = Create(trainer with { Party = [trainer.Party[0] with { Level = level }] });
        CollectionAssert.AreEqual(moves.Select(m => "pret:MOVE_" + m).ToArray(), made.Party[0].Pokemon.Moves.Select(m => m.Move.Value).ToArray());
        Assert.IsTrue(made.Party[0].Pokemon.Moves.All(m => m.CurrentPp == Mechanics.Catalog.Moves[m.Move].Pp));
        Assert.AreEqual(2, made.Trace.Length);
    }

    [TestMethod]
    public void CustomMovesReplaceNaturalMovesAndOmitTrailingMoveNoneWithoutMoreRandomDraws()
    {
        // Raw sParty_LeaderBrock explicitly omits Geodude's naturally learned Rock Throw.
        var brock = SharedTrainerCatalog.LoadProject(TestAssets.Root).Definition(new("pret:TRAINER_LEADER_BROCK"));
        var made = Create(brock);
        CollectionAssert.AreEqual(new[] { "pret:MOVE_TACKLE", "pret:MOVE_DEFENSE_CURL", "pret:MOVE_NONE", "pret:MOVE_NONE" }, brock.Party[0].CustomMoves.Select(m => m.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "pret:MOVE_TACKLE", "pret:MOVE_DEFENSE_CURL" }, made.Party[0].Pokemon.Moves.Select(m => m.Move.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "pret:MOVE_TACKLE", "pret:MOVE_BIND", "pret:MOVE_ROCK_TOMB" }, made.Party[1].Pokemon.Moves.Select(m => m.Move.Value).ToArray());
        CollectionAssert.AreEqual(new[] { 35, 40, 35, 20, 10 }, made.Party.SelectMany(p => p.Pokemon.Moves).Select(m => m.CurrentPp).ToArray());
        Assert.IsTrue(made.Party.SelectMany(p => p.Pokemon.Moves).All(m => m.CurrentPp == m.MaximumPp));
        Assert.AreEqual(new RngState(1052882606, 4), made.Rng);
        Assert.AreEqual(4, made.Trace.Length);
    }

    [TestMethod]
    public void ShinyOtCandidateIsRejectedBeforeGeneratingTheNextPartyMember()
    {
        // Independent LCG vector: seed 516 yields OT 960336822, shiny XOR 4 for BEN's
        // Rattata PID 0x7AA88. Reject that pair, then consume one accepted pair per member.
        var made = VsSeekerTrainerPartyFactory.Create(Trainers, Mechanics, new("pret:TRAINER_YOUNGSTER_BEN"), new(516));
        CollectionAssert.AreEqual(new[] { 4140322785u, 3941433175u }, made.Party.Select(p => p.Pokemon.OriginalTrainerId).ToArray());
        CollectionAssert.AreEqual(new ushort[] { 37814, 14653, 20449, 63176, 32599, 60141 }, made.Trace.Select(draw => draw.Raw).ToArray());
        Assert.AreEqual("trainer.party.0.ot.attempt.1.low16", made.Trace[2].Purpose);
        Assert.AreEqual("trainer.party.1.ot.attempt.0.low16", made.Trace[4].Purpose);
        Assert.AreEqual(new RngState(3941464018, 6), made.Rng);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(50, 6)]
    [DataRow(255, 31)]
    public void SourceDifficultyScalesToOneFixedIvForAllStats(int sourceIv, int expectedIv)
    {
        var trainer = Trainers.Trainers[new("pret:TRAINER_LASS_JANICE")];
        var made = Create(trainer with { Party = [trainer.Party[0] with { SourceIv = sourceIv }] });
        Assert.AreEqual(new SixStats(expectedIv, expectedIv, expectedIv, expectedIv, expectedIv, expectedIv), made.Party[0].Pokemon.Ivs);
        Assert.AreEqual(2, made.Trace.Length);
    }

    private static TrainerPartyCreationResult Create(TrainerDefinition trainer) =>
        VsSeekerTrainerPartyFactory.Create(VsSeekerTrainerCatalog.Authenticate(new([trainer])), Mechanics, trainer.Id, new(42));
}
