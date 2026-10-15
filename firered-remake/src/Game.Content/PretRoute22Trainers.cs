using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Typed adapter over the already byte-authenticated Route22 dependency records. No new raw importer.</summary>
public static class PretRoute22Trainers
{
    public static Route22TrainerCatalog Load(string projectRoot) => Load(PretRoute22Region.Load(projectRoot), PretRoute22Pokemon.Load(projectRoot));
    public static Route22TrainerCatalog Load(PretRoute22Region region, PretRoute22Pokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        var files = region.SourceFiles.ToDictionary(x => x.Path, StringComparer.Ordinal);
        // Region dependencies carry species symbols but not the species-name source bytes.
        // The separately authenticated Pokemon increment supplies that provenance and charmap.
        foreach (string path in new[] { "src/data/text/species_names.h","charmap.txt" })
            files.TryAdd(path, pokemon.SourceFiles.Single(f => f.Path == path));
        TrainerSourceRef Ref(string path, string symbol, int line)
        {
            if (!files.TryGetValue(path, out var file) || line < 1)
                throw new InvalidDataException("Route22 trainer source is absent from the authenticated inventory.");
            return new(path,symbol,line,line);
        }
        TrainerSourceRef JsonRef(JsonElement value, string symbol)
        {
            string path = value.GetProperty("path").GetString()!;
            int line = value.GetProperty("line").GetInt32();
            return Ref(path, symbol, line);
        }
        ImmutableArray<int> Encode(string name) => name.Select(c => 0xbb + c - 'A').ToImmutableArray();
        var trainers = ImmutableDictionary.CreateBuilder<TrainerId, TrainerDefinition>();
        var battles = ImmutableDictionary.CreateBuilder<TrainerId, Route22TrainerBattleDefinition>();
        foreach (var t in region.EarlyRivalTrainers.EnumerateArray())
        {
            string symbol = t.GetProperty("trainer").GetString()!;
            var id = new TrainerId("pret:" + symbol);
            var declaration = t.GetProperty("trainerDefinition");
            var fields = declaration.GetProperty("fields");
            string rival = symbol["TRAINER_RIVAL_ROUTE22_EARLY_".Length..];
            string title = rival[..1] + rival[1..].ToLowerInvariant();
            const string ai = "AI_SCRIPT_CHECK_BAD_MOVE | AI_SCRIPT_TRY_TO_FAINT | AI_SCRIPT_CHECK_VIABILITY";
            if (fields.GetProperty("trainerName").GetString() != "_(\"TERRY\")" ||
                fields.GetProperty("items").GetString() != "{}" || fields.GetProperty("doubleBattle").GetString() != "FALSE" ||
                fields.GetProperty("aiFlags").GetString() != ai || fields.GetProperty("encounterMusic_gender").GetString() != "TRAINER_ENCOUNTER_MUSIC_MALE" ||
                fields.GetProperty("party").GetString() != "NO_ITEM_CUSTOM_MOVES(sParty_RivalRoute22Early" + title + ")" ||
                t.GetProperty("partyEncoding").GetString() != "NO_ITEM_CUSTOM_MOVES")
                throw new InvalidDataException("Unsupported Route22 early-rival source declaration.");
            var party = t.GetProperty("party").EnumerateArray().Select(m => {
                string species = m.GetProperty("species").GetString()!;
                int sid = species switch { "SPECIES_PIDGEY" => 16, "SPECIES_SQUIRTLE" => 7, "SPECIES_BULBASAUR" => 1, "SPECIES_CHARMANDER" => 4, _ => throw new InvalidDataException("Unknown rival party species.") };
                string name = species["SPECIES_".Length..];
                int iv = m.GetProperty("ivByte").GetInt32();
                if (m.GetProperty("fixedIv").GetInt32() != iv * 31 / 255) throw new InvalidDataException("Rival IV scaling differs.");
                return new TrainerPartyMemberDefinition(new("pret:" + species), sid, name, Encode(name),
                    m.GetProperty("level").GetInt32(), iv, "ITEM_NONE",
                    m.GetProperty("moves").EnumerateArray().Select(x => new MoveId("pret:" + x.GetString()!)).ToImmutableArray(),
                    JsonRef(t.GetProperty("partySource"), "sParty_RivalRoute22Early" + title),
                    Ref("src/data/text/species_names.h", species, sid + 2));
            }).ToImmutableArray();
            trainers.Add(id, new(id, t.GetProperty("trainerId").GetInt32(), fields.GetProperty("trainerClass").GetString()!, 81,
                fields.GetProperty("trainerPic").GetString()!, 106, "TERRY", Encode("TERRY"), 0,
                ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"], false, t.GetProperty("partyFlags").GetInt32(),
                t.GetProperty("aiFlags").GetUInt32(), ai.Split(" | ", StringSplitOptions.None).ToImmutableArray(), 4, party,
                JsonRef(declaration.GetProperty("source"), symbol), Ref("src/battle_main.c", "TRAINER_CLASS_RIVAL_EARLY", 456)));
            int branch = t.GetProperty("trainerId").GetInt32() - 329;
            string script = "Route22_EventScript_EarlyRival" + title;
            battles.Add(id, new(id, t.GetProperty("starterVar").GetInt32(), new("pret:" + t.GetProperty("playerStarter").GetString()!),
                t.GetProperty("battleMode").GetInt32(), t.GetProperty("rivalBattleFlags").GetInt32(), 8,
                t.GetProperty("wonTrainerFlag").GetInt32(), t.GetProperty("basePrizeMoney").GetInt32(), script,
                "Route22_Text_EarlyRivalDefeat", "Text_RivalVictory", "EventScript_DoNoIntroTrainerBattle", "CB2_EndTrainerBattle",
                Ref("data/maps/Route22/scripts.inc", script, 60 + branch * 4)));
        }
        var result = new Route22TrainerCatalog(Route22TrainerPartyFactory.SourceCommit, Route22TrainerPartyFactory.RulesVersion,
            false, trainers.ToImmutable(), battles.ToImmutable());
        Route22TrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
}
