using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WildRewardExperienceEvent), "experience")]
[JsonDerivedType(typeof(WildRewardEvEvent), "evs")]
[JsonDerivedType(typeof(WildRewardLevelEvent), "level")]
[JsonDerivedType(typeof(WildRewardFriendshipEvent), "friendship")]
[JsonDerivedType(typeof(WildRewardMoveLearnedEvent), "move-learned")]
[JsonDerivedType(typeof(WildRewardMovePendingEvent), "move-pending")]
public abstract record WildRewardEvent;
public sealed record WildRewardExperienceEvent(uint Before, uint After, int Gained) : WildRewardEvent;
public sealed record WildRewardEvEvent(SixStats Before, SixStats After, SixStats Gained) : WildRewardEvent;
public sealed record WildRewardLevelEvent(int Before, int After, BattleStats StatsBefore, BattleStats StatsAfter,
    int HpBefore, int HpAfter) : WildRewardEvent;
public sealed record WildRewardFriendshipEvent(int Before, int After) : WildRewardEvent;
public sealed record WildRewardMoveLearnedEvent(int Level, MoveId Move, int Slot) : WildRewardEvent;
public sealed record WildRewardMovePendingEvent(int Level, MoveId Move) : WildRewardEvent;
public sealed record WildPendingMove(int Level, MoveId Move);

/// <summary>
/// Unhealed reward state. PendingMoves must be retained and resolved in order before returning to
/// the field; later candidates may become AlreadyKnown after the earlier replacement decisions.
/// ExperienceGained is the source announcement; ExperienceCredited can be smaller at level 100.
/// </summary>
public sealed record WildVictoryRewardResult(PartyPokemon Pokemon, int ExperienceGained, SixStats EvsGained,
    int LevelsGained, int FriendshipDelta, int HpDelta, ImmutableArray<WildRewardEvent> Events,
    ImmutableArray<WildPendingMove> PendingMoves)
{
    public int ExperienceCredited => Events.OfType<WildRewardExperienceEvent>().Sum(e => checked((int)(e.After - e.Before)));
}

/// <summary>
/// Pure ordinary wild settlement for one locally owned, participating, non-egg member and a
/// defeated opponent admitted by an explicit catalog profile. Caller establishes ownership,
/// participation and no Pokerus, then commits atomically with its settlement ledger. No healing, money or IO.
/// </summary>
public static class WildVictoryRewards
{
    public const string RulesVersion = "firered-route1-wild-rewards-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;

