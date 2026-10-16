using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Content;
using Game.Domain.Trainers;

if (args.Length == 2 && args[0] == "inspect")
{
    var inspectCatalog = PretCampaignCatalog.LoadCurrent(args[1]);
    var inspectSession = CampaignSession.CreateCurrent(inspectCatalog);
    Console.WriteLine(JsonSerializer.Serialize(new { content = inspectCatalog.ContentVersion, rules = inspectSession.RulesVersion,
        newGameValidation = inspectSession.ValidatePlayerSave(inspectSession.State), fanClubEnabled = inspectCatalog.TrainerFanClubEnabled }));
    return;
}

if (args.Length != 7) throw new ArgumentException("PROJECT_ROOT SOURCE NEW_DESTINATION KNOWN_SOURCE_SHA256 EXPECTED_OLD_CONTENT CURRENT_CONTENT CURRENT_RULES");
string sourcePath = Path.GetFullPath(args[1]), destinationPath = Path.GetFullPath(args[2]);
if (sourcePath == destinationPath || File.Exists(destinationPath) || Directory.Exists(destinationPath))
    throw new InvalidDataException("conversion_requires_new_destination");
byte[] original = File.ReadAllBytes(sourcePath);
string originalHash = Convert.ToHexStringLower(SHA256.HashData(original));
if (originalHash != args[3] || args[3].Length != 64) throw new InvalidDataException("conversion_source_sha_mismatch");
_ = SaveCodec.Decode<JsonElement>(original, "firered-current-campaign-v1", args[4]);
var originalNode = JsonNode.Parse(original)!.AsObject();
var originalEnvelope = originalNode.Deserialize<SaveEnvelope>() ?? throw new InvalidDataException("conversion_envelope_missing");
if (originalEnvelope.SchemaVersion != SaveCodec.CurrentSchema || originalEnvelope.RulesVersion != "firered-current-campaign-v1" ||
    originalEnvelope.ContentVersion != args[4] || originalEnvelope.GameEdition != "firered" || originalEnvelope.Generation < 0 ||
    CanonicalJson.Hash(originalEnvelope.Payload) != originalEnvelope.PayloadHash)
    throw new InvalidDataException("conversion_source_envelope_invalid");
var catalog = PretCampaignCatalog.LoadCurrent(args[0]);
var fresh = CampaignSession.CreateCurrent(catalog);
if (catalog.ContentVersion != args[5] || fresh.RulesVersion != args[6] || !catalog.TrainerFanClubEnabled)
    throw new InvalidDataException("conversion_target_identity_mismatch");
var payload = originalNode["Payload"]!.DeepClone().AsObject();
if (payload.ContainsKey("PlayTime")) throw new InvalidDataException("conversion_source_has_clock");
var variables = payload["Script"]!["Variables"]!.AsObject();
var flags = payload["Script"]!["Flags"]!.AsObject();
string[] fanKeys = [TrainerFanClubRules.CounterVariable, TrainerFanClubRules.LoseFanHoursVariable, TrainerFanClubRules.SceneVariable];
if (fanKeys.Any(key => variables[key] is {} value && value.GetValue<int>() != 0))
    throw new InvalidDataException("conversion_source_has_fans");
var hall = payload["HallOfFame"] as JsonObject;
if ((flags["FLAG_SYS_GAME_CLEAR"]?.GetValue<bool>() ?? false) != (hall is not null))
    throw new InvalidDataException("conversion_source_clear_inconsistent");
