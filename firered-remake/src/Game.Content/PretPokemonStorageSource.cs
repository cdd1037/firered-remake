using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretPcMenuVariant(string Id, bool Pokedex, bool GameClear,
    ImmutableArray<string> TextSymbols, ImmutableArray<int> ResultValues, int CancelResult,
    bool WrapAround, OpeningItemSourceRef Source);
public sealed record PretPokemonStorageMenuOption(string Option, int Result, string TextSymbol,
    string DescriptionSymbol, string RuntimeStatus, OpeningItemSourceRef Source);
public sealed record PretPokemonStorageSourceText(string Text, OpeningItemSourceRef Source);

/// <summary>
/// Original shared Pokemon Center PC source, authenticated independently of its application handlers.
/// Existing map/species/item owners remain authoritative. Unsupported branches stay in source IR.
/// </summary>
public sealed class PretPokemonStorageSource
{
    public const string Profile = "firered-pokemon-storage-source-v1";
    public const string Entry = "EventScript_PC";
    public const string StorageEntry = "EventScript_AccessPokemonStorage";
    public const string ReturnEntry = "EventScript_PCMainMenu";
    public const string PowerOffEntry = "EventScript_TurnOffPC";
    public const string NamingFlag = "FLAG_SYS_NOT_SOMEONES_PC";
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public ImmutableDictionary<string, PretPokemonStorageSourceText> UiTextSources { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<PretPcMenuVariant> PcMenuVariants { get; }
    public ImmutableArray<PretPokemonStorageMenuOption> StorageMenu { get; }
    public ImmutableArray<OpeningItemSourceRef> RulesSources { get; }

    public ImmutableArray<string> Limitations { get; }
    public string GeneratedDirectory { get; }

    private PretPokemonStorageSource(string generatedDirectory, ImmutableDictionary<string, int> constants,
        ImmutableDictionary<string, PretPokemonStorageSourceText> uiTexts, ImmutableArray<PretWorldSourceFile> files,
        ImmutableArray<PretPcMenuVariant> pcMenus, ImmutableArray<PretPokemonStorageMenuOption> storageMenu,
        ImmutableArray<OpeningItemSourceRef> rules, ImmutableArray<string> limitations)
    {
        GeneratedDirectory = generatedDirectory; Constants = constants;
        UiTextSources = uiTexts; SourceUiTexts = uiTexts.ToImmutableDictionary(x => x.Key, x => x.Value.Text, StringComparer.Ordinal);
        SourceFiles = files; PcMenuVariants = pcMenus; StorageMenu = storageMenu; RulesSources = rules;
        Limitations = limitations;
    }

    public static PretPokemonStorageSource Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);

        var generatedDirectory = Path.Combine(project, "content/generated/pokemon-storage");
        byte[] Read(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }

        var generated = Read(generatedDirectory,"pokemon-storage.json");
        using var document = JsonDocument.Parse(generated);
        var root = document.RootElement;
        Require(root.GetProperty("schemaVersion").GetInt32() == 1 , "Storage profile differs.");
        using var manifest = JsonDocument.Parse(Read(generatedDirectory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 , "Storage generated manifest differs.");
        var output = m.GetProperty("outputs")[0];
        Require(output.GetProperty("path").GetString() == "pokemon-storage.json" , "Storage generated output metadata differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();

        var inventory = files.ToImmutable();
        var options = ContentJson.CreateOptions();
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing storage document value.");
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("constants").EnumerateObject())
        {
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
        }
        Require(constants["TOTAL_BOXES_COUNT"] == 14 && constants["IN_BOX_COUNT"] == 30 && constants["MB_PC"] == 131 &&
            constants["FLAG_SYS_NOT_SOMEONES_PC"] == 0x834 && constants["FLAG_SYS_PC_STORAGE_DISABLED"] == 0x841 &&
            constants["SCR_MENU_CANCEL"] == 127, "Storage required source constants differ.");
        var uiTexts = Decode<ImmutableDictionary<string, PretPokemonStorageSourceText>>(root.GetProperty("uiTexts"));
        var pcMenus = Decode<ImmutableArray<PretPcMenuVariant>>(root.GetProperty("pcMenuVariants"));
        var storageMenu = Decode<ImmutableArray<PretPokemonStorageMenuOption>>(root.GetProperty("storageMenu"));
        var rules = Decode<ImmutableArray<OpeningItemSourceRef>>(root.GetProperty("rulesSources"));
        Require(pcMenus.Length == 3 && storageMenu.Length == 5, "Incomplete storage source contract.");
        return new(generatedDirectory, constants.ToImmutable(), uiTexts, inventory,
            pcMenus, storageMenu, rules, Decode<ImmutableArray<string>>(root.GetProperty("limitations")));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe storage path.");
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Storage directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Storage file symlink is unsupported.");
        }
        return result;
    }
}
