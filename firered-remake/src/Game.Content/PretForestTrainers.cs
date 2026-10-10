using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Adapts the already byte-authenticated forest dependency catalog, preserving English source-name bytes.</summary>
public static class PretForestTrainers
{
    public static ForestTrainerCatalog Load(string projectRoot) => Load(PretForestRegion.Load(projectRoot));

    public static ForestTrainerCatalog Load(PretForestRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        var files = region.SourceFiles.ToDictionary(x => x.Path, StringComparer.Ordinal);
        TrainerSourceRef Ref(JsonElement value)
        {
            string path = value.GetProperty("path").GetString()!;
            string hash = value.GetProperty("sha256").GetString()!;
            int line = value.GetProperty("line").GetInt32();
            if (value.GetProperty("commit").GetString() != ForestTrainerPartyFactory.SourceCommit ||
                value.GetProperty("kind").GetString() != "pret" || value.GetProperty("repository").GetString() != "pret/pokefirered" ||
                line < 1 || !files.TryGetValue(path, out var file) || file.Sha256 != hash)
                throw new InvalidDataException("Forest trainer source identity differs.");
            return new(path, value.GetProperty("symbol").GetString()!, line, line, hash);
        }
        var trainers = ImmutableDictionary.CreateBuilder<TrainerId, TrainerDefinition>();
        var battles = ImmutableDictionary.CreateBuilder<TrainerId, ForestTrainerBattleDefinition>();
        var speciesSource = files["src/data/text/species_names.h"];
        var moneySource = files["src/battle_main.c"];
        foreach (var value in region.Dependencies.GetProperty("trainers").EnumerateArray())
        {
            var id = new TrainerId("pret:" + value.GetProperty("symbol").GetString());
            var declaration = value.GetProperty("declaration");
            var partyRef = Ref(value.GetProperty("partySourceRef"));
            var party = value.GetProperty("party").EnumerateArray().Select(member =>
            {
                string species = member.GetProperty("species").GetString()!;
                string name = member.GetProperty("speciesName").GetString()!;
                int sourceId = member.GetProperty("sourceSpeciesId").GetInt32();
                // The pinned names table has one header line, then a source-species-indexed row.
                var nameRef = new TrainerSourceRef(speciesSource.Path, species, sourceId + 2, sourceId + 2, speciesSource.Sha256);
                return new TrainerPartyMemberDefinition(new("pret:" + species), sourceId, name,
                    name.Select(c => 0xbb + c - 'A').ToImmutableArray(), member.GetProperty("level").GetInt32(),
                    member.GetProperty("sourceIv").GetInt32(), member.GetProperty("heldItem").GetString()!,
                    member.GetProperty("customMoves").EnumerateArray().Select(m => new MoveId("pret:" + m.GetString())).ToImmutableArray(),
                    partyRef, nameRef);
            }).ToImmutableArray();
            if (declaration.GetProperty("doubleBattle").GetString() != "FALSE" || declaration.GetProperty("items").GetString() != "{}" ||
                declaration.GetProperty("aiFlags").GetString() != "AI_SCRIPT_CHECK_BAD_MOVE")
                throw new InvalidDataException("Unsupported forest trainer declaration.");
            trainers.Add(id, new(id, value.GetProperty("sourceTrainerId").GetInt32(), declaration.GetProperty("trainerClass").GetString()!,
                value.GetProperty("sourceTrainerClass").GetInt32(), declaration.GetProperty("trainerPic").GetString()!,
                value.GetProperty("sourceTrainerPic").GetInt32(), value.GetProperty("name").GetString()!,
                value.GetProperty("encodedName").EnumerateArray().Select(x => x.GetInt32()).ToImmutableArray(),
                value.GetProperty("encounterMusicGender").GetInt32(), ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],
                false, value.GetProperty("partyFlags").GetInt32(), value.GetProperty("aiFlags").GetUInt32(),
                ["AI_SCRIPT_CHECK_BAD_MOVE"], value.GetProperty("baseMoney").GetInt32(), party,
                Ref(value.GetProperty("sourceRef")), new(moneySource.Path, "TRAINER_CLASS_BUG_CATCHER", 460, 460, moneySource.Sha256)));
            string script = value.GetProperty("scriptEntry").GetString()!;
            battles.Add(id, new(id, 0, 8, script, value.GetProperty("introText").GetString()!, value.GetProperty("defeatText").GetString()!,
                script.Replace("EventScript", "Text", StringComparison.Ordinal) + "PostBattle",
                "EventScript_TryDoNormalTrainerBattle", "CB2_EndTrainerBattle", Ref(value.GetProperty("scriptSourceRef"))));
        }
        var result = new ForestTrainerCatalog(ForestTrainerPartyFactory.SourceCommit, ForestTrainerPartyFactory.RulesVersion,
            false, trainers.ToImmutable(), battles.ToImmutable());
        ForestTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
}