    public static WildVictoryRewardResult Apply(PokemonCatalog catalog, PartyPokemon participantAfterBattle,
        PokemonInstance defeatedPokemon, string currentRegion, int aliveParticipants = 1, bool forestRules = false, bool trainerBattle = false, bool pewterRules = false, bool route22Rules = false, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonApproachRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool pokemonTowerRules = false, bool snorlaxRules = false, bool fuchsiaRules = false, WildBattleCombatant? defeatedBattleView = null)
    {
        if((catalog.Species.Count==107)!=fuchsiaRules)throw new NotSupportedException("Explicit Fuchsia reward profile required.");
        if ((catalog.Species.Count == 101) != (pokemonTowerRules && !snorlaxRules)) throw new NotSupportedException("Explicit Pokemon Tower reward profile required.");
        if (!fuchsiaRules && (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102) != snorlaxRules) throw new NotSupportedException("Explicit Snorlax catalog profile required.");
        pokemonTowerRules |= snorlaxRules;
        rocketHideoutRules |= pokemonTowerRules;
        celadonGymRules |= rocketHideoutRules;
        if ((catalog.Species.Count == 98) != (celadonGymRules && !rocketHideoutRules) || (catalog.Species.Count == 99) != (rocketHideoutRules && !pokemonTowerRules)) throw new NotSupportedException("Explicit Celadon Gym reward profile required.");
        if (celadonGymRules) celadonApproachRules = false;
        if ((catalog.Species.Count == 94) != celadonApproachRules) throw new NotSupportedException("Celadon approach rewards require the explicit 94-species profile.");
        rockTunnelRules |= celadonApproachRules || celadonGymRules;
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules;
        ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        ceruleanRules |= billRules;
        mtMoonRules |= ceruleanRules;
        route3Rules |= mtMoonRules;
        route22Rules |= route3Rules;
        if(trainerBattle&&!forestRules)throw new NotSupportedException("Trainer rewards require the authenticated forest profile.");
        if(aliveParticipants is <1 or >6)throw new ArgumentOutOfRangeException(nameof(aliveParticipants));
        if(pewterRules&&!forestRules)throw new NotSupportedException("Pewter rewards require forest growth rules.");
        if(route22Rules&&!forestRules)throw new NotSupportedException("Route22 rewards require forest growth rules.");
        pewterRules |= route22Rules;
        ValidateRewardCatalog(catalog,forestRules,pewterRules,route22Rules,route3Rules,mtMoonRules,ceruleanRules,billRules,vermilionRules,ssAnneRules,vermilionGymRules,route11Rules,rockTunnelRules);
        ValidateOwned(catalog, participantAfterBattle, route22Rules, route11Rules, rockTunnelRules);
        if(defeatedBattleView is not null&&defeatedBattleView.Pokemon!=defeatedPokemon)throw new ArgumentException("Reward battle view mismatch.",nameof(defeatedBattleView));
        var opponentError = defeatedBattleView?.Transform is not null ? WildBattleTransformationRules.Validate(catalog,defeatedBattleView,fuchsiaRules) : PokemonStateValidator.Validate(catalog, defeatedPokemon);
        if (opponentError is not null) throw new ArgumentException(opponentError, nameof(defeatedPokemon));
        if (fuchsiaRules && FuchsiaBattleScope.SupportsSpecies(defeatedPokemon.Species) ? false : snorlaxRules && EeveeBattleScope.SupportsSharedSpecies(defeatedPokemon.Species) ? false : pokemonTowerRules && PokemonTowerBattleScope.SupportsSpecies(defeatedPokemon.Species) ? false : rockTunnelRules && RocketHideoutBattleScope.SupportsSharedSpecies(defeatedPokemon.Species) ? false : route11Rules && defeatedPokemon.SourceSpeciesId is 37 or 50 or 51 or 60 or 61 or 82 ? false : vermilionGymRules && defeatedPokemon.SourceSpeciesId == 26 ? false : ssAnneRules && defeatedPokemon.SourceSpeciesId is 2 or 5 or 8 or 58 or 64 or 72 or 77 ? false : vermilionRules && defeatedPokemon.SourceSpeciesId is 12 or 20 or 52 ? false : billRules && defeatedPokemon.SourceSpeciesId==79 ? false : ceruleanRules?defeatedPokemon.SourceSpeciesId is not (1 or 4 or 7 or 10 or 11 or 13 or 14 or 16 or 17 or 19 or 21 or 23 or 25 or 27 or 29 or 32 or 35 or 39 or 41 or 43 or 46 or 56 or 63 or 66 or 69 or 74 or 81 or 88 or 90 or 95 or 96 or 100 or 109 or 116 or 118 or 120 or 121 or 129):mtMoonRules?defeatedPokemon.SourceSpeciesId is not (1 or 4 or 7 or 10 or 11 or 13 or 14 or 16 or 19 or 21 or 23 or 25 or 27 or 29 or 32 or 35 or 39 or 41 or 43 or 46 or 56 or 69 or 74 or 81 or 88 or 95 or 100 or 109 or 129):route3Rules?defeatedPokemon.SourceSpeciesId is not (1 or 4 or 7 or 10 or 11 or 13 or 14 or 16 or 19 or 21 or 23 or 25 or 27 or 29 or 32 or 39 or 56 or 74 or 95 or 129):route22Rules?defeatedPokemon.SourceSpeciesId is not (1 or 4 or 7 or 10 or 11 or 13 or 14 or 16 or 19 or 21 or 25 or 27 or 56 or 74 or 95):pewterRules?defeatedPokemon.SourceSpeciesId is not (10 or 11 or 13 or 14 or 16 or 19 or 25 or 27 or 74 or 95):forestRules?defeatedPokemon.SourceSpeciesId is not (10 or 11 or 13 or 14 or 16 or 19 or 25):defeatedPokemon.SourceSpeciesId is not (16 or 19))
            throw new NotSupportedException("Only defeated source Pidgey and Rattata are supported.");
        if (defeatedPokemon.CurrentHp != 0)
            throw new ArgumentException("Wild rewards require a defeated opponent.", nameof(defeatedPokemon));
        if (!ValidRegion(currentRegion))
            throw new ArgumentException("A source MAPSEC region is required.", nameof(currentRegion));
        if ((participantAfterBattle.HeldItem != "ITEM_NONE" && !(fuchsiaRules ? FuchsiaBattleScope.SupportsHeldItem(participantAfterBattle.HeldItem) : pokemonTowerRules ? PokemonTowerBattleScope.SupportsHeldItem(participantAfterBattle.HeldItem) : rockTunnelRules ? RocketHideoutBattleScope.SupportsHeldItemForCatalog(catalog, participantAfterBattle.HeldItem) : route11Rules ? Route11BattleScope.SupportsHeldItem(participantAfterBattle.HeldItem) : ssAnneRules ? SsAnneBattleScope.SupportsHeldItem(participantAfterBattle.HeldItem) : vermilionRules ? VermilionBattleScope.SupportsHeldItem(participantAfterBattle.HeldItem) : ceruleanRules ? MtMoonBattleScope.SupportsHeldItem(participantAfterBattle.HeldItem) : route3Rules && participantAfterBattle.HeldItem == "ITEM_ORAN_BERRY")) || participantAfterBattle.Ball == "ITEM_LUXURY_BALL")
            throw new NotSupportedException("Held-item modifiers and Luxury Ball friendship are outside this reward profile.");
        if(!forestRules&&participantAfterBattle.Status!=0)throw new NotSupportedException("Status requires the forest reward profile.");
        if (participantAfterBattle.Pokemon.Moves.Any(m => m.PpUps != 0))
            throw new NotSupportedException("PP Ups are outside the shared move-learning profile.");

        var result = PokemonRewardRecipe.Calculate(catalog, participantAfterBattle.Pokemon,
            catalog.Species[defeatedPokemon.Species], defeatedPokemon.Level, aliveParticipants, 0,
            new(true, false, false, trainerBattle, false, 1,
                pokemonTowerRules && participantAfterBattle.HeldItem == "ITEM_SOOTHE_BELL", false,
                participantAfterBattle.MetLocation == currentRegion), rejectLevelEvolution: !forestRules);
        var owned = ReferenceEquals(result.Pokemon, participantAfterBattle.Pokemon) ? participantAfterBattle :
            participantAfterBattle with { Pokemon = result.Pokemon };
        if (participantAfterBattle.Pokemon.CurrentHp != 0 && participantAfterBattle.Pokemon.Level != 100)
            ValidateOwned(catalog, owned, route22Rules, route11Rules);
        return new(owned, result.ExperienceGained, result.EvsGained, result.LevelsGained, result.FriendshipDelta,
            result.HpDelta, result.Events, result.PendingMoves);
    }

