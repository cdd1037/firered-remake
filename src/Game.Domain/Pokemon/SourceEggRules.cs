using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Pokemon;

public sealed record SourceEggTrainer(uint TrainerId, string Name, int Gender, int Language = 2);
public sealed record SourceEggCreationResult(PartyPokemon Egg, RngState Rng, ImmutableArray<RandomTrace> Trace);
public sealed record SourceEggHatchResult(PartyPokemon Pokemon, RngState Rng, ImmutableArray<RandomTrace> Trace);
public sealed record SourceEggCycleResult(ImmutableArray<PartyPokemon> Party, int? HatchPartyIndex);
public sealed record SourceEggShardResult(RngState Rng, ImmutableArray<RandomTrace> Trace,
    ImmutableArray<int> AnimationIndexes);

/// <summary>Pinned daycare.c egg receipt, party-cycle scan and hatch reconstruction. No allocator,
/// dex, pending-egg flag, party delivery or world mutation. The caller commits those atomically.</summary>
public static class SourceEggRules
{
    public const int HatchLevel = 5;
    public const int ReceiptDrawCount = 9;
    public const int HatchDrawCount = 2;
    public const int NormalShardDrawCount = 19;

    /// <summary>Call only after the party-capacity/confirmation transaction has succeeded.
    /// Uses the stored ushort PID low half; leaves residents and pending state untouched.</summary>
    public static SourceEggCreationResult Receive(CampaignPokemonContent content,
        PokemonBreedingData metadata, FourIslandDayCareState daycare, long nextInstanceId,
        SourceEggTrainer trainer, string metLocation, int metGame, RngState rng)
    {
        ValidateContent(content, metadata);
        ArgumentNullException.ThrowIfNull(daycare);
        ValidateTrainer(trainer, metLocation, metGame);
        if (nextInstanceId <= 0 || nextInstanceId == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(nextInstanceId));
        if (!daycare.HasPendingEgg || daycare.First is null || daycare.Second is null)
            throw new ArgumentException("egg_pending_parents");
        var parents = new[] { daycare.First.Pokemon, daycare.Second.Pokemon };
        foreach (var parent in parents)
        {
            if (PokemonStateValidator.ValidateParty(content, parent) is { } invalid) throw new ArgumentException(invalid);
            if (parent.IsEgg) throw new ArgumentException("egg_parent_is_egg");
            if (parent.InstanceId >= nextInstanceId) throw new ArgumentException("egg_instance_allocator");
        }
        if (parents[0].InstanceId == parents[1].InstanceId) throw new ArgumentException("egg_parent_identity");
        var breeding = metadata.Species.ToImmutableDictionary(p => p.Key, p => p.Value.Breeding);
        if (FourIslandDayCareRules.Compatibility(daycare, breeding) == 0) throw new ArgumentException("egg_incompatible_parents");
        int mother = 0, father = 1;
        for (int i = 0; i < parents.Length; i++)
        {
            if (parents[i].Pokemon.Species == Species("DITTO")) { mother = i ^ 1; father = i; }
            else if (parents[i].Pokemon.Gender == PokemonGender.Female) { mother = i; father = i ^ 1; }
        }
        var species = metadata.Species[parents[mother].Pokemon.Species].BaseEggSpecies;
        if ((daycare.OffspringPersonality & 0x8000) != 0)
        {
            if (species == Species("NIDORAN_F")) species = Species("NIDORAN_M");
            if (species == Species("ILLUMISE")) species = Species("VOLBEAT");
        }
        if (parents[father].Pokemon.Species == Species("DITTO") && parents[mother].Pokemon.Gender != PokemonGender.Female)
            (mother, father) = (father, mother);
        if (species == Species("WYNAUT") && parents.All(p => p.HeldItem != "ITEM_LAX_INCENSE")) species = Species("WOBBUFFET");
        if (species == Species("AZURILL") && parents.All(p => p.HeldItem != "ITEM_SEA_INCENSE")) species = Species("MARILL");
        if (!metadata.Species.TryGetValue(species, out var row) || !content.Catalog.Species.ContainsKey(species))
            throw new NotSupportedException("egg_offspring_not_owned");
        PreflightRng(rng, ReceiptDrawCount);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>(ReceiptDrawCount);
        var high = Gen3Rng.Next(rng, "daycare.egg.personality-high");
        rng = high.State; trace.Add(high.Trace);
        uint personality = daycare.OffspringPersonality | ((uint)high.Value << 16);
        var created = PokemonFactory.Create(content.Catalog, new(species, HatchLevel, trainer.TrainerId,
            IvPolicy.Random, PersonalityPolicy.Fixed(personality)), rng);
        rng = created.Rng; trace.AddRange(created.Trace);
        var basis = created.Pokemon.Ivs;
        int[] available = [0, 1, 2, 3, 4, 5], selected = new int[3], chosenParents = new int[3];
        for (int i = 0; i < selected.Length; i++)
        {
            var draw = Gen3Rng.Next(rng, "daycare.egg.inherit-stat", 6 - i);
            rng = draw.State; trace.Add(draw.Trace);
            selected[i] = available[draw.Trace.Result];
            // Source passes the selected VALUE as an array INDEX. Keep its stale tail and
            // repeated-stat behavior; this is deliberately not RemoveAt(selected position).
            available[selected[i]] = 0xff;
            var copy = available.ToArray();
            int destination = 0;
            foreach (int value in copy) if (value != 0xff) available[destination++] = value;
        }
        for (int i = 0; i < chosenParents.Length; i++)
        {
            var draw = Gen3Rng.Next(rng, "daycare.egg.inherit-parent", 2);
            rng = draw.State; trace.Add(draw.Trace); chosenParents[i] = draw.Trace.Result;
        }
        var ivs = basis.Values.ToArray();
        for (int i = 0; i < selected.Length; i++) ivs[selected[i]] = parents[chosenParents[i]].Pokemon.Ivs.Values[selected[i]];
        var moves = created.Pokemon.Moves;
        void Learn(MoveId move) => moves = PokemonMoveRules.LearnAutomatically(content.Catalog, moves, move);
        foreach (var move in parents[father].Pokemon.Moves)
            if (row.EggMoves.Contains(move.Move)) Learn(move.Move);
        foreach (var move in parents[father].Pokemon.Moves)
            foreach (var machine in metadata.AllMachineMoves)
                if (move.Move == machine && metadata.MachineCompatibility[species].Contains(machine)) Learn(machine);
        foreach (var move in parents[father].Pokemon.Moves)
            if (parents[mother].Pokemon.Moves.Any(m => m.Move == move.Move) &&
                content.Catalog.Species[species].LevelUpMoves.Any(m => m.Move == move.Move)) Learn(move.Move);
        var pokemon = created.Pokemon with
        {
            Ivs = new(ivs[0], ivs[1], ivs[2], ivs[3], ivs[4], ivs[5]),
            EggStatCalculationIvs = basis, Friendship = row.Breeding.EggCycles, Moves = moves
        };
        var egg = new PartyPokemon(nextInstanceId, pokemon, null, trainer.Name, trainer.Gender,
            1, metLocation, 0, metGame, "ITEM_POKE_BALL", "ITEM_NONE") { IsEgg = true };
        if (PokemonStateValidator.ValidateParty(content, egg) is { } error) throw new InvalidOperationException(error);
        return new(egg, rng, trace.ToImmutable());
    }

