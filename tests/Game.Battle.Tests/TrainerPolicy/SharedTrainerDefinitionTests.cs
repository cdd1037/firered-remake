using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Battle.TrainerPolicy;

[TestClass]
public sealed class SharedTrainerDefinitionTests
{
    private static PretCanonicalPokemonCatalog Canonical => TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static GamePokemonCatalog Pokemon => TestAssets.Cached(() => PretGamePokemonCatalog.LoadContent(
        File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath)), Canonical, Mechanics));
    private static GameOpponentPokemonCatalog Opponents => Pokemon.Opponents;

    [TestMethod]
    [DataRow("cinnabar")]
    [DataRow("first-sevii")]
    [DataRow("saffron")]
    [DataRow("celadon")]
    [DataRow("fuchsia")]
    [DataRow("silph")]
    [DataRow("victory-road")]
    [DataRow("viridian")]
    [DataRow("six-island")]
    [DataRow("sevii-ruby")]
    public void RegionalFactoriesUseSharedPartyFieldsAndCanonicalMoves(string region)
    {
        var fixture = Factory(region);
        Assert.IsTrue(fixture.Trainers.Count > 0);
        foreach (var trainer in fixture.Trainers.Values)
        {
            var made = fixture.Create(trainer);
            Assert.AreSame(trainer, made.Trainer);
            Assert.AreEqual(trainer.Party.Length, made.Party.Length);
            for (var index = 0; index < made.Party.Length; index++)
            {
                var definition = trainer.Party[index];
                var member = made.Party[index];
                Assert.AreEqual(definition.Species, member.Pokemon.Species);
                Assert.AreEqual(definition.Level, member.Pokemon.Level);
                Assert.AreEqual(definition.HeldItem, member.HeldItem);
                var moves = (trainer.PartyFlags & 1) != 0
                    ? definition.CustomMoves.TakeWhile(move => move.Value != "pret:MOVE_NONE")
                    : PokemonMoveRules.InitialMoves(Mechanics.Catalog, Mechanics.Catalog.Species[definition.Species], definition.Level).Select(move => move.Move);
                CollectionAssert.AreEqual(moves.ToArray(), member.Pokemon.Moves.Select(move => move.Move).ToArray());
            }
        }
        var first = fixture.Trainers.Values.First();
        var edited = first with { Party = first.Party.SetItem(0, first.Party[0] with { Level = first.Party[0].Level + 1 }) };
        Assert.AreEqual(edited.Party[0].Level, fixture.Create(edited).Party[0].Pokemon.Level,
            "Party generation must consume the supplied definition rather than an embedded historical copy.");
    }

    [TestMethod]
    public void PrizeCalculationUsesCurrentDefinitionMoneyAndLastPartyLevel()
    {
        var victory = PretVictoryRoadRules.LoadTrainers(TestAssets.Root);
        var doubleTrainer = victory.Trainers[VictoryRoadDoubleTrainerEncounter.TrainerId];
        var changed = doubleTrainer with { BaseMoney = doubleTrainer.BaseMoney + 1,
            Party = doubleTrainer.Party.SetItem(doubleTrainer.Party.Length - 1, doubleTrainer.Party[^1] with { Level = 51 }) };
        var updated = victory with { Trainers = victory.Trainers.SetItem(changed.Id, changed) };
        Assert.AreEqual(8 * changed.BaseMoney * 51, VictoryRoadTrainerPartyFactory.BasePrize(updated, changed.Id));

        var viridian = PretViridianGymRules.LoadTrainers(TestAssets.Root);
        var leader = viridian.Trainers[new("pret:TRAINER_LEADER_GIOVANNI")];
        changed = leader with { BaseMoney = leader.BaseMoney + 1,
            Party = leader.Party.SetItem(leader.Party.Length - 1, leader.Party[^1] with { Level = 51 }) };
        Assert.AreEqual(4 * changed.BaseMoney * 51, TrainerPartyFactory.BasePrize(changed));
    }

    [TestMethod]
    [DataRow("cinnabar")]
    [DataRow("first-sevii")]
    [DataRow("victory-road")]
    [DataRow("six-island")]
    [DataRow("sevii-ruby")]
    public void DoubleEntryAndFrameValidationUseTheSameSharedDefinition(string region)
    {
        var fixture = Double(region);
        var entry = fixture.Begin(fixture.Trainer);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(fixture.Validate(fixture.Trainer, entry.State!));
        var changed = fixture.Trainer with {
            Party = fixture.Trainer.Party.SetItem(0, fixture.Trainer.Party[0] with { Level = fixture.Trainer.Party[0].Level + 1 })
        };
        var altered = fixture.Begin(changed);
        Assert.IsTrue(altered.Succeeded, altered.Fault);
        Assert.IsNull(fixture.Validate(changed, altered.State!));
        Assert.IsNotNull(fixture.Validate(fixture.Trainer, altered.State!));
        var single = fixture.Begin(fixture.Trainer with { DoubleBattle = false });
        Assert.IsFalse(single.Succeeded);
        Assert.AreEqual(new RngState(42), single.Rng);
    }

    [TestMethod]
    public void DoubleEntryAndAiUseSourceTrainerIdentityWithoutAnEmbeddedAllowlist()
    {
        var original = PretSixIslandRules.LoadTrainers(TestAssets.Root);
        var source = original.BattlesByScript.Values.First(battle => battle.Mode == 4);
        var trainer = original.Trainers[source.Trainer] with { Id = new("pret:TRAINER_SOURCE_DEFINED_PAIR") };
        var script = source with { Trainer = trainer.Id, ScriptEntry = "AnotherMap_EventScript_Pair" };
        var catalog = new SixIslandTrainerCatalog(
            ImmutableDictionary<TrainerId, TrainerDefinition>.Empty.Add(trainer.Id, trainer),
            ImmutableDictionary<string, CeruleanTrainerBattleDefinition>.Empty.Add(script.ScriptEntry, script));
        WildBattleCalledPartyMember Player(uint personality) => new(SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_CLEFAIRY"), 60, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(1)).Pokemon);
        ImmutableArray<WildBattleCalledPartyMember> party = [Player(0), Player(2)];
        var entry = SixIslandDoubleTrainerEncounter.Begin(catalog, Opponents, script.ScriptEntry, party, new(42));
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SixIslandDoubleTrainerEncounter.ValidateFrame(catalog, Opponents, trainer.Id, entry.State!));
        var actions = SixIslandDoubleTrainerEncounter.ChooseEnemyActions(catalog, Opponents, trainer.Id,
            entry.State!, entry.State!.Revision);
        Assert.IsTrue(actions.Succeeded, actions.Fault);
        Assert.AreEqual(2, actions.Choices.Length);
        Assert.IsNotNull(SixIslandDoubleTrainerEncounter.ValidateFrame(catalog, Opponents, source.Trainer, entry.State!));
        Assert.IsFalse(SixIslandDoubleTrainerEncounter.Begin(catalog, Opponents, source.ScriptEntry, party, new(42)).Succeeded);
        var single = catalog with { Trainers = catalog.Trainers.SetItem(trainer.Id, trainer with { DoubleBattle = false }) };
        Assert.IsFalse(SixIslandDoubleTrainerEncounter.Begin(single, Opponents, script.ScriptEntry, party, new(42)).Succeeded);
    }

    private sealed record FactoryFixture(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
        Func<TrainerDefinition, TrainerPartyCreationResult> Create);

    private static FactoryFixture Factory(string region)
    {
        switch (region)
        {
            case "cinnabar":
                var cinnabar = PretCinnabarTrainers.Load(TestAssets.Root);
                return new(cinnabar.Trainers, trainer => CinnabarTrainerPartyFactory.Create(cinnabar with { Trainers = cinnabar.Trainers.SetItem(trainer.Id, trainer) }, Opponents, trainer.Id, new(42)));
            case "first-sevii":
                var first = PretFirstSeviiRules.LoadTrainers(TestAssets.Root);
                return new(first.Trainers, trainer => FirstSeviiTrainerPartyFactory.Create(first with { Trainers = first.Trainers.SetItem(trainer.Id, trainer) }, Opponents, trainer.Id, new(42)));
            case "celadon":
                var celadon = PretCeladonGymTrainers.Load(Canonical, TestAssets.Root);
                return new(celadon.Trainers, trainer => TrainerPartyFactory.Create(celadon.Trainers.SetItem(trainer.Id, trainer), Opponents, trainer.Id, new(42)));
            case "fuchsia":
                var fuchsia = PretFuchsiaGymTrainers.Load(Canonical, TestAssets.Root);
                return new(fuchsia.Trainers, trainer => TrainerPartyFactory.Create(fuchsia.Trainers.SetItem(trainer.Id, trainer), Opponents, trainer.Id, new(42)));
            case "saffron":
                var saffronContent = PretSaffronGymTrainers.Load(Canonical, TestAssets.Root);
                var saffron = saffronContent;
                return new(saffron.Trainers, trainer => TrainerPartyFactory.Create(saffron.Trainers.SetItem(trainer.Id, trainer), Opponents, trainer.Id, new(42)));
            case "silph":
                var silph = PretSilphTrainers.Load(Canonical, TestAssets.Root);
                return new(silph.Trainers, trainer => TrainerPartyFactory.Create(silph.Trainers.SetItem(trainer.Id, trainer), Opponents, trainer.Id, new(42)));
            case "victory-road":
                var victory = PretVictoryRoadRules.LoadTrainers(TestAssets.Root);
                return new(victory.Trainers, trainer => VictoryRoadTrainerPartyFactory.Create(victory with { Trainers = victory.Trainers.SetItem(trainer.Id, trainer) }, Opponents, trainer.Id, new(42)));
            case "viridian":
                var viridian = PretViridianGymRules.LoadTrainers(TestAssets.Root);
                return new(viridian.Trainers, trainer => TrainerPartyFactory.Create(viridian.Trainers.SetItem(trainer.Id, trainer), Opponents, trainer.Id, new(42)));
            case "six-island":
                var six = PretSixIslandRules.LoadTrainers(TestAssets.Root);
                return new(six.Trainers, trainer => SixIslandTrainerPartyFactory.Create(six with { Trainers = six.Trainers.SetItem(trainer.Id, trainer) }, Opponents, trainer.Id, new(42)));
            case "sevii-ruby":
                var ruby = PretSeviiRubyRules.LoadTrainers(TestAssets.Root);
                return new(ruby.Trainers, trainer => SeviiRubyTrainerPartyFactory.Create(ruby with { Trainers = ruby.Trainers.SetItem(trainer.Id, trainer) }, Opponents, trainer.Id, new(42)));
            default: throw new ArgumentOutOfRangeException(nameof(region));
        }
    }

    private sealed record DoubleFixture(TrainerDefinition Trainer, Func<TrainerDefinition, SourceTwinsEncounterResult> Begin,
        Func<TrainerDefinition, SourceBattleFrame, string?> Validate);

    private static DoubleFixture Double(string region)
    {
        WildBattleCalledPartyMember Player(uint personality) => new(SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_CLEFAIRY"), 60, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(1)).Pokemon);
        ImmutableArray<WildBattleCalledPartyMember> party = [Player(0), Player(2)];
        switch (region)
        {
            case "cinnabar":
                var cinnabar = PretCinnabarTrainers.Load(TestAssets.Root); var cId = CinnabarDoubleTrainerEncounter.TrainerId;
                var cScript = cinnabar.BattlesByScript.Values.First(battle => battle.Trainer == cId).ScriptEntry;
                CinnabarTrainerCatalog C(TrainerDefinition trainer) => cinnabar with { Trainers = cinnabar.Trainers.SetItem(cId, trainer) };
                return new(cinnabar.Trainers[cId], trainer => CinnabarDoubleTrainerEncounter.Begin(C(trainer), Opponents, cScript, party, new(42)),
                    (trainer, frame) => CinnabarDoubleTrainerEncounter.ValidateFrame(C(trainer), Opponents, frame));
            case "first-sevii":
                var first = PretFirstSeviiRules.LoadTrainers(TestAssets.Root); var fId = FirstSeviiDoubleTrainerEncounter.TrainerId;
                var fBattle = first.BattlesBySource.Values.First(battle => battle.Trainer == fId);
                FirstSeviiTrainerCatalog F(TrainerDefinition trainer) => first with { Trainers = first.Trainers.SetItem(fId, trainer) };
                return new(first.Trainers[fId], trainer => FirstSeviiDoubleTrainerEncounter.Begin(F(trainer), Opponents, fBattle.ScriptEntry, fBattle.Source.StartLine, party, new(42)),
                    (trainer, frame) => FirstSeviiDoubleTrainerEncounter.ValidateFrame(F(trainer), Opponents, frame));
            case "victory-road":
                var victory = PretVictoryRoadRules.LoadTrainers(TestAssets.Root); var vId = VictoryRoadDoubleTrainerEncounter.TrainerId;
                var vScript = victory.BattlesByScript.Values.First(battle => battle.Trainer == vId).ScriptEntry;
                VictoryRoadTrainerCatalog V(TrainerDefinition trainer) => victory with { Trainers = victory.Trainers.SetItem(vId, trainer) };
                return new(victory.Trainers[vId], trainer => VictoryRoadDoubleTrainerEncounter.Begin(V(trainer), Opponents, vScript, party, new(42)),
                    (trainer, frame) => VictoryRoadDoubleTrainerEncounter.ValidateFrame(V(trainer), Opponents, frame));
            case "six-island":
                var six = PretSixIslandRules.LoadTrainers(TestAssets.Root); var sId = new TrainerId("pret:TRAINER_TWINS_MIU_MIA");
                var sScript = six.BattlesByScript.Values.First(battle => battle.Trainer == sId).ScriptEntry;
                SixIslandTrainerCatalog S(TrainerDefinition trainer) => six with { Trainers = six.Trainers.SetItem(sId, trainer) };
                return new(six.Trainers[sId], trainer => SixIslandDoubleTrainerEncounter.Begin(S(trainer), Opponents, sScript, party, new(42)),
                    (trainer, frame) => SixIslandDoubleTrainerEncounter.ValidateFrame(S(trainer), Opponents, sId, frame));
            case "sevii-ruby":
                var ruby = PretSeviiRubyRules.LoadTrainers(TestAssets.Root); var rId = SeviiRubyDoubleTrainerEncounter.TrainerId;
                var rScript = ruby.BattlesByScript.Values.First(battle => battle.Trainer == rId).ScriptEntry;
                SeviiRubyTrainerCatalog R(TrainerDefinition trainer) => ruby with { Trainers = ruby.Trainers.SetItem(rId, trainer) };
                return new(ruby.Trainers[rId], trainer => SeviiRubyDoubleTrainerEncounter.Begin(R(trainer), Opponents, rScript, party, new(42)),
                    (trainer, frame) => SeviiRubyDoubleTrainerEncounter.ValidateFrame(R(trainer), Opponents, frame));
            default: throw new ArgumentOutOfRangeException(nameof(region));
        }
    }
}
