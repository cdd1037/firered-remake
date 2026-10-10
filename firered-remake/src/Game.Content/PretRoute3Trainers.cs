using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Trainers;
namespace Game.Content;

/// <summary>The eight initial Route3 battles, adapted from authenticated source declarations.</summary>
public static class PretRoute3Trainers
{
    public static Route3TrainerCatalog Load(string root) => Load(PretRoute3Region.Load(root), PretRoute3Pokemon.Load(root));
    public static Route3TrainerCatalog Load(PretRoute3Region region, PretRoute3Pokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region); ArgumentNullException.ThrowIfNull(pokemon);
        var files = region.SourceFiles.ToDictionary(x => x.Path, StringComparer.Ordinal);
        TrainerSourceRef Ref(PretPewterSourceLocation r)
        {
            if (r.Kind != SourceKind.Pret || r.Repository != "pret/pokefirered" ||
                r.Commit != Route3TrainerPartyFactory.SourceCommit || r.Line < 1 ||
                !files.TryGetValue(r.Path, out var file) || file.Sha256 != r.Sha256)
                throw new InvalidDataException("Route3 trainer source identity differs.");
            return new(r.Path, r.Symbol, r.Line, r.Line, r.Sha256);
        }
        var names = pokemon.SourceFiles.Single(f => f.Path == "src/data/text/species_names.h");
        var money = files["src/battle_main.c"];
        var trainers = ImmutableDictionary.CreateBuilder<TrainerId, TrainerDefinition>();
        var battles = ImmutableDictionary.CreateBuilder<TrainerId, ForestTrainerBattleDefinition>();
        foreach (var t in region.Trainers.Where(t => Route3TrainerPartyFactory.SupportedTrainers.Contains(new("pret:" + t.Symbol))))
        {
            var id = new TrainerId("pret:" + t.Symbol); var d = t.Declaration;
            if (d["doubleBattle"] != "FALSE" || d["items"] != "{}" || d["aiFlags"] != "AI_SCRIPT_CHECK_BAD_MOVE" ||
                !d["party"].StartsWith("NO_ITEM_DEFAULT_MOVES(", StringComparison.Ordinal))
                throw new InvalidDataException("Unsupported Route3 trainer source declaration.");
            var party = t.Party.Select(m =>
            {
                var species = new SpeciesId("pret:" + m.Species);
                if (!pokemon.Catalog.Species.ContainsKey(species)) throw new InvalidDataException("Route3 trainer species absent.");
                var encoded = m.SpeciesName.Select(c => c switch { '♀' => 0xb6, '♂' => 0xb5,
                    >= 'A' and <= 'Z' => 0xbb + c - 'A', _ => throw new InvalidDataException("Unsupported source species-name glyph.") }).ToImmutableArray();
                if (pokemon.EncodedNames.TryGetValue(m.Species, out var canonical) &&
                    !canonical.Bytes.SequenceEqual(encoded.Add(0xff))) throw new InvalidDataException("Route3 species-name bytes differ.");
                return new TrainerPartyMemberDefinition(species, m.SourceSpeciesId, m.SpeciesName,
                    encoded, m.Level, m.SourceIv, m.HeldItem,
                    m.CustomMoves.Select(x => new MoveId("pret:" + x)).ToImmutableArray(), Ref(t.PartySourceRef),
                    new(names.Path, m.Species, m.SourceSpeciesId + 2, m.SourceSpeciesId + 2, names.Sha256));
            }).ToImmutableArray();
            int line = d["trainerClass"] switch { "TRAINER_CLASS_YOUNGSTER" => 459, "TRAINER_CLASS_BUG_CATCHER" => 460, "TRAINER_CLASS_LASS" => 467, _ => throw new InvalidDataException("Route3 trainer class differs.") };
            trainers.Add(id, new(id, t.SourceTrainerId, d["trainerClass"], t.SourceTrainerClass, d["trainerPic"],
                t.SourceTrainerPic, t.Name, t.EncodedName, t.EncounterMusicGender,
                ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"], false, t.PartyFlags, (uint)t.AiFlags,
                ["AI_SCRIPT_CHECK_BAD_MOVE"], t.BaseMoney, party, Ref(t.SourceRef), new(money.Path, d["trainerClass"], line, line, money.Sha256)));
            battles.Add(id, new(id, 0, 8, t.ScriptEntry, t.IntroText, t.DefeatText,
                t.ScriptEntry.Replace("EventScript", "Text", StringComparison.Ordinal) + "PostBattle",
                "EventScript_TryDoNormalTrainerBattle", "CB2_EndTrainerBattle", Ref(t.ScriptSourceRef)));
        }
        var result = new Route3TrainerCatalog(Route3TrainerPartyFactory.SourceCommit, Route3TrainerPartyFactory.RulesVersion,
            false, trainers.ToImmutable(), battles.ToImmutable());
        Route3TrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
}
