using System.Collections.Immutable;
namespace Game.Domain.Pokemon;
/// <summary>Ownership/met data around the already validated creature rules; no engine/UI dependencies.</summary>
public sealed record PartyPokemon(long InstanceId,PokemonInstance Pokemon,string Nickname,string OriginalTrainerName,
    int OriginalTrainerGender,int Language,string MetLocation,int MetLevel,int MetGame,string Ball,string HeldItem)
{
    public int Mail{get;init;}=255;
    public uint Status{get;init;}
    public bool IsEgg{get;init;}
}
public sealed record StarterChoiceDefinition(int Index,int SourceSpeciesId,int RivalSourceSpeciesId,string BallId,string BallHideFlag,string RivalBallId,string RivalBallHideFlag);
public sealed record CampaignPokemonContent(PokemonCatalog Catalog,ImmutableSortedDictionary<int,string> SpeciesNames,string NicknameCharacters)
{
    public ImmutableArray<StarterChoiceDefinition> StarterChoices{get;init;}=[];
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowSleepStatus{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowFreezeStatus{get;init;}
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
