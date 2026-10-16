using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.Trainers;

namespace Game.Domain.FirstBattle;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LabBattleExperienceEvent), "experience")]
[JsonDerivedType(typeof(LabBattleEvEvent), "evs")]
[JsonDerivedType(typeof(LabBattleLevelEvent), "level")]
[JsonDerivedType(typeof(LabBattleFriendshipEvent), "friendship")]
[JsonDerivedType(typeof(LabBattleMoneyEvent), "money")]
public abstract record LabBattleRewardEvent;
public sealed record LabBattleExperienceEvent(uint Before, uint After, int Gained) : LabBattleRewardEvent;
public sealed record LabBattleEvEvent(SixStats Before, SixStats After, SixStats Gained) : LabBattleRewardEvent;
public sealed record LabBattleLevelEvent(int Before, int After, BattleStats StatsBefore, BattleStats StatsAfter,
    int HpBefore, int HpAfter) : LabBattleRewardEvent;
public enum LabBattleFriendshipReason { LevelUp, Fainted }
public sealed record LabBattleFriendshipEvent(int Before, int After, LabBattleFriendshipReason Reason) : LabBattleRewardEvent;
public sealed record LabBattleMoneyEvent(int Before, int After, int Awarded) : LabBattleRewardEvent;

/// <summary>Unhealed settlement. HpDelta is growth alone, relative to the post-battle HP.</summary>
public sealed record LabBattleRewardResult(PartyPokemon Pokemon, int Money, int ExperienceGained,
    SixStats EvsGained, int LevelsGained, int FriendshipDelta, int HpDelta,
    ImmutableArray<LabBattleRewardEvent> Events)
{
    public int MoneyGained => Events.OfType<LabBattleMoneyEvent>().Sum(e => e.After - e.Before);
}

/// <summary>
/// Pure single locally owned starter / level-5 Oak rival settlement. No IO, RNG, story advancement,
/// healing or command ledger. Caller verifies local ownership and atomically commits with its ledger.
/// </summary>
public static class LabBattleRewards
{
    public const string RulesVersion = "firered-oaks-lab-rewards-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const int MaximumMoney = 999999;

