using Game.Domain.Ids;
namespace Game.Domain.Pokemon;
/// <summary>Compatibility facade for the Route 12 record; sizing is shared by all species.</summary>
public static class MagikarpSizeRecordRules
{
    public const string SourceCommit = PokemonSizeRecordRules.SourceCommit;
    public const string SourcePath = PokemonSizeRecordRules.SourcePath;
    public const string ScriptPath = "data/maps/Route12_FishingHouse/scripts.inc";
    public const ushort DefaultRecord = 0;
    public const int PartyCapacity = 6;
    public const int MagikarpHeightDecimeters = 9;
    public static SpeciesId MagikarpId { get; } = new("pret:SPECIES_MAGIKARP");
    public static ushort Hash(uint personality, SixStats ivs) => PokemonSizeRecordRules.Hash(personality, ivs);
    public static uint SizeTenthsCentimeters(ushort record) => PokemonSizeRecordRules.SizeTenthsCentimeters(record, MagikarpHeightDecimeters);
    public static string FormatInches(uint size) => PokemonSizeRecordRules.FormatInches(size);
    public static string FormatCentimeters(uint size) => PokemonSizeRecordRules.FormatCentimeters(size);
    public static PokemonSizeComparison Compare(int index, PartyPokemon? mon, ushort record) => PokemonSizeRecordRules.Compare(index, mon, record, MagikarpId, MagikarpHeightDecimeters);
    public static PokemonRecordScriptDecision Evaluate(int index, PartyPokemon? mon, ushort record, bool set) => PokemonSizeRecordRules.Evaluate(index, mon, record, set, MagikarpId, MagikarpHeightDecimeters);
}
