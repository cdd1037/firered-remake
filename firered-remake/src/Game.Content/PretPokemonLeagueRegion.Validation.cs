using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretPokemonLeagueRegion
{
    private static SilphSourcePackage PokemonLeagueSource(string root) => new(root, "pokemon-league-region", Profile,
        "content/sources/pokemon-league-region.lock.json", LockSha256, 66292,
        ManifestSha256, 15196, 203, 83);

    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(!contract.GetProperty("runtimeIntegrated").GetBoolean() && contract.GetProperty("firstClearOnly").GetBoolean() &&
            !source.Document("declarations.json").GetProperty("ownedCatalogModified").GetBoolean(), "Source evidence cannot admit runtime or ownership changes.");
        var sizes = new[] { ("map.indigo-plateau-pokemon-center-1f", 25, 18), ("map.pokemon-league-loreleis-room", 13, 13),
            ("map.pokemon-league-brunos-room", 13, 13), ("map.pokemon-league-agathas-room", 13, 13),
            ("map.pokemon-league-lances-room", 28, 24), ("map.pokemon-league-champions-room", 13, 20), ("map.pokemon-league-hall-of-fame", 11, 13) };
        foreach (var (id, width, height) in sizes)
        {
            var m = maps[new(id)];
            Need(m.Width == width && m.Height == height && m.BorderWidth == 2 && m.BorderHeight == 2 &&
                !m.Events.GetProperty("requires_flash").GetBoolean() && !m.Events.GetProperty("allow_escaping").GetBoolean(), "Original League room geometry/escape policy differs.");
        }
        var rooms = contract.GetProperty("rooms").EnumerateArray().ToArray();
        var names = new[] { "LORELEI", "BRUNO", "AGATHA", "LANCE" };
        Need(rooms.Length == 4, "Four League room contracts required.");
        for (int i = 0; i < rooms.Length; i++)
        {
            var r = rooms[i]; var map = maps[MapIdFromSourceSymbol(r.GetProperty("map").GetString()!)];
            Need(r.GetProperty("order").GetInt32() == i + 1 && r.GetProperty("entryScene").GetInt32() == i &&
                r.GetProperty("insideScene").GetInt32() == i + 1 && r.GetProperty("actorLocalId").GetInt32() == 1 &&
                r.GetProperty("defeatedFlag").GetString() == "FLAG_DEFEATED_" + names[i] &&
                r.GetProperty("battleTrainer").GetString() == "TRAINER_ELITE_FOUR_" + names[i] &&
                r.GetProperty("battleSelectionFlag").GetString() == "FLAG_SYS_CAN_LINK_WITH_RS" && !r.GetProperty("runtimeAdmitted").GetBoolean() &&
                JsonElement.DeepEquals(r.GetProperty("actor"), map.Events.GetProperty("object_events")[0]), "Original League scene, actor or battle branch differs.");
        }
        var branches = contract.GetProperty("champion").GetProperty("branches").EnumerateArray().ToArray();
        var players = new[] { "BULBASAUR", "SQUIRTLE", "CHARMANDER" }; var rivals = new[] { "CHARMANDER", "BULBASAUR", "SQUIRTLE" };
        Need(branches.Length == 3, "Champion starter branch count differs.");
        for (int i = 0; i < 3; i++) Need(branches[i].GetProperty("starterVariableValue").GetInt32() == i &&
            branches[i].GetProperty("playerStarter").GetString() == "SPECIES_" + players[i] &&
            branches[i].GetProperty("rivalStarter").GetString() == "SPECIES_" + rivals[i] &&
            branches[i].GetProperty("firstTrainer").GetString() == "TRAINER_CHAMPION_FIRST_" + rivals[i], "Player/rival starter mapping differs.");
        Need(contract.GetProperty("tilePatches").GetArrayLength() == 5 && contract.GetProperty("trainerOccurrences").GetArrayLength() == 14 &&
            contract.GetProperty("objects").GetArrayLength() == 15 && source.Document("npc-source.json").GetProperty("invisibleActors").GetArrayLength() == 2,
            "Original League patch/trainer/object scope differs.");
        Need(contract.GetProperty("defeatReset").GetProperty("noAutomaticHealingBetweenRooms").GetBoolean() &&
            contract.GetProperty("defeatReset").GetProperty("noLossContinuationToVictoryScript").GetBoolean() &&
            contract.GetProperty("defeatReset").GetProperty("respawn").GetProperty("x").GetInt32() == 13 &&
            contract.GetProperty("defeatReset").GetProperty("respawn").GetProperty("y").GetInt32() == 12 &&
            !contract.GetProperty("hallOfFame").GetProperty("runtimeAdmitted").GetBoolean() &&
            contract.GetProperty("hallOfFame").GetProperty("saveMode").GetString() == "SAVE_HALL_OF_FAME" &&
            contract.GetProperty("hallOfFame").GetProperty("continueMap").GetString() == "MAP_PALLET_TOWN" &&
            contract.GetProperty("hallOfFame").GetProperty("continueX").GetInt32() == 6 && contract.GetProperty("hallOfFame").GetProperty("continueY").GetInt32() == 8,
            "League loss/save boundary differs.");
        Need(contract.GetProperty("boundaries").EnumerateArray().All(b => !b.GetProperty("runtimeAdmitted").GetBoolean()) &&
            contract.GetProperty("boundaries")[0].GetProperty("destination").GetString() == "MAP_INDIGO_PLATEAU_POKEMON_CENTER_2F" &&
            contract.GetProperty("boundaries")[1].GetProperty("sceneValue").GetInt32() == 1 &&
            !contract.GetProperty("reusedAnchors")[0].GetProperty("generatedGeometryCopied").GetBoolean(), "Unadmitted 2F/credits/reused-exterior boundary differs.");
        foreach (var block in source.Document("scripts.json").GetProperty("blocks").EnumerateArray())
        {
            var reference = block.GetProperty("sourceRef"); var path = reference.GetProperty("path").GetString()!;
            Need(source.Files.Any(f => f.Path == path && f.Sha256 == reference.GetProperty("sha256").GetString()), "Script source hash differs.");
            var original = Encoding.UTF8.GetString(source.Raw[path]).Split('\n');
            Need(original[reference.GetProperty("line").GetInt32() - 1].TrimEnd(':', '\r') == block.GetProperty("label").GetString(), "Script label line differs.");
            foreach (var line in block.GetProperty("lines").EnumerateArray()) Need(original[line.GetProperty("line").GetInt32() - 1].TrimEnd('\r') == line.GetProperty("text").GetString(), "Script transcript differs.");
        }
        foreach (var mechanism in contract.GetProperty("nativeMechanisms").EnumerateArray())
        {
            var r = mechanism.GetProperty("sourceRef"); var p = r.GetProperty("path").GetString()!; int start = r.GetProperty("line").GetInt32(), end = mechanism.GetProperty("endLine").GetInt32();
            source.Reference(p, r.GetProperty("sha256").GetString()!, start, end, mechanism.GetProperty("symbol").GetString()!);
            Need(string.Join("\n", Encoding.UTF8.GetString(source.Raw[p]).Split('\n').Skip(start - 1).Take(end - start + 1)) == mechanism.GetProperty("text").GetString() &&
                !mechanism.GetProperty("runtimeImplemented").GetBoolean(), "Native mechanism transcript/admission differs.");
        }
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "League source map symbol differs.");
        Need(constants["VAR_MAP_SCENE_POKEMON_LEAGUE"] > 0 && constants["HEAL_LOCATION_INDIGO_PLATEAU"] == 10 && constants["HEAL_LOCATION_PALLET_TOWN"] == 1,
            "League source scene/heal constants differ.");
    }
}