    public static LabBattleRewardResult Apply(PokemonCatalog catalog, PartyPokemon ownedBefore,
        PokemonInstance afterBattlePlayer, PokemonInstance opponent, FirstBattleOutcome outcome,
        int money, TrainerDefinition trainer, string currentRegion)
    {
        // No state escapes before all input/profile checks complete. Catalog byte authenticity is
        // enforced by the Content loader; these checks also reject changed reward-bearing fields.
        ValidateRewardCatalog(catalog);
        ValidateOwned(catalog, ownedBefore);
        ValidateStarter(catalog, afterBattlePlayer);
        ValidateStarter(catalog, opponent);
        ValidateStarter(catalog, ownedBefore.Pokemon);
        if (ownedBefore.HeldItem != "ITEM_NONE" || ownedBefore.Ball != "ITEM_POKE_BALL")
            throw new NotSupportedException("Only the no-held-item, Poke Ball lab starter is supported.");
        if (ownedBefore.Pokemon.CurrentHp == 0)
            throw new ArgumentException("The pre-battle starter must have been alive.", nameof(ownedBefore));
        if (!ValidRegion(currentRegion)) throw new ArgumentException("A source MAPSEC region is required.", nameof(currentRegion));
        if (money is < 0 or > MaximumMoney) throw new ArgumentOutOfRangeException(nameof(money));
        var before = ownedBefore.Pokemon;
        if (afterBattlePlayer with { CurrentHp = before.CurrentHp, Moves = before.Moves } != before ||
            afterBattlePlayer.Moves.Length != before.Moves.Length ||
            afterBattlePlayer.Moves.Where((move, i) =>
                move with { CurrentPp = before.Moves[i].CurrentPp } != before.Moves[i] ||
                move.CurrentPp > before.Moves[i].CurrentPp).Any())
            throw new ArgumentException("Only HP and consumed PP may differ from the owned pre-battle instance.", nameof(afterBattlePlayer));
        ValidateTrainer(trainer, before, opponent);
        // The source ORs two team-HP checks: Won=1, Lost=2, Draw=3. In this one-member profile
        // the result is derived from BOTH HP values, never from the Won bit on a draw.
        int expectedOutcome = (opponent.CurrentHp == 0 ? 1 : 0) | (afterBattlePlayer.CurrentHp == 0 ? 2 : 0);
        if (expectedOutcome == 0 || (int)outcome != expectedOutcome)
            throw new ArgumentException("A terminal outcome must agree with both parties' remaining HP.", nameof(outcome));

        var events = ImmutableArray.CreateBuilder<LabBattleRewardEvent>();
        var pokemon = afterBattlePlayer;
        int experienceGained = 0, levelsGained = 0;
        var evsGained = SixStats.Zero;
        // Cmd_getexp requires a defeated opponent and a living participating player. Draw gives no EV/EXP.
        if (opponent.CurrentHp == 0 && pokemon.CurrentHp > 0)
        {
            var defeated = catalog.Species[opponent.Species];
            var species = catalog.Species[pokemon.Species];
            experienceGained = defeated.ExperienceYield * opponent.Level / 7 * 150 / 100;
            uint experience = checked(pokemon.Experience + (uint)experienceGained);
            int level = GrowthRules.LevelFromExperience(species.GrowthRate, experience);
            if (level != 6 || !PokemonMoveRules.AtLevel(species, level).IsEmpty ||
                species.Evolutions.Any(e => e.Level <= level))
                throw new NotSupportedException("Only the source level-5 to level-6 growth without move learning or evolution is supported.");
            evsGained = defeated.EvYield;
            // Starter-profile validation requires zero EVs; this one-point yield cannot reach the caps.
            var evs = Add(pokemon.Evs, evsGained);
            // Source message then EV credit, before the controller's EXP/level mutation.
            events.Add(new LabBattleExperienceEvent(pokemon.Experience, experience, experienceGained));
            events.Add(new LabBattleEvEvent(pokemon.Evs, evs, evsGained));
            var stats = CalculateStats(species, pokemon, evs, level);
            int hp = pokemon.CurrentHp + stats.MaxHp - pokemon.Stats.MaxHp;
            events.Add(new LabBattleLevelEvent(pokemon.Level, level, pokemon.Stats, stats, pokemon.CurrentHp, hp));
            int friendshipIncrement = pokemon.Friendship < 100 ? 5 : pokemon.Friendship < 200 ? 3 : 2;
            if (ownedBefore.MetLocation == currentRegion) friendshipIncrement++;
            int friendship = Math.Min(255, pokemon.Friendship + friendshipIncrement);
            events.Add(new LabBattleFriendshipEvent(pokemon.Friendship, friendship, LabBattleFriendshipReason.LevelUp));
            levelsGained = level - pokemon.Level;
            pokemon = pokemon with { Experience = experience, Evs = evs, Level = level, Stats = stats, CurrentHp = hp, Friendship = friendship };
        }
        else if (pokemon.CurrentHp == 0)
        {
            // Both members are level 5; source AdjustFriendshipOnBattleFaint selects FAINT_SMALL.
            int friendship = Math.Max(0, pokemon.Friendship - 1);
            events.Add(new LabBattleFriendshipEvent(pokemon.Friendship, friendship, LabBattleFriendshipReason.Fainted));
            pokemon = pokemon with { Friendship = friendship };
        }

        int moneyAfter = money;
        if (expectedOutcome == 1)
        {
            int award = 4 * trainer.Party[^1].Level * trainer.BaseMoney;
            moneyAfter = Math.Min(MaximumMoney, money + award);
            events.Add(new LabBattleMoneyEvent(money, moneyAfter, award));
        }
        var ownedAfter = ownedBefore with { Pokemon = pokemon };
        ValidateOwned(catalog, ownedAfter);
        return new(ownedAfter, moneyAfter, experienceGained, evsGained, levelsGained,
            pokemon.Friendship - before.Friendship, pokemon.CurrentHp - afterBattlePlayer.CurrentHp, events.ToImmutable());
    }

    /// <summary>
    /// Separate source HealPlayerParty effect for a represented non-egg member, including PP Ups.
    /// Preserves ownership, EXP, EVs, levels, stats and friendship; clears the existing status word.
    /// </summary>
    public static PartyPokemon Heal(PokemonCatalog catalog, PartyPokemon owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        // Status clearing is part of this operation, though persistent ailments are not yet executable.
        ValidateOwned(catalog, owned with { Status = 0 });
        var p = owned.Pokemon;
        var moves = p.Moves.Select(slot =>
        {
            int basePp = catalog.Moves[slot.Move].Pp;
            int pp = basePp + basePp * 20 * slot.PpUps / 100;
            return slot with { CurrentPp = pp };
        }).ToImmutableArray();
        return owned with { Pokemon = p with { CurrentHp = p.Stats.MaxHp, Moves = moves }, Status = 0 };
    }

    private static void ValidateOwned(PokemonCatalog catalog, PartyPokemon owned)
    {
        PokemonFactory.ValidateCatalog(catalog);
        var error = PokemonStateValidator.ValidateOwned(catalog, owned, false, false, false);
        if (error is not null) throw new ArgumentException(error, nameof(owned));
    }

    private static void ValidateStarter(PokemonCatalog catalog, PokemonInstance pokemon)
    {
        var error = PokemonStateValidator.Validate(catalog, pokemon);
        if (error is not null) throw new ArgumentException(error, nameof(pokemon));
        if (pokemon.SourceSpeciesId is not (1 or 4 or 7) || pokemon.Level != 5 || pokemon.Experience != 135 || pokemon.Evs != SixStats.Zero)
            throw new NotSupportedException("Only level-5, EXP-135, zero-EV lab starters are supported by settlement.");
        var initial = PokemonMoveRules.InitialMoves(catalog, catalog.Species[pokemon.Species], 5);
        string attack = FirstBattleProfile.AttackMove(pokemon.SourceSpeciesId);
        string status = FirstBattleProfile.StatusMove(pokemon.SourceSpeciesId);
        if (initial.Length != 2 || initial[0].Move.Value != attack ||
            initial[1].Move.Value != status ||
            pokemon.Moves.Length != initial.Length || pokemon.Moves.Where((m, i) =>
                m with { CurrentPp = initial[i].CurrentPp } != initial[i]).Any())
            throw new NotSupportedException("Only the source two initial moves without PP Ups are supported by settlement.");
    }

