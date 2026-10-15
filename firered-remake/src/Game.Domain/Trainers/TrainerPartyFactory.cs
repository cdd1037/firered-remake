using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Bounded CreateNPCTrainerParty / OT_ID_RANDOM_NO_SHINY. No battle execution or IO.</summary>
public static class TrainerPartyFactory
{
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        new[] { "SQUIRTLE", "BULBASAUR", "CHARMANDER" }
            .Select(x => new TrainerId("pret:TRAINER_RIVAL_OAKS_LAB_" + x)).ToImmutableArray();

    public static TrainerPartyCreationResult Create(TrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {
        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Only the three Oak's lab trainers are supported.");
        return CreateValidated(trainer, pokemon, rng);
    }

    // Both bounded trainer profiles validate their own roster before reaching this unchanged
    // source construction path. Sharing it does not widen the public lab catalog contract.
    internal static TrainerPartyCreationResult CreateValidated(TrainerDefinition trainer, PokemonCatalog pokemon, RngState rng, SourceMechanicsCatalog? source = null)
    {
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unsupported RNG algorithm.");
        if (source is not null && !ReferenceEquals(source.Catalog, pokemon)) throw new ArgumentException("Source mechanics proof belongs to a different catalog.");
        foreach (var member in trainer.Party)
        {
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || species.SourceSpeciesId != member.SourceSpeciesId)
                throw new ArgumentException("Trainer party refers to missing or inconsistent Pokemon content.");
        }
        if (rng.Cursor > ulong.MaxValue - (ulong)trainer.Party.Length * 2)
            throw new OverflowException("RNG cursor cannot accommodate the minimum trainer OT draws.");

        var result = ImmutableArray.CreateBuilder<TrainerPartyPokemon>(trainer.Party.Length);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        uint nameHash = 0; // The source deliberately accumulates across party members, rather than resetting here.
        for (int index = 0; index < trainer.Party.Length; index++)
        {
            var member = trainer.Party[index];
            foreach (int value in trainer.EncodedTrainerName) nameHash = unchecked(nameHash + (uint)value);
            foreach (int value in member.EncodedSpeciesName) nameHash = unchecked(nameHash + (uint)value);
            uint personality = unchecked((trainer.DoubleBattle ? 0x80u : (trainer.EncounterMusicGender & 0x80) != 0 ? 0x78u : 0x88u) + (nameHash << 8));
            int fixedIv = member.SourceIv * 31 / 255;
            uint ot;
            ulong attempt = 0;
            do
            {
                // Rejection sampling has no fixed draw count. Failure returns no partial state to the caller.
                if (rng.Cursor > ulong.MaxValue - 2)
                    throw new OverflowException("RNG cursor exhausted during no-shiny OT rejection sampling.");
                var low = Gen3Rng.Next(rng, $"trainer.party.{index}.ot.attempt.{attempt}.low16");
                var high = Gen3Rng.Next(low.State, $"trainer.party.{index}.ot.attempt.{attempt}.high16");
                rng = high.State;
                trace.Add(low.Trace); trace.Add(high.Trace);
                ot = (uint)low.Value | ((uint)high.Value << 16);
                attempt++;
            } while (((ot >> 16) ^ (ot & 0xffff) ^ (personality >> 16) ^ (personality & 0xffff)) < 8);

            // Fixed/fixed creation consumes no more draws. Initial moves, PP, nature, gender and stats
            // intentionally reuse the already-source-verified Pokemon factory instead of duplicating it.
            var request = new CreatePokemonRequest(member.Species, member.Level, ot, IvPolicy.Fixed(fixedIv), PersonalityPolicy.Fixed(personality));
            var created = source is null ? PokemonFactory.Create(pokemon, request, rng) : SourcePokemonFactory.Create(source, request, rng);
            if (created.Rng != rng || !created.Trace.IsEmpty)
                throw new InvalidOperationException("Fixed Pokemon creation unexpectedly consumed RNG.");
            result.Add(new(created.Pokemon, member.HeldItem));
        }
        var createdParty = new TrainerPartyCreationResult(trainer, result.MoveToImmutable(), rng, trace.ToImmutable());
        return createdParty with { Roster = TrainerEncounterRoster.Create(pokemon, createdParty) };
    }

    public static void ValidateCatalog(TrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(EarlyRivalBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (!SupportedTrainers.Contains(battle.Trainer) || battle.Mode != 9 || battle.RivalFlags != 3 ||
            battle.BattleTypeFlags != 24 || battle.EntryScript != "EventScript_DoNoIntroTrainerBattle" ||
            battle.Callback != "CB2_EndTrainerBattle" ||
            battle.DefeatText != "PalletTown_ProfessorOaksLab_Text_RivalDefeat" || battle.VictoryText != "Text_RivalVictory" ||
            battle.PostBattleScript != "PalletTown_ProfessorOaksLab_EventScript_EndRivalBattle")
            throw new NotSupportedException("Only the source tutorial/heal Oak's lab early-rival profile is supported.");
    }

}
