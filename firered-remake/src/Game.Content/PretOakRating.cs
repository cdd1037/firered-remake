using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record OakRatingCounts(int Seen, int Caught);
public sealed record OakRatingBand(int MinimumCaught, int MaximumCaught, string TextSymbol);
public sealed record OakRatingSelection(string TextSymbol, string Text, bool KantoComplete, string FanfareSymbol, int FanfareId);
/// <summary>Player-safe count/text projection. National fields are null while the three-field gate is closed.</summary>
public sealed record OakRatingResult(OakRatingCounts Kanto, string KantoSummary, OakRatingSelection Rating,
    OakRatingCounts? National, string? NationalSummary, bool? NationalComplete,
    string? NationalCommentSymbol, string? NationalComment, bool ShouldSetOakSawDexCompletion);

/// <summary>
/// Pinned original Oak rating source and pure projections. Does not enable scripts, mutate flags,
/// acquire species, infer ownership from a party, or unlock National mode.
/// </summary>
public sealed class PretOakRating
{
    public const string Profile = "firered-oak-rating-v1";
    public const string SourceCommit = PretWorldMap.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableHashSet<int> NationalExcluded = [151, 249, 250, 251, 385, 386];
    public ImmutableArray<OakRatingBand> RatingBands { get; }
    /// <summary>The rating library only. Loading it never marks VM/native operations supported.</summary>
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    /// <summary>Original PC/lab callers for explicit integration review; unrelated branches remain unsupported.</summary>
    public ImmutableArray<PretSourceScriptFile> CallerScripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement SourceContract { get; }

    private PretOakRating(ImmutableArray<OakRatingBand> bands, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableArray<PretSourceScriptFile> callers, ImmutableDictionary<string, string> textSources,
        ImmutableDictionary<string, string> ui, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, JsonElement contract)
    {
        RatingBands = bands; Scripts = scripts; CallerScripts = callers; TextSources = textSources;
        SourceUiTexts = ui; Constants = constants; SourceFiles = files; SourceContract = contract;
    }

    /// <summary>Original Kanto count tiers, including Mew's 150-caught exception; no National count is rated here.</summary>
    public OakRatingSelection SelectKantoRating(int caughtCount, bool mewCaught)
    {
        Require(caughtCount is >= 0 and <= 151 && !(caughtCount == 0 && mewCaught) && !(caughtCount == 151 && !mewCaught),
            "Invalid Kanto caught-count/Mew combination.");
        bool complete = caughtCount == 151 || caughtCount == 150 && !mewCaught;
        string symbol = complete ? "PokedexRating_Text_Complete" : caughtCount == 150
            ? "PokedexRating_Text_LessThan150" : RatingBands.Single(b => caughtCount >= b.MinimumCaught && caughtCount <= b.MaximumCaught).TextSymbol;
        string fanfare = complete ? "MUS_EVOLVED" : "MUS_DEX_RATING";
        return new(symbol, SourceUiTexts[symbol], complete, fanfare, Constants[fanfare]);
    }

