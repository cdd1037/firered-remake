using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.WildBattle;

namespace Game.Domain.Pokemon;

/// <summary>Uncommitted permanent-instance calculation. The caller owns identity, atomic settlement
/// and ordered move decisions. ExperienceGained is the announcement, not necessarily the credit.</summary>
public sealed record PokemonRewardResult(PokemonInstance Pokemon, int ExperienceGained, SixStats EvsGained,
    int LevelsGained, int FriendshipDelta, int HpDelta, ImmutableArray<WildRewardEvent> Events,
    ImmutableArray<WildPendingMove> PendingMoves)
{
    public int ExperienceCredited => Events.OfType<WildRewardExperienceEvent>().Sum(e => checked((int)(e.After - e.Before)));
}
internal readonly record struct PokemonRewardModifiers(bool Participated, bool ExpShare, bool LuckyEgg,
    bool TrainerBattle, bool Traded, int EvMultiplier, bool SootheBell, bool LuxuryBall, bool SameMetRegion);

/// <summary>Single shared Cmd_getexp / MonGainEVs / level-up recipe. Public boundaries own admission.</summary>
internal static class PokemonRewardRecipe
{
    private static int Experience(int yield, int level, int aliveParticipants, int aliveExpShareHolders,
        PokemonRewardModifiers modifiers)
    {
        int calculated = yield * level / 7;
        // If no living participant exists, nobody can read the participant share. Do not evaluate
        // the retail C division by zero merely to compute an unused value for an EXP Share holder.
        int participantShare = aliveParticipants == 0 ? 0 : Math.Max(1,
            (aliveExpShareHolders > 0 ? calculated / 2 : calculated) / aliveParticipants);
        int itemShare = aliveExpShareHolders == 0 ? 0 : Math.Max(1, calculated / 2 / aliveExpShareHolders);
        int experience = (modifiers.Participated ? participantShare : 0) + (modifiers.ExpShare ? itemShare : 0);
        if (modifiers.LuckyEgg) experience = experience * 150 / 100;
        if (modifiers.TrainerBattle) experience = experience * 150 / 100;
        if (modifiers.Traded) experience = experience * 150 / 100;
        return experience;
    }

    internal static PokemonRewardResult Calculate(PokemonCatalog catalog, PokemonInstance participantAfterBattle,
        PokemonSpeciesDefinition defeated, int defeatedLevel, int aliveParticipants, int aliveExpShareHolders,
        PokemonRewardModifiers modifiers, bool rejectLevelEvolution = false)
    {
        var before = participantAfterBattle;
        // Cmd_getexp rejects level 100 before MonGainEVs, and checks living HP for both EXP/EVs.
        // In particular, a simultaneous faint is not a victory reward even if its outcome has bit 1.
        if (before.CurrentHp == 0 || before.Level == 100)
            return new(before, 0, SixStats.Zero, 0, 0, 0, [], []);

        var species = catalog.Species[before.Species];
        // Cmd_getexp divides the already truncated base EXP by living participants. EVs are
        // credited in full to each eligible participant; the caller owns membership and order.
        int experienceGained = Experience(defeated.ExperienceYield, defeatedLevel, aliveParticipants, aliveExpShareHolders, modifiers);
        uint experience = Math.Min(checked(before.Experience + (uint)experienceGained),
            GrowthRules.ExperienceAtLevel(species.GrowthRate, 100));
        int finalLevel = GrowthRules.LevelFromExperience(species.GrowthRate, experience);
        if (rejectLevelEvolution && finalLevel > before.Level && species.Evolutions.Any(e => e.Method==PokemonEvolutionMethod.Level&&e.Level <= finalLevel))
            throw new NotSupportedException("A level evolution is now eligible; the evolution flow is not implemented.");

        var values = before.Evs.Values.ToArray();
        var gains = new int[6];
        int total = values.Sum();
        for (int i = 0; i < 6; i++)
        {
            gains[i] = Math.Min(defeated.EvYield.Values[i] * modifiers.EvMultiplier,
                Math.Min(255 - values[i], 510 - total));
            values[i] += gains[i];
            total += gains[i];
        }
        var evsGained = new SixStats(gains[0],gains[1],gains[2],gains[3],gains[4],gains[5]);
        var evs = new SixStats(values[0],values[1],values[2],values[3],values[4],values[5]);
        var events = ImmutableArray.CreateBuilder<WildRewardEvent>();
        var pending = ImmutableArray.CreateBuilder<WildPendingMove>();
        events.Add(new WildRewardExperienceEvent(before.Experience, experience, experienceGained));
        events.Add(new WildRewardEvEvent(before.Evs, evs, evsGained));
        // The EXP event is the announcement. EV mutation precedes the first level/stat mutation.
        var pokemon = before with
        {
            Evs = evs,
            StatCalculationEvs = evs == before.Evs ? before.StatCalculationEvs : before.StatCalculationEvs ?? before.Evs
        };
        for (int level = before.Level + 1; level <= finalLevel; level++)
        {
            var stats = PokemonStatCalculation.Calculate(species, level, pokemon.Nature, pokemon.Ivs, evs, catalog.MechanicsData);
            int hp = pokemon.CurrentHp + stats.MaxHp - pokemon.Stats.MaxHp;
            events.Add(new WildRewardLevelEvent(pokemon.Level, level, pokemon.Stats, stats, pokemon.CurrentHp, hp));
            int increment = pokemon.Friendship < 100 ? 5 : pokemon.Friendship < 200 ? 3 : 2;
            // AdjustFriendship applies the 150% base delta before the met-location bonus.
            if (modifiers.SootheBell) increment = increment * 150 / 100;
            if (modifiers.LuxuryBall) increment++;
            if (modifiers.SameMetRegion) increment++;
            int friendship = Math.Min(255, pokemon.Friendship + increment);
            events.Add(new WildRewardFriendshipEvent(pokemon.Friendship, friendship));
            pokemon = pokemon with
            {
                Level = level, Experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, level),
                Stats = stats, StatCalculationEvs = null, CurrentHp = hp, Friendship = friendship
            };
            foreach (var move in PokemonMoveRules.AtLevel(species, level))
            {
                // Once a replacement is pending, future knowledge depends on that decision.
                // Keep every subsequent candidate, including one that is currently known.
                if (pending.Count > 0)
                {
                    pending.Add(new(level, move));
                    events.Add(new WildRewardMovePendingEvent(level, move));
                    continue;
                }
                var learned = PokemonMoveRules.TryLearn(catalog, pokemon.Moves, move);
                if (learned.Outcome == MoveLearningOutcome.NeedsReplacement)
                {
                    pending.Add(new(level, move));
                    events.Add(new WildRewardMovePendingEvent(level, move));
                }
                else if (learned.Outcome == MoveLearningOutcome.Added)
                {
                    pokemon = pokemon with { Moves = learned.Moves };
                    events.Add(new WildRewardMoveLearnedEvent(level, move, pokemon.Moves.Length - 1));
                }
            }
        }
        pokemon = pokemon with { Experience = experience };
        return new(pokemon, experienceGained, evsGained, pokemon.Level - before.Level,
            pokemon.Friendship - before.Friendship, pokemon.CurrentHp - before.CurrentHp,
            events.ToImmutable(), pending.ToImmutable());
    }
}
