using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>The twelve Mt. Moon battles, adapted from authenticated source declarations.</summary>
public static class PretMtMoonTrainers
{
    public static MtMoonTrainerCatalog Load(string root) => Load(PretMtMoonRegion.Load(root), PretMtMoonPokemon.Load(root));

    public static MtMoonTrainerCatalog Load(PretMtMoonRegion region, PretMtMoonPokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        if (region.Trainers.IsDefault || region.Trainers.Length != 12)
            throw new InvalidDataException("All twelve MtMoon source trainers are required.");
        var files = region.SourceFiles.ToDictionary(x => x.Path, StringComparer.Ordinal);
        TrainerSourceRef Ref(PretPewterSourceLocation r)
        {
            if (r.Kind != SourceKind.Pret || r.Repository != "pret/pokefirered" ||
                r.Commit != MtMoonTrainerPartyFactory.SourceCommit || r.Line < 1 ||
                !files.TryGetValue(r.Path, out var file) || file.Sha256 != r.Sha256)
                throw new InvalidDataException("MtMoon trainer source identity differs.");
            return new(r.Path, r.Symbol, r.Line, r.Line, r.Sha256);
        }
        var names = pokemon.SourceFiles.Single(f => f.Path == "src/data/text/species_names.h");
        var money = files["src/battle_main.c"];
        var trainers = ImmutableDictionary.CreateBuilder<TrainerId, TrainerDefinition>();
        var battles = ImmutableDictionary.CreateBuilder<TrainerId, ForestTrainerBattleDefinition>();
        foreach (var t in region.Trainers)
        {
            var id = new TrainerId("pret:" + t.Symbol);
            if (!MtMoonTrainerPartyFactory.SupportedTrainers.Contains(id))
                throw new InvalidDataException("Unknown MtMoon source trainer.");
            var d = t.Declaration;
            string partySymbol = "sParty_" + string.Concat(t.Symbol["TRAINER_".Length..].Split('_')
                .Select(part => part[..1] + part[1..].ToLowerInvariant()));
            string music = d["trainerClass"] switch
            {
                "TRAINER_CLASS_YOUNGSTER" or "TRAINER_CLASS_BUG_CATCHER" => "TRAINER_ENCOUNTER_MUSIC_MALE",
                "TRAINER_CLASS_LASS" => "TRAINER_ENCOUNTER_MUSIC_FEMALE | F_TRAINER_FEMALE",
                "TRAINER_CLASS_SUPER_NERD" => "TRAINER_ENCOUNTER_MUSIC_SUSPICIOUS",
                "TRAINER_CLASS_HIKER" => "TRAINER_ENCOUNTER_MUSIC_HIKER",
                "TRAINER_CLASS_TEAM_ROCKET" => "TRAINER_ENCOUNTER_MUSIC_AQUA",
                _ => throw new InvalidDataException("MtMoon trainer class differs.")
            };
            if (d.Count != 8 || d["doubleBattle"] != "FALSE" || d["items"] != "{}" ||
                d["aiFlags"] != "AI_SCRIPT_CHECK_BAD_MOVE" || d["encounterMusic_gender"] != music ||
                d["trainerName"] != "_(\"" + t.Name + "\")" || d["party"] != "NO_ITEM_DEFAULT_MOVES(" + partySymbol + ")" ||
                t.SourceRef.Path != "src/data/trainers.h" || t.SourceRef.Symbol != t.Symbol ||
                t.PartySourceRef.Path != "src/data/trainer_parties.h" || t.PartySourceRef.Symbol != partySymbol ||
                t.ScriptSourceRef.Symbol != t.ScriptEntry || t.PostBattleScript is not null || t.MusicOption is not null)
                throw new InvalidDataException("Unsupported MtMoon trainer source declaration.");
            var party = t.Party.Select(m =>
            {
                var species = new SpeciesId("pret:" + m.Species);
                if (!pokemon.Catalog.Species.ContainsKey(species) ||
                    !pokemon.SpeciesNames.TryGetValue(m.SourceSpeciesId, out var sourceName) || sourceName != m.SpeciesName)
                    throw new InvalidDataException("MtMoon trainer species or source name differs.");
                var encoded = m.SpeciesName.Select(c => c is >= 'A' and <= 'Z' ? 0xbb + c - 'A'
                    : throw new InvalidDataException("Unsupported source species-name glyph.")).ToImmutableArray();
                // EncodedNames covers the new increment; inherited names are authenticated above.
                if (pokemon.EncodedNames.TryGetValue(m.Species, out var canonical) &&
                    !canonical.Bytes.SequenceEqual(encoded.Add(0xff)))
                    throw new InvalidDataException("MtMoon species-name bytes differ.");
                return new TrainerPartyMemberDefinition(species, m.SourceSpeciesId, m.SpeciesName, encoded,
                    m.Level, m.SourceIv, m.HeldItem, m.CustomMoves.Select(x => new MoveId("pret:" + x)).ToImmutableArray(),
                    Ref(t.PartySourceRef), new(names.Path, m.Species, m.SourceSpeciesId + 2, m.SourceSpeciesId + 2, names.Sha256));
            }).ToImmutableArray();
            int line = d["trainerClass"] switch
            {
                "TRAINER_CLASS_YOUNGSTER" => 459, "TRAINER_CLASS_BUG_CATCHER" => 460, "TRAINER_CLASS_HIKER" => 461,
                "TRAINER_CLASS_SUPER_NERD" => 464, "TRAINER_CLASS_TEAM_ROCKET" => 466, "TRAINER_CLASS_LASS" => 467,
                _ => throw new InvalidDataException("MtMoon trainer money class differs.")
            };
            trainers.Add(id, new(id, t.SourceTrainerId, d["trainerClass"], t.SourceTrainerClass, d["trainerPic"],
                t.SourceTrainerPic, t.Name, t.EncodedName, t.EncounterMusicGender,
                ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"], false, t.PartyFlags, (uint)t.AiFlags,
                ["AI_SCRIPT_CHECK_BAD_MOVE"], t.BaseMoney, party, Ref(t.SourceRef),
                new(money.Path, d["trainerClass"], line, line, money.Sha256)));
            bool miguel = id == MtMoonTrainerPartyFactory.Miguel;
            string scriptPath = "data/maps/" + (miguel || t.SourceTrainerId is >= 351 and <= 354 ? "MtMoon_B2F" : "MtMoon_1F") + "/scripts.inc";
            if (t.ScriptSourceRef.Path != scriptPath) throw new InvalidDataException("MtMoon trainer script source differs.");
            battles.Add(id, new(id, miguel ? 3 : 0, 8, t.ScriptEntry, t.IntroText, t.DefeatText,
                miguel ? "MtMoon_B2F_Text_WellEachTakeAFossil" : t.ScriptEntry.Replace("EventScript", "Text", StringComparison.Ordinal) + "PostBattle",
                miguel ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle",
                "CB2_EndTrainerBattle", Ref(t.ScriptSourceRef)));
        }
        var result = new MtMoonTrainerCatalog(MtMoonTrainerPartyFactory.SourceCommit, MtMoonTrainerPartyFactory.RulesVersion,
            false, trainers.ToImmutable(), battles.ToImmutable());
        MtMoonTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
}