    /// <summary>Exactly one source hatch-cycle tick, supplied by FourIslandDayCareRules. A newly
    /// decremented zero does not hatch. The first already-zero egg stops the party-order scan.</summary>
    public static SourceEggCycleResult AdvanceHatchCycle(CampaignPokemonContent content,
        ImmutableArray<PartyPokemon> party)
    {
        if (PokemonStateValidator.ValidatePartyCollection(content, party, long.MaxValue) is { } error)
            throw new ArgumentException(error);
        for (int i = 0; i < party.Length; i++)
        {
            if (!party[i].IsEgg) continue;
            if (party[i].Pokemon.Friendship == 0) return new(party, i);
            party = party.SetItem(i, party[i] with { Pokemon = party[i].Pokemon with { Friendship = party[i].Pokemon.Friendship - 1 } });
        }
        return new(party, null);
    }

    /// <summary>CreatedHatchedMon + AddHatchedMonToParty: two discarded IV draws, then restored
    /// inherited IVs/moves and a fresh stat calculation. ScriptHatchMon uses this without shards.
    /// Markings survive hatch; Pokerus remains outside the supported model.</summary>
    public static SourceEggHatchResult Hatch(CampaignPokemonContent content, PartyPokemon egg,
        SourceEggTrainer trainer, string hatchLocation, RngState rng)
    {
        if (PokemonStateValidator.ValidateParty(content, egg) is { } error) throw new ArgumentException(error);
        if (!egg.IsEgg) throw new ArgumentException("hatch_not_egg");
        ValidateTrainer(trainer, hatchLocation, egg.MetGame);
        PreflightRng(rng, HatchDrawCount);
        var old = egg.Pokemon;
        var created = PokemonFactory.Create(content.Catalog, new(old.Species, HatchLevel, trainer.TrainerId,
            IvPolicy.Random, PersonalityPolicy.Fixed(old.Personality)), rng);
        var stats = PokemonStatCalculation.Calculate(content.Catalog.Species[old.Species], HatchLevel,
            old.Nature, old.Ivs, SixStats.Zero, content.Catalog.MechanicsData);
        var moves = old.Moves.Select(m => new PokemonMoveSlot(m.Move, content.Catalog.Moves[m.Move].Pp,
            content.Catalog.Moves[m.Move].Pp)).ToImmutableArray();
        var pokemon = created.Pokemon with { Ivs = old.Ivs, Stats = stats, CurrentHp = stats.MaxHp, Moves = moves, Friendship = 120 };
        var hatched = new PartyPokemon(egg.InstanceId, pokemon, null,
            trainer.Name, trainer.Gender, trainer.Language, hatchLocation, 0, egg.MetGame, "ITEM_POKE_BALL", "ITEM_NONE")
        { ModernFatefulEncounter = egg.ModernFatefulEncounter, Markings = egg.Markings };
        if (PokemonStateValidator.ValidateParty(content, hatched) is { } invalid) throw new InvalidOperationException(invalid);
        return new(hatched, created.Rng, created.Trace);
    }

