using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record CinnabarTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>24 exact teams and31 battle entries, retaining alternate script entry points.</summary>
public static class PretCinnabarTrainers
{
    public const string Profile = "firered-cinnabar-trainers-v1";
    public const string GeneratedSha256 = "34cb3ef72a83ad5c30fd7d5e1eb47770211af5f94d30e92eb9dc44ae57f563c5";
    public const int TrainerCount = 24, PartyMemberCount = 59, BattleEntryCount = 31;
    public static CinnabarTrainerDocument LoadDocument(string root)
    {
        var source = CinnabarSource.Catalogs(root); var d = source.Parse<CinnabarTrainerDocument>("trainers.json");
        SilphSourcePackage.Need(d.SourceCommit == SilphSourcePackage.SourceCommit && d.Profile == Profile && !d.CompleteSourceRoster &&
            d.Trainers.Length == TrainerCount && d.Battles.Length == BattleEntryCount && d.Trainers.Sum(t => t.Party.Length) == PartyMemberCount &&
            d.Trainers.Select(t => t.Id).Distinct().Count() == TrainerCount && d.Battles.Select(b => b.ScriptEntry).Distinct().Count() == BattleEntryCount &&
            d.ResolvedMoves.GetArrayLength() == TrainerCount, "Cinnabar trainer scope differs.");
        foreach (var trainer in d.Trainers)
        {
            source.Reference(trainer.Source); source.Reference(trainer.MoneySource);
            foreach (var member in trainer.Party) { source.Reference(member.Source); source.Reference(member.SpeciesNameSource); }
        }
        foreach (var battle in d.Battles) source.Reference(battle.Source);
        var catalog = new CinnabarTrainerCatalog(d.SourceCommit, d.Profile, false,
            d.Trainers.ToImmutableDictionary(t => t.Id), d.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        CinnabarTrainerPartyFactory.ValidateCatalog(catalog);
        return d;
    }
    public static CinnabarTrainerCatalog Load(string root)
    {
        var d = LoadDocument(root);
        return new(d.SourceCommit, d.Profile, false, d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
    }
}
