using System.Collections.Immutable;
namespace Game.Domain.Pokemon;
/// <summary>Ownership/met data around the already validated creature rules; no engine/UI dependencies.
/// A null CustomNickname uses the current species display name; non-null always means a literal name.
/// Eggs carry no custom nickname and are presented from their explicit IsEgg state.</summary>
public sealed record PartyPokemon(long InstanceId,PokemonInstance Pokemon,
    [property: System.Text.Json.Serialization.JsonRequired] string? CustomNickname,string OriginalTrainerName,
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
    public CampaignPokemonContent(GamePokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        Pokemon = pokemon;
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
    public static string? Validate(string input)
    {
        if (input is null || !NameTextRules.IsWithinLength(input, NameTextRules.NicknameLength)) return "nickname_length";
        if (!NameTextRules.HasValidCharacters(input)) return "nickname_character_unsupported";
        return null;
    }
    // Null means the current species label. A stored custom name is always a nonblank literal.
    public static bool IsValidCustomNickname(string? value) => value is null ||
        NameTextRules.IsValidName(value, NameTextRules.NicknameLength);

    // The input buffer starts empty. Blank input preserves nullable naming intent; every
    // nonblank submission is literal, even when it equals the current species display name.
    public static string? Apply(string? originalCustomNickname, string input)
    {
        var error = Validate(input); if (error is not null) throw new ArgumentException(error);
        return string.IsNullOrWhiteSpace(input) ? originalCustomNickname : input;
    }
}
