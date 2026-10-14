using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record VsSeekerBattleOccurrence(string ActorScript, string ScriptEntry, string Opcode,
    ImmutableArray<string> Arguments, TrainerId Trainer, bool IsRematch, int Mode, TrainerSourceRef Source);
public sealed record VsSeekerResolvedMoves(TrainerId Trainer, ImmutableArray<ImmutableArray<MoveId>> PartyMoves);
public sealed record VsSeekerTrainerDocument(string SourceCommit, string Profile,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<VsSeekerResolvedMoves> ResolvedMoves);

/// <summary>The exact 351 original definitions referenced by sRematches, authenticated once.
/// Species and move mechanics remain owned by the existing canonical source catalog.</summary>
public sealed class VsSeekerTrainerCatalog
{
    public const string Profile = "firered-vs-seeker-parties-v1";
    public const string SemanticSha256 = "7122f07f0e5e550731ca33ac377b3b996d6ea6655ba56f09e60b3e1f410460fc";
    public ImmutableDictionary<TrainerId, TrainerDefinition> Trainers { get; }
    public ImmutableDictionary<TrainerId, ImmutableArray<ImmutableArray<MoveId>>> ResolvedMoves { get; }
    public ImmutableHashSet<SpeciesId> Species { get; }
    private VsSeekerTrainerCatalog(VsSeekerTrainerDocument document)
    {
        Trainers = document.Trainers.ToImmutableDictionary(t => t.Id);
        ResolvedMoves = document.ResolvedMoves.ToImmutableDictionary(t => t.Trainer, t => t.PartyMoves);
        Species = document.Trainers.SelectMany(t => t.Party).Select(p => p.Species).ToImmutableHashSet();
    }
    public static VsSeekerTrainerCatalog Authenticate(VsSeekerTrainerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SourceCommit != TrainerPartyFactory.SourceCommit || document.Profile != Profile ||
            document.Trainers.IsDefault || document.Trainers.Length != 351 || document.ResolvedMoves.IsDefault ||
            document.ResolvedMoves.Length != 351 || CampaignSourceFingerprint.Compute(document) != SemanticSha256)
            throw new ArgumentException("vs_seeker_trainer_source");
        return new(document);
    }
}

public static class VsSeekerTrainerPartyFactory
{
    public static int BasePrize(VsSeekerTrainerCatalog catalog, TrainerId id)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!catalog.Trainers.TryGetValue(id, out var trainer)) throw new ArgumentException("vs_seeker_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }

    /// <summary>Uses the shared original IV/PID/name/OT generator and its receipt. Custom slots
    /// replace the default moves after generation, consume no RNG, and terminate at MOVE_NONE.</summary>
    public static TrainerPartyCreationResult Create(VsSeekerTrainerCatalog trainers,
        SourceMechanicsCatalog source, TrainerId id, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(trainers); ArgumentNullException.ThrowIfNull(source);
        if (!ReferenceEquals(SourceMechanicsCatalog.For(source.Catalog), source) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new ArgumentException("vs_seeker_trainer_catalog");
        var expected = trainers.ResolvedMoves[id];
        for (int i = 0; i < trainer.Party.Length; i++)
        {
            var member = trainer.Party[i];
            if (!source.Catalog.Species.TryGetValue(member.Species, out var species) || species.SourceSpeciesId != member.SourceSpeciesId ||
                expected[i].Any(m => !source.Catalog.Moves.ContainsKey(m))) throw new ArgumentException("vs_seeker_trainer_mechanics");
            var initial = (trainer.PartyFlags & 1) != 0
                ? member.CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                : PokemonMoveRules.InitialMovesCore(source.Catalog, species, member.Level).Select(m => m.Move);
            if (!initial.SequenceEqual(expected[i])) throw new ArgumentException("vs_seeker_trainer_moves");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        return (trainer.PartyFlags & 1) == 0 ? made : made with {
            Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
                Moves = expected[i].Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp,
                    source.Catalog.Moves[m].Pp)).ToImmutableArray()
            } }).ToImmutableArray()
        };
    }
}