    /// <summary>Bounded Safari-owned participant settlement through the shared source growth recipe.
    /// Campaign ownership, participation and settlement receipts remain the application's authority.</summary>
    public static WildVictoryRewardResult ApplySafariOwned(SafariOwnedPokemonCatalog owned,
        PartyPokemon participantAfterBattle, PokemonInstance defeatedPokemon, string currentRegion,
        int aliveParticipants = 1, bool trainerBattle = false, WildBattleCombatant? defeatedBattleView = null) =>
        ApplySafariOwned((OwnedBattlePokemonCatalog)owned, participantAfterBattle, defeatedPokemon, currentRegion,
            aliveParticipants, trainerBattle, defeatedBattleView);

    public static WildVictoryRewardResult ApplySafariOwned(OwnedBattlePokemonCatalog owned,
        PartyPokemon participantAfterBattle, PokemonInstance defeatedPokemon, string currentRegion,
        int aliveParticipants = 1, bool trainerBattle = false, WildBattleCombatant? defeatedBattleView = null) =>
        ApplyOwnedReward(owned, participantAfterBattle, defeatedPokemon, currentRegion, aliveParticipants,
            trainerBattle, defeatedBattleView, null);

    private static WildVictoryRewardResult ApplyOwnedReward(OwnedBattlePokemonCatalog owned,
        PartyPokemon participantAfterBattle, PokemonInstance defeatedPokemon, string currentRegion,
        int aliveParticipants, bool trainerBattle, WildBattleCombatant? defeatedBattleView, string? trainerProfile)
    {
        ArgumentNullException.ThrowIfNull(owned);
        var catalog = owned.Catalog;
        ValidateOwned(catalog, participantAfterBattle, route22Rules: true, route11Rules: true, rockTunnelRules: true);
        if (aliveParticipants is < 1 or > 6 || !ValidRegion(currentRegion)) throw new ArgumentException("safari_reward_context");
        if (defeatedPokemon is null || !(trainerBattle ? owned.AdmitsTrainerOpponent(defeatedPokemon.Species, trainerProfile) : catalog.Species.ContainsKey(defeatedPokemon.Species)) || defeatedPokemon.CurrentHp != 0)
            throw new ArgumentException("safari_reward_defeated");
        if (defeatedBattleView is not null && defeatedBattleView.Pokemon != defeatedPokemon)
            throw new ArgumentException("safari_reward_view_mismatch");
        if (defeatedBattleView is { } view)
        {
            if (WildBattleTransformationRules.Validate(owned.Mechanics.Catalog, view, enabled: true) is { } error) throw new ArgumentException(error);
        }
        else if (SourcePokemonValidator.ValidatePermanent(owned.Mechanics, defeatedPokemon) is { } error) throw new ArgumentException(error);
        // This participant-only campaign adapter does not manufacture a full-party EXP Share context.
        if (participantAfterBattle.HeldItem == "ITEM_EXP_SHARE") throw new NotSupportedException("safari_reward_full_party_required");
        var result = PokemonRewardRecipe.Calculate(catalog, participantAfterBattle.Pokemon,
            owned.Mechanics.Catalog.Species[defeatedPokemon.Species], defeatedPokemon.Level, aliveParticipants, 0,
            new(true, false, participantAfterBattle.HeldItem == "ITEM_LUCKY_EGG", trainerBattle, false,
                participantAfterBattle.HeldItem == "ITEM_MACHO_BRACE" ? 2 : 1,
                participantAfterBattle.HeldItem == "ITEM_SOOTHE_BELL", participantAfterBattle.Ball == "ITEM_LUXURY_BALL",
                participantAfterBattle.MetLocation == currentRegion), mechanics: owned.Mechanics);
        var projected = participantAfterBattle with { Pokemon = result.Pokemon };
        ValidateOwned(catalog, projected, route22Rules: true, route11Rules: true, rockTunnelRules: true);
        return new(projected, result.ExperienceGained, result.EvsGained, result.LevelsGained,
            result.FriendshipDelta, result.HpDelta, result.Events, result.PendingMoves);
    }

