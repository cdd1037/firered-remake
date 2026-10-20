using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Complete Easy Chat vocabulary and source-owned Mail/profile editor templates.
/// Selection availability is applied by Domain against live progression, never during source loading.</summary>
public sealed class PretMailComposer
{
    public const string ResourceId = "firered-mail-composer-v1";
    public SourceEasyChatCatalog Catalog { get; }
    public ImmutableDictionary<string, string> Texts { get; }

    private PretMailComposer(SourceEasyChatCatalog catalog, ImmutableArray<ComposerText> texts)
    {
        Catalog = catalog; Texts = texts.ToImmutableDictionary(t => t.Symbol, t => t.Text, StringComparer.Ordinal);

    }

    public static PretMailComposer Load(string root, SourceTextPack? pack = null)
    {
        var source = new SilphSourcePackage(root,"data/services/mail-composer");
        var document = SafariSource.Parse<ComposerDocument>(source.Read("catalog.json"));
        var contracts = SafariSource.Parse<ComposerContracts>(source.Read("contracts.json"));
        Need(document.Groups.Length == 22 && !document.Words.IsDefaultOrEmpty && document.Alphabetical.Length == 27 &&
            document.Groups.Select(g => g.Id).SequenceEqual(Enumerable.Range(0, 22)) &&
            document.Alphabetical.Select(g => g.Id).SequenceEqual(Enumerable.Range(0, 27)), "Mail composer complete group scope differs.");
        var words = document.Words.ToImmutableDictionary(w => w.Value);
        foreach (var word in document.Words)
        {
            Need(word.Group is >= 0 and < 22 && word.Index is >= 0 and < 512 && word.Value == ((word.Group << 9) | word.Index) &&
                !string.IsNullOrWhiteSpace(word.Symbol) && word.Text.Length > 0, "Easy Chat word representation differs.");
        }
        foreach (var group in document.Groups)
        {
            Need(group.WordValues.Length != 0 && group.WordValues.Distinct().Count() == group.WordValues.Length &&
                group.WordValues.All(v => words.TryGetValue(v, out var word) && word.Group == group.Id) &&
                group.WordValues.Length == document.Words.Count(w => w.Group == group.Id), "Easy Chat group membership differs.");
        }
        foreach (var bucket in document.Alphabetical)
        {
            Need(bucket.Alternatives.Length != 0 && bucket.Alternatives.All(a => !a.IsDefaultOrEmpty && a.All(words.ContainsKey)),
                "Easy Chat alphabetical alternatives differ.");
        }
        Need(contracts.NameCharacters.GetValueOrDefault(" ", byte.MaxValue) == 0 &&
            contracts.NameCharacters.GetValueOrDefault("A", byte.MaxValue) == 0xBB &&
            contracts.NameCharacters.All(p => p.Key.Length == 1 && p.Value < 0xFA), "Original author charmap differs.");
        var texts = contracts.Texts.ToImmutableDictionary(t => t.Symbol, t => t.Text, StringComparer.Ordinal);
        Need(!contracts.Templates.IsDefaultOrEmpty && contracts.Templates.Length == 2 &&
            contracts.Templates.Select(t => t.Id).Order().SequenceEqual(new[] { "mail", "profile" }), "Easy Chat template selection differs.");
        var templates = contracts.Templates.ToImmutableDictionary(t => t.Id, StringComparer.Ordinal);
        foreach (var template in templates.Values)
        {
            Need(template.Columns > 0 && template.Rows > 0 && template.WordCount == Math.Min(template.Columns * template.Rows, 9) &&
                !template.InstructionTextSymbols.IsDefaultOrEmpty && !template.ConfirmTextSymbols.IsDefaultOrEmpty &&
                !template.CancelTextSymbols.IsDefaultOrEmpty &&
                template.InstructionTextSymbols.Concat(template.ConfirmTextSymbols).Concat(template.CancelTextSymbols).All(texts.ContainsKey) &&
                (template.TitleTextSymbol is null || texts.ContainsKey(template.TitleTextSymbol)), "Easy Chat template text/layout differs.");
        }
        Need(templates["profile"] is { Type: 0, Columns: 2, Rows: 2, WordCount: 4, FrameId: 0, TitleTextSymbol: not null } &&
            templates["mail"] is { Type: 4, Columns: 2, Rows: 5, WordCount: 9, FrameId: 2, TitleTextSymbol: null }, "Easy Chat editor layout differs.");
        var profile = contracts.ProfileDefinition;
        Need(profile is not null && profile.Type == templates["profile"].Type &&
            !profile.DefaultWords.IsDefault && profile.DefaultWords.Length == templates["profile"].WordCount &&
            !profile.SpecialPhrase.IsDefault && profile.SpecialPhrase.Length == templates["profile"].WordCount &&
            profile.DefaultWords.Concat(profile.SpecialPhrase).All(words.ContainsKey), "Easy Chat profile phrases differ.");
        var catalog = new SourceEasyChatCatalog(document.Groups, words, document.Alphabetical,
            contracts.SeenPokemonGroup, contracts.NationalPokemonGroup, contracts.DeoxysSpecies, contracts.UnownSpecies)
            { Texts = texts, Templates = templates, ProfileDefinition = profile };
        var selectedPack = pack ?? SourceTextPack.Current;
        var labels = selectedPack.Apply("easy-chat-groups", document.Groups.ToDictionary(group => group.Symbol, group => group.Name)
            .Concat(document.Alphabetical.Select(bucket => KeyValuePair.Create(
                "alphabet:" + bucket.Id.ToString(CultureInfo.InvariantCulture), bucket.Name)))
            .ToDictionary(pair => pair.Key, pair => pair.Value), preserveControls: true);
        var replacements = selectedPack.Apply("easy-chat-words",
            words.Values.Where(word => !IsSpeciesWord(catalog, word) && !IsMoveWord(word)).ToDictionary(
                word => word.Value.ToString(CultureInfo.InvariantCulture), word => word.Text), preserveControls: true);
        catalog = catalog with {
            Groups = document.Groups.Select(group => group with { Name = labels[group.Symbol] }).ToImmutableArray(),
            Alphabetical = document.Alphabetical.Select(bucket => bucket with {
                Name = labels["alphabet:" + bucket.Id.ToString(CultureInfo.InvariantCulture)] }).ToImmutableArray(),
            Words = words.ToImmutableDictionary(pair => pair.Key, pair =>
                IsSpeciesWord(catalog, pair.Value) || IsMoveWord(pair.Value) ? pair.Value :
                    pair.Value with { Text = replacements[pair.Key.ToString(CultureInfo.InvariantCulture)] }) };
        return new(catalog, contracts.Texts);
    }