    /// <summary>
    /// Inputs are persistent national dex numbers (1..386), not internal species IDs or current party members.
    /// Duplicates are counted once; invalid numbers and caught-not-seen data fail before producing any result.
    /// </summary>
    public OakRatingResult EvaluateNationalNumbers(IEnumerable<int> seenNationalNumbers,
        IEnumerable<int> caughtNationalNumbers, PokedexMenuAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);
        Require(access.HasPokedex, "Oak rating requires the Pokédex gate.");
        var seen = ValidateNumbers(seenNationalNumbers);
        var caught = ValidateNumbers(caughtNationalNumbers);
        Require(caught.IsSubsetOf(seen), "Oak rating caught numbers must also be seen.");
        var kanto = new OakRatingCounts(seen.Count(n => n <= 151), caught.Count(n => n <= 151));
        var rating = SelectKantoRating(kanto.Caught, caught.Contains(151));
        string kantoSummary = FormatCounts("PokedexRating_Text_SeenXOwnedY", kanto);
        if (!access.NationalEnabled)
            return new(kanto, kantoSummary, rating, null, null, null, null, null, false);
        var national = new OakRatingCounts(seen.Count, caught.Count);
        bool complete = Enumerable.Range(1, 386).All(n => NationalExcluded.Contains(n) || caught.Contains(n));
        string comment = complete ? "PokedexRating_Text_YouveCompletedDex" : "PokedexRating_Text_LookForwardToFilledNationalDex";
        return new(kanto, kantoSummary, rating, national, FormatCounts("PokedexRating_Text_NationalDexSeenXOwnedY", national),
            complete, comment, SourceUiTexts[comment], complete);
    }

    /// <summary>Map source species IDs through the existing authenticated 386-entry catalog; returns no species metadata.</summary>
    public OakRatingResult EvaluateSpeciesIds(IEnumerable<int> seenSpeciesIds, IEnumerable<int> caughtSpeciesIds,
        PretPokedexMenu pokedex, PokedexMenuAccess access)
    {
        ArgumentNullException.ThrowIfNull(pokedex);
        ArgumentNullException.ThrowIfNull(seenSpeciesIds);
        ArgumentNullException.ThrowIfNull(caughtSpeciesIds);
        int NationalNumber(int id) => pokedex.Entries.TryGetValue(id, out var entry) ? entry.NationalNumber :
            throw new InvalidDataException("Unknown source species ID for Oak rating.");
        return EvaluateNationalNumbers(seenSpeciesIds.Select(NationalNumber), caughtSpeciesIds.Select(NationalNumber), access);
    }

    public string InPersonIntroSymbol(bool oakSawDexCompletion) => oakSawDexCompletion
        ? "PokedexRating_Text_LoveSeeingYourPokedex" : "PokedexRating_Text_HowIsPokedexComingAlong";

    private string FormatCounts(string symbol, OakRatingCounts counts) => SourceUiTexts[symbol]
        .Replace("{STR_VAR_1}", counts.Seen.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{STR_VAR_2}", counts.Caught.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static ImmutableHashSet<int> ValidateNumbers(IEnumerable<int> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        var set = numbers.ToImmutableHashSet();
        Require(set.All(n => n is >= 1 and <= 386), "Oak rating national dex number must be within 1..386.");
        return set;
    }

    public static PretOakRating Load(string projectRoot)
    {
        string root = Path.GetFullPath(projectRoot);
        byte[] Read(string relative)
        {
            string path = SafeFile(root, relative);
            var info = new FileInfo(path);
            Require(info.Exists, "Missing Oak rating input: " + relative);
            return File.ReadAllBytes(path);
        }
        using var lockDocument = JsonDocument.Parse(Read("content/oak-rating-source-lock.json"));
        var sourceLock = lockDocument.RootElement;
        using var catalogDocument = JsonDocument.Parse(Read("content/generated/oak-rating/oak-rating.json"));
        var catalog = catalogDocument.RootElement;
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        var scriptPaths = new HashSet<string>(StringComparer.Ordinal) {
            "data/scripts/pokedex_rating.inc", "data/scripts/pc.inc", "data/maps/PalletTown_ProfessorOaksLab/scripts.inc",
            "data/text/pokedex_rating.inc", "data/event_scripts.s", "data/maps/PalletTown_ProfessorOaksLab/text.inc"
        };
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var e in sourceLock.GetProperty("files").EnumerateArray())
        {
            string path = e.GetProperty("path").GetString()!;

            if (scriptPaths.Contains(path))
                raw.Add(path, Utf8.GetString(Read(PretSourcePaths.FireRedRawDirectory + "/" + path)));
            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var options = ContentJson.CreateOptions();
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing Oak rating value.");
        var bands = Decode<ImmutableArray<OakRatingBand>>(catalog.GetProperty("ratingBands"));
        Require(bands.Length == 15 && bands.Select((b, i) => b.MinimumCaught == i * 10 && b.MaximumCaught == i * 10 + 9 &&
            b.TextSymbol == "PokedexRating_Text_LessThan" + (i + 1) * 10).All(v => v), "Oak rating tiers differ.");
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in catalog.GetProperty("constants").EnumerateObject())
        {
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        }
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var text in catalog.GetProperty("texts").EnumerateArray())
        {
            texts.Add(text.GetProperty("symbol").GetString()!, text.GetProperty("text").GetString()!);
        }
        var rules = catalog.GetProperty("rules");
        Require(rules.GetProperty("regionalCount").GetInt32() == 151 && rules.GetProperty("nationalCount").GetInt32() == 386 &&
            rules.GetProperty("nationalCompletionExcluded").EnumerateArray().Select(e => e.GetInt32()).ToImmutableHashSet().SetEquals(NationalExcluded),
            "Oak rating count rules differ.");
        PretSourceScriptFile Script(string path) => new(path, raw[path], new(SourceKind.Pret,path,"oak-rating-source-library","pret/pokefirered",SourceCommit));
        return new(bands, [Script("data/scripts/pokedex_rating.inc")],
            [Script("data/scripts/pc.inc"), Script("data/maps/PalletTown_ProfessorOaksLab/scripts.inc")],
                    new[] { "data/text/pokedex_rating.inc","data/event_scripts.s","data/maps/PalletTown_ProfessorOaksLab/text.inc" }
                .ToImmutableDictionary(p => p, p => raw[p], StringComparer.Ordinal),
            texts.ToImmutable(), constants.ToImmutable(), inventory, catalog.Clone());
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(s => s is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Oak rating path.");
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Oak rating directory symlink is unsupported.");
        string result = root;
        foreach (string part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Oak rating symlink is unsupported.");
        }
        return result;
    }
}
