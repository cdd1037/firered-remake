using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.Items;

public enum RareCandyOutcome { LevelRaised, NoEffect, UnsupportedEvolutionTarget }

/// <summary>
/// One field-use result. Consume ItemConsumption in the same transaction as Pokemon, before
/// displaying the level/stat pages. Resolve old-species PendingMoves before Evolution; cancelling
/// a move or the later evolution never refunds the candy or rolls back the level/stat changes.
/// </summary>
public sealed record RareCandyResult(RareCandyOutcome Outcome, PartyPokemon Pokemon, int LevelBefore,
    uint ExperienceBefore, BattleStats StatsBefore, int HpBefore, int FriendshipBefore,
    ImmutableArray<LevelEvolutionLearnedMove> LearnedMoves, ImmutableArray<LevelEvolutionPendingMove> PendingMoves,
    LevelEvolutionEligibility? Evolution, SpeciesId? UnsupportedTarget = null)
{
    public int ItemConsumption => Outcome == RareCandyOutcome.LevelRaised ? 1 : 0;
    public int ExperienceGained => checked((int)(Pokemon.Pokemon.Experience - ExperienceBefore));
    public int FriendshipDelta => Pokemon.Pokemon.Friendship - FriendshipBefore;
    public int HpDelta => Pokemon.Pokemon.CurrentHp - HpBefore;
}

