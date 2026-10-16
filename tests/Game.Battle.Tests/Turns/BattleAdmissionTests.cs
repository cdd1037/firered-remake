using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Battle.Turns;

[TestClass, TestCategory("Game")]
public sealed class BattleAdmissionTests
{
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));

    [TestMethod]
    public void LearningAndEntryUseTheExecutingRecipeInventoryForEditedDefinitions()
    {
        var source = Mechanics.Catalog;
        Assert.AreEqual(354, source.Moves.Count, "Source parity fixture, not a runtime inventory gate.");
        foreach (var original in source.Moves.Values)
        {
            var edited = original with { Pp = original.Pp == 40 ? 39 : original.Pp + 1,
                Accuracy = original.Accuracy == 100 ? 99 : original.Accuracy + 1 };
            var catalog = source with { Moves = source.Moves.SetItem(edited.Id, edited) };
            Assert.IsTrue(SourceBattleKernel.SupportsRecipe(edited), edited.Id.Value);
            Assert.IsTrue(WildBattleRules.IsCalledMoveEffectSupported(edited), edited.Id.Value);
            Assert.IsTrue(PokemonMoveRules.IsLearningSupported(catalog, edited.Id), edited.Id.Value);
            var learned = PokemonMoveRules.TryLearn(catalog, [], edited.Id);
            Assert.AreEqual(MoveLearningOutcome.Added, learned.Outcome, edited.Id.Value);
            Assert.AreEqual(edited.Pp, learned.Moves[0].MaximumPp);
        }
    }

    [TestMethod]
    [DataRow("GROWL")]
    [DataRow("TAIL_WHIP")]
    [DataRow("HARDEN")]
    [DataRow("FOCUS_ENERGY")]
    [DataRow("DISABLE")]
    public void EditedDefinitionEntersAndExecutesItsOwnPp(string symbol)
    {
        var original = Mechanics.Catalog.Moves[new("pret:MOVE_" + symbol)];
        var edited = original with { Pp = original.Pp - 1, Accuracy = 100 };
        var proof = SourceMechanicsCatalog.FromDefinitions(Mechanics.Catalog with {
            Moves = Mechanics.Catalog.Moves.SetItem(edited.Id, edited) });
        var player = Mon(proof, "SMEARGLE", edited.Id);
        var opponent = Mon(proof, "BLISSEY", new("pret:MOVE_SPLASH"));
        var entry = SourceWildBattlePolicy.Start(proof, [player], 0, opponent, new(31));
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        var completed = CompleteTurn(proof.Catalog, entry.State!);
        Assert.AreEqual(edited.Pp - 1, completed.State.Get(new(0)).Body!.Pokemon.Moves[0].CurrentPp);
        Assert.IsTrue(completed.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == 0 && e.Move == edited.Id));
    }

    [TestMethod]
    [DataRow("GROWL")]
    [DataRow("STRUGGLE")]
    public void UnsupportedEffectsAndInvalidDefinitionsRemainRejected(string symbol)
    {
        var original = Mechanics.Catalog.Moves[new("pret:MOVE_" + symbol)];
        foreach (var invalid in new[] { original with { Effect = "EFFECT_UNIMPLEMENTED" },
            original with { Pp = 0 }, original with { Target = "MOVE_TARGET_UNIMPLEMENTED" },
            original with { Flags = default } })
        {
            Assert.IsFalse(SourceBattleKernel.SupportsRecipe(invalid));
            Assert.IsFalse(WildBattleRules.IsCalledMoveEffectSupported(invalid));
            var catalog = Mechanics.Catalog with { Moves = Mechanics.Catalog.Moves.SetItem(invalid.Id, invalid) };
            Assert.IsFalse(PokemonMoveRules.IsLearningSupported(catalog, invalid.Id));
        }
        var unknown = original with { Effect = "EFFECT_UNIMPLEMENTED" };
        var proof = SourceMechanicsCatalog.FromDefinitions(Mechanics.Catalog with {
            Moves = Mechanics.Catalog.Moves.SetItem(unknown.Id, unknown) });
        var entry = SourceWildBattlePolicy.Start(proof, [Mon(proof, "SMEARGLE", unknown.Id)], 0,
            Mon(proof, "BLISSEY", new("pret:MOVE_SPLASH")), new(31));
        Assert.IsFalse(entry.Succeeded);
        Assert.AreEqual("source_mechanics_move_not_implemented", entry.Fault);
        Assert.IsNull(entry.State);
        Assert.IsTrue(entry.Events.IsEmpty && entry.Trace.IsEmpty);
    }

    [TestMethod]
    public void AbilityAdmissionMatchesImplementedKernelAndRejectsUnknownSymbols()
    {
        foreach (var ability in Mechanics.Abilities.Keys)
            Assert.IsTrue(SourceBattleKernel.SupportsAbility(ability), ability);
        Assert.IsTrue(SourceBattleKernel.SupportsAbility("ABILITY_NONE"));
        Assert.IsFalse(SourceBattleKernel.SupportsAbility("ABILITY_UNIMPLEMENTED"));
    }

    [TestMethod]
    [DataRow("ITEM_TM10", true)]
    [DataRow("ITEM_TM01", true)]
    [DataRow("ITEM_ORAN_BERRY", true)]
    [DataRow("ITEM_HM01", false)]
    [DataRow("ITEM_POKE_FLUTE", false)]
    [DataRow("ITEM_ENIGMA_BERRY", false)]
    [DataRow("ITEM_UNIMPLEMENTED", false)]
    public void HeldItemsUseRealHoldabilityIncludingOrdinaryMachines(string item, bool accepted)
    {
        var player = Mon(Mechanics, "SMEARGLE", new("pret:MOVE_SPLASH")) with { HeldItem = item };
        var opponent = Mon(Mechanics, "BLISSEY", new("pret:MOVE_SPLASH"));
        var entry = SourceWildBattlePolicy.Start(Mechanics, [player], 0, opponent, new(31));
        Assert.AreEqual(accepted, entry.Succeeded, entry.Fault);
        if (!accepted)
        {
            Assert.IsNull(entry.State);
            Assert.IsTrue(entry.Events.IsEmpty && entry.Trace.IsEmpty);
            return;
        }
        var result = CompleteTurn(Mechanics.Catalog, entry.State!);
        Assert.AreEqual(item, result.State.Get(new(0)).Body!.HeldItem);
    }

    [TestMethod]
    public void OrdinaryWaterRequiresItsExactBoundCandidateAndRejectsMissingOrConflictingOrigin()
    {
        var content = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var owned = content.PokemonContent!.Pokemon;
        var table = content.WaterEncounters!.Values.OrderBy(t => t.Map.Value, StringComparer.Ordinal).First();
        var slot = table.Slots[0];
        var binding = WaterEncounterCatalog.BindBattle(content.WaterEncounters, owned,
            new(table.Map, slot.Index, slot.Species, slot.MinimumLevel));
        var opponent = SourcePokemonFactory.Create(owned.Mechanics, new(slot.Species, slot.MinimumLevel, 42,
            IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var player = Mon(owned.Mechanics, "PIDGEY", new("pret:MOVE_TACKLE"));
        var valid = SourceWildBattlePolicy.StartOwned(owned, [player], 0, new(opponent), new(31),
            new() { OrdinaryWater = binding }, WildBattleTerrain.Water);
        Assert.IsTrue(valid.Succeeded, valid.Fault);
        Assert.AreSame(binding, valid.State!.WildEncounter!.OrdinaryWater);
        Assert.IsTrue(valid.Trace.Any(t => t.Purpose == "wild-battle.held-item"));
        Assert.IsFalse(SourceWildBattlePolicy.StartOwned(owned, [player], 0, new(opponent), new(31)).Succeeded);
        Assert.IsFalse(SourceWildBattlePolicy.StartOwned(owned, [player], 0, new(opponent), new(31),
            new() { OrdinaryWater = binding, FirstSeviiWild = true }).Succeeded);
        Assert.IsFalse(binding.Matches(opponent with { Level = opponent.Level + 1 }));
    }

    [TestMethod]
    [DataRow("Moltres", false)]
    [DataRow("Zapdos", false)]
    [DataRow("Electrode1", true)]
    [DataRow("Electrode2", true)]
    [DataRow("HoOh", false)]
    [DataRow("Lugia", false)]
    [DataRow("Deoxys", false)]
    public void ScriptedEntryPreservesLegendaryDrawSuppressionAndElectrodeDraw(string kind, bool heldDraw)
    {
        var owned = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root)).PokemonContent!.Pokemon;
        PokemonInstance opponent;
        SourceWildEncounterContext context;
        if (kind == "Moltres")
        {
            var definition = SourceScriptedEncounterRegistry.Definitions.Single(d => d.Id == "static.moltres");
            var origin = SourceScriptedEncounterRegistry.Origin(definition);
            opponent = SourceTableWildBattleContract.Generate(owned, origin, definition.Level, new(42), 42).Pokemon;
            context = new() { SourceTable = origin };
        }
        else if (Enum.TryParse<PowerPlantEncounterKind>(kind, out var plantKind))
        {
            var made = PowerPlantPokemonFactory.Create(owned, plantKind, 42, new(42));
            opponent = made.Pokemon;
            context = new() { PowerPlant = made.Origin };
        }
        else
        {
            var made = EventIslandPokemonFactory.Create(owned, Enum.Parse<EventIslandEncounterKind>(kind), 42, new(42));
            opponent = made.Pokemon;
            context = new() { EventIsland = made.Origin };
        }
        var rng = new RngState(31);
        var expected = ImmutableArray.CreateBuilder<RandomTrace>();
        void Draw(string purpose, int bound = 65536)
        {
            var draw = Gen3Rng.Next(rng, purpose, bound);
            rng = draw.State;
            expected.Add(draw.Trace);
        }
        for (int slot = 0; slot < 4; slot++) Draw($"wild-battle.entry.ai-setup.slot-{slot}", 16);
        if (heldDraw) Draw("wild-battle.held-item", 100);
        Draw("wild-battle.entry.random-turn-number");
        var entry = SourceWildBattlePolicy.StartOwned(owned,
            [Mon(owned.Mechanics, "PIDGEY", new("pret:MOVE_TACKLE"))], 0, new(opponent), new(31), context);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.AreEqual("ITEM_NONE", entry.State!.Get(new(1)).Body!.HeldItem);
        Assert.AreEqual(rng, entry.State.Rng);
        CollectionAssert.AreEqual(expected.ToArray(), entry.Trace.ToArray());
    }

    [TestMethod]
    public void MagnitudeKeepsItsProbabilityBucketsAndSingleStrengthDraw()
    {
        var levels = new HashSet<int>();
        for (uint seed = 0; seed < 128; seed++)
        {
            var player = Mon(Mechanics, "SMEARGLE", new("pret:MOVE_MAGNITUDE"));
            var opponent = Mon(Mechanics, "BLISSEY", new("pret:MOVE_SPLASH"));
            var entry = SourceWildBattlePolicy.Start(Mechanics, [player], 0, opponent, new(seed));
            Assert.IsTrue(entry.Succeeded, entry.Fault);
            var result = CompleteTurn(Mechanics.Catalog, entry.State!);
            var magnitude = result.Events.OfType<SourceMagnitudeEvent>().Single();
            int roll = result.Trace.Single(t => t.Purpose.EndsWith(".magnitude", StringComparison.Ordinal)).Result % 100;
            var expected = roll < 5 ? (4, 10) : roll < 15 ? (5, 30) : roll < 35 ? (6, 50) :
                roll < 65 ? (7, 70) : roll < 85 ? (8, 90) : roll < 95 ? (9, 110) : (10, 150);
            Assert.AreEqual(expected, (magnitude.Magnitude, magnitude.Power));
            levels.Add(magnitude.Magnitude);
        }
        Assert.IsTrue(levels.SetEquals(Enumerable.Range(4, 7)), "Exercise every Magnitude bucket.");
    }

    private static WildBattleCombatant Mon(SourceMechanicsCatalog proof, string species, MoveId move)
    {
        var pokemon = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), 50, 42,
            IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        int pp = proof.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static (SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events, ImmutableArray<RandomTrace> Trace)
        CompleteTurn(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        var choices = frame.Slots.Where(c => frame.CanAct(c.Slot)).Select(c => {
            var move = catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0,
                new(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(catalog, frame, frame.Revision, choices);
        var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        for (int step = 0; ; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault);
            events.AddRange(result.Events); trace.AddRange(result.Trace);
            if (result.CompletedCleanTurn || result.State.FourTurn?.Phase == SourceFourActionPhase.BattleEnded)
                return (result.State, events.ToImmutable(), trace.ToImmutable());
            Assert.IsTrue(step < 32, "Bounded single turn.");
            result = SourceFourActionTurns.Advance(catalog, result.State, result.Continuation!.Value);
        }
    }
}
