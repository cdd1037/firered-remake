using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>
/// Authenticated source for the first postgame National Dex scene on the existing Pallet/Oak maps.
/// Loading this package does not admit scripts, mutate saves, or enable Ruby/Network Machine/later Sevii.
/// Merge original source files by path AND hash before using the central PretStoryCompiler.
/// </summary>
public sealed class PretNationalDexSource
{
    public const string Profile = "firered-national-dex-source-v1";
    public const string FullSourceHash = "c52545d5d565e9ac54f962a91005192e65ded15b9717cc3fcc04c1adb9fb1d10";
    public const string LockSha256 = "5187c2de2fb88ff33d3fff7611d98d2342f2f3a48292ffde6d30cd82162d17f9";
    public const string ManifestSha256 = "e5e9eda94a1b469004cc7fba42facca1f5d4adc254d5e29dca17086d5985d1ca";
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement MovementBindings { get; }

    private PretNationalDexSource(SilphSourcePackage source)
    {
        SourceFiles = source.Files;
        Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json");
        var constants = source.Document("constants.json");
        MovementBindings = constants.GetProperty("movementBindings");
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        string Raw(string path) => new UTF8Encoding(false, true).GetString(source.Raw[path]);
        void Reference(JsonElement reference)
        {
            string path = reference.GetProperty("path").GetString()!;
            int line = reference.TryGetProperty("line", out var value) ? value.GetInt32() : 1;
            Need(reference.GetProperty("kind").GetString() == "pret" && reference.GetProperty("repository").GetString() == "pret/pokefirered" &&
                reference.GetProperty("commit").GetString() == PretWorldMap.SourceCommit, "National Dex source identity differs.");
            source.Reference(path, reference.GetProperty("sha256").GetString()!, line, line, reference.GetProperty("symbol").GetString()!);
        }
        foreach (var document in new[] { Contracts, ScriptInventory, constants })
            Need(document.GetProperty("schemaVersion").GetInt32() == 1 && document.GetProperty("profile").GetString() == Profile,
                "National Dex document profile differs.");
        Need(Contracts.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            Contracts.GetProperty("sourceCommit").GetString() == PretWorldMap.SourceCommit &&
            Contracts.GetProperty("sourceTree").GetString() == SilphSourcePackage.SourceTree &&
            JsonElement.DeepEquals(Contracts.GetProperty("inputFiles"), source.Lock.GetProperty("files")) &&
            !Contracts.GetProperty("runtimeIntegrated").GetBoolean() && Contracts.GetProperty("mapCount").GetInt32() == 0,
            "National Dex package scope differs.");
        foreach (string section in new[] { "blocks", "dispatchBlocks" })
            foreach (var block in ScriptInventory.GetProperty(section).EnumerateArray())
            {
                var reference = block.GetProperty("sourceRef"); Reference(reference);
                var lines = Raw(reference.GetProperty("path").GetString()!).Split('\n');
                foreach (var row in block.GetProperty("lines").EnumerateArray())
                {
                    int index = row.GetProperty("line").GetInt32() - 1;
                    Need(index >= 0 && index < lines.Length && lines[index] == row.GetProperty("text").GetString(), "National Dex instruction differs.");
                }
            }
        foreach (var native in Contracts.GetProperty("nativeDependencies").EnumerateArray()) Reference(native.GetProperty("sourceRef"));
        foreach (var binding in Contracts.GetProperty("specialBindings").EnumerateArray()) Reference(binding.GetProperty("sourceRef"));
        foreach (var binding in MovementBindings.EnumerateObject()) Reference(binding.Value.GetProperty("sourceRef"));
        Constants = constants.GetProperty("values").EnumerateObject().ToImmutableDictionary(p => p.Name, p =>
        {
            Reference(p.Value.GetProperty("sourceRef"));
            if (p.Value.TryGetProperty("generatorSource", out var generator)) Reference(generator);
            return p.Value.GetProperty("value").GetInt32();
        }, StringComparer.Ordinal);
        var grant = Contracts.GetProperty("grant");
        Need(Contracts.GetProperty("entry").GetProperty("minimumCaught").GetInt32() == 60 &&
            grant.GetProperty("nationalMagic").GetInt32() == 0xB9 && grant.GetProperty("variableValue").GetInt32() == 0x6258 &&
            grant.GetProperty("variable").GetString() == "VAR_NATIONAL_DEX" && grant.GetProperty("flag").GetString() == "FLAG_SYS_NATIONAL_DEX" &&
            grant.GetProperty("labArrivalScene").GetInt32() == 7 && grant.GetProperty("labGrantedScene").GetInt32() == 8 &&
            grant.GetProperty("labNextTransitionScene").GetInt32() == 9 &&
            ScriptInventory.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "National Dex grant contract differs.");
        Scripts = ScriptInventory.GetProperty("sourcePaths").EnumerateArray().Select(p =>
        {
            string path = p.GetString()!;
            return new PretSourceScriptFile(path, Raw(path), new(SourceKind.Pret, path, "national-dex-source-library", "pret/pokefirered", PretWorldMap.SourceCommit, files[path].Sha256));
        }).ToImmutableArray();
        TextSources = ScriptInventory.GetProperty("textPaths").EnumerateArray().ToImmutableDictionary(p => p.GetString()!, p => Raw(p.GetString()!), StringComparer.Ordinal);
        AdditionalSourceHashes = ["national-dex-source-lock:" + LockSha256, "national-dex-source-manifest:" + ManifestSha256,
            .. SourceFiles.Select(f => "national-dex-source:" + f.Path + ":" + f.Sha256)];
    }

    public static PretNationalDexSource Load(string projectRoot) => new(new SilphSourcePackage(projectRoot,
        "national-dex-source", Profile, "content/sources/national-dex-source.lock.json", LockSha256, 9210,
        ManifestSha256, 1018, 31, 3));

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