    private static void ValidateTrainer(TrainerDefinition trainer, PokemonInstance player, PokemonInstance opponent)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        var (opponentSpecies, trainerNumber, personality) = player.SourceSpeciesId switch
        {
            1 => (4, 328, 0x000B9188u),
            4 => (7, 326, 0x000A4188u),
            7 => (1, 327, 0x000AE388u),
            _ => throw new NotSupportedException("Unknown lab starter.")
        };
        string suffix = opponentSpecies switch { 1 => "BULBASAUR", 4 => "CHARMANDER", _ => "SQUIRTLE" };
        if (trainer.Id.Value != "pret:TRAINER_RIVAL_OAKS_LAB_" + suffix || trainer.SourceTrainerId != trainerNumber ||
            trainer.TrainerClass != "TRAINER_CLASS_RIVAL_EARLY" || trainer.SourceTrainerClass != 81 || trainer.BaseMoney != 4 ||
            trainer.DoubleBattle || trainer.PartyFlags != 0 || trainer.AiFlags != 7 ||
            trainer.Items.IsDefault || trainer.Items.Length != 4 || trainer.Items.Any(i => i != "ITEM_NONE") ||
            trainer.Party.IsDefault || trainer.Party.Length != 1 || trainer.Party[0] is not { } member ||
            member.SourceSpeciesId != opponentSpecies || member.Species != opponent.Species || member.Level != 5 ||
            member.SourceIv != 0 || member.HeldItem != "ITEM_NONE" || member.CustomMoves.IsDefault || !member.CustomMoves.IsEmpty ||
            opponent.SourceSpeciesId != opponentSpecies || opponent.Personality != personality || opponent.Ivs != SixStats.Zero)
            throw new NotSupportedException("Opponent and trainer must match the pinned source lab counterpick.");
    }

    private static void ValidateRewardCatalog(PokemonCatalog catalog)
    {
        PokemonFactory.ValidateCatalog(catalog);
        foreach (var (name, number, yield, ev, stats) in new[]
        {
            ("BULBASAUR", 1, 64, new SixStats(0, 0, 0, 0, 1, 0), new SixStats(45, 49, 49, 45, 65, 65)),
            ("CHARMANDER", 4, 65, new SixStats(0, 0, 0, 1, 0, 0), new SixStats(39, 52, 43, 65, 60, 50)),
            ("SQUIRTLE", 7, 66, new SixStats(0, 0, 1, 0, 0, 0), new SixStats(44, 48, 65, 43, 50, 64))
        })
        {
            var species = catalog.Species[new SpeciesId("pret:SPECIES_" + name)];
            if (species.SourceSpeciesId != number || species.ExperienceYield != yield || species.EvYield != ev ||
                species.GrowthRate != GrowthRate.MediumSlow || species.BaseStats != stats || species.Friendship != 70 ||
                !PokemonMoveRules.AtLevel(species, 6).IsEmpty || species.Evolutions.Any(e => e.Level <= 6))
                throw new NotSupportedException("Changed source starter reward/growth fields are not supported.");
        }
    }

    private static BattleStats CalculateStats(PokemonSpeciesDefinition species, PokemonInstance p, SixStats ev, int level)
    {
        var b = species.BaseStats;
        int Other(int value, int iv, int effort, Stat stat) => StatRules.CalculateOther(value, iv, effort, level, p.Nature, stat);
        return new(StatRules.CalculateHp(b.Hp, p.Ivs.Hp, ev.Hp, level),
            Other(b.Attack, p.Ivs.Attack, ev.Attack, Stat.Attack), Other(b.Defense, p.Ivs.Defense, ev.Defense, Stat.Defense),
            Other(b.SpecialAttack, p.Ivs.SpecialAttack, ev.SpecialAttack, Stat.SpecialAttack),
            Other(b.SpecialDefense, p.Ivs.SpecialDefense, ev.SpecialDefense, Stat.SpecialDefense),
            Other(b.Speed, p.Ivs.Speed, ev.Speed, Stat.Speed));
    }

    private static SixStats Add(SixStats a, SixStats b) => new(a.Hp + b.Hp, a.Attack + b.Attack, a.Defense + b.Defense,
        a.Speed + b.Speed, a.SpecialAttack + b.SpecialAttack, a.SpecialDefense + b.SpecialDefense);
    private static bool ValidRegion(string? value) => value is not null && value.Length is > 7 and <= 128 &&
        value.StartsWith("MAPSEC_", StringComparison.Ordinal) && value[7..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
