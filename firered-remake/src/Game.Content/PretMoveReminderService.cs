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
    public const string LockSha256 = "2a1170f654bd268418ed7e5f1eda56f5af7ab64152ea7e8dd8829247290455b1";
    public const string ContractSha256 = "6259fb2d70a130cbb516bfc94ac125cdc0c2cba3a5144a092bed4517082d9381";
    public MoveReminderCatalog Catalog { get; }
    public JsonElement Contracts { get; }
    public ImmutableDictionary<string, string> Texts => Catalog.Texts;
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretMoveReminderService(MoveReminderCatalog catalog, JsonElement contracts, ImmutableDictionary<string, string> texts,
        ImmutableArray<string> hashes)
    { Catalog = catalog; Contracts = contracts; TextSources = texts; AdditionalSourceHashes = hashes; }

    public static PretMoveReminderService Load(string root, PokemonCatalog owned)
    {
        static byte[] Read(string path, int count, string hash, string? blob = null)
        {
            if (count is < 0 or > 2 * 1024 * 1024) throw new InvalidDataException("move_reminder_source_limit");
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length != count) throw new InvalidDataException("move_reminder_source_size");
            var bytes = new byte[count]; input.ReadExactly(bytes);
            if (input.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("move_reminder_source_hash");
            if (blob is not null)
            {
                byte[] header = Encoding.UTF8.GetBytes($"blob {count}\0");
                if (Convert.ToHexStringLower(SHA1.HashData(header.Concat(bytes).ToArray())) != blob)
                    throw new InvalidDataException("move_reminder_source_blob");
            }
            return bytes;
        }
        using var document = JsonDocument.Parse(Read(Path.Combine(root, "scripts/move-reminder.lock.json"), 3981, LockSha256));
        var doc = document.RootElement;
        if (doc.GetProperty("schemaVersion").GetInt32() != 1 || doc.GetProperty("profile").GetString() != Profile ||
            doc.GetProperty("commit").GetString() != PokemonFactory.SourceCommit || doc.GetProperty("files").GetArrayLength() != 17)
            throw new InvalidDataException("move_reminder_source_scope");
        using var contract = JsonDocument.Parse(Read(Path.Combine(root, "scripts/move-reminder.contract.json"), 2139, ContractSha256));
        var hashes = ImmutableArray.CreateBuilder<string>(); hashes.Add("move-reminder-lock:" + LockSha256);
        hashes.Add("move-reminder-contract:" + ContractSha256);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in doc.GetProperty("files").EnumerateArray())
        {
            string path = row.GetProperty("path").GetString()!, hash = row.GetProperty("sha256").GetString()!;
            if (Path.IsPathRooted(path) || path.Split('/').Any(p => p is "" or "." or "..") || files.ContainsKey(path))
                throw new InvalidDataException("move_reminder_source_path");
            files.Add(path, Read(Path.Combine(root, "content/raw/move-reminder", path), row.GetProperty("bytes").GetInt32(), hash,
                row.GetProperty("gitBlobSha1").GetString()));
            hashes.Add("move-reminder-source:" + path + ":" + hash);
        }
        var catalog = MoveReminderCatalog.Authenticate(owned, files["src/pokemon.c"], files["src/learn_move.c"], files["src/strings.c"]);
        return new(catalog, contract.RootElement.Clone(), ImmutableDictionary<string, string>.Empty
            .Add("src/strings.c", Encoding.UTF8.GetString(files["src/strings.c"]))
            .Add("data/maps/TwoIsland_House/text.inc", Encoding.UTF8.GetString(files["data/maps/TwoIsland_House/text.inc"])), hashes.ToImmutable());
    }
}