    /// <summary>Normal EggHatch consumes these 19 draws after Hatch. Logical 1+2+16 shard order
    /// is fixed; rendering, frame rate, skipping and headless mode must not call Random themselves.</summary>
    public static SourceEggShardResult CreateNormalHatchShards(RngState rng)
    {
        PreflightRng(rng, NormalShardDrawCount);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>(NormalShardDrawCount);
        var animations = ImmutableArray.CreateBuilder<int>(NormalShardDrawCount);
        for (int i = 0; i < NormalShardDrawCount; i++)
        {
            var draw = Gen3Rng.Next(rng, "daycare.hatch.shard-animation", 4);
            rng = draw.State; trace.Add(draw.Trace); animations.Add(draw.Trace.Result);
        }
        return new(rng, trace.ToImmutable(), animations.ToImmutable());
    }

    private static SpeciesId Species(string symbol) => new("pret:SPECIES_" + symbol);
    private static void ValidateContent(CampaignPokemonContent content, PokemonBreedingData metadata)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!ReferenceEquals(content.Catalog.BreedingData, metadata))
            throw new ArgumentException("egg_content_identity");
        if (PokemonStateValidator.ValidatePartyCollection(content, [], 1) is { } error) throw new ArgumentException(error);
    }
    private static void ValidateTrainer(SourceEggTrainer trainer, string location, int metGame)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        if (!NameTextRules.IsValidTrainerName(trainer.Name) || trainer.Gender is not (0 or 1) ||
            trainer.Language is not (1 or 2 or 3 or 4 or 5 or 7)) throw new ArgumentException("egg_trainer");
        if (location is null || location.Length > 128 || !location.StartsWith("MAPSEC_", StringComparison.Ordinal) ||
            location.Length <= 7 || location[7..].Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c) && c != '_'))
            throw new ArgumentException("egg_met_location");
        if (metGame is not (1 or 2 or 3 or 4 or 5 or 7 or 8 or 10 or 11 or 12 or 15)) throw new ArgumentException("egg_met_game");
    }
    private static void PreflightRng(RngState rng, int count)
    {
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unsupported RNG algorithm.");
        if (rng.Cursor > ulong.MaxValue - (ulong)count) throw new OverflowException("RNG cursor cannot accommodate egg operation.");
    }
}