payload["PlayTime"] = JsonSerializer.SerializeToNode(new CampaignPlayTime(0, 0, 0, 0, true));
variables[TrainerFanClubRules.CounterVariable] = hall is null ? 0 : 0x780;
variables[TrainerFanClubRules.LoseFanHoursVariable] = 0;
variables[TrainerFanClubRules.SceneVariable] = hall is null ? 0 : 1;
if (hall is not null)
{
    if (hall["FanClubUpdateDeferred"]?.GetValue<bool>() != true || hall["FirstHallOfFamePlayTime"] is not null)
        throw new InvalidDataException("conversion_source_hall_format_invalid");
    hall.Remove("FanClubUpdateDeferred");
    hall["FirstHallOfFamePlayTime"] = null;
    foreach (string flag in TrainerFanClubRules.FirstClearHideFlags) flags[flag] = false;
    if (payload["GameStats"] is not JsonObject) payload["GameStats"] = new JsonObject();
    var stats = payload["GameStats"]!.AsObject();
    if (stats.ContainsKey("GAME_STAT_FIRST_HOF_PLAY_TIME")) throw new InvalidDataException("conversion_source_has_hall_clock");
    stats["GAME_STAT_FIRST_HOF_PLAY_TIME"] = 0;
}
var state = payload.Deserialize<CampaignState>(new JsonSerializerOptions { AllowOutOfOrderMetadataProperties = true })
    ?? throw new InvalidDataException("conversion_state_missing");
if (fresh.ValidatePlayerSave(state) is {} error) throw new InvalidDataException("conversion_semantic_invalid: " + error);
byte[] encoded = SaveCodec.Encode(state, originalEnvelope.Generation, fresh.RulesVersion, catalog.ContentVersion);
var loaded = SaveCodec.Decode<CampaignState>(encoded, fresh.RulesVersion, catalog.ContentVersion, validate: fresh.ValidatePlayerSave);
var continued = CampaignSession.RestoreCurrent(catalog, loaded.State, loaded.Generation);
if (continued.Revision != originalEnvelope.Generation || CanonicalJson.Hash(continued.State) != CanonicalJson.Hash(state))
    throw new InvalidDataException("conversion_restore_mutated_state");
var changes = new List<PayloadChange>();
void Diff(JsonNode? before, JsonNode? after, string path)
{
    if (JsonNode.DeepEquals(before, after)) return;
    if (before is JsonObject b && after is JsonObject a)
    {
        foreach (var key in b.Select(p => p.Key).Union(a.Select(p => p.Key)).Order(StringComparer.Ordinal))
            Diff(b[key], a[key], path + "/" + key);
    }
    else changes.Add(new(path, before?.DeepClone(), after?.DeepClone()));
}
Diff(originalNode["Payload"], JsonSerializer.SerializeToNode(state), "/Payload");
var allowed = new HashSet<string>(["/Payload/PlayTime", "/Payload/HallOfFame/FanClubUpdateDeferred",
    "/Payload/GameStats/GAME_STAT_FIRST_HOF_PLAY_TIME", ..fanKeys.Select(key => "/Payload/Script/Variables/" + key),
    ..TrainerFanClubRules.FirstClearHideFlags.Select(flag => "/Payload/Script/Flags/" + flag)], StringComparer.Ordinal);
if (changes.Any(change => !allowed.Contains(change.Path)))
    throw new InvalidDataException("conversion_unexpected_payload_change: " + string.Join(",", changes.Where(c => !allowed.Contains(c.Path)).Select(c => c.Path)));
using (var file = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
{ file.Write(encoded); file.Flush(true); }
if (!File.ReadAllBytes(destinationPath).AsSpan().SequenceEqual(encoded) ||
    Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(sourcePath))) != originalHash)
    throw new IOException("conversion_readback_failed");
Console.WriteLine(JsonSerializer.Serialize(new
{
    source = sourcePath, destination = destinationPath, generation = originalEnvelope.Generation,
    sourceSha256 = originalHash, destinationSha256 = Convert.ToHexStringLower(SHA256.HashData(encoded)),
    previousRules = originalEnvelope.RulesVersion, rules = fresh.RulesVersion,
    previousContent = originalEnvelope.ContentVersion, content = catalog.ContentVersion,
    pastPlayTime = "UNKNOWN", observedClock = state.PlayTime,
    historicalFirstHallOfFameTime = hall is null ? "NO_PREVIOUS_HALL" : "UNKNOWN",
    changedPayloadFields = changes
}, new JsonSerializerOptions { WriteIndented = true }));

sealed record PayloadChange(string Path, JsonNode? Before, JsonNode? After);
