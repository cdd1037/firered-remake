using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

public sealed record SourceEasyChatWord(ushort Value, int Group, int Index, string Symbol, string Text,
    bool Enabled);
public sealed record SourceEasyChatGroup(int Id, string Symbol, string Name, ImmutableArray<ushort> WordValues);
/// <summary>Each entry retains ordered alternatives. The original picks the first unlocked spelling,
/// suppressing duplicate Pokémon names across the two source groups.</summary>
public sealed record SourceEasyChatAlphabeticalGroup(int Id, string Name,
    ImmutableArray<ImmutableArray<ushort>> Alternatives);
public sealed record SourceEasyChatCatalog(ImmutableArray<SourceEasyChatGroup> Groups,
    ImmutableDictionary<ushort, SourceEasyChatWord> Words, ImmutableArray<SourceEasyChatAlphabeticalGroup> Alphabetical,
    ImmutableDictionary<string, byte> NameCharacters, int SeenPokemonGroup, int NationalPokemonGroup,
    int DeoxysSpecies, int UnownSpecies)
{
    public ImmutableDictionary<string, string> Texts { get; init; } = ImmutableDictionary<string, string>.Empty;
}
/// <summary>SeenSpeciesIds contains original internal SPECIES_* numbers, not National Dex numbers.</summary>
public sealed record SourceEasyChatSelection(bool GameClear, bool NationalDex, ImmutableHashSet<int> SeenSpeciesIds);
public sealed record SourceMailAuthor(string Name, ImmutableArray<byte> NameBytes, ImmutableArray<byte> TrainerIdBytes);

/// <summary>Original selection rules from easy_chat.c. Selection unlocks are intentionally separate
/// from persisted/received message validity. FireRed swaps the Pokémon group numeric constants.</summary>
public static class SourceEasyChatRules
{
    public const ushort BlankWord = ushort.MaxValue;
    public static ImmutableArray<ushort> BlankWords { get; } = Enumerable.Repeat(BlankWord, 9).ToImmutableArray();

    public static ImmutableArray<SourceEasyChatGroup> SelectableGroups(SourceEasyChatCatalog catalog, SourceEasyChatSelection selection)
    {
        var groups = ImmutableArray.CreateBuilder<SourceEasyChatGroup>();
        if (selection.SeenSpeciesIds.Count != 0) groups.Add(catalog.Groups[catalog.SeenPokemonGroup]);
        for (int id = 1; id <= 16; id++) groups.Add(catalog.Groups[id]);
        if (selection.GameClear) for (int id = 17; id <= 19; id++) groups.Add(catalog.Groups[id]);
        if (selection.NationalDex) groups.Add(catalog.Groups[catalog.NationalPokemonGroup]);
        return groups.ToImmutable();
    }

    public static bool IsKnownWord(SourceEasyChatCatalog catalog, ushort value) => value == BlankWord || catalog.Words.ContainsKey(value);
    public static string WordText(SourceEasyChatCatalog catalog, ushort value) => value == BlankWord ? "" :
        catalog.Words.TryGetValue(value, out var word) ? word.Text : "???";

    public static bool IsSelectable(SourceEasyChatCatalog catalog, SourceEasyChatSelection selection, ushort value)
    {
        if (!catalog.Words.TryGetValue(value, out var word)) return false;
        if (word.Group == catalog.SeenPokemonGroup) return selection.SeenSpeciesIds.Contains(word.Index);
        if (word.Group == catalog.NationalPokemonGroup)
            return selection.NationalDex && (word.Index != catalog.DeoxysSpecies || selection.SeenSpeciesIds.Contains(word.Index));
        if (word.Group is >= 1 and <= 16) return word.Enabled;
        return word.Group is >= 17 and <= 19 && selection.GameClear && word.Enabled;
    }

    public static ImmutableArray<SourceEasyChatWord> SelectableWords(SourceEasyChatCatalog catalog,
        SourceEasyChatSelection selection, int groupId) => groupId < 0 || groupId >= catalog.Groups.Length ? [] :
        catalog.Groups[groupId].WordValues.Where(v => IsSelectable(catalog, selection, v)).Select(v => catalog.Words[v]).ToImmutableArray();

    public static ImmutableArray<SourceEasyChatWord> AlphabeticalWords(SourceEasyChatCatalog catalog,
        SourceEasyChatSelection selection, int bucket)
    {
        if (bucket < 0 || bucket >= catalog.Alphabetical.Length) return [];
        var words = ImmutableArray.CreateBuilder<SourceEasyChatWord>();
        foreach (var alternatives in catalog.Alphabetical[bucket].Alternatives)
            foreach (var value in alternatives)
                if (IsSelectable(catalog, selection, value)) { words.Add(catalog.Words[value]); break; }
        return words.ToImmutable();
    }

    public static string? ValidateWords(SourceEasyChatCatalog catalog, ImmutableArray<ushort> words, bool requireNonempty = false)
    {
        if (words.IsDefault || words.Length != 9) return "mail_word_count";
        if (words.Any(w => !IsKnownWord(catalog, w))) return "mail_unknown_word";
        return requireNonempty && words.All(w => w == BlankWord) ? "mail_message_empty" : null;
    }
}
