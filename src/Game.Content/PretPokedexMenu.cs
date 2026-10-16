using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record PokedexMenuTexture(string TexturePath, int Width, int Height);
public sealed record PokedexMenuEntry(int SourceSpeciesId, int NationalNumber, string Name, string Category,
    string DisplayCategory, int HeightDecimeters, int WeightHectograms, string HeightDisplay, string WeightDisplay,
    string Description, string Type1, string Type2, string HabitatTitle, PokedexMenuTexture? Portrait,
    PokedexMenuTexture? Footprint);
public sealed record PokedexMenuListLayout(int Width, int Height, int X, int Y, int WindowWidth, int WindowHeight,
    int VisibleRows, int NumberX, int NameX, int CursorX, int CaughtX, int Type1X, int Type2X, int TopY, int RowHeight);
public sealed record PokedexMenuRules(int RegionalCount, int NationalCount, string NumericalLength, string UnseenLabel,
    bool SeenCanOpen, bool CaughtRequiredForDescription, bool CaughtRequiredForMeasures, bool CaughtRequiredForFootprint,
    bool CaughtRequiredForTypes, string UnseenCategory, string UnseenHeight, string UnseenWeight, int PageScroll,
    int PokedexFlag, int NationalFlag, int NationalVariable, int NationalVariableValue, int NationalMagic, bool StartRequiresSeen);
/// <summary>Original canonical species permutations, including source-defined ordering of equal heights/weights.</summary>
public sealed record PokedexMenuOrders(ImmutableArray<int> Alphabetical, ImmutableArray<int> Type,
    ImmutableArray<int> Lightest, ImmutableArray<int> Smallest);
public sealed record PokedexMenuDocument(int SchemaVersion, ImmutableArray<PokedexMenuEntry> Entries,
    CaptureDexLayout EntryLayout, ImmutableArray<PokedexMenuTexture> Assets, PokedexMenuTexture OwnedMarker,
    ImmutableDictionary<string, string> UiTexts, PokedexMenuListLayout ListLayout, PokedexMenuRules Rules,
    PokedexMenuOrders Orders, ImmutableArray<PokedexHabitatDefinition> Habitats, ImmutableArray<ImmutableArray<PokedexHabitatSlot>> HabitatLayouts, ImmutableArray<int> SilhouetteRgb, ImmutableDictionary<int,PokedexSizeTransform> Sizes, PokedexAreaDefinition Areas);

public enum PokedexMenuOrder { NumericalKanto, Alphabetical, Type, Lightest, Smallest, NumericalNational }

/// <summary>Actual source gates; a flag alone does not unlock National mode.</summary>
public sealed record PokedexMenuAccess(bool HasPokedex, bool NationalFlag = false, int NationalMagic = 0, int NationalDexVariable = 0)
{
    public bool NationalEnabled => NationalFlag && NationalMagic == 0xB9 && NationalDexVariable == 0x6258;
}
public sealed record PokedexMenuRow(int SourceSpeciesId, int NationalNumber, string DisplayName,
    bool Seen, bool Caught, string? Type1, string? Type2)
{
    public bool CanOpen => Seen;
    public int SourcePackedIndex => SourceSpeciesId | (Seen ? 1 << 16 : 0) | (Caught ? 1 << 17 : 0);
}
/// <summary>Counts cover the available dex range; NationalOrder denotes that range, not an alphabetical/type ordering.</summary>
public sealed record PokedexMenuList(ImmutableArray<PokedexMenuRow> Rows, int SeenCount, int CaughtCount, bool NationalOrder);
/// <summary>Player-safe projection. Uncaught records contain no hidden measures, category or description.</summary>
public sealed record PokedexMenuPage(int SourceSpeciesId, int NationalNumber, string Name, bool Caught,
    string DisplayCategory, string HeightDisplay, string WeightDisplay, string Description,
    PokedexMenuTexture? Portrait, PokedexMenuTexture? Footprint);
/// <summary>Scroll offset and visible cursor row, matching the original list menu's two saved fields.</summary>
public readonly record struct PokedexMenuCursor(int ScrollOffset, int Row)
{
    public int SelectedIndex => ScrollOffset + Row;
}

