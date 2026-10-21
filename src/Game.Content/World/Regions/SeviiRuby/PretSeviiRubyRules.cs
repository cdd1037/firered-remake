using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record SeviiRubyTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Initial regional trainer definitions and battle occurrences.</summary>
public static class PretSeviiRubyRules
{
    private static readonly RegionalTrainerLoader<SeviiRubyTrainerDocument, SeviiRubyTrainerCatalog> Loader = new(
        "content/generated/data/regions/sevii-ruby/trainers.json", "Missing Sevii Ruby source document.",
        document => document.ByScript((trainers, battles) => new SeviiRubyTrainerCatalog(trainers, battles),
            "Missing Ruby trainer records."), SeviiRubyTrainerPartyFactory.ValidateCatalog);

    public static SeviiRubyTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static SeviiRubyTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
