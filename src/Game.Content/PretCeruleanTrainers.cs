using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record CeruleanTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Generated parties and battle entries; English name bytes feed the NPC personality calculation.</summary>
public static class PretCeruleanTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/cerulean/trainers.json";
    public static OrdinaryTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<CeruleanTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Cerulean trainer document schema.");
        var catalog = new OrdinaryTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles, "cerulean-trainer");
        foreach (var battle in document.Battles.Where(b => b.Mode != 0))
        {
            bool misty = battle.Trainer.Value == "pret:TRAINER_LEADER_MISTY";
            bool grunt = battle.Trainer.Value == "pret:TRAINER_TEAM_ROCKET_GRUNT_5";
            string intro = misty ? "CeruleanCity_Gym_Text_MistyIntro" : grunt ? "CeruleanCity_Text_GruntIntro" : "CeruleanCity_Text_RivalIntro";
            string post = misty ? "CeruleanCity_Gym_Text_ExplainCascadeBadge" : grunt ? "CeruleanCity_Text_RecoveredTM28FromGrunt" : "CeruleanCity_Text_RivalPostBattle";
            if (battle.IntroText != intro || battle.PostBattleText != post)
                throw new InvalidDataException("cerulean_special_dialogue_binding");
        }
        return catalog;
    }

    internal static byte[] ReadPinned(string root, string path)
    {
        if (Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Cerulean trainer path.");
        return File.ReadAllBytes(Path.Combine(root, path));
    }
}