/// <summary>Immutable game metadata and presentation projections, with no acquisition/state mutation.</summary>
public sealed partial class PretPokedexMenu
{
    public const string SourceCommit = PretCaptureDex.SourceCommit;
    public const string Profile = "firered-pokedex-menu-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly string _projectRoot;
    private readonly ImmutableDictionary<string, PokedexMenuTexture> _textures;
    public PokedexMenuDocument Document { get; }
    /// <summary>Keys are source species IDs, not national numbers (Treecko=277, Ralts=392, Deoxys=410).</summary>
    public ImmutableDictionary<int, PokedexMenuEntry> Entries { get; }
    public ImmutableSortedDictionary<int, PokedexMenuEntry> NationalEntries { get; }
    public CaptureDexLayout EntryLayout => Document.EntryLayout;
    public PokedexMenuListLayout ListLayout => Document.ListLayout;
    /// <summary>Original 12x12 caught icon. Draw at list-window x40 and the row's text y, only for caught rows.</summary>
    public PokedexMenuTexture OwnedMarker => Document.OwnedMarker;
    public ImmutableDictionary<string, string> UiTexts => Document.UiTexts;

    private PretPokedexMenu(string root, PokedexMenuDocument document)
    {
        _projectRoot = root;
        Document = document;
        Entries = document.Entries.ToImmutableDictionary(e => e.SourceSpeciesId);
        NationalEntries = document.Entries.ToImmutableSortedDictionary(e => e.NationalNumber, e => e);
        Require(document.Rules.RegionalCount > 0 && document.Rules.RegionalCount <= document.Rules.NationalCount &&
            document.Rules.NationalCount == Entries.Count &&
            NationalEntries.Keys.SequenceEqual(Enumerable.Range(1, document.Rules.NationalCount)),
            "Pokédex entry number range is invalid.");
        foreach (var order in new[] { document.Orders.Alphabetical, document.Orders.Type, document.Orders.Lightest, document.Orders.Smallest })
            Require(!order.IsDefault && order.Length == Entries.Count && order.Distinct().Count() == Entries.Count &&
                order.All(Entries.ContainsKey), "Pokédex search order must contain each canonical species exactly once.");
        foreach (var label in new[] { "gText_NumericalMode", "gText_NumericalModeKanto", "gText_NumericalModeNational",
                "gText_AToZMode", "gText_TypeMode", "gText_LightestMode", "gText_SmallestMode", "gText_SearchNoColor" })
            Require(document.UiTexts.TryGetValue(label, out var text) && !string.IsNullOrWhiteSpace(text),
                "Pokédex order label is missing.");
        ValidateHabitats();
        ValidateAreas();
        _textures = document.Entries.SelectMany(e => new[] { e.Portrait,e.Footprint }).OfType<PokedexMenuTexture>()
            .Concat(document.Assets).DistinctBy(a => a.TexturePath)
            .ToImmutableDictionary(a => a.TexturePath, StringComparer.Ordinal);
    }

