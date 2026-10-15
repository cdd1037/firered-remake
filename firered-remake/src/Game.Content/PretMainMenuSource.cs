using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretMainMenuWindow(string Symbol, int Id, int Bg, int TilemapLeft, int TilemapTop,
    int Width, int Height, int PaletteNum, int BaseBlock, OpeningItemSourceRef Source)
{
    public int PixelX => TilemapLeft * 8;
    public int PixelY => TilemapTop * 8;
    public int PixelWidth => Width * 8;
    public int PixelHeight => Height * 8;
    public int FrameX => PixelX - 8;
    public int FrameY => PixelY - 8;
    public int FrameWidth => PixelWidth + 16;
    public int FrameHeight => PixelHeight + 16;
}
public sealed record PretMainMenuBackground(string Symbol, int Bg, int CharBaseIndex, int MapBaseIndex,
    int Priority, OpeningItemSourceRef Source);
public sealed record PretMainMenuTextColor(string Symbol, int Background, int Foreground, int Shadow, OpeningItemSourceRef Source);
public sealed record PretMainMenuPalette(string Symbol, int Bank, ImmutableArray<int> Rgb555,
    ImmutableArray<ImmutableArray<int>> Rgb8, OpeningItemSourceRef Source);
public sealed record PretMainMenuGenderColor(string GenderSymbol, int Gender, int Bank, int Index,
    int Rgb555, ImmutableArray<int> Rgb8, OpeningItemSourceRef Source);
public sealed record PretMainMenuTextPlacement(string Id, string WindowSymbol, int WindowId, string FontSymbol,
    int FontId, int X, int Y, string ColorSymbol, int Speed, string TextSymbol, OpeningItemSourceRef Source);
public sealed record PretMainMenuText(string Text, string Encoding, OpeningItemSourceRef Source);
public sealed record PretMainMenuSelectionRegion(string WindowSymbol, int Left, int Right, int Top, int Bottom,
    OpeningItemSourceRef Source)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}
public sealed record PretMainMenuSaveStatus(string Symbol, int Value, string MenuTypeSymbol, int MenuType,
    string? ErrorTextSymbol, string FrameFunction, bool MysteryGiftEligible, OpeningItemSourceRef Source);
public sealed record PretMainMenuConstant(int Value, OpeningItemSourceRef Source);
public sealed record PretMainMenuFrameDrawCall(string Bg, int TileId, string Left, string Top, string Width, string Height, int PaletteNum);
public sealed record PretMainMenuRules(int LogicalWidth, int LogicalHeight, int TileSize, int WindowFillColor,
    int DarkeningCoefficient, int InitialCursor, ImmutableArray<int> CursorYMax, bool NavigationWraps,
    bool EmptySaveEntersNewGameDirectly, string NewGameEntry, string ContinueEntry, string CancelEntry,
    string ErrorAcknowledgementButton, bool NewGameSelectionPromptsOverwrite, string OverwritePromptLocation,
    bool OrdinaryOverwriteDefaultYes, bool DifferentFileOverwriteDefaultYes, int PlayerNameLength,
    int PlayerNameBufferLength, int PlayerNameTerminator, ImmutableArray<string> PlayerGenderSymbols,
    int PlayTimeHoursDigits, int PlayTimeMinutesDigits, string PokedexVisibleFlag, string PokedexCountKind,
    string BadgeStartFlag, int BadgeCount, int FrameTileBase, int FramePaletteBank,
    ImmutableArray<PretMainMenuFrameDrawCall> FrameDrawCalls, ImmutableArray<OpeningItemSourceRef> SourceRefs);