    /// <summary>Silph settlement authenticates the exact original trainer roster and current EXP boundary.
    /// Participation, receipt consumption and any remaining party recipients belong to the application.</summary>
    public static WildVictoryRewardResult ApplySilphTrainer(SilphOwnedPokemonCatalog owned,
        PartyPokemon participantAfterBattle, Game.Domain.Trainers.ForestTrainerCombatState battle,
        string currentRegion, int aliveParticipants = 1)
    {
        ArgumentNullException.ThrowIfNull(owned); ArgumentNullException.ThrowIfNull(battle);
        OwnedBattlePokemonCatalog proof = owned;
        if (battle.RulesVersion != Game.Domain.Trainers.ForestTrainerCombatRules.SilphRulesVersion ||
            !Game.Domain.Battle.SourceBattleAdmission.IsOwnedCampaign(proof, battle.Battle) ||
            battle.Phase != Game.Domain.Trainers.ForestTrainerCombatPhase.AwaitingExperience ||
            Game.Domain.Trainers.ForestTrainerCombatRules.Validate(owned.Mechanics.Catalog, battle) is not null ||
            participantAfterBattle?.Pokemon is null || battle.Battle.CalledParty is not { } party ||
            !party.Player.Any(member => WildBattleSourcePartyEffects.SamePokemon(member.Pokemon, participantAfterBattle.Pokemon) &&
                member.Status == participantAfterBattle.Status && WildBattleSourcePartyEffects.PersistentHeldItem(member) == participantAfterBattle.HeldItem))
            throw new ArgumentException("silph_trainer_reward_context");
        return ApplySafariOwned(proof, participantAfterBattle, battle.Battle.Opponent.Pokemon,
            currentRegion, aliveParticipants, trainerBattle: true, defeatedBattleView: battle.Battle.Opponent);
    }

