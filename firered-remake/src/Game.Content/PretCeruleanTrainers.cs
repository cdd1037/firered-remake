using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Seven authenticated Cerulean parties; English name bytes remain inputs to the source NPC PID.</summary>
public static class PretCeruleanTrainers
{
    public const string LockSha256 = "c19f153c821aeefa2c8562371c2afdabc29aa02e4b5c6d5d05bcbd2d41d9dadb";
    public static CeruleanTrainerCatalog Load(string root) => Load(PretCeruleanRegion.Load(root), PretCeruleanPokemon.Load(root));

    public static CeruleanTrainerCatalog Load(PretCeruleanRegion region, PretCeruleanPokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region); ArgumentNullException.ThrowIfNull(pokemon);
        string root = Path.GetFullPath(Path.Combine(region.GeneratedDirectory, "../../.."));
        Require(root == Path.GetFullPath(Path.Combine(pokemon.GeneratedDirectory, "../../..")), "Cerulean catalogs have different roots.");
        var supplement = ReadSource(root);
        var moneyText = Encoding.UTF8.GetString(supplement["src/battle_main.c"]);
        var setup = Encoding.UTF8.GetString(supplement["src/battle_setup.c"]);
        Require(setup.Contains("gMain.savedCallback = CB2_EndTrainerBattle;", StringComparison.Ordinal) &&
            setup.Contains("return EventScript_DoNoIntroTrainerBattle;", StringComparison.Ordinal) &&
            setup.Contains("return EventScript_TryDoNormalTrainerBattle;", StringComparison.Ordinal), "Cerulean callback source differs.");
        var files = region.SourceFiles.ToDictionary(x => x.Path, StringComparer.Ordinal);
        var names = pokemon.SourceFiles.Single(f => f.Path == "src/data/text/species_names.h");
        TrainerSourceRef Ref(JsonElement value)
        {
            string path = value.GetProperty("path").GetString()!;
            int start = value.TryGetProperty("line", out var line) ? line.GetInt32() : value.GetProperty("startLine").GetInt32();
            int end = value.TryGetProperty("endLine", out line) ? line.GetInt32() : start;
            Require(value.GetProperty("kind").GetString() == "pret" && value.GetProperty("repository").GetString() == "pret/pokefirered" &&
                value.GetProperty("commit").GetString() == CeruleanTrainerPartyFactory.SourceCommit && start > 0 && end >= start &&
                files.TryGetValue(path, out var f) && value.GetProperty("sha256").GetString() == f.Sha256, "Cerulean trainer source identity differs.");
            return new(path, value.GetProperty("symbol").GetString()!, start, end, value.GetProperty("sha256").GetString()!);
        }
        ImmutableArray<int> Encode(string name) => name.Select(c => c is >= 'A' and <= 'Z' ? 0xbb + c - 'A' :
            throw new InvalidDataException("Unsupported source trainer/species name glyph.")).ToImmutableArray();
        int Constant(string symbol) => symbol == "F_TRAINER_FEMALE" ? 1 << 7 : region.Constants.TryGetValue(symbol, out int value) ? value :
            throw new InvalidDataException("Missing source trainer constant: " + symbol);
        int Expression(string text) => text.Split('|', StringSplitOptions.TrimEntries).Aggregate(0, (value, symbol) => value | Constant(symbol));
        Require(region.Trainers.GetArrayLength() == 7, "All seven Cerulean trainers are required.");
        var scoped = pokemon.Scope.GetProperty("trainers").EnumerateArray().ToDictionary(t => t.GetProperty("trainer").GetString()!, StringComparer.Ordinal);
        Require(scoped.Count == 7, "All seven Cerulean Pokemon parties are required.");
        var trainers = ImmutableDictionary.CreateBuilder<TrainerId, TrainerDefinition>();
        var battles = ImmutableDictionary.CreateBuilder<TrainerId, CeruleanTrainerBattleDefinition>();
        foreach (var t in region.Trainers.EnumerateArray())
        {
            string symbol = t.GetProperty("symbol").GetString()!;
            var id = new TrainerId("pret:" + symbol);
            Require(CeruleanTrainerPartyFactory.SupportedTrainers.Contains(id) && scoped.TryGetValue(symbol, out _), "Unknown Cerulean source trainer.");
            var scope = scoped[symbol]; var source = Ref(t.GetProperty("sourceRef")); var partySource = Ref(scope.GetProperty("partySource"));
            var regionPartySource = Ref(t.GetProperty("party").GetProperty("sourceRef"));
            Require(source == Ref(scope.GetProperty("trainerSource")) with { EndLine = source.EndLine } &&
                regionPartySource == partySource with { EndLine = regionPartySource.EndLine } &&
                scope.GetProperty("sourceTrainerId").GetInt32() == t.GetProperty("sourceTrainerId").GetInt32(), "Independent Cerulean party provenance differs.");
            var declaration = Regex.Matches(t.GetProperty("sourceText").GetString()!, @"^\s*\.(\w+) = (.+),$", RegexOptions.Multiline | RegexOptions.CultureInvariant)
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
            string format = t.GetProperty("partyFormat").GetString()!;
            bool custom = format == "NO_ITEM_CUSTOM_MOVES";
            Require((custom || format == "NO_ITEM_DEFAULT_MOVES") && format == scope.GetProperty("partyFormat").GetString() &&
                declaration.Count == 8 && declaration["doubleBattle"] == "FALSE" && declaration["party"] == format + "(" + partySource.Symbol + ")",
                "Unsupported Cerulean trainer declaration.");
            var nameMatch = Regex.Match(declaration["trainerName"], "^_\\(\"([A-Z]+)\"\\)$", RegexOptions.CultureInvariant);
            Require(nameMatch.Success, "Cerulean trainer name differs.");
            string name = nameMatch.Groups[1].Value;
            var items = declaration["items"].Trim('{', '}').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Require(items.Length <= 4 && items.All(x => x == "ITEM_SUPER_POTION"), "Unsupported Cerulean trainer items.");
            var party = scope.GetProperty("party").EnumerateArray().Select(m =>
            {
                string speciesSymbol = m.GetProperty("species").GetString()!;
                var species = new SpeciesId("pret:" + speciesSymbol);
                Require(pokemon.Catalog.Species.TryGetValue(species, out _), "Unknown Cerulean party species.");
                int speciesId = pokemon.Catalog.Species[species].SourceSpeciesId;
                string speciesName = pokemon.SpeciesNames[speciesId]; var encoded = Encode(speciesName);
                Require(speciesSymbol == "SPECIES_" + speciesName, "Cerulean source species name differs.");
                if (pokemon.EncodedNames.TryGetValue(speciesSymbol, out var canonical))
                    Require(canonical.Bytes.SequenceEqual(encoded.Add(0xff)), "Cerulean source species-name bytes differ.");
                var moves = custom ? m.GetProperty("sourceMoveSlots").EnumerateArray().Select(v => new MoveId("pret:" + v.GetString())).ToImmutableArray() : [];
                return new TrainerPartyMemberDefinition(species, speciesId, speciesName, encoded, m.GetProperty("level").GetInt32(),
                    m.GetProperty("iv").GetInt32(), "ITEM_NONE", moves, partySource, new(names.Path, speciesSymbol, speciesId + 2, speciesId + 2, names.Sha256));
            }).ToImmutableArray();
            string trainerClass = declaration["trainerClass"];
            var money = Regex.Match(moneyText, @"^\s*\{" + trainerClass + @", (\d+)\},$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
            Require(money.Success, "Missing Cerulean trainer money class.");
            int moneyLine = moneyText.AsSpan(0, money.Index).Count('\n') + 1;
            trainers.Add(id, new(id, t.GetProperty("sourceTrainerId").GetInt32(), trainerClass, Constant(trainerClass),
                declaration["trainerPic"], Constant(declaration["trainerPic"]), name, Encode(name), Expression(declaration["encounterMusic_gender"]),
                items.Concat(Enumerable.Repeat("ITEM_NONE", 4 - items.Length)).ToImmutableArray(), false, custom ? 1 : 0,
                (uint)Expression(declaration["aiFlags"]), declaration["aiFlags"].Split('|', StringSplitOptions.TrimEntries).ToImmutableArray(),
                int.Parse(money.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), party, Ref(scope.GetProperty("trainerSource")),
                new("src/battle_main.c", trainerClass, moneyLine, moneyLine, Hash(supplement["src/battle_main.c"]))));
            bool rival = symbol.StartsWith("TRAINER_RIVAL_", StringComparison.Ordinal), grunt = id == CeruleanTrainerPartyFactory.Rocket, misty = id == CeruleanTrainerPartyFactory.Misty;
            string map = rival || grunt ? "CeruleanCity" : "CeruleanCity_Gym";
            string person = rival ? "Rival" : grunt ? "Grunt" : name[..1] + name[1..].ToLowerInvariant();
            string last = symbol.Split('_')[^1];
            string suffix = rival ? last[..1] + last[1..].ToLowerInvariant() : "";
            string entry = map + "_EventScript_" + person + suffix, intro = map + "_Text_" + person + "Intro", defeat = map + "_Text_" + person + "Defeat";
            var script = region.Scripts.Single(s => s.Path == "data/maps/" + map + "/scripts.inc");
            string expected = rival || grunt ? "trainerbattle_no_intro " + symbol + ", " + defeat :
                "trainerbattle_single " + symbol + ", " + intro + ", " + defeat + (misty ? ", CeruleanCity_Gym_EventScript_MistyDefeated, NO_MUSIC" : "");
            var label = Regex.Match(script.Text, "^" + entry + @"::\n(?<body>(?:(?!^\w+::).)*?)\t" + Regex.Escape(expected) + "$", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            Require(label.Success, "Cerulean battle source operands differ: " + symbol);
            int scriptLine = script.Text.AsSpan(0, label.Index).Count('\n') + 1;
            battles.Add(id, new(id, rival || grunt ? 3 : misty ? 1 : 0, 0, 8, entry, intro, defeat, "",
                misty ? "CeruleanCity_Gym_Text_ExplainCascadeBadge" : grunt ? "CeruleanCity_Text_RecoveredTM28FromGrunt" : map + "_Text_" + person + "PostBattle",
                rival || grunt ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle", "CB2_EndTrainerBattle",
                new(script.Path, entry, scriptLine, scriptLine + label.Value.Count(c => c == '\n'), files[script.Path].Sha256))
                { PostBattleScript = misty ? "CeruleanCity_Gym_EventScript_MistyDefeated" : null, MusicOption = misty ? "NO_MUSIC" : null });
        }
        var result = new CeruleanTrainerCatalog(CeruleanTrainerPartyFactory.SourceCommit, CeruleanTrainerPartyFactory.RulesVersion, false, trainers.ToImmutable(), battles.ToImmutable());
        CeruleanTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }

    internal static Dictionary<string, byte[]> ReadSource(string root)
    {
        using var json = JsonDocument.Parse(ReadPinned(root, "scripts/cerulean-trainers.lock.json", LockSha256, 3714));
        var document = json.RootElement;
        Require(document.GetProperty("repository").GetString() == "pret/pokefirered" && document.GetProperty("commit").GetString() == CeruleanTrainerPartyFactory.SourceCommit &&
            document.GetProperty("files").GetArrayLength() == 15, "Cerulean trainer lock identity differs.");
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in document.GetProperty("files").EnumerateArray())
        {
            string path = entry.GetProperty("path").GetString()!;
            var bytes = ReadPinned(root, "content/raw/cerulean-trainers/" + path, entry.GetProperty("sha256").GetString()!, entry.GetProperty("byteLength").GetInt32());
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == entry.GetProperty("gitBlobSha1").GetString() && result.TryAdd(path, bytes), "Cerulean trainer Git blob differs.");
        }
        return result;
    }
    internal static byte[] ReadPinned(string root, string path, string hash, int length)
    {
        Require(length is > 0 and <= 1_000_000 && !Path.IsPathRooted(path) && !path.Contains('\\') && !path.Split('/').Any(x => x is "" or "." or ".."), "Unsafe Cerulean trainer path or size.");
        var info = new FileInfo(Path.GetFullPath(Path.Combine(root, path)));
        Require(info.LinkTarget is null, "Cerulean trainer symlink rejected.");
        for (var parent = info.Directory; parent is not null; parent = parent.Parent) Require(parent.LinkTarget is null, "Cerulean trainer directory symlink rejected.");
        using var stream = info.OpenRead(); Require(stream.Length == length, "Cerulean trainer input size differs: " + path);
        var bytes = new byte[length]; stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && Hash(bytes) == hash, "Cerulean trainer input hash differs: " + path); return bytes;
    }
    internal static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
