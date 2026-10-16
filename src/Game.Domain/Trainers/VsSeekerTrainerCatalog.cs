using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record VsSeekerBattleOccurrence(string ActorScript, string ScriptEntry, string Opcode,
    ImmutableArray<string> Arguments, TrainerId Trainer, bool IsRematch, int Mode, TrainerSourceRef Source);
public sealed record VsSeekerTrainerDocument(
    ImmutableArray<TrainerDefinition> Trainers);

/// <summary>The trainer selection referenced by the rematch rules.
/// Species and move mechanics remain owned by the existing canonical source catalog.</summary>
public sealed class VsSeekerTrainerCatalog
{
    public ImmutableDictionary<TrainerId, TrainerDefinition> Trainers { get; }
    public ImmutableHashSet<SpeciesId> Species { get; }
    private VsSeekerTrainerCatalog(VsSeekerTrainerDocument document)
    {
        Trainers = document.Trainers.ToImmutableDictionary(t => t.Id);
        Species = document.Trainers.SelectMany(t => t.Party).Select(p => p.Species).ToImmutableHashSet();
    }
    public static VsSeekerTrainerCatalog Authenticate(VsSeekerTrainerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Trainers.IsDefaultOrEmpty)
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
        return TrainerPartyFactory.BasePrize(trainer);
    }

    /// <summary>Uses the shared original IV/PID/name/OT generator and its receipt. Custom slots
    /// replace the default moves after generation, consume no RNG, and terminate at MOVE_NONE.</summary>
    public static TrainerPartyCreationResult Create(VsSeekerTrainerCatalog trainers,
        SourceMechanicsCatalog source, TrainerId id, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(trainers); ArgumentNullException.ThrowIfNull(source);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new ArgumentException("vs_seeker_trainer_catalog");
        // Natural moves are produced once by PokemonFactory. Only custom slots need
        // validation here; MOVE_NONE terminates the source party's explicit move list.
        if ((trainer.PartyFlags & 1) != 0 && trainer.Party.Any(member =>
            member.CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Any(m => !source.Catalog.Moves.ContainsKey(m))))
            throw new ArgumentException("vs_seeker_trainer_moves");
        return TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
    }
}
