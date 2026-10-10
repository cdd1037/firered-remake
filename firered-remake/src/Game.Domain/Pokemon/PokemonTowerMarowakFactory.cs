using System.Collections.Immutable;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Pokemon;
/// <summary>Revealed scripted mother only. Does not start a battle or assign wild held items.</summary>
public static class PokemonTowerMarowakFactory
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public static PokemonCreationResult Create(PokemonCatalog catalog, uint originalTrainerId, RngState rng)
    {
        PokemonFactory.ValidateCatalog(catalog);
        if (catalog.Species.Count != 101) throw new NotSupportedException("pokemon_tower_catalog_required");
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        ushort Draw(string purpose) { var draw = Gen3Rng.Next(rng, purpose); rng = draw.State; trace.Add(draw.Trace); return draw.Value; }
        uint personality;
        // CreateMonWithGenderNatureLetter, letter=0: draw Random32 until female/Serious match.
        do { personality = (uint)Draw("tower.marowak.personality.low16") | ((uint)Draw("tower.marowak.personality.high16") << 16); }
        while (personality % 25 != 12 || PokemonFactory.GenderFromPersonality(127, personality) != PokemonGender.Female);
        var made = PokemonFactory.Create(catalog, new(new("pret:SPECIES_MAROWAK"), 30, originalTrainerId,
            IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), rng);
        return new(made.Pokemon, made.Rng, trace.ToImmutable());
    }
}