    /// <summary>Names are projected from their existing catalogs using numeric source identities.
    /// Word values, source ordering, unlock flags, encoding and saved phrases stay unchanged.</summary>
    public SourceEasyChatCatalog WithDisplayNames(PretPokedexMenu pokemon, PretMetronomeMoveCatalog moves)
    {
        var moveNames = moves.Moves.Values.ToDictionary(move => move.SourceMoveId, move => moves.MoveNames[move.Id]);
        string Name(SourceEasyChatWord word)
        {
            if (IsSpeciesWord(Catalog, word))
                return pokemon.Entries.TryGetValue(word.Index, out var entry) ? entry.Name :
                    throw new InvalidDataException("easy_chat_species_name_missing: " + word.Value);
            if (IsMoveWord(word))
                return moveNames.TryGetValue(word.Index, out var name) ? name :
                    throw new InvalidDataException("easy_chat_move_name_missing: " + word.Value);
            return word.Text;
        }
        return Catalog with { Words = Catalog.Words.ToImmutableDictionary(pair => pair.Key,
            pair => pair.Value with { Text = Name(pair.Value) }) };
    }

    private static bool IsSpeciesWord(SourceEasyChatCatalog catalog, SourceEasyChatWord word) =>
        word.Group == catalog.SeenPokemonGroup || word.Group == catalog.NationalPokemonGroup;
    private static bool IsMoveWord(SourceEasyChatWord word) => word.Group is 18 or 19;

    private sealed record ComposerDocument(ImmutableArray<SourceEasyChatGroup> Groups,
        ImmutableArray<SourceEasyChatWord> Words, ImmutableArray<SourceEasyChatAlphabeticalGroup> Alphabetical);
    private sealed record ComposerText(string Symbol, string Text);
    private sealed record ComposerContracts(int SeenPokemonGroup, int NationalPokemonGroup,
        int DeoxysSpecies, int UnownSpecies,
        ImmutableDictionary<string, byte> NameCharacters, ImmutableArray<ComposerText> Texts,
        ImmutableArray<SourceEasyChatTemplate> Templates, SourceEasyChatProfileDefinition? ProfileDefinition);
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
