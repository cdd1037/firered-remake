using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Rules;

namespace Game.Domain.Pokemon;

/// <summary>A one-shot proposal for one party member after its battle rewards and move decisions.</summary>
public sealed record LevelEvolutionEligibility(long InstanceId, SpeciesId PreviousSpecies, SpeciesId TargetSpecies,
    int Level, int LevelBeforeBattle, PokemonSourceRef Source);
public enum LevelEvolutionOutcome { Evolved, Cancelled }
public sealed record LevelEvolutionLearnedMove(int Level, MoveId Move, int Slot);
public sealed record LevelEvolutionPendingMove(int Level, MoveId Move);
public sealed record LevelEvolutionResult(LevelEvolutionOutcome Outcome, PartyPokemon Pokemon,
    SpeciesId PreviousSpecies, SpeciesId TargetSpecies, bool NicknameChanged,
    ImmutableArray<LevelEvolutionLearnedMove> LearnedMoves, ImmutableArray<LevelEvolutionPendingMove> PendingMoves);

/// <summary>
/// Pinned FireRed ordinary post-victory EVO_LEVEL rules. No RNG, battle rewards, queue state, UI or IO.
/// The caller consumes each leveled party slot once, in ascending party order, even after cancellation.
/// Item evolution has a separate entry point; eggs and unverified held-item effects are unsupported.
/// </summary>
public static class LevelEvolutionRules
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-level-evolution-v1";

    /// <summary>
    /// Call after all EXP levels and old-species move decisions. A final level at or above the
    /// threshold suffices, but a new level in this won battle is required. Pikachu's item method
    /// never becomes a level trigger. The campaign owner applies National Dex scene restrictions
    /// before presenting any non-Kanto proposal; badges and RTC do not gate these rules.
    /// </summary>
    public static LevelEvolutionEligibility? Eligibility(CampaignPokemonContent content, PartyPokemon afterRewards,
        int levelBeforeBattle, bool battleWon = true)
    {
        Validate(content, afterRewards);
        if (levelBeforeBattle < 1 || levelBeforeBattle > afterRewards.Pokemon.Level)
            throw new ArgumentOutOfRangeException(nameof(levelBeforeBattle));
        if (!battleWon || levelBeforeBattle == afterRewards.Pokemon.Level || afterRewards.HeldItem == "ITEM_EVERSTONE")
            return null;

        // GetEvolutionTargetSpecies scans all entries; the last qualifying normal method wins.
        var evolution = content.Catalog.Species[afterRewards.Pokemon.Species].Evolutions.LastOrDefault(e =>
            e.Method == PokemonEvolutionMethod.Level && e.Level <= afterRewards.Pokemon.Level);
        return evolution is null ? null : new(afterRewards.InstanceId, afterRewards.Pokemon.Species,
            evolution.Target, afterRewards.Pokemon.Level, levelBeforeBattle, evolution.Source);
    }

    /// <summary>
    /// Builds one immutable proposal per eligible member in source party order. Do not rebuild this
    /// list after applying a proposal: multiple gained levels never chain two evolutions in a battle.
    /// Levels must correspond to the same party slots at battle entry; the caller owns that fact.
    /// </summary>
    public static ImmutableArray<LevelEvolutionEligibility> ForParty(CampaignPokemonContent content,
        ImmutableArray<PartyPokemon> afterRewards, ImmutableArray<int> levelsBeforeBattle, bool battleWon = true)
    {
        if (afterRewards.IsDefault || afterRewards.Length > 6 || levelsBeforeBattle.IsDefault ||
            levelsBeforeBattle.Length != afterRewards.Length)
            throw new ArgumentException("Evolution requires matching zero-through-six party slots and entry levels.");
        var contentError = PokemonStateValidator.ValidatePartyCollection(content, [], 1);
        if (contentError is not null) throw new ArgumentException(contentError, nameof(content));
        var proposals = ImmutableArray.CreateBuilder<LevelEvolutionEligibility>();
        var instanceIds = new HashSet<long>();
        foreach (var (member, index) in afterRewards.Select((member, index) => (member, index)))
        {
            var eligibility = Eligibility(content, member, levelsBeforeBattle[index], battleWon);
            if (!instanceIds.Add(member.InstanceId)) throw new ArgumentException("Duplicate party instance ID.");
            if (eligibility is not null) proposals.Add(eligibility);
        }
        return proposals.ToImmutable();
    }

    /// <summary>
    /// Commits one species change, recalculates stats from all current EVs, preserves living HP
    /// deficit (and leaves fainted HP at zero), then tries target-species moves at this exact level.
    /// A replacement is never automatic. Retain PendingMoves and resolve them before the next scene.
    /// </summary>
    public static LevelEvolutionResult Apply(CampaignPokemonContent content, PartyPokemon afterRewards,
        LevelEvolutionEligibility eligibility)
    {
        ValidateProposal(content, afterRewards, eligibility);
        return ApplySpeciesChange(content, afterRewards, eligibility.TargetSpecies);
    }

    // Item use reaches the same original species/stat/name/exact-level-move operation.
    // Eligibility remains separate, and the existing public level result is unchanged.
    internal static LevelEvolutionResult ApplySpeciesChange(CampaignPokemonContent content, PartyPokemon afterRewards,
        SpeciesId targetSpecies)
    {
        var before = afterRewards.Pokemon;
        var target = content.Catalog.Species[targetSpecies];
        var previous = content.Catalog.Species[before.Species];
        // Source changes species without changing the stored ability bit. These bounded families
        // keep compatible ability slots and growth curves, unlike an arbitrary species replacement.
        bool authenticatedOwned = OwnedBattlePokemonCatalog.For(content.Catalog) is not null;
        bool sourceExtendedFamily = authenticatedOwned &&
            (before.Species.Value, targetSpecies.Value) is
                ("pret:SPECIES_SLUGMA", "pret:SPECIES_MAGCARGO") or
                ("pret:SPECIES_WOOPER", "pret:SPECIES_QUAGSIRE") or
                ("pret:SPECIES_SWINUB", "pret:SPECIES_PILOSWINE") or
                ("pret:SPECIES_SENTRET", "pret:SPECIES_FURRET") or
                ("pret:SPECIES_NATU", "pret:SPECIES_XATU") or
                ("pret:SPECIES_GLOOM", "pret:SPECIES_BELLOSSOM") or
                ("pret:SPECIES_HOPPIP", "pret:SPECIES_SKIPLOOM") or
                ("pret:SPECIES_SKIPLOOM", "pret:SPECIES_JUMPLUFF");
        if (NpcTradePokemonOriginRules.ValidateInstance(content.Catalog, before, out int? tradeAbilitySlot) is { } originError)
            throw new ArgumentException(originError, nameof(afterRewards));
        int targetAbilitySlot = tradeAbilitySlot ?? (target.Ability2 == "ABILITY_NONE" ? 0 : (int)(before.Personality & 1));
        if (target.GrowthRate != previous.GrowthRate || target.SourceSpeciesId > 151 && !sourceExtendedFamily ||
            before.AbilitySlot != targetAbilitySlot || targetAbilitySlot == 1 && target.Ability2 == "ABILITY_NONE")
            throw new NotSupportedException("Evolution requires a pinned compatible owned family definition.");
        var stats = CalculateStats(target, before);
        int hp = before.CurrentHp == 0 ? 0 : before.CurrentHp + stats.MaxHp - before.Stats.MaxHp;
        if (hp < 0 || hp > stats.MaxHp)
            throw new NotSupportedException("The original negative-HP recalculation bug is outside these evolution families.");
        var pokemon = before with
        {
            Species = target.Id, SourceSpeciesId = target.SourceSpeciesId, Stats = stats,
            StatCalculationEvs = null, CurrentHp = hp,
            Gender = PokemonFactory.GenderFromPersonality(target.GenderRatio, before.Personality),
            AbilityId = before.AbilitySlot == 0 ? target.Ability1 : target.Ability2
        };
        bool rename = afterRewards.Language == 2 && string.Equals(afterRewards.Nickname,
            content.SpeciesNames[before.SourceSpeciesId], StringComparison.Ordinal);
        var learned = ImmutableArray.CreateBuilder<LevelEvolutionLearnedMove>();
        var pending = ImmutableArray.CreateBuilder<LevelEvolutionPendingMove>();
        foreach (var move in PokemonMoveRules.AtLevel(target, pokemon.Level))
        {
            // A pending replacement can change which later entries are already known.
            if (pending.Count > 0)
            {
                pending.Add(new(pokemon.Level, move));
                continue;
            }
            if (pokemon.Moves.Any(slot => slot.Move == move)) continue;
            if (!PokemonMoveRules.IsLearningSupported(content.Catalog, move) || pokemon.Moves.Length == 4)
            {
                pending.Add(new(pokemon.Level, move));
                continue;
            }
            var definition = content.Catalog.Moves[move];
            learned.Add(new(pokemon.Level, move, pokemon.Moves.Length));
            pokemon = pokemon with { Moves = pokemon.Moves.Add(new(move, definition.Pp, definition.Pp)) };
        }
        var evolved = afterRewards with
        {
            Pokemon = pokemon, Nickname = rename ? content.SpeciesNames[target.SourceSpeciesId] : afterRewards.Nickname
        };
        Validate(content, evolved);
        return new(LevelEvolutionOutcome.Evolved, evolved, before.Species, target.Id, rename,
            learned.ToImmutable(), pending.ToImmutable());
    }

    /// <summary>
    /// Cancel this scene only: EXP, levels, old-species learned moves and other battle rewards stay.
    /// The caller consumes this proposal and may offer evolution again after a later won level-up.
    /// </summary>
    public static LevelEvolutionResult Cancel(CampaignPokemonContent content, PartyPokemon afterRewards,
        LevelEvolutionEligibility eligibility)
    {
        ValidateProposal(content, afterRewards, eligibility);
        return new(LevelEvolutionOutcome.Cancelled, afterRewards, afterRewards.Pokemon.Species,
            eligibility.TargetSpecies, false, [], []);
    }

    private static void ValidateProposal(CampaignPokemonContent content, PartyPokemon afterRewards,
        LevelEvolutionEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(eligibility);
        var expected = Eligibility(content, afterRewards, eligibility.LevelBeforeBattle);
        if (expected is null || expected != eligibility)
            throw new ArgumentException("Evolution proposal is stale, ineligible or belongs to another Pokemon.", nameof(eligibility));
    }

    internal static void Validate(CampaignPokemonContent content, PartyPokemon owned)
    {
        var error = PokemonStateValidator.ValidateParty(content, owned);
        if (error is not null) throw new ArgumentException(error, nameof(owned));
        // Only Everstone has a prevent-evolution hold effect in this bounded static item profile.
        // In particular, Enigma Berry can obtain that effect from save data not represented here.
        bool staticHeldItem = OwnedBattlePokemonCatalog.For(content.Catalog) is { } ownership
            ? Game.Domain.Battle.SourceBattleAdmission.SupportsHeldItem(ownership.Mechanics.Catalog, owned.HeldItem)
            : content.Catalog.Species.Count switch
        {
            107 => Game.Domain.WildBattle.FuchsiaBattleScope.SupportsHeldItem(owned.HeldItem),
            40 => owned.HeldItem == "ITEM_ORAN_BERRY",
            102 or 103 => Game.Domain.WildBattle.SnorlaxBattleScope.SupportsHeldItem(owned.HeldItem),
            101 => Game.Domain.WildBattle.PokemonTowerBattleScope.SupportsHeldItem(owned.HeldItem),
            99 => Game.Domain.WildBattle.RocketHideoutBattleScope.SupportsHeldItem(owned.HeldItem),
            93 or 94 or 98 => Game.Domain.WildBattle.RockTunnelBattleScope.SupportsHeldItem(owned.HeldItem),
            79 => Game.Domain.WildBattle.Route11BattleScope.SupportsHeldItem(owned.HeldItem),
            73 => Game.Domain.WildBattle.SsAnneBattleScope.SupportsHeldItem(owned.HeldItem),
            70 => Game.Domain.Pokemon.VermilionPickupRules.IsKnownHeldItem(owned.HeldItem),
            56 or 65 or 68 => owned.HeldItem is "ITEM_ORAN_BERRY" or "ITEM_TINY_MUSHROOM" or "ITEM_BIG_MUSHROOM" or "ITEM_MOON_STONE",
            _ => false
        };
        if (owned.HeldItem is not ("ITEM_NONE" or "ITEM_EVERSTONE") && !staticHeldItem)
            throw new NotSupportedException("Unsupported evolution held-item effect.");
    }

    private static BattleStats CalculateStats(PokemonSpeciesDefinition target, PokemonInstance pokemon)
    {
        var b = target.BaseStats; var iv = pokemon.Ivs; var ev = pokemon.Evs; int level = pokemon.Level;
        int Other(int value, int individual, int effort, Stat stat) =>
            StatRules.CalculateOther(value, individual, effort, level, pokemon.Nature, stat);
        return new(StatRules.CalculateHp(b.Hp, iv.Hp, ev.Hp, level),
            Other(b.Attack, iv.Attack, ev.Attack, Stat.Attack), Other(b.Defense, iv.Defense, ev.Defense, Stat.Defense),
            Other(b.SpecialAttack, iv.SpecialAttack, ev.SpecialAttack, Stat.SpecialAttack),
            Other(b.SpecialDefense, iv.SpecialDefense, ev.SpecialDefense, Stat.SpecialDefense),
            Other(b.Speed, iv.Speed, ev.Speed, Stat.Speed));
    }
}
