using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Authenticated service evidence and native text; field map admission is PretMoveReminderHouse.</summary>
public sealed class PretMoveReminderService
{
    public const string Profile = "firered-move-reminder-v1";
    public MoveReminderCatalog Catalog { get; }
    public JsonElement Contracts { get; }
    public ImmutableDictionary<string, string> Texts => Catalog.Texts;
    public ImmutableDictionary<string, string> TextSources { get; }

    private PretMoveReminderService(MoveReminderCatalog catalog,JsonElement contracts,ImmutableDictionary<string, string> texts)
    { Catalog = catalog; Contracts = contracts; TextSources = texts; }

    public static PretMoveReminderService Load(string root, PokemonCatalog owned)
    {
        static byte[] Read(string path)
        {
var bytes = File.ReadAllBytes(path);
return bytes;
        }
        using var document = JsonDocument.Parse(Read(Path.Combine(root, "scripts/move-reminder.lock.json")));
        var doc = document.RootElement;
        if (doc.GetProperty("schemaVersion").GetInt32() != 1 )
            throw new InvalidDataException("move_reminder_source_scope");
        using var contract = JsonDocument.Parse(Read(Path.Combine(root, "scripts/move-reminder.contract.json")));
var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in doc.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!;
            if (Path.IsPathRooted(path) || path.Split('/').Any(p => p is "" or "." or "..") || files.ContainsKey(path))
                throw new InvalidDataException("move_reminder_source_path");
            if (path is not "src/strings.c" and not "data/maps/TwoIsland_House/text.inc") continue;
            files.Add(path, Read(Path.Combine(root, PretSourcePaths.FireRedRawDirectory, path)));
        }
        var catalog = MoveReminderCatalog.Authenticate(owned, files["src/strings.c"]);
        return new(catalog,contract.RootElement.Clone(),ImmutableDictionary<string, string>.Empty
            .Add("src/strings.c", Encoding.UTF8.GetString(files["src/strings.c"]))
            .Add("data/maps/TwoIsland_House/text.inc", Encoding.UTF8.GetString(files["data/maps/TwoIsland_House/text.inc"])));
    }
}
