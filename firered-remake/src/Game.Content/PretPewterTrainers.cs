using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Adapts the authenticated region dependency records, retaining canonical English source-name bytes.</summary>
public static class PretPewterTrainers
{
    public static PewterTrainerCatalog Load(string projectRoot) => Load(PretPewterRegion.Load(projectRoot));
    public static PewterTrainerCatalog Load(PretPewterRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        var files = region.SourceFiles.ToDictionary(x => x.Path, StringComparer.Ordinal);
        TrainerSourceRef Ref(PretPewterSourceLocation value)
        {
            if (value.Kind != SourceKind.Pret || value.Line < 1 ||
                !files.TryGetValue(value.Path, out var file) )
                throw new InvalidDataException("Pewter trainer source identity differs.");
            return new(value.Path,value.Symbol,value.Line,value.Line);
        }
        var trainers = ImmutableDictionary.CreateBuilder<TrainerId, TrainerDefinition>();
        var battles = ImmutableDictionary.CreateBuilder<TrainerId, PewterTrainerBattleDefinition>();
        var speciesSource = files["src/data/text/species_names.h"];
        var moneySource = files["src/battle_main.c"];
        foreach (var t in region.Trainers)
        {
            var id = new TrainerId("pret:" + t.Symbol);
            var d = t.Declaration;
            bool brock = id == PewterTrainerPartyFactory.Brock;
            string ai = brock ? "AI_SCRIPT_CHECK_BAD_MOVE | AI_SCRIPT_TRY_TO_FAINT | AI_SCRIPT_CHECK_VIABILITY" : "AI_SCRIPT_CHECK_BAD_MOVE";
            if (d["doubleBattle"] != "FALSE" || d["items"] != "{}" || d["aiFlags"] != ai ||
                d["party"] != (brock ? "NO_ITEM_CUSTOM_MOVES(sParty_LeaderBrock)" : "NO_ITEM_CUSTOM_MOVES(sParty_CamperLiam)"))
                throw new InvalidDataException("Unsupported Pewter trainer declaration.");
            var party = t.Party.Select(m => new TrainerPartyMemberDefinition(new("pret:" + m.Species),
                m.SourceSpeciesId, m.SpeciesName, m.SpeciesName.Select(c => 0xbb + c - 'A').ToImmutableArray(),
                m.Level, m.SourceIv, m.HeldItem, m.CustomMoves.Select(x => new MoveId("pret:" + x)).ToImmutableArray(),
                Ref(t.PartySourceRef), new(speciesSource.Path,m.Species,m.SourceSpeciesId + 2,                    m.SourceSpeciesId + 2))).ToImmutableArray();
            trainers.Add(id, new(id, t.SourceTrainerId, d["trainerClass"], t.SourceTrainerClass,
                d["trainerPic"], t.SourceTrainerPic, t.Name, t.EncodedName, t.EncounterMusicGender,
                ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"], false, t.PartyFlags, (uint)t.AiFlags,
                ai.Split(" | ", StringSplitOptions.None).ToImmutableArray(), t.BaseMoney, party,
                Ref(t.SourceRef), new(moneySource.Path,d["trainerClass"],brock ? 453 : 482,brock ? 453 : 482)));
            battles.Add(id, new(id, brock ? 1 : 0, 8, t.ScriptEntry, t.IntroText, t.DefeatText,
                "PewterCity_Gym_Text_" + (brock ? "Brock" : "Liam") + "PostBattle", t.PostBattleScript, t.MusicOption,
                "EventScript_TryDoNormalTrainerBattle", "CB2_EndTrainerBattle", Ref(t.ScriptSourceRef)));
        }
        var result = new PewterTrainerCatalog(PewterTrainerPartyFactory.SourceCommit, PewterTrainerPartyFactory.RulesVersion,
            false, trainers.ToImmutable(), battles.ToImmutable());
        PewterTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
}