    public static PretPokedexMenu Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        const string directory = "content/generated/sprites/pokemon/pokedex-menu/";
        var bytes = ReadPinned(root,directory + "pokedex-menu.json");
        var document = ContentJson.ParseStrict<PokedexMenuDocument>(Utf8.GetString(bytes), ContentJson.CreateOptions());
        Require(document.SchemaVersion == 3 && !document.Entries.IsDefaultOrEmpty,
            "Pokédex menu format is invalid.");
        return new(root, document);
    }

    /// <summary>Reuses this catalog only within the content root that supplied its bytes.</summary>
    public PretPokedexMenu ForProjectRoot(string projectRoot)
    {
        Require(string.Equals(Path.TrimEndingDirectorySeparator(_projectRoot),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Pokédex catalog belongs to a different project root.");
        return this;
    }

    /// <summary>Original Start menu requires the Pokédex flag and at least one seen species.</summary>
    public bool CanOpen(PokedexMenuAccess access, IEnumerable<int> seenSpecies)
        => access.HasPokedex && ValidateSpecies(seenSpecies).Count != 0;

    /// <summary>Original menu choices. Search is available before National unlock and follows its current range.</summary>
    public ImmutableArray<PokedexMenuOrder> AvailableOrders(PokedexMenuAccess access)
    {
        if (!access.HasPokedex) return [];
        return access.NationalEnabled
            ? [PokedexMenuOrder.NumericalKanto, PokedexMenuOrder.NumericalNational, PokedexMenuOrder.Alphabetical,
                PokedexMenuOrder.Type, PokedexMenuOrder.Lightest, PokedexMenuOrder.Smallest]
            : [PokedexMenuOrder.NumericalKanto, PokedexMenuOrder.Alphabetical, PokedexMenuOrder.Type,
                PokedexMenuOrder.Lightest, PokedexMenuOrder.Smallest];
    }

    public string GetOrderLabel(PokedexMenuOrder order, PokedexMenuAccess access)
    {
        Require(AvailableOrders(access).Contains(order), "Pokédex order is not available.");
        return UiTexts[order switch
        {
            PokedexMenuOrder.NumericalKanto => access.NationalEnabled ? "gText_NumericalModeKanto" : "gText_NumericalMode",
            PokedexMenuOrder.NumericalNational => "gText_NumericalModeNational",
            PokedexMenuOrder.Alphabetical => "gText_AToZMode",
            PokedexMenuOrder.Type => "gText_TypeMode",
            PokedexMenuOrder.Lightest => "gText_LightestMode",
            PokedexMenuOrder.Smallest => "gText_SmallestMode",
            _ => throw new InvalidDataException("Unknown Pokédex order.")
        }];
    }

    /// <summary>
    /// Source Search modes use fixed permutations, not free-text/type/range filters. A–Z includes seen entries;
    /// type, lightest and smallest include caught entries. Equal measures keep original table order.
    /// Search uses the National unlock globally, independent of the last numerical-list selection.
    /// </summary>
    public PokedexMenuList BuildOrderedList(IEnumerable<int> seenSpecies, IEnumerable<int> caughtSpecies,
        PokedexMenuAccess access, PokedexMenuOrder order)
    {
        Require(AvailableOrders(access).Contains(order), "Pokédex order is not available.");
        if (order is PokedexMenuOrder.NumericalKanto or PokedexMenuOrder.NumericalNational)
            return BuildNumericalList(seenSpecies, caughtSpecies, access, order == PokedexMenuOrder.NumericalNational);
        var seen = ValidateSpecies(seenSpecies);
        var caught = ValidateSpecies(caughtSpecies);
        Require(caught.IsSubsetOf(seen), "Caught Pokédex species must also be seen.");
        int capacity = access.NationalEnabled ? Document.Rules.NationalCount : Document.Rules.RegionalCount;
        var speciesOrder = order switch
        {
            PokedexMenuOrder.Alphabetical => Document.Orders.Alphabetical,
            PokedexMenuOrder.Type => Document.Orders.Type,
            PokedexMenuOrder.Lightest => Document.Orders.Lightest,
            PokedexMenuOrder.Smallest => Document.Orders.Smallest,
            _ => throw new InvalidDataException("Unknown Pokédex search order.")
        };
        var visible = order == PokedexMenuOrder.Alphabetical ? seen : caught;
        var rows = speciesOrder.Where(id => Entries[id].NationalNumber <= capacity && visible.Contains(id))
            .Select(id => BuildRow(Entries[id], seen, caught)).ToImmutableArray();
        return new(rows, seen.Count(i => Entries[i].NationalNumber <= capacity),
            caught.Count(i => Entries[i].NationalNumber <= capacity), access.NationalEnabled);
    }

    /// <summary>Builds numerical order through the highest seen number, retaining unseen holes.</summary>
    public PokedexMenuList BuildNumericalList(IEnumerable<int> seenSpecies, IEnumerable<int> caughtSpecies,
        PokedexMenuAccess access, bool nationalOrder = false)
    {
        Require(access.HasPokedex, "Pokédex has not been received.");
        Require(!nationalOrder || access.NationalEnabled, "National Pokédex gate is not enabled.");
        var seen = ValidateSpecies(seenSpecies);
        var caught = ValidateSpecies(caughtSpecies);
        Require(caught.IsSubsetOf(seen), "Caught Pokédex species must also be seen.");
        int capacity = nationalOrder ? Document.Rules.NationalCount : Document.Rules.RegionalCount;
        int length = seen.Select(i => Entries[i].NationalNumber).Where(i => i <= capacity).DefaultIfEmpty(0).Max();
        var rows = Enumerable.Range(1, length).Select(number => BuildRow(NationalEntries[number], seen, caught)).ToImmutableArray();
        return new(rows, seen.Count(i => Entries[i].NationalNumber <= capacity), caught.Count(i => Entries[i].NationalNumber <= capacity), nationalOrder);
    }

    /// <summary>Only seen rows open. Uncaught flavor text and footprints are absent, with source question-mark measures.</summary>
    public PokedexMenuPage BuildPage(int sourceSpeciesId, IEnumerable<int> seenSpecies, IEnumerable<int> caughtSpecies,
        PokedexMenuAccess access)
    {
        var list = BuildNumericalList(seenSpecies, caughtSpecies, access, access.NationalEnabled);
        var row = list.Rows.FirstOrDefault(r => r.SourceSpeciesId == sourceSpeciesId);
        Require(row is { Seen: true }, "Unseen or unavailable Pokédex entry cannot be opened.");
        var entry = Entries[sourceSpeciesId];
        return new(sourceSpeciesId, entry.NationalNumber, entry.Name, row!.Caught,
            row.Caught ? entry.DisplayCategory : Document.Rules.UnseenCategory,
            row.Caught ? entry.HeightDisplay : Document.Rules.UnseenHeight,
            row.Caught ? entry.WeightDisplay : Document.Rules.UnseenWeight,
            row.Caught ? entry.Description : string.Empty, entry.Portrait, row.Caught ? entry.Footprint : null);
    }

    /// <summary>Original detail up/down skips unseen holes without wrapping; preserves the list return cursor.</summary>
    public PokedexMenuCursor MoveDetail(PokedexMenuList list, PokedexMenuCursor cursor, int direction)
    {
        ValidateCursor(list, cursor);
        Require(direction is -1 or 1, "Pokédex direction must be -1 or 1.");
        for (int i = cursor.SelectedIndex + direction; i >= 0 && i < list.Rows.Length; i += direction)
            if (list.Rows[i].Seen) return CenterDetailCursor(list.Rows.Length, i);
        return cursor;
    }

    /// <summary>Original ListMenu uses three-row up/five-row down thresholds and repeated nine-row page steps.</summary>
    public PokedexMenuCursor MoveList(PokedexMenuList list, PokedexMenuCursor cursor, int direction, bool page = false)
    {
        ValidateCursor(list, cursor);
        Require(direction is -1 or 1, "Pokédex direction must be -1 or 1.");
        int visible = Math.Min(9, list.Rows.Length);
        if (visible == 0) return cursor;
        int offset = cursor.ScrollOffset, row = cursor.Row;
        for (int step = 0; step < (page ? visible : 1); step++)
        {
            int threshold = visible == 1 ? 0 : direction < 0 ? visible - (visible / 2 + visible % 2) - 1 : visible / 2 + visible % 2;
            if (direction < 0)
            {
                if (offset == 0) row = Math.Max(0, row - 1);
                else if (row > threshold) row--;
                else { offset--; row = threshold; }
            }
            else
            {
                if (offset == list.Rows.Length - visible) row = Math.Min(visible - 1, row + 1);
                else if (row < threshold) row++;
                else { offset++; row = threshold; }
            }
        }
        return new(offset, row);
    }

    public static PokedexMenuCursor CenterDetailCursor(int count, int selectedIndex)
    {
        Require(selectedIndex >= 0 && selectedIndex < count, "Invalid Pokédex cursor bounds.");
        int offset = count <= 9 || selectedIndex < 4 ? 0 : selectedIndex >= count - 4 ? count - 9 : selectedIndex - 4;
        return new(offset, selectedIndex - offset);
    }

    /// <summary>Reads only an exact catalog asset. Paths are project-relative; forged metadata and symlinks reject.</summary>
    public byte[] ReadTexture(PokedexMenuTexture texture)
    {
        Require(_textures.TryGetValue(texture.TexturePath, out var expected) && texture == expected,
            "Unknown or forged Pokédex menu texture.");
        return ReadPinned(_projectRoot,texture.TexturePath);
    }

    private HashSet<int> ValidateSpecies(IEnumerable<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = values.ToHashSet();
        Require(result.All(Entries.ContainsKey), "Unknown or noncanonical source species ID in Pokédex.");
        return result;
    }
    private PokedexMenuRow BuildRow(PokedexMenuEntry entry, HashSet<int> seen, HashSet<int> caught)
    {
        bool isSeen = seen.Contains(entry.SourceSpeciesId), isCaught = caught.Contains(entry.SourceSpeciesId);
        return new(entry.SourceSpeciesId, entry.NationalNumber, isSeen ? entry.Name : Document.Rules.UnseenLabel,
            isSeen, isCaught, isCaught ? entry.Type1 : null, isCaught && entry.Type2 != entry.Type1 ? entry.Type2 : null);
    }
    private static void ValidateCursor(PokedexMenuList list, PokedexMenuCursor cursor)
    {
        int count = list.Rows.Length;
        Require(cursor.ScrollOffset >= 0 && cursor.Row >= 0 && cursor.Row < Math.Max(1, Math.Min(9, count)) &&
            cursor.ScrollOffset <= Math.Max(0, count - 9) && cursor.SelectedIndex < Math.Max(1, count), "Invalid Pokédex cursor.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static byte[] ReadPinned(string root,string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\') ||
            relative.Split('/').Any(p => p is "" or "." or "..")) throw new InvalidDataException("Unsafe Pokédex path.");
        for (var ancestor = new DirectoryInfo(root); ancestor is not null; ancestor = ancestor.Parent)
            Require(ancestor.LinkTarget is null, "Pokédex directory symlink rejected.");
        var path = root;
        foreach (var part in relative.Split('/'))
        {
            path = Path.Combine(path, part);
            Require(new FileInfo(path).LinkTarget is null && new DirectoryInfo(path).LinkTarget is null, "Pokédex file/directory symlink rejected.");
        }
        var bytes = File.ReadAllBytes(path);
        return bytes;
    }
}
