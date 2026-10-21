using System.Reflection;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.WildBattles;

[TestClass]
public sealed class SafariGroundEntryOrderTests
{
    // Controlled pending candidates exercise the actual Application entry, not a natural walk.
    [TestMethod]
    [DataRow(1u)]
    [DataRow(42u)]
    [DataRow(1234u)]
    public void GroundEntryKeepsGenerationFourSetupHeldAndTurnDrawOrder(uint seed)
    {
        var content = CurrentCampaignFixture.Catalog;
        var ownership = content.GamePokemon!;
        var session = CampaignSession.CreateCurrent(content, new(42));
        var fresh = session.State;
        var player = SourcePokemonFactory.Create(ownership.Mechanics,
            new(new("pret:SPECIES_BULBASAUR"), 5, fresh.TrainerId, IvPolicy.Random, PersonalityPolicy.Random), new(42));
        // Diagnostic state only: no player checkpoint, historical receipt or save is fabricated.
        var basis = fresh with { NextPokemonId = 2,
            Party = [new(1, player.Pokemon, null, fresh.PlayerName, fresh.PlayerGender, 2,
                "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE")] };
        var map = new MapId("map.safari-zone-center");
        var table = content.LandEncounters![map];
        var enter = typeof(CampaignSession).GetMethod("GenerateSafariBattle", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var slot in table.Slots)
        {
            var rng = new RngState(seed, 19);
            var encounter = new EarlyLandEncounterState(new(123),
                Pending: new(new(map, slot.Index, slot.Species, slot.MinimumLevel), EarlyLandEncounterRules.TallGrassBehavior),
                RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
            var state = basis with
            {
                World = basis.World with { Player = basis.World.Player with { MapId = map }, Rng = rng },
                Script = basis.Script with { Flags = basis.Script.Flags.SetItem("FLAG_SYS_SAFARI_MODE", true) },
                SafariVisit = new(30, 600, true),
                Exploration = basis.Exploration! with { EarlyEncounter = encounter, Captured = [] }
            };
            var made = SevenIslandWildPokemonFactory.Generate(ownership, table, encounter, rng, state.TrainerId);
            var trace = made.RandomTrace.ToBuilder();
            var expected = made.GlobalRng;
            for (int index = 0; index < 4; index++)
            {
                var draw = Gen3Rng.Next(expected, "safari.entry.ai-setup.slot-" + index, 16);
                expected = draw.State; trace.Add(draw.Trace);
            }
            var held = Gen3Rng.Next(expected, "wild-battle.held-item", 100);
            trace.Add(held.Trace);
            var turn = Gen3Rng.Next(held.State, "safari:turn-random");
            trace.Add(turn.Trace);
            var result = (CampaignState)enter.Invoke(session, [state])!;
            CollectionAssert.AreEqual(trace.ToArray(), result.SafariBattle!.RandomTrace.ToArray());
            Assert.AreEqual(turn.State, result.World.Rng);
            Assert.IsNull(result.Exploration!.EarlyEncounter!.Pending);
            Assert.IsNull(result.WildBattle);
            // Explicit Safari source item slots, independent of the helper under test.
            var item = slot.Species.Value switch
            {
                "pret:SPECIES_PARAS" or "pret:SPECIES_PARASECT" when held.Trace.Result >= 95 => "ITEM_BIG_MUSHROOM",
                "pret:SPECIES_PARAS" or "pret:SPECIES_PARASECT" when held.Trace.Result >= 45 => "ITEM_TINY_MUSHROOM",
                "pret:SPECIES_CHANSEY" when held.Trace.Result >= 95 => "ITEM_LUCKY_EGG",
                "pret:SPECIES_VENOMOTH" when held.Trace.Result >= 95 => "ITEM_SILVER_POWDER",
                _ => "ITEM_NONE"
            };
            Assert.AreEqual(item, result.SafariBattle.HeldItem);
        }
    }
}
