using System.Collections.Immutable;
using Game.Content;
using FireRed.Tests.Battle.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;

namespace FireRed.Tests.Battle.TrainerPolicy;

[TestClass]
public sealed class SharedTrainerConstructionTests
{
    private static GamePokemonCatalog Owned => RegionalWildDefinitionTests.Owned;

    [TestMethod]
    [DataRow(0, false, 0)]
    [DataRow(1, false, 0)]
    [DataRow(2, false, 0)]
    [DataRow(3, false, 128)]
    [DataRow(3, true, 128)]
    public void PartyFormatsPreserveAccumulatedNameHashOtRejectionAndMoveSlots(int flags, bool doubles, int gender)
    {
        var species = Owned.Catalog.Species[new("pret:SPECIES_BULBASAUR")];
        var template = SharedTrainerCatalog.LoadProject(TestAssets.Root).Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        var member = template.Party[0] with { Species = species.Id, SourceSpeciesId = species.SourceSpeciesId,
            EncodedSpeciesName = [4, 5, 6], Level = 20, SourceIv = 100,
            HeldItem = (flags & 2) != 0 ? "ITEM_ORAN_BERRY" : "ITEM_NONE",
            CustomMoves = (flags & 1) != 0 ? [new("pret:MOVE_GROWL"), new("pret:MOVE_TACKLE"), new("pret:MOVE_NONE"), new("pret:MOVE_NONE")] : [] };
        var trainer = template with { PartyFlags = flags, DoubleBattle = doubles, EncounterMusicGender = gender,
            EncodedTrainerName = [1, 2, 3], Party = [member, member] };
        var definitions = ImmutableDictionary<TrainerId, TrainerDefinition>.Empty.Add(trainer.Id, trainer);
        // This independent seed produces a rejected shiny OT on the first male single draw.
        var initial = new RngState(3320, 19);
        var made = TrainerPartyFactory.Create(definitions, Owned.Opponents, trainer.Id, initial);
        uint seed = initial.Seed; ulong cursor = initial.Cursor;
        var expectedTrace = new List<RandomTrace>();
        uint hash = 0;
        for (int i = 0; i < 2; i++)
        {
            hash += 21;
            uint personality = (doubles ? 0x80u : gender == 128 ? 0x78u : 0x88u) + (hash << 8);
            uint ot; int attempt = 0;
            do
            {
                ushort Draw(string half)
                {
                    uint before = seed;
                    seed = unchecked(seed * 1103515245u + 24691u);
                    ushort raw = (ushort)(seed >> 16);
                    expectedTrace.Add(new(++cursor, $"trainer.party.{i}.ot.attempt.{attempt}.{half}", before, seed, raw, 65536, raw));
                    return raw;
                }
                uint low = Draw("low16"); uint high = Draw("high16");
                ot = low | (high << 16); attempt++;
            } while (((ot >> 16) ^ (ot & 65535) ^ (personality >> 16) ^ (personality & 65535)) < 8);
            var pokemon = made.Party[i].Pokemon;
            Assert.AreEqual(personality, pokemon.Personality);
            Assert.AreEqual(ot, pokemon.OriginalTrainerId);
            Assert.IsTrue(pokemon.Ivs.Values.All(value => value == 100 * 31 / 255));
            Assert.AreEqual(member.HeldItem, made.Party[i].HeldItem);
            var moves = (flags & 1) != 0 ? member.CustomMoves.Take(2).ToArray() :
                PokemonMoveRules.InitialMoves(Owned.Catalog, species, 20).Select(slot => slot.Move).ToArray();
            CollectionAssert.AreEqual(moves, pokemon.Moves.Select(slot => slot.Move).ToArray());
            Assert.IsTrue(pokemon.Moves.All(slot => slot.PpUps == 0 && slot.CurrentPp == slot.MaximumPp &&
                slot.MaximumPp == Owned.Catalog.Moves[slot.Move].Pp));
        }
        Assert.AreEqual(new RngState(seed, cursor), made.Rng);
        CollectionAssert.AreEqual(expectedTrace.ToArray(), made.Trace.ToArray());
        Assert.IsNotNull(made.Roster);
        CollectionAssert.AreEqual(made.Party.ToArray(), made.Roster.Party.ToArray());
        if (!doubles && gender == 0) Assert.IsTrue(made.Trace.Length > 4, "The fixture must exercise shiny OT rejection.");
    }

    [TestMethod]
    public void RepeatedScriptOccurrencesKeepDistinctSourceLines()
    {
        var catalog = PretFirstSeviiRules.LoadTrainers(TestAssets.Root);
        var repeats = catalog.BattlesBySource.Values.GroupBy(battle => battle.ScriptEntry)
            .Where(group => group.Count() > 1).ToArray();
        Assert.IsTrue(repeats.Length > 0);
        foreach (var group in repeats)
        foreach (var battle in group)
            Assert.AreSame(battle, catalog.Battle(battle.ScriptEntry, battle.Source.StartLine));
        Assert.Throws<NotSupportedException>(() => catalog.Battle(repeats[0].Key, -1));
    }
}
