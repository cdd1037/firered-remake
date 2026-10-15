using System.Collections.Immutable;
namespace Game.Domain.Pokemon;
/// <summary>Ownership/met data around the already validated creature rules; no engine/UI dependencies.</summary>
public sealed record PartyPokemon(long InstanceId,PokemonInstance Pokemon,string Nickname,string OriginalTrainerName,
    int OriginalTrainerGender,int Language,string MetLocation,int MetLevel,int MetGame,string Ball,string HeldItem)
{
    // Acquisition metadata only; obedience never reads this flag.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ModernFatefulEncounter{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ChampionRibbon{get;init;}
    // Original PC marking bits: circle, square, triangle, heart. New creatures start unmarked.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int Markings{get;init;}
    public int Mail{get;init;}=255;
    public uint Status{get;init;}
    public bool IsEgg{get;init;}
}
public sealed record StarterChoiceDefinition(int Index,int SourceSpeciesId,int RivalSourceSpeciesId,string BallId,string BallHideFlag,string RivalBallId,string RivalBallHideFlag);
public sealed record CampaignPokemonContent
{
    public GamePokemonCatalog Pokemon { get; init; }
    public ImmutableSortedDictionary<int, string> SpeciesNames { get; init; }
    public string NicknameCharacters { get; init; }
    public CampaignPokemonContent(GamePokemonCatalog pokemon, ImmutableSortedDictionary<int, string> speciesNames, string nicknameCharacters)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        if (nicknameCharacters is null || nicknameCharacters.Length != PokemonStateValidator.NicknameAlphabet.Length ||
            !nicknameCharacters.ToHashSet().SetEquals(PokemonStateValidator.NicknameAlphabet))
            throw new ArgumentException("pokemon_nickname_alphabet");
        if (speciesNames is null || speciesNames.Count != pokemon.Catalog.Species.Count ||
            pokemon.Catalog.Species.Values.Any(s => !speciesNames.TryGetValue(s.SourceSpeciesId, out var name) ||
                string.IsNullOrWhiteSpace(name) || name.Length > 10 || name.Any(c => !nicknameCharacters.Contains(c))))
            throw new ArgumentException("pokemon_species_names");
        Pokemon = pokemon; SpeciesNames = speciesNames; NicknameCharacters = nicknameCharacters;
    }
    public PokemonCatalog Catalog => Pokemon.Catalog;
    public ImmutableArray<StarterChoiceDefinition> StarterChoices{get;init;}=[];
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowSleepStatus{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowFreezeStatus{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowToxicStatus{get;init;}
}
public static class PokemonNicknameRules
{
    public static string? Validate(string input,string allowedCharacters)
    {
        if(input is null||input.Length>10)return "nickname_length";
        if(input.Any(c=>!allowedCharacters.Contains(c,StringComparison.Ordinal)))return "nickname_character_unsupported";
        return null;
    }
    // The original input buffer starts empty. An all-space or empty submission restores its backup.
    public static string Apply(string original,string input,string allowedCharacters)
    {
        var error=Validate(input,allowedCharacters);if(error is not null)throw new ArgumentException(error);
        return input.All(c=>c==' ')?original:input;
    }
}
