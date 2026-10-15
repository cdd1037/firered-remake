using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Authenticated complete original Easy Chat vocabulary and nine-slot Mail composition.
/// Selection availability is applied by Domain against live progression, never during source loading.</summary>
public sealed class PretMailComposer
{
    public const string ResourceId = "firered-mail-composer-v1";
    public SourceEasyChatCatalog Catalog { get; }
    public ImmutableDictionary<string, string> Texts { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }

    private PretMailComposer(SilphSourcePackage source, SourceEasyChatCatalog catalog, ImmutableArray<ComposerText> texts)
    {
        Catalog = catalog; Texts = texts.ToImmutableDictionary(t => t.Symbol, t => t.Text, StringComparer.Ordinal);
        SourceFiles = source.Files;
    }

    public static PretMailComposer Load(string root)
    {
        var source = new SilphSourcePackage(root,"mail-composer","scripts/mail-composer.lock.json");
        var document = source.Parse<ComposerDocument>("catalog.json");
        var contracts = source.Parse<ComposerContracts>("contracts.json");
        Need(            document.Groups.Length == 22 && document.Words.Length == 1813 && document.Alphabetical.Length == 27 &&
            document.Groups.Select(g => g.Id).SequenceEqual(Enumerable.Range(0, 22)) &&
            document.Alphabetical.Select(g => g.Id).SequenceEqual(Enumerable.Range(0, 27)), "Mail composer complete group scope differs.");
        Need(contracts.SeenPokemonGroup == 21 && contracts.NationalPokemonGroup == 0 && contracts.DeoxysSpecies == 410 &&
            contracts.UnownSpecies == 201 && contracts.BlankWord == 65535 && contracts.MailWordCount == 9 &&
            contracts.AuthorMinimumPaddedLength == 6 && contracts.AuthorMaximumLength == 7 &&
            contracts.TrainerIdByteOrder == "player-save-byte-order-little-endian" && contracts.References.Length == 14 && contracts.Texts.Length == 12,
            "Original Mail author/selection contract differs.");
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
        Need(contracts.NameCharacters.Count > 100 && contracts.NameCharacters[" "] == 0 && contracts.NameCharacters["A"] == 0xBB &&
            contracts.NameCharacters.All(p => p.Key.Length == 1 && p.Value < 0xFA), "Original author charmap differs.");
        var catalog = new SourceEasyChatCatalog(document.SourceCommit, document.Groups, words, document.Alphabetical,
            contracts.NameCharacters, contracts.SeenPokemonGroup, contracts.NationalPokemonGroup, contracts.DeoxysSpecies, contracts.UnownSpecies)
            { Texts = contracts.Texts.ToImmutableDictionary(t => t.Symbol, t => t.Text, StringComparer.Ordinal) };
        return new(source, catalog, contracts.Texts);
    }

    private sealed record ComposerDocument(string SourceCommit, ImmutableArray<SourceEasyChatGroup> Groups,
        ImmutableArray<SourceEasyChatWord> Words, ImmutableArray<SourceEasyChatAlphabeticalGroup> Alphabetical);
    private sealed record ComposerText(string Symbol, string Text, PokemonSourceRef Source);
    private sealed record ComposerContracts(string SourceCommit, int SeenPokemonGroup, int NationalPokemonGroup,
        int DeoxysSpecies, int UnownSpecies, int BlankWord, int MailWordCount, string TrainerIdByteOrder,
        int AuthorMinimumPaddedLength, int AuthorMaximumLength, ImmutableArray<PokemonSourceRef> References,
        ImmutableDictionary<string, byte> NameCharacters, ImmutableArray<ComposerText> Texts);
    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
