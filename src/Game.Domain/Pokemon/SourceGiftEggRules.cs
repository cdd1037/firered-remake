using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Pokemon;

/// <summary>Pinned ScriptGiveEgg/CreateEgg(TRUE), without party delivery, flags, dex or allocator mutation.</summary>
public static class SourceGiftEggRules
{
    public const string MetLocation = "METLOC_SPECIAL_EGG";
    public const int MetLocationSourceId = 0xfd;
    public const int CreationDrawCount = 4;
    public static SpeciesId Togepi { get; } = new("pret:SPECIES_TOGEPI");

    /// <summary>The caller must first pass the original script's capacity check and commit the
    /// returned Egg, RNG and allocator together. The only original admitted gift is Togepi.</summary>
    public static SourceEggCreationResult GiveEgg(CampaignPokemonContent content,
        PokemonBreedingData metadata, SpeciesId species, long nextInstanceId,
        SourceEggTrainer trainer, int metGame, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(trainer);
        if (!ReferenceEquals(content.Catalog.BreedingData, metadata) || !content.Catalog.Species.ContainsKey(Togepi))
            throw new ArgumentException("gift_egg_content_identity");
        if (PokemonStateValidator.ValidatePartyCollection(content, [], 1) is { } error) throw new ArgumentException(error);
        if (species != Togepi || !metadata.Species.TryGetValue(species, out var row) || row.Breeding.EggCycles != 10)
            throw new ArgumentException("gift_egg_species");
        if (nextInstanceId <= 0 || nextInstanceId == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(nextInstanceId));
        if (!NameTextRules.IsValidTrainerName(trainer.Name) || trainer.Gender is not (0 or 1) ||
            trainer.Language is not (1 or 2 or 3 or 4 or 5 or 7)) throw new ArgumentException("gift_egg_trainer");
        if (metGame is not (1 or 2 or 3 or 4 or 5 or 7 or 8 or 10 or 11 or 12 or 15))
            throw new ArgumentException("gift_egg_met_game");
        var result = PokemonFactory.Create(content.Catalog,
            new(species, SourceEggRules.HatchLevel, trainer.TrainerId, IvPolicy.Random, PersonalityPolicy.Random), rng);
        var pokemon = result.Pokemon with { Friendship = row.Breeding.EggCycles, EggStatCalculationIvs = result.Pokemon.Ivs };
        var egg = new PartyPokemon(nextInstanceId, pokemon, null,
            trainer.Name, trainer.Gender, 1, MetLocation, 0, metGame, "ITEM_POKE_BALL", "ITEM_NONE") { IsEgg = true };
        if (PokemonStateValidator.ValidateParty(content, egg) is { } invalid) throw new InvalidOperationException(invalid);
        return new(egg, result.Rng, result.Trace);
    }

    /// <summary>GetLeadMonIndex selects the first non-Egg even when fainted; source fallback is slot zero.
    /// Empty source slots have friendship zero. Returns the original seven values, zero through six.</summary>
    public static int GetLeadMonFriendship(CampaignPokemonContent content, ImmutableArray<PartyPokemon> party)
    {
        ValidateParty(content, party);
        var lead = party.FirstOrDefault(p => !p.IsEgg) ?? party.FirstOrDefault();
        int friendship = lead?.Pokemon.Friendship ?? 0;
        return friendship == 255 ? 6 : friendship >= 200 ? 5 : friendship >= 150 ? 4 :
            friendship >= 100 ? 3 : friendship >= 50 ? 2 : friendship > 0 ? 1 : 0;
    }

    /// <summary>MON_DATA_SPECIES_OR_EGG hides an Egg's species. Compare the complete uint trainer ID;
    /// source does not compare OT name/gender or exclude fainted party members.</summary>
    public static bool PlayerPartyContainsSpeciesWithPlayerId(CampaignPokemonContent content,
        ImmutableArray<PartyPokemon> party, SpeciesId species, uint trainerId)
    {
        ValidateParty(content, party);
        if (!content.Catalog.Species.ContainsKey(species)) throw new ArgumentException("gift_party_species");
        return party.Any(p => !p.IsEgg && p.Pokemon.Species == species && p.Pokemon.OriginalTrainerId == trainerId);
    }

    internal static bool AdmitsSpecialOrigin(PokemonCatalog catalog, PartyPokemon pokemon) =>
        pokemon.IsEgg && pokemon.Pokemon.Species == Togepi &&
        pokemon.Pokemon.EggStatCalculationIvs == pokemon.Pokemon.Ivs &&
        catalog.BreedingData?.Species.ContainsKey(Togepi) == true;

    private static void ValidateParty(CampaignPokemonContent content, ImmutableArray<PartyPokemon> party)
    {
        if (PokemonStateValidator.ValidatePartyCollection(content, party, long.MaxValue) is { } error)
            throw new ArgumentException(error);
    }
}
