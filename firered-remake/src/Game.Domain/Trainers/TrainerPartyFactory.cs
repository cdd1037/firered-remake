using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Bounded CreateNPCTrainerParty / OT_ID_RANDOM_NO_SHINY. No battle execution or IO.</summary>
public static class TrainerPartyFactory
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string RulesVersion = "firered-oaks-lab-trainers-v1";
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
    internal static TrainerPartyCreationResult CreateValidated(TrainerDefinition trainer, PokemonCatalog pokemon, RngState rng)
    {
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unsupported RNG algorithm.");
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
            uint personality = unchecked(((trainer.EncounterMusicGender & 0x80) != 0 ? 0x78u : 0x88u) + (nameHash << 8));
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
            var created = PokemonFactory.Create(pokemon,
                new(member.Species, member.Level, ot, IvPolicy.Fixed(fixedIv), PersonalityPolicy.Fixed(personality)), rng);
            if (created.Rng != rng || !created.Trace.IsEmpty)
                throw new InvalidOperationException("Fixed Pokemon creation unexpectedly consumed RNG.");
            result.Add(new(created.Pokemon, member.HeldItem));
        }
        return new(trainer, result.MoveToImmutable(), rng, trace.ToImmutable());
    }

    public static void ValidateCatalog(TrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster)
            throw new NotSupportedException("Unknown source/profile or unsupported complete-roster claim.");
        if (catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 3 || catalog.Battles.Count != 3)
            throw new ArgumentException("The complete bounded three-trainer catalog is required.");
        foreach (var id in SupportedTrainers)
        {
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.SourceTrainerId != 326 + SupportedTrainers.IndexOf(id))
                throw new ArgumentException("Missing or inconsistent trainer identity.");
            if (trainer.TrainerClass != "TRAINER_CLASS_RIVAL_EARLY" || trainer.SourceTrainerClass != 81 ||
                trainer.TrainerPic != "TRAINER_PIC_RIVAL_EARLY" || trainer.SourceTrainerPic != 106 ||
                trainer.SourceTrainerName != "TERRY" || trainer.EncounterMusicGender != 0 || trainer.DoubleBattle ||
                trainer.PartyFlags != 0 || trainer.AiFlags != 7 || trainer.BaseMoney != 4 ||
                trainer.Items.IsDefault || trainer.Items.Length != 4 || trainer.Items.Any(x => x != "ITEM_NONE") ||
                trainer.AiFlagSymbols.IsDefault || !trainer.AiFlagSymbols.SequenceEqual(new[] {
                    "AI_SCRIPT_CHECK_BAD_MOVE", "AI_SCRIPT_TRY_TO_FAINT", "AI_SCRIPT_CHECK_VIABILITY" }) ||
                !ValidEncoding(trainer.SourceTrainerName, trainer.EncodedTrainerName))
                throw new NotSupportedException("Unsupported or inconsistent lab trainer parameters.");
            if (trainer.Party.IsDefault || trainer.Party.Length != 1 || trainer.Party[0] is not { } member ||
                member.Species.Value != "pret:SPECIES_" + id.Value["pret:TRAINER_RIVAL_OAKS_LAB_".Length..] ||
                member.SpeciesName != id.Value["pret:TRAINER_RIVAL_OAKS_LAB_".Length..] || member.Level != 5 || member.SourceIv != 0 ||
                member.HeldItem != "ITEM_NONE" || member.CustomMoves.IsDefault || !member.CustomMoves.IsEmpty ||
                !ValidEncoding(member.SpeciesName, member.EncodedSpeciesName))
                throw new NotSupportedException("Only the source single level-5, zero-IV, no-item default-move parties are supported.");
            int sourceSpeciesId = member.SpeciesName switch { "BULBASAUR" => 1, "CHARMANDER" => 4, "SQUIRTLE" => 7, _ => -1 };
            if (member.SourceSpeciesId != sourceSpeciesId) throw new ArgumentException("Inconsistent source species ID.");
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new ArgumentException("Missing or inconsistent lab battle parameters.");
            ValidateBattle(battle);
        }
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

    private static bool ValidEncoding(string value, ImmutableArray<int> bytes) =>
        value is not null && !bytes.IsDefaultOrEmpty && value.Length == bytes.Length &&
        value.Select((c, index) => c is >= 'A' and <= 'Z' && bytes[index] == 0xbb + c - 'A').All(x => x);
}