/// <summary>
/// Authenticated original main-menu presentation and entry contracts. Does not create a session,
/// interpret save health, overwrite saves, or implement the title/Oak introduction.
/// </summary>
public sealed class PretMainMenuSource
{
    public const string Profile = "firered-main-menu-source-v1";
    public const string NewGameOnlyWindow = "MAIN_MENU_WINDOW_NEWGAME_ONLY";
    public const string ContinueWindow = "MAIN_MENU_WINDOW_CONTINUE";
    public const string NewGameWindow = "MAIN_MENU_WINDOW_NEWGAME";
    public const string ErrorWindow = "MAIN_MENU_WINDOW_ERROR";
    public ImmutableDictionary<string, PretMainMenuWindow> Windows { get; }
    public ImmutableArray<PretMainMenuBackground> Backgrounds { get; }
    public ImmutableDictionary<string, PretMainMenuTextColor> TextColors { get; }
    public ImmutableDictionary<string, PretMainMenuPalette> Palettes { get; }
    public ImmutableDictionary<int, PretMainMenuGenderColor> GenderColors { get; }
    public ImmutableArray<PretMainMenuTextPlacement> TextPlacements { get; }
    public ImmutableDictionary<string, PretMainMenuText> Texts { get; }
    public ImmutableDictionary<string, PretMainMenuSelectionRegion> SelectionRegions { get; }
    public ImmutableDictionary<string, PretMainMenuSaveStatus> SaveStatuses { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PretMainMenuConstant> ConstantSources { get; }
    public PretMainMenuRules Rules { get; }
    public ImmutableArray<string> Limitations { get; }
    public string GeneratedDirectory { get; }

    private PretMainMenuSource(string directory, JsonElement root)
    {
        GeneratedDirectory = directory;
        var options = ContentJson.CreateOptions();
        T Decode<T>(string member) => root.GetProperty(member).Deserialize<T>(options)
            ?? throw new InvalidDataException("Main-menu value missing: " + member);
        Windows = Decode<ImmutableArray<PretMainMenuWindow>>("windows").ToImmutableDictionary(x => x.Symbol, StringComparer.Ordinal);
        Backgrounds = Decode<ImmutableArray<PretMainMenuBackground>>("backgrounds");
        TextColors = Decode<ImmutableArray<PretMainMenuTextColor>>("textColors").ToImmutableDictionary(x => x.Symbol, StringComparer.Ordinal);
        Palettes = Decode<ImmutableArray<PretMainMenuPalette>>("palettes").ToImmutableDictionary(x => x.Symbol, StringComparer.Ordinal);
        GenderColors = Decode<ImmutableArray<PretMainMenuGenderColor>>("genderColors").ToImmutableDictionary(x => x.Gender);
        TextPlacements = Decode<ImmutableArray<PretMainMenuTextPlacement>>("textPlacements");
        Texts = Decode<ImmutableDictionary<string, PretMainMenuText>>("texts");
        SelectionRegions = Decode<ImmutableArray<PretMainMenuSelectionRegion>>("selectionRegions").ToImmutableDictionary(x => x.WindowSymbol, StringComparer.Ordinal);
        SaveStatuses = Decode<ImmutableArray<PretMainMenuSaveStatus>>("saveStatuses").ToImmutableDictionary(x => x.Symbol, StringComparer.Ordinal);
        ConstantSources = Decode<ImmutableDictionary<string, PretMainMenuConstant>>("constants");
        Constants = ConstantSources.ToImmutableDictionary(x => x.Key, x => x.Value.Value, StringComparer.Ordinal);
        Rules = Decode<PretMainMenuRules>("rules");
        Limitations = Decode<ImmutableArray<string>>("limitations");
    }

    public static PretMainMenuSource Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var generatedDirectory = Path.Combine(project, "content/generated/main-menu");
        byte[] ReadPinned(string root,string path)
        {
var bytes = File.ReadAllBytes(SafeFile(root,path));
return bytes;
        }
        using var document = JsonDocument.Parse(ReadPinned(generatedDirectory,"main-menu.json"));
        var root = document.RootElement;
        Require(root.GetProperty("schemaVersion").GetInt32() == 1 ,
            "Main-menu identity differs.");
        var result = new PretMainMenuSource(generatedDirectory, root);
        Require(result.Windows.Count == 5 && result.TextPlacements.Length == 15 && result.Palettes.Count == 2 &&
            result.SaveStatuses.Count == 5 && result.SelectionRegions.Count == 5 && result.GenderColors.Count == 2 &&
            result.Rules.FrameDrawCalls.Length == 8, "Main-menu metadata incomplete.");
        Require(result.Constants["FONT_NORMAL"] == 2 && result.Constants["PLAYER_NAME_LENGTH"] == 7 &&
            result.Constants["MALE"] == 0 && result.Constants["FEMALE"] == 1 && result.Constants["SAVE_STATUS_ERROR"] == 255,
            "Main-menu source constants differ.");
        foreach (var window in result.Windows.Values)
            Require(window.Id == result.Constants[window.Symbol] && window.PixelX >= 0 && window.PixelY >= 0 &&
                window.PixelX + window.PixelWidth <= result.Rules.LogicalWidth && window.PixelY + window.PixelHeight <= result.Rules.LogicalHeight,
                "Main-menu window outside source viewport.");
        foreach (var placement in result.TextPlacements)
            Require(result.Windows[placement.WindowSymbol].Id == placement.WindowId && result.TextColors.ContainsKey(placement.ColorSymbol) &&
                placement.FontId == result.Constants[placement.FontSymbol], "Main-menu text reference differs.");
        return result;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe main-menu path.");
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Main-menu directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Main-menu file symlink is unsupported.");
        }
        return result;
    }
}
