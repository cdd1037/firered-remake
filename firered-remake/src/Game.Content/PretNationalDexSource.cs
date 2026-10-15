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
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Contracts { get; }
    public JsonElement ScriptInventory { get; }
    public JsonElement MovementBindings { get; }

    private PretNationalDexSource(SilphSourcePackage source)
    {
        SourceFiles = source.Files;
        Contracts = source.Document("contracts.json");
        ScriptInventory = source.Document("scripts.json");
        MovementBindings = source.Document("movement-bindings.json");
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        string Raw(string path) => new UTF8Encoding(false, true).GetString(source.Raw[path]);
        foreach (var document in new[] { Contracts,ScriptInventory })
            Need(document.GetProperty("schemaVersion").GetInt32() == 1 ,
                "National Dex document profile differs.");
        Need(            !Contracts.GetProperty("runtimeIntegrated").GetBoolean() ,
            "National Dex package scope differs.");
        foreach (string section in new[] { "blocks","dispatchBlocks" })
            foreach (var block in ScriptInventory.GetProperty(section).EnumerateArray())
            {
                var reference = block.GetProperty("sourceRef");                 var lines = Raw(reference.GetProperty("path").GetString()!).Split('\n');
                foreach (var row in block.GetProperty("lines").EnumerateArray())
                {
                    int index = row.GetProperty("line").GetInt32() - 1;
                    Need(index >= 0 && index < lines.Length && lines[index] == row.GetProperty("text").GetString(), "National Dex instruction differs.");
                }
            }
        Constants = PretSharedConstants.ForPackage(source.Directory);
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
            return new PretSourceScriptFile(path, Raw(path), new(SourceKind.Pret,path,"national-dex-source-library","pret/pokefirered",PretWorldMap.SourceCommit));
        }).ToImmutableArray();
        TextSources = ScriptInventory.GetProperty("textPaths").EnumerateArray().ToImmutableDictionary(p => p.GetString()!, p => Raw(p.GetString()!), StringComparer.Ordinal);
    }

    public static PretNationalDexSource Load(string projectRoot) => new(new SilphSourcePackage(projectRoot,        "national-dex-source","content/sources/national-dex-source.lock.json"));

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