    /// <summary>Exact Saffron trainer settlement; Slowbro rewards require this authenticated
    /// source battle at its actual EXP boundary. Historical rewards retain their earlier species scope.</summary>
    public static WildVictoryRewardResult ApplySaffronGymTrainer(SaffronGymOpponentPokemonCatalog opponents,
        PartyPokemon participantAfterBattle, Game.Domain.Trainers.ForestTrainerCombatState battle,
        string currentRegion, int aliveParticipants = 1)
    {
        ArgumentNullException.ThrowIfNull(opponents);
        var owned = opponents.Owned; ArgumentNullException.ThrowIfNull(battle);
        OwnedBattlePokemonCatalog proof = owned;
        if (!ReferenceEquals(SaffronGymOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            battle.Battle.SourceCampaignRulesVersion != SaffronGymOpponentPokemonCatalog.Profile ||
            battle.RulesVersion != Game.Domain.Trainers.ForestTrainerCombatRules.SaffronGymRulesVersion ||
            !Game.Domain.Battle.SourceBattleAdmission.IsOwnedCampaign(proof, battle.Battle) ||
            battle.Phase != Game.Domain.Trainers.ForestTrainerCombatPhase.AwaitingExperience ||
            Game.Domain.Trainers.ForestTrainerCombatRules.Validate(owned.Mechanics.Catalog, battle) is not null ||
            participantAfterBattle?.Pokemon is null || battle.Battle.CalledParty is not { } party ||
            !party.Player.Any(member => WildBattleSourcePartyEffects.SamePokemon(member.Pokemon, participantAfterBattle.Pokemon) &&
                member.Status == participantAfterBattle.Status && WildBattleSourcePartyEffects.PersistentHeldItem(member) == participantAfterBattle.HeldItem))
            throw new ArgumentException("saffron_gym_trainer_reward_context");
        return ApplyOwnedReward(proof, participantAfterBattle, battle.Battle.Opponent.Pokemon,
            currentRegion, aliveParticipants, true, battle.Battle.Opponent, SaffronGymOpponentPokemonCatalog.Profile);
    }

    /// <summary>Original Silph roster settlement with the current authenticated Cinnabar ownership.</summary>
    public static WildVictoryRewardResult ApplySilphTrainer(CinnabarOpponentPokemonCatalog opponents,
        PartyPokemon participantAfterBattle, Game.Domain.Trainers.ForestTrainerCombatState battle,
        string currentRegion, int aliveParticipants = 1) =>
        ApplyCinnabarHistoricalTrainer(opponents, participantAfterBattle, battle, currentRegion,
            aliveParticipants, Game.Domain.Trainers.ForestTrainerCombatRules.SilphRulesVersion);

    /// <summary>Original Saffron roster settlement with the current authenticated Cinnabar ownership.</summary>
    public static WildVictoryRewardResult ApplySaffronGymTrainer(CinnabarOpponentPokemonCatalog opponents,
        PartyPokemon participantAfterBattle, Game.Domain.Trainers.ForestTrainerCombatState battle,
        string currentRegion, int aliveParticipants = 1) =>
        ApplyCinnabarHistoricalTrainer(opponents, participantAfterBattle, battle, currentRegion,
            aliveParticipants, Game.Domain.Trainers.ForestTrainerCombatRules.SaffronGymRulesVersion);

    private static WildVictoryRewardResult ApplyCinnabarHistoricalTrainer(CinnabarOpponentPokemonCatalog opponents,
        PartyPokemon participantAfterBattle, Game.Domain.Trainers.ForestTrainerCombatState battle,
        string currentRegion, int aliveParticipants, string trainerRulesVersion)
    {
        ArgumentNullException.ThrowIfNull(opponents); ArgumentNullException.ThrowIfNull(battle);
        OwnedBattlePokemonCatalog proof = opponents.Owned;
        if (!ReferenceEquals(CinnabarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            battle.Battle.SourceCampaignRulesVersion != CinnabarOpponentPokemonCatalog.Profile ||
            battle.RulesVersion != trainerRulesVersion ||
            !Game.Domain.Battle.SourceBattleAdmission.IsOwnedCampaign(proof, battle.Battle) ||
            battle.Phase != Game.Domain.Trainers.ForestTrainerCombatPhase.AwaitingExperience ||
            Game.Domain.Trainers.ForestTrainerCombatRules.Validate(proof.Mechanics.Catalog, battle) is not null ||
            participantAfterBattle?.Pokemon is null || battle.Battle.CalledParty is not { } party ||
            !party.Player.Any(member => WildBattleSourcePartyEffects.SamePokemon(member.Pokemon, participantAfterBattle.Pokemon) &&
                member.Status == participantAfterBattle.Status && WildBattleSourcePartyEffects.PersistentHeldItem(member) == participantAfterBattle.HeldItem))
            throw new ArgumentException("cinnabar_historical_trainer_reward_context");
        return ApplyOwnedReward(proof, participantAfterBattle, battle.Battle.Opponent.Pokemon,
            currentRegion, aliveParticipants, true, battle.Battle.Opponent, CinnabarOpponentPokemonCatalog.Profile);
    }

    public static WildVictoryRewardResult ApplyCinnabarTrainer(CinnabarOpponentPokemonCatalog opponents,
        PartyPokemon participantAfterBattle, Game.Domain.Trainers.ForestTrainerCombatState battle,
        string currentRegion, int aliveParticipants = 1)
    {
        ArgumentNullException.ThrowIfNull(opponents);
        var owned = opponents.Owned; ArgumentNullException.ThrowIfNull(battle);
        OwnedBattlePokemonCatalog proof = owned;
        if (!ReferenceEquals(CinnabarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            battle.Battle.SourceCampaignRulesVersion != CinnabarOpponentPokemonCatalog.Profile ||
            battle.RulesVersion != Game.Domain.Trainers.ForestTrainerCombatRules.CinnabarRulesVersion ||
            !Game.Domain.Battle.SourceBattleAdmission.IsOwnedCampaign(proof, battle.Battle) ||
            battle.Phase != Game.Domain.Trainers.ForestTrainerCombatPhase.AwaitingExperience ||
            Game.Domain.Trainers.ForestTrainerCombatRules.Validate(owned.Mechanics.Catalog, battle) is not null ||
            participantAfterBattle?.Pokemon is null || battle.Battle.CalledParty is not { } party ||
            !party.Player.Any(member => WildBattleSourcePartyEffects.SamePokemon(member.Pokemon, participantAfterBattle.Pokemon) &&
                member.Status == participantAfterBattle.Status && WildBattleSourcePartyEffects.PersistentHeldItem(member) == participantAfterBattle.HeldItem))
            throw new ArgumentException("cinnabar_trainer_reward_context");
        return ApplyOwnedReward(proof, participantAfterBattle, battle.Battle.Opponent.Pokemon,
            currentRegion, aliveParticipants, true, battle.Battle.Opponent, CinnabarOpponentPokemonCatalog.Profile);
    }

    private static void ValidateOwned(PokemonCatalog catalog, PartyPokemon owned, bool route22Rules = false, bool route11Rules = false, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        var names = catalog.Species.Values.ToImmutableSortedDictionary(s => s.SourceSpeciesId,
            s => s.SourceSpeciesId switch { 29 => "NIDORAN♀", 32 => "NIDORAN♂", _ => s.Id.Value["pret:SPECIES_".Length..] });
        var error = PokemonStateValidator.ValidateParty(new(catalog, names, PokemonStateValidator.NicknameAlphabet) { AllowSleepStatus = route22Rules, AllowFreezeStatus = route11Rules,
            AllowToxicStatus = OwnedBattlePokemonCatalog.For(catalog) is not null || catalog.Species.Count == 107 && catalog.Moves.Count == 354 }, owned);
        if (error is not null) throw new ArgumentException(error, nameof(owned));
        if (route22Rules && owned.Pokemon.AbilityId == "ABILITY_VITAL_SPIRIT" && WildBattleStatus.IsSleeping(owned.Status))
            throw new NotSupportedException("Vital Spirit cannot enter rewards asleep in the Route22 profile.");
    }

    private static void ValidateRewardCatalog(PokemonCatalog catalog,bool forestRules,bool pewterRules,bool route22Rules,bool route3Rules,bool mtMoonRules,bool ceruleanRules,bool billRules,bool vermilionRules,bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        PokemonFactory.ValidateCatalog(catalog);
        if (route22Rules)
        {
            if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : mtMoonRules ? 56 : route3Rules ? 40 : 32)) throw new NotSupportedException("Rewards require the complete versioned species catalog.");
            foreach (var (name, id, experience, ev) in new[] {
                ("BULBASAUR", 1, 64, new SixStats(0,0,0,0,1,0)),
                ("CHARMANDER", 4, 65, new SixStats(0,0,0,1,0,0)),
                ("SQUIRTLE", 7, 66, new SixStats(0,0,1,0,0,0)),
                ("SPEAROW", 21, 58, new SixStats(0,0,0,1,0,0)),
                ("MANKEY", 56, 74, new SixStats(0,1,0,0,0,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_" + name), out var species) ||
                    species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("The pinned Route22 EXP/EV fields are required.");
        }
        if (route3Rules)
        {
            foreach (var (name, id, experience, ev) in new[] {
                ("EKANS", 23, 62, new SixStats(0,1,0,0,0,0)),
                ("NIDORAN_F", 29, 59, new SixStats(1,0,0,0,0,0)),
                ("NIDORINA", 30, 117, new SixStats(2,0,0,0,0,0)),
                ("NIDORAN_M", 32, 60, new SixStats(0,1,0,0,0,0)),
                ("NIDORINO", 33, 118, new SixStats(0,2,0,0,0,0)),
                ("JIGGLYPUFF", 39, 76, new SixStats(2,0,0,0,0,0)),
                ("MAGIKARP", 129, 20, new SixStats(0,0,0,1,0,0)),
                ("GYARADOS", 130, 214, new SixStats(0,2,0,0,0,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_" + name), out var species) ||
                    species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("The pinned Route3 EXP/EV fields are required.");
        }
        if (mtMoonRules)
        {
            // Exact new species fields from the pinned species_info.h; ordinary arithmetic below is shared.
            foreach (var (name, id, experience, ev) in new[] {
                ("ARBOK", 24, 147, new SixStats(0,2,0,0,0,0)),
                ("NIDOQUEEN", 31, 194, new SixStats(3,0,0,0,0,0)),
                ("NIDOKING", 34, 195, new SixStats(0,3,0,0,0,0)),
                ("CLEFAIRY", 35, 68, new SixStats(2,0,0,0,0,0)),
                ("CLEFABLE", 36, 129, new SixStats(3,0,0,0,0,0)),
                ("WIGGLYTUFF", 40, 109, new SixStats(3,0,0,0,0,0)),
                ("ZUBAT", 41, 54, new SixStats(0,0,0,1,0,0)),
                ("GOLBAT", 42, 171, new SixStats(0,0,0,2,0,0)),
                ("ODDISH", 43, 78, new SixStats(0,0,0,0,1,0)),
                ("PARAS", 46, 70, new SixStats(0,1,0,0,0,0)),
                ("PARASECT", 47, 128, new SixStats(0,2,1,0,0,0)),
                ("BELLSPROUT", 69, 84, new SixStats(0,1,0,0,0,0)),
                ("MAGNEMITE", 81, 89, new SixStats(0,0,0,0,1,0)),
                ("GRIMER", 88, 90, new SixStats(1,0,0,0,0,0)),
                ("VOLTORB", 100, 103, new SixStats(0,0,0,1,0,0)),
                ("KOFFING", 109, 114, new SixStats(0,0,1,0,0,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_" + name), out var species) ||
                    species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("The pinned MtMoon EXP/EV fields are required.");
        }
        if (catalog.Species.Count == 94)
        {
            if (!catalog.Species.TryGetValue(new("pret:SPECIES_MUK"), out var muk) || muk.SourceSpeciesId != 89 || muk.ExperienceYield != 157 || muk.EvYield != new SixStats(1,1,0,0,0,0))
                throw new NotSupportedException("Pinned Muk EXP/EV fields are required.");
        }
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102 or 107)
        {
            foreach (var (name, id, experience, ev) in new[] {
                ("GASTLY", 92, 95, new SixStats(0,0,0,0,1,0)),
                ("HAUNTER", 93, 126, new SixStats(0,0,0,0,2,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_" + name), out var species) ||
                    species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("Pinned Pokemon Tower EXP/EV fields are required.");
        }
        if (rockTunnelRules)
        {
            foreach (var (name,id,experience,ev) in new[] { ("CUBONE",104,87,new SixStats(0,0,1,0,0,0)),
("ELECTRODE",101,150,new SixStats(0,0,0,2,0,0)),
("HYPNO",97,165,new SixStats(0,0,0,0,0,2)),
("LICKITUNG",108,127,new SixStats(2,0,0,0,0,0)),
("MACHOKE",67,146,new SixStats(0,2,0,0,0,0)),
("MAROWAK",105,124,new SixStats(0,0,2,0,0,0)),
("PRIMEAPE",57,149,new SixStats(0,2,0,0,0,0)),
("RHYHORN",111,135,new SixStats(0,0,1,0,0,0)),
("SEADRA",117,155,new SixStats(0,0,1,0,1,0)),
("SEAKING",119,170,new SixStats(0,2,0,0,0,0)),
("TENTACRUEL",73,205,new SixStats(0,0,0,0,0,2)),
("VENOMOTH",49,138,new SixStats(0,0,0,1,1,0)),
("VENONAT",48,75,new SixStats(0,0,0,0,0,1)),
("WEEZING",110,173,new SixStats(0,0,2,0,0,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_"+name),out var species) || species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("Pinned Rock Tunnel EXP/EV fields are required.");
        }
        if (route11Rules)
        {
            foreach (var (name,id,experience,ev) in new[] {
                ("DIGLETT",50,81,new SixStats(0,0,0,1,0,0)), ("DUGTRIO",51,153,new SixStats(0,0,0,2,0,0)),
                ("MAGNETON",82,161,new SixStats(0,0,0,0,2,0)), ("POLIWAG",60,77,new SixStats(0,0,0,1,0,0)),
                ("POLIWHIRL",61,131,new SixStats(0,0,0,2,0,0)), ("VULPIX",37,63,new SixStats(0,0,0,1,0,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_"+name),out var species) || species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("Pinned Route11 EXP/EV fields are required.");
        }
        if (ssAnneRules)
        {
            foreach (var (name,id,experience,ev) in new[] { ("IVYSAUR",2,141,new SixStats(0,0,0,0,1,1)), ("CHARMELEON",5,142,new SixStats(0,0,0,1,1,0)),
                ("WARTORTLE",8,143,new SixStats(0,0,1,0,0,1)), ("GROWLITHE",58,91,new SixStats(0,1,0,0,0,0)),
                ("TENTACOOL",72,105,new SixStats(0,0,0,0,0,1)), ("PONYTA",77,152,new SixStats(0,0,0,1,0,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_"+name),out var species) || species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("Pinned S.S. Anne EXP/EV fields are required.");
        }
        if(vermilionRules)
        {
            foreach(var (name,id,experience,ev) in new[]{("MEOWTH",52,69,new SixStats(0,0,0,1,0,0)),("BUTTERFREE",12,160,new SixStats(0,0,0,0,2,1)),("RATICATE",20,116,new SixStats(0,0,0,2,0,0))})
                if(!catalog.Species.TryGetValue(new("pret:SPECIES_"+name),out var species)||species.SourceSpeciesId!=id||species.ExperienceYield!=experience||species.EvYield!=ev)
                    throw new NotSupportedException("The pinned Vermilion EXP/EV fields are required.");
        }
        if(billRules)
        {
            foreach(var (name,id,experience,ev) in new[]{("SLOWPOKE",79,99,new SixStats(1,0,0,0,0,0)),("GLOOM",44,132,new SixStats(0,0,0,0,2,0)),("WEEPINBELL",70,151,new SixStats(0,2,0,0,0,0))})
                if(!catalog.Species.TryGetValue(new("pret:SPECIES_"+name),out var species)||species.SourceSpeciesId!=id||species.ExperienceYield!=experience||species.EvYield!=ev)
                    throw new NotSupportedException("The pinned Bill EXP/EV fields are required.");
        }
        if (ceruleanRules)
        {
            foreach (var (name, id, experience, ev) in new[] {
                ("PIDGEOTTO", 17, 113, new SixStats(0,0,0,2,0,0)),
                ("ABRA", 63, 73, new SixStats(0,0,0,0,1,0)),
                ("KADABRA", 64, 145, new SixStats(0,0,0,0,2,0)),
                ("MACHOP", 66, 88, new SixStats(0,1,0,0,0,0)),
                ("DROWZEE", 96, 102, new SixStats(0,0,0,0,0,1)),
                ("SHELLDER", 90, 97, new SixStats(0,0,1,0,0,0)),
                ("HORSEA", 116, 83, new SixStats(0,0,0,0,1,0)),
                ("GOLDEEN", 118, 111, new SixStats(0,1,0,0,0,0)),
                ("STARYU", 120, 106, new SixStats(0,0,0,1,0,0)),
                ("STARMIE", 121, 207, new SixStats(0,0,0,2,0,0)) })
                if (!catalog.Species.TryGetValue(new("pret:SPECIES_" + name), out var species) ||
                    species.SourceSpeciesId != id || species.ExperienceYield != experience || species.EvYield != ev)
                    throw new NotSupportedException("The pinned Cerulean EXP/EV fields are required.");
        }
        foreach (var (name, number, yield) in new[] { ("PIDGEY", 16, 55), ("RATTATA", 19, 57) })
        {
            if (!catalog.Species.TryGetValue(new SpeciesId("pret:SPECIES_" + name), out var species) ||
                species.SourceSpeciesId != number || species.ExperienceYield != yield ||
                species.EvYield != new SixStats(0, 0, 0, 1, 0, 0))
                throw new NotSupportedException("The pinned Route 1 wild EXP/EV source fields are required.");
        }
        if(pewterRules)
        {
            foreach(var (name,id,experience) in new[]{("GEODUDE",74,86),("SANDSHREW",27,93),("ONIX",95,108)})
                if(!catalog.Species.TryGetValue(new("pret:SPECIES_"+name),out var species)||species.SourceSpeciesId!=id||species.ExperienceYield!=experience||species.EvYield!=new SixStats(0,0,1,0,0,0))throw new NotSupportedException("The pinned Pewter EXP/EV fields are required.");
        }
        if(forestRules)
        {
            foreach(var (name,id,experience,ev) in new[]{("CATERPIE",10,53,new SixStats(1,0,0,0,0,0)),("METAPOD",11,72,new SixStats(0,0,2,0,0,0)),("WEEDLE",13,52,new SixStats(0,0,0,1,0,0)),("KAKUNA",14,71,new SixStats(0,0,2,0,0,0)),("PIKACHU",25,82,new SixStats(0,0,0,2,0,0))})
                if(!catalog.Species.TryGetValue(new("pret:SPECIES_"+name),out var species)||species.SourceSpeciesId!=id||species.ExperienceYield!=experience||species.EvYield!=ev)throw new NotSupportedException("The pinned forest EXP/EV fields are required.");
        }
    }

    private static bool ValidRegion(string? value) => value is not null && value.Length is > 7 and <= 128 &&
        value.StartsWith("MAPSEC_", StringComparison.Ordinal) && value[7..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