/// <summary>
/// Pinned FireRed Rare Candy field effect for an authenticated owned campaign catalog.
/// National Dex scene gating belongs to the campaign owner. Pure and deterministic: no bag,
/// battle, RNG, scene, history, persistence or IO.
/// </summary>
public static class RareCandyRules
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-rare-candy-v1";
    public const string Item = "ITEM_RARE_CANDY";

    /// <summary>
    /// Sets EXP to exactly the next threshold, recalculates all stats from current EVs, preserves
    /// living HP deficit, revives fainted HP by the max-HP increase, and applies item friendship
    /// once. Status and existing PP/PP Ups survive. Level 100 has no effect, even when fainted.
    /// Caller must establish field context, bag ownership and exclusive modal state.
    /// </summary>
    public static RareCandyResult Apply(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership,
        PartyPokemon owned, string currentRegion)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(ownership);
        // The current composition binds the exact owned proof once. Opponent catalogs and
        // reconstructed copies cannot enter through matching species counts or profile text.
        if (!ReferenceEquals(ownership.Catalog, content.Catalog) ||
            !ReferenceEquals(OwnedBattlePokemonCatalog.For(content.Catalog), ownership))
            throw new ArgumentException("rare_candy_owned_catalog_identity", nameof(ownership));
        LevelEvolutionRules.Validate(content, owned);
        if (!ValidRegion(currentRegion))
            throw new ArgumentException("A source MAPSEC region is required.", nameof(currentRegion));

        var before = owned.Pokemon;
        RareCandyResult Unchanged(RareCandyOutcome outcome, SpeciesId? target = null) => new(outcome, owned,
            before.Level, before.Experience, before.Stats, before.CurrentHp, before.Friendship, [], [], null, target);
        // The party callback checks MAX_LEVEL before calling the generic item-effect executor.
        // Otherwise the item's revive flag could misleadingly make a level-100 target usable.
        if (before.Level == GrowthRules.MaximumLevel) return Unchanged(RareCandyOutcome.NoEffect);

        int level = before.Level + 1;
        // The source catalog deliberately leaves these Kanto targets outside the current profile.
        // Never spend an item and silently discard a normal evolution that the original offers.
        // Everstone suppresses that offer. Golbat's non-Kanto Crobat target is instead blocked by
        // the original pre-National-Dex scene and does not prevent this ordinary field item use.
        var missingTarget = MissingLevelTarget(before.SourceSpeciesId, level);
        if (owned.HeldItem != "ITEM_EVERSTONE" && missingTarget is not null && !content.Catalog.Species.ContainsKey(missingTarget.Value))
            return Unchanged(RareCandyOutcome.UnsupportedEvolutionTarget, missingTarget);

        var species = content.Catalog.Species[before.Species];
        var stats = CalculateStats(species, before, level);
        int hpIncrease = stats.MaxHp - before.Stats.MaxHp;
        if (hpIncrease < 0)
            throw new NotSupportedException("A source level-up cannot reduce max HP in this catalog.");
        int hp = before.CurrentHp == 0 ? Math.Min(stats.MaxHp, Math.Max(1, hpIncrease)) : before.CurrentHp + hpIncrease;
        int friendshipChange = before.Friendship < 100 ? 5 : before.Friendship < 200 ? 3 : 2;
        if (owned.Ball == "ITEM_LUXURY_BALL") friendshipChange++;
        if (owned.MetLocation == currentRegion) friendshipChange++;
        var pokemon = before with
        {
            Level = level, Experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, level),
            Stats = stats, StatCalculationEvs = null, CurrentHp = hp,
            Friendship = Math.Min(255, before.Friendship + friendshipChange)
        };

        var learned = ImmutableArray.CreateBuilder<LevelEvolutionLearnedMove>();
        var pending = ImmutableArray.CreateBuilder<LevelEvolutionPendingMove>();
        foreach (var move in PokemonMoveRules.AtLevel(species, level))
        {
            // Once one choice is outstanding, knowledge of every subsequent candidate depends
            // on that choice. The application rechecks candidates in source order when reached.
            if (pending.Count > 0) { pending.Add(new(level, move)); continue; }
            if (pokemon.Moves.Any(slot => slot.Move == move)) continue;
            if (!PokemonMoveRules.IsLearningSupported(content.Catalog, move) || pokemon.Moves.Length == 4) { pending.Add(new(level, move)); continue; }
            var definition = content.Catalog.Moves[move];
            learned.Add(new(level, move, pokemon.Moves.Length));
            pokemon = pokemon with { Moves = pokemon.Moves.Add(new(move, definition.Pp, definition.Pp)) };
        }

        var raised = owned with { Pokemon = pokemon };
        LevelEvolutionRules.Validate(content, raised);
        // The eligibility fields do not depend on moves, so this proposal remains valid after
        // the caller resolves old-species choices. Apply/Cancel must run only after those choices.
        var evolution = LevelEvolutionRules.Eligibility(content, raised, before.Level);
        return new(RareCandyOutcome.LevelRaised, raised, before.Level, before.Experience, before.Stats,
            before.CurrentHp, before.Friendship, learned.ToImmutable(), pending.ToImmutable(), evolution);
    }

    private static SpeciesId? MissingLevelTarget(int species, int level) => (species, level) switch
    {
        (43, >= 21) => new("pret:SPECIES_GLOOM"),
        (56, >= 28) => new("pret:SPECIES_PRIMEAPE"),
        (69, >= 21) => new("pret:SPECIES_WEEPINBELL"),
        (81, >= 30) => new("pret:SPECIES_MAGNETON"),
        (88, >= 38) => new("pret:SPECIES_MUK"),
        (100, >= 30) => new("pret:SPECIES_ELECTRODE"),
        (109, >= 35) => new("pret:SPECIES_WEEZING"),
        (111, >= 42) => new("pret:SPECIES_RHYDON"),
        _ => null
    };

    private static BattleStats CalculateStats(PokemonSpeciesDefinition species, PokemonInstance pokemon, int level)
    {
        var b = species.BaseStats; var iv = pokemon.Ivs; var ev = pokemon.Evs;
        int Other(int value, int individual, int effort, Stat stat) =>
            StatRules.CalculateOther(value, individual, effort, level, pokemon.Nature, stat);
        return new(StatRules.CalculateHp(b.Hp, iv.Hp, ev.Hp, level),
            Other(b.Attack, iv.Attack, ev.Attack, Stat.Attack), Other(b.Defense, iv.Defense, ev.Defense, Stat.Defense),
            Other(b.SpecialAttack, iv.SpecialAttack, ev.SpecialAttack, Stat.SpecialAttack),
            Other(b.SpecialDefense, iv.SpecialDefense, ev.SpecialDefense, Stat.SpecialDefense),
            Other(b.Speed, iv.Speed, ev.Speed, Stat.Speed));
    }

    private static bool ValidRegion(string? value) => value is not null && value.Length is > 7 and <= 128 &&
        value.StartsWith("MAPSEC_", StringComparison.Ordinal) &&
        value[7..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
