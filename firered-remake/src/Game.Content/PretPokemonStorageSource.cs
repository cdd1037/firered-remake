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
    public const string LockSha256 = "f2813e6f5bc06039e1a6dae4064d1fb4005856ae3f01727edc52056079e5e220";
    public const string GeneratedSha256 = "b2d29a69f482c0c9cf89db4fdeb42dce02908a6511dc88872c6e4fb6b078bfa3";
    public const string ManifestSha256 = "2c39f97fa6ee10e3257434e291a604c8e55ee19ac2638fded108c4f7b1649041";
    public const string RawManifestSha256 = "58e57a5572072efbb18b9c5f253f57f0abe81f781c3754805044344ad4688de1";
    public const string FullSourceHash = "226a0450af50caaf7f93d2319f4688f3c2c9d52aec95d0f33f6bdb04a955341c";
    public const string Entry = "EventScript_PC";
    public const string StorageEntry = "EventScript_AccessPokemonStorage";
    public const string ReturnEntry = "EventScript_PCMainMenu";
    public const string PowerOffEntry = "EventScript_TurnOffPC";
    public const string NamingFlag = "FLAG_SYS_NOT_SOMEONES_PC";
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public ImmutableDictionary<string, PretPokemonStorageSourceText> UiTextSources { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public ImmutableArray<PretPcMenuVariant> PcMenuVariants { get; }
    public ImmutableArray<PretPokemonStorageMenuOption> StorageMenu { get; }
    public ImmutableArray<OpeningItemSourceRef> RulesSources { get; }
    public JsonElement ScriptInventory { get; }
    public ImmutableArray<string> Limitations { get; }
    public string GeneratedDirectory { get; }

    private PretPokemonStorageSource(string generatedDirectory, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableDictionary<string, PretPokemonStorageSourceText> uiTexts, ImmutableArray<PretWorldSourceFile> files,
        ImmutableArray<PretPcMenuVariant> pcMenus, ImmutableArray<PretPokemonStorageMenuOption> storageMenu,
        ImmutableArray<OpeningItemSourceRef> rules, JsonElement scriptInventory, ImmutableArray<string> limitations)
    {
        GeneratedDirectory = generatedDirectory; Scripts = scripts; TextSources = texts; Constants = constants;
        UiTextSources = uiTexts; SourceUiTexts = uiTexts.ToImmutableDictionary(x => x.Key, x => x.Value.Text, StringComparer.Ordinal);
        SourceFiles = files; PcMenuVariants = pcMenus; StorageMenu = storageMenu; RulesSources = rules;
        ScriptInventory = scriptInventory; Limitations = limitations;
        AdditionalSourceHashes = ["pokemon-storage-lock:" + LockSha256, "pokemon-storage-generated:" + GeneratedSha256,
            .. files.Select(f => "pokemon-storage:" + f.Path + ":" + f.Sha256)];
    }

    public static PretPokemonStorageSource Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var rawDirectory = Path.Combine(project, "content/raw/pokemon-storage");
        var generatedDirectory = Path.Combine(project, "content/generated/pokemon-storage");
        byte[] ReadPinned(string root, string path, string expectedHash, int? expectedLength = null)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            Require(Hash(bytes) == expectedHash && (expectedLength is null || bytes.Length == expectedLength), "Pinned storage bytes differ: " + path);
            return bytes;
        }
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/fetch-pokemon-storage.lock.json", LockSha256));
        var generated = ReadPinned(generatedDirectory, "pokemon-storage.json", GeneratedSha256);
        using var document = JsonDocument.Parse(generated);
        var root = document.RootElement;
        Require(root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("profile").GetString() == Profile &&
            root.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && root.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            root.GetProperty("lockSha256").GetString() == LockSha256, "Storage profile differs.");
        Require(JsonElement.DeepEquals(root.GetProperty("inputFiles"), sourceLock.RootElement.GetProperty("files")), "Storage source inventory differs.");
        using var manifest = JsonDocument.Parse(ReadPinned(generatedDirectory, "manifest.json", ManifestSha256));
        var m = manifest.RootElement;
        Require(m.EnumerateObject().Count() == 7 && m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("profile").GetString() == Profile && m.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit &&
            m.GetProperty("lockSha256").GetString() == LockSha256 && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 43 && m.GetProperty("outputs").GetArrayLength() == 1, "Storage generated manifest differs.");
        var output = m.GetProperty("outputs")[0];
        Require(output.EnumerateObject().Count() == 3 && output.GetProperty("path").GetString() == "pokemon-storage.json" &&
            output.GetProperty("sha256").GetString() == GeneratedSha256 && output.GetProperty("byteLength").GetInt32() == generated.Length, "Storage generated output metadata differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var sha = entry.GetProperty("sha256").GetString()!;
            var blob = entry.GetProperty("gitBlobSha1").GetString()!;
            var length = entry.GetProperty("byteLength").GetInt32();
            var bytes = ReadPinned(rawDirectory, path, sha, length);
            var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            Require(Convert.ToHexStringLower(SHA1.HashData([.. header, .. bytes])) == blob, "Storage Git blob differs: " + path);
            var text = new UTF8Encoding(false, true).GetString(bytes);
            Require(raw.TryAdd(path, text), "Duplicate storage source path.");
            files.Add(new(path, blob, length, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
            switch (entry.GetProperty("roles")[0].GetString())
            {
                case "script":
                    var label = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant);
                    Require(label.Success, "Storage script has no original label.");
                    scripts.Add(new(path, text, new(SourceKind.Pret, path, label.Groups[1].Value, "pret/pokefirered", PretWorldMap.SourceCommit, sha)));
                    break;
                case "text": texts.Add(path, text); break;
            }
        }
        var inventory = files.ToImmutable();
        Require(inventory.Length == 43 && Hash(Encoding.UTF8.GetBytes(string.Concat(inventory.OrderBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash, "Storage source set differs.");
        using var rawManifest = JsonDocument.Parse(ReadPinned(rawDirectory, "manifest.json", RawManifestSha256));
        var rm = rawManifest.RootElement;
        Require(rm.EnumerateObject().Count() == 6 && rm.GetProperty("schemaVersion").GetInt32() == 1 &&
            rm.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && rm.GetProperty("tree").GetString() == root.GetProperty("tree").GetString() &&
            rm.GetProperty("inputCount").GetInt32() == inventory.Length && rm.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            rm.GetProperty("lockSha256").GetString() == LockSha256, "Storage raw manifest differs.");
        var options = ContentJson.CreateOptions();
        T Decode<T>(JsonElement value) => value.Deserialize<T>(options) ?? throw new InvalidDataException("Missing storage document value.");
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        void CheckSource(OpeningItemSourceRef source)
        {
            Require(byPath.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256 &&
                !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
                source.EndLine <= raw[source.Path].Split('\n').Length, "Storage source reference differs.");
        }
        var constants = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("constants").EnumerateObject())
        {
            constants.Add(property.Name, property.Value.GetProperty("value").GetInt32());
            CheckSource(Decode<OpeningItemSourceRef>(property.Value.GetProperty("source")));
        }
        Require(constants["TOTAL_BOXES_COUNT"] == 14 && constants["IN_BOX_COUNT"] == 30 && constants["MB_PC"] == 131 &&
            constants["FLAG_SYS_NOT_SOMEONES_PC"] == 0x834 && constants["FLAG_SYS_PC_STORAGE_DISABLED"] == 0x841 &&
            constants["SCR_MENU_CANCEL"] == 127, "Storage required source constants differ.");
        var uiTexts = Decode<ImmutableDictionary<string, PretPokemonStorageSourceText>>(root.GetProperty("uiTexts"));
        foreach (var text in uiTexts.Values) CheckSource(text.Source);
        var pcMenus = Decode<ImmutableArray<PretPcMenuVariant>>(root.GetProperty("pcMenuVariants"));
        var storageMenu = Decode<ImmutableArray<PretPokemonStorageMenuOption>>(root.GetProperty("storageMenu"));
        foreach (var menu in pcMenus) CheckSource(menu.Source);
        foreach (var option in storageMenu) CheckSource(option.Source);
        var rules = Decode<ImmutableArray<OpeningItemSourceRef>>(root.GetProperty("rulesSources"));
        foreach (var source in rules) CheckSource(source);
        Require(pcMenus.Length == 3 && storageMenu.Length == 5 && scripts.Count == 5 && texts.Count == 4 &&
            root.GetProperty("scripts").GetProperty("unresolvedLabelReferences").GetArrayLength() == 0, "Incomplete storage source contract.");
        return new(generatedDirectory, scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(), uiTexts, inventory,
            pcMenus, storageMenu, rules, root.GetProperty("scripts").Clone(), Decode<ImmutableArray<string>>(root.GetProperty("limitations")));
    }

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
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
