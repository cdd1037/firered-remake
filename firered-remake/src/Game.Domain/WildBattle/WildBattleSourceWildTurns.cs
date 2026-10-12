using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.Randomness;

namespace Game.Domain.WildBattle;

/// <summary>Explicit ordinary-wild owner. Real party state stays in CalledParty; no save-game extension.</summary>
public sealed record WildBattleSourceWildState
{
    public string RulesVersion { get; init; } = WildBattleRules.SourceWildRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSourceWildTurn? Turn { get; init; }
}
public sealed record WildBattleSourceWildTurn(WildBattleSide First, int PlayerSlot, int OpponentSlot, int NextAction = 0)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSourceEndTurnCursor? EndTurn { get; init; }
}

public static partial class WildBattleRules
{
    public const string SourceWildRulesVersion = "firered-source-wild-turns-v1";
    public static bool IsSourceWild(WildBattleState? state) => state?.SourceWild is not null;

    /// <summary>Starts with the already generated wild instance and the actual complete owned party.</summary>
    public static WildBattleResult StartSourceWild(PokemonCatalog catalog, ImmutableArray<WildBattleCombatant> playerParty,
        int activeIndex, WildBattleCombatant opponent, RngState rng, WildBattleTerrain terrain = WildBattleTerrain.Plain,
        bool oldRodFishing = false, PokemonTowerBattleKind? towerKind = null, bool scriptedSnorlax = false)
    {
        if (!SourceBattleAdmission.IsCurrentCampaignCatalog(catalog)) return Fail(null, "source_wild_catalog");
        return StartSourceWildCore(catalog, playerParty, activeIndex, opponent, rng, terrain,
            oldRodFishing, towerKind, scriptedSnorlax);
    }

    /// <summary>Explicit mechanics-only ordinary singles; this adds no campaign encounter or save eligibility.</summary>
    public static WildBattleResult StartSourceWild(SourceMechanicsCatalog mechanics, BattleRulesProfile profile,
        ImmutableArray<WildBattleCombatant> playerParty, int activeIndex, WildBattleCombatant opponent,
        RngState rng, WildBattleTerrain terrain = WildBattleTerrain.Plain)
    {
        if (SourceBattleAdmission.ValidateEntry(mechanics, profile, playerParty, activeIndex, opponent) is { } error)
            return Fail(null, error);
        return StartSourceWildCore(mechanics.Catalog, playerParty, activeIndex, opponent, rng, terrain,
            false, null, false, profile);
    }

    /// <summary>Bounded campaign ownership with full source execution and original encounter setup draws.</summary>
    public static WildBattleResult StartSafariOwnedWild(SafariOwnedPokemonCatalog owned,
        ImmutableArray<WildBattleCombatant> playerParty, int activeIndex, WildBattleCombatant opponent,
        RngState rng, WildBattleTerrain terrain = WildBattleTerrain.Plain, bool oldRodFishing = false,
        PokemonTowerBattleKind? towerKind = null, bool scriptedSnorlax = false) =>
        StartSafariOwnedWild((OwnedBattlePokemonCatalog)owned, playerParty, activeIndex, opponent, rng,
            terrain, oldRodFishing, towerKind, scriptedSnorlax);

    public static WildBattleResult StartSafariOwnedWild(OwnedBattlePokemonCatalog owned,
        ImmutableArray<WildBattleCombatant> playerParty, int activeIndex, WildBattleCombatant opponent,
        RngState rng, WildBattleTerrain terrain = WildBattleTerrain.Plain, bool oldRodFishing = false,
        PokemonTowerBattleKind? towerKind = null, bool scriptedSnorlax = false)
    {
        if (SourceBattleAdmission.ValidateOwnedEntry(owned, playerParty, activeIndex) is { } error)
            return Fail(null, error);
        if (opponent?.Pokemon is null || !owned.AdmitsWildOpponent(opponent.Pokemon))
            return Fail(null, "source_campaign_opponent_species");
        if (SourceBattleAdmission.ValidateEntry(owned.Mechanics, BattleRulesProfile.CanonicalSourceSingles,
            playerParty, activeIndex, opponent) is { } sourceError) return Fail(null, sourceError);
        return StartSourceWildCore(owned.Mechanics.Catalog, playerParty, activeIndex, opponent, rng,
            terrain, oldRodFishing, towerKind, scriptedSnorlax, BattleRulesProfile.CanonicalSourceSingles, owned);
    }

    private static WildBattleResult StartSourceWildCore(PokemonCatalog catalog, ImmutableArray<WildBattleCombatant> playerParty,
        int activeIndex, WildBattleCombatant opponent, RngState rng, WildBattleTerrain terrain,
        bool oldRodFishing, PokemonTowerBattleKind? towerKind, bool scriptedSnorlax, BattleRulesProfile? profile = null, OwnedBattlePokemonCatalog? owned = null, bool firstSeviiWild = false)
    {
        if (playerParty.IsDefaultOrEmpty || playerParty.Length > 6 || activeIndex < 0 || activeIndex >= playerParty.Length ||
            playerParty.Any(m => m?.Pokemon is null) || opponent?.Pokemon is null) return Fail(null, "source_wild_party");
        if (!Enum.IsDefined(terrain) || ValidateRng(rng) is not null) return Fail(null, "source_wild_entry_options");
        if (playerParty.Append(opponent).Any(m => !Neutral(m) || m.CalledOverlay is not null || m.CalledVolatile is not null ||
            m.CalledTurnState is not null || m.CalledDamage is not null || m.SourceMoveHistory is not null || m.KnockedOffItem is not null || m.SourcePartyStatusOverride is not null))
            return Fail(null, "source_wild_entry_volatile");
        if (playerParty[activeIndex].Pokemon.CurrentHp == 0 || opponent.Pokemon.CurrentHp == 0) return Fail(null, "wild_battle_entry_fainted");
        if (playerParty.Append(opponent).Any(m => WildBattleSourceFieldEffects.IsValidToxic(m.Status) &&
            m.Status != WildBattleSourceFieldEffects.ResetToxicCounter(m.Status))) return Fail(null, "source_wild_entry_status");
        if (scriptedSnorlax && (!SnorlaxBattleContract.SourceInitialInstance(opponent.Pokemon) || opponent.Status != 0 || opponent.HeldItem != "ITEM_NONE"))
            return Fail(null, "snorlax_scripted_identity");
        if (towerKind == PokemonTowerBattleKind.RevealedMarowak && (!SourceRevealedMarowak(opponent.Pokemon) || opponent.HeldItem != "ITEM_NONE" ||
            !opponent.Pokemon.Moves.Select(m => m.Move.Value).SequenceEqual(new[] { "pret:MOVE_HEADBUTT", "pret:MOVE_LEER", "pret:MOVE_FOCUS_ENERGY", "pret:MOVE_BONEMERANG" })))
            return Fail(null, "pokemon_tower_revealed_marowak_identity");
        if (towerKind == PokemonTowerBattleKind.OrdinaryWild) towerKind = null;
        if (oldRodFishing && (opponent.Pokemon.SourceSpeciesId != 129 || opponent.Pokemon.Level is < 5 or > 10))
            return Fail(null, "wild_battle_old_rod_context_unsupported");
        if ((profile is null || owned is not null) && !oldRodFishing && !scriptedSnorlax && towerKind is not (PokemonTowerBattleKind.DisguisedMarowak or PokemonTowerBattleKind.RevealedMarowak) &&
            !(firstSeviiWild ? Game.Domain.Encounters.FirstSeviiWildPokemonFactory.WildSpecies.Contains(opponent.Pokemon.Species) : owned?.SupportsOrdinaryWildOpponent(opponent.Pokemon) ?? FuchsiaBattleScope.SupportsWildOpponent(opponent.Pokemon.SourceSpeciesId))) return Fail(null, "wild_battle_opponent_species_unsupported");
        var player = playerParty[activeIndex] with { FirstTurnCounter = 2 };
        opponent = opponent with { FirstTurnCounter = 2 };
        WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
        var state = new WildBattleState(player, opponent, 0, 0, 0, rng, 0, WildBattleOutcome.Ongoing, RulesVersion: FuchsiaRulesVersion)
        { Terrain = terrain, TowerBattleKind = towerKind, ScriptedSnorlax = scriptedSnorlax, SourceWild = new(),
            SourceMechanicsRulesVersion = profile?.Version, SourceCampaignRulesVersion = owned?.RulesVersion,
            CalledParty = new(playerParty.Select(Member).ToImmutableArray(), [Member(opponent)], activeIndex, 0) };
        if (playerParty.Any(m => WildBattleSourceFieldEffects.IsValidToxic(m.Status)) || WildBattleSourceFieldEffects.IsValidToxic(opponent.Status))
            state = state with { CalledField = new() };
        if (ValidateSourceWild(catalog, state) is { } error) return Fail(null, error);
        return Execute(null, state, context =>
        {
            context.sourceHeldCatalog = catalog;
            for (int slot = 0; slot < 4; slot++) context.Draw($"wild-battle.entry.ai-setup.slot-{slot}", 16);
            // The mechanics profile starts a supplied battle fixture, not a campaign encounter.
            // Its explicit held item is preserved; only real campaign entry rolls wild loot.
            if ((profile is null || owned is not null) && towerKind != PokemonTowerBattleKind.RevealedMarowak)
            {
                int roll = context.Draw("wild-battle.held-item", 100) % 100;
                string item = opponent.Pokemon.SourceSpeciesId switch
                {
                    _ when firstSeviiWild => Game.Domain.Encounters.FirstSeviiWildPokemonFactory.HeldItem(opponent.Pokemon.Species,roll),
                    _ when owned?.RulesVersion == CinnabarOwnedPokemonCatalog.Profile &&
                        Game.Domain.Encounters.CinnabarWildPokemonFactory.WildSpecies.Contains(opponent.Pokemon.Species) =>
                        Game.Domain.Encounters.CinnabarWildPokemonFactory.HeldItem(opponent.Pokemon.Species,roll),
                    132 => roll >= 95 ? "ITEM_METAL_POWDER" : "ITEM_NONE",
                    _ when scriptedSnorlax => SnorlaxBattleContract.WildHeldItem(roll),
                    93 or 104 or 105 => PokemonTowerBattleContract.WildHeldItem(opponent.Pokemon.SourceSpeciesId, roll),
                    58 => roll is >= 45 and <= 94 ? "ITEM_RAWST_BERRY" : "ITEM_NONE",
                    95 => roll >= 95 ? "ITEM_HARD_STONE" : "ITEM_NONE",
                    52 => roll >= 95 ? "ITEM_NUGGET" : "ITEM_NONE",
                    _ => MtMoonBattleData.WildHeldItem(opponent.Pokemon.SourceSpeciesId, roll)
                };
                context.State = context.State with { Opponent = context.State.Opponent with { HeldItem = item } };
            }
            context.Add(n => new WildBattleStartedEvent(n, 0));
            var first = context.Order(WildBattleOrderPhase.Entry);
            context.SourceWeatherInitialEntry(first);
            context.ApplyRoute3EntryAbility(WildBattleSide.Player, weather: false);
            context.ApplyRoute3EntryAbility(WildBattleSide.Opponent, weather: false);
            context.SourceTraceResolvePending();
            context.SourceCanonicalHeldItem(first, SourceBerryPhase.SwitchIn);
            context.SourceCanonicalHeldItem(Other(first), SourceBerryPhase.SwitchIn);
            context.Ready(false);
        });
    }

    public static string? ValidateSourceWild(PokemonCatalog catalog, WildBattleState state)
    {
        if (SourceBattleAdmission.ValidateRulesProfile(catalog, state) is { } profileError) return profileError;
        if (!SourceBattleAdmission.IsCanonicalSourceState(catalog, state) && !SourceBattleAdmission.IsCurrentCampaignCatalog(catalog))
            return "source_wild_catalog";
        if (state.SourceWild?.RulesVersion != SourceWildRulesVersion || state.RulesVersion != FuchsiaRulesVersion)
            return "source_wild_version";
        if (state.CalledParty is not { BattleKind: WildBattleCalledBattleKind.Wild, OpponentActiveIndex: 0 } party || party.Opponent.Length != 1)
            return "source_wild_actual_parties_required";
        if (state.AwaitingReplacement != (party.PendingSwitch?.Kind == WildBattleCalledSwitchKind.FaintedReplacement))
            return "source_wild_replacement_phase";
        if (ValidateCalledMoveActionState(catalog, state with { AwaitingReplacement = false }) is { } error) return error;
        var turn = state.SourceWild.Turn;
        if (turn is null) return party.PendingSwitch is not null ? "source_wild_missing_turn" : null;
        if (state.Outcome != WildBattleOutcome.Ongoing || !Enum.IsDefined(turn.First) || turn.PlayerSlot is < -1 or > 3 ||
            turn.OpponentSlot is < -1 or > 3 || turn.NextAction is < 0 or > 2) return "source_wild_turn";
        if (party.PendingSwitch is null) return "source_wild_unpaused_turn";
        if (turn.EndTurn is { } end && (turn.NextAction != 2 || end.Turn != state.Turn ||
            end.RulesVersion != SourceEndTurnRulesVersion || !Enum.IsDefined(end.First) || !Enum.IsDefined(end.Stage) ||
            end.NextBattler is < 0 or > 2 || end.MinimumRevision < 0 || end.MinimumRevision > state.Revision ||
            end.MinimumEventSequence < 0 || end.MinimumEventSequence > state.EventSequence)) return "source_wild_end_turn";
        return null;
    }

    private static WildBattleResult ResolveSourceWildMove(PokemonCatalog catalog, WildBattleState state, int playerSlot)
    {
        var error = ValidateAction(catalog, state) ?? ValidateSourceMoveSelection(catalog, state, WildBattleSide.Player, playerSlot);
        if (error is not null) return Fail(state, error);
        return Execute(state, state, context =>
        {
            int opponentSlot = context.ChooseSourceWildOpponent(catalog);
            context.State = context.State with { ForestPlayerMoveSlot = playerSlot,
                ForestPlayerLastMoveSlot = playerSlot < 0 ? state.ForestPlayerLastMoveSlot : playerSlot };
            var first = context.SourceActionsOrder(catalog);
            context.PrepareMtMoonTurn(catalog, playerSlot, opponentSlot);
            context.SetSourceWildTurn(new(first, playerSlot, opponentSlot));
            context.ResumeSourceWildMoves(catalog);
        });
    }

    /// <summary>Baton Pass is a mandatory player selection. Resumes the already selected opposing action.</summary>
    public static WildBattleResult ChooseSourceWildParty(PokemonCatalog catalog, WildBattleState state, int partyIndex)
    {
        var error = IsSourceWild(state) ? ValidateSourceWild(catalog, state) : "source_wild_version";
        var pending = state.CalledParty?.PendingSwitch;
        if (error is null && (pending is not { Side: WildBattleSide.Player, Kind: WildBattleCalledSwitchKind.BatonPass } ||
            state.SourceWild?.Turn is null)) error = "source_wild_baton_phase";
        if (error is null && !pending!.LegalPartyIndices.Contains(partyIndex)) error = "source_wild_party_choice";
        if (error is not null) return Fail(state, error);
        return Execute(state, state, context =>
        {
            context.SourceCalledPartySwitch(catalog, WildBattleSide.Player, partyIndex, pending!.Kind, pending.Invocation);
            if (!context.SourceWildPauseForReplacement()) context.ResumeSourceWildMoves(catalog);
            else context.SourceWildCommitRevision();
        });
    }

    internal sealed partial class Execution
    {
        internal void SetSourceWildTurn(WildBattleSourceWildTurn? turn) => State = State with { SourceWild = State.SourceWild! with { Turn = turn } };
        internal void SourceWildCommitRevision() => State = State with { Revision = checked(initialRevision + 1) };
        internal void ConfigureSourceWild()
        {
            if (!IsSourceWild(State)) return;
            CalledPartySwitchCommitted = (side, kind) =>
            {
                if (side != WildBattleSide.Player) throw new UnsupportedAction("source_wild_opponent_switch");
                // The called switch event carries stable party indices; ordinary UI retains its established event.
                var switched = Events.OfType<WildBattleCalledPartySwitchedEvent>().Last();
                var prior = State.CalledParty!.Player[switched.OutgoingIndex];
                var previous = new WildBattleCombatant(prior.Pokemon, HeldItem: prior.HeldItem, Status: prior.Status);
                if (kind == WildBattleCalledSwitchKind.FaintedReplacement)
                    Add(n => new WildBattlePlayerReplacedEvent(n, State.Turn, previous, State.Player));
                else Add(n => new WildBattlePlayerSwitchedEvent(n, State.Turn, previous, State.Player));
            };
        }
        internal int ChooseSourceWildOpponent(PokemonCatalog catalog)
        {
            SourceCalledSynchronizeParties();
            var mon = State.Opponent;
            int slot;
            if ((mon.CalledTurnState?.Lock?.PpSlot ?? ForcedMoveSlot(mon)) is { } forced) slot = forced;
            else if (ValidateSourceMoveSelection(catalog, State, WildBattleSide.Opponent, StruggleSlot) is null) slot = StruggleSlot;
            else if (mon.EncoreMoveSlot is { } encore) slot = encore;
            else
            {
                do slot = Draw("wild-battle.opponent.choose-slot", 4) & 3;
                while (slot >= mon.Pokemon.Moves.Length || !IsCalledMoveSelectable(catalog, mon, slot) ||
                    IsImprisoned(mon, State.Player, mon.Pokemon.Moves[slot].Move));
            }
            State = State with { ForestOpponentMoveSlot = slot, ForestOpponentLastMoveSlot = slot < 0 ? State.ForestOpponentLastMoveSlot : slot };
            return slot;
        }
        internal void ExecuteSourceWildMove(PokemonCatalog catalog, WildBattleSide side, int slot)
        {
            SourceCalledSynchronizeParties();
            if (State.Outcome != WildBattleOutcome.Ongoing || Get(side).Pokemon.CurrentHp == 0) return;
            // A queued move may be redirected by Encore applied by the preceding move.
            if (slot >= 0 && Get(side).CalledTurnState?.Lock is null && ForcedMoveSlot(Get(side)) is null && Get(side).EncoreMoveSlot is { } encore)
            {
                slot = encore;
                if (SourceMoveRestrictionRules.EncoreOverwritten(Get(side))) ChangeEncore(side, encore, 0, "move-changed");
            }
            var turn = State.SourceWild!.Turn;
            int order = turn is null ? 1 : turn.First == side ? 0 : 1;
            var actionContext = new WildBattleCalledActionContext(order, WildBattleSourcePartyEffects.ActiveIndex(State.CalledParty!, side));
            if (Get(side).CalledTurnState?.Lock is not null) ExecuteCalledMoveContinuation(catalog, side, actionContext);
            else if (slot == StruggleSlot) ExecuteSourceForcedStruggle(catalog, side, actionContext);
            else ExecuteSourceMoveAction(catalog, new(side, slot) { Context = actionContext }, catalog.Moves[Get(side).Pokemon.Moves[slot].Move]);
            SourceCalledSynchronizeParties();
        }
        internal void ResumeSourceWildMoves(PokemonCatalog catalog)
        {
            // Resumed Baton or a completed item/switch action precedes the next queued move.
            if (State.SourceWild!.Turn is { NextAction: > 0 }) SourceHeldActionFinished();
            while (State.SourceWild!.Turn is { NextAction: < 2 } turn)
            {
                var side = turn.NextAction == 0 ? turn.First : Other(turn.First);
                SetSourceWildTurn(turn with { NextAction = turn.NextAction + 1 });
                bool attempted = State.Outcome == WildBattleOutcome.Ongoing && Get(side).Pokemon.CurrentHp > 0;
                ExecuteSourceWildMove(catalog, side, side == WildBattleSide.Player ? turn.PlayerSlot : turn.OpponentSlot);
                if (SourceWildPauseForReplacement()) { SourceWildCommitRevision(); return; }
                if (attempted) SourceHeldActionFinished();
                if (State.Outcome != WildBattleOutcome.Ongoing || State.Player.Pokemon.CurrentHp == 0 || State.Opponent.Pokemon.CurrentHp == 0) break;
            }
            FinishSourceWildTurn(catalog);
        }
        internal bool SourceWildPauseForReplacement()
        {
            SourceCalledSynchronizeParties();
            if (State.Outcome is WildBattleOutcome.Caught or WildBattleOutcome.Escaped) return false;
            var party = State.CalledParty!;
            if (!WildBattleSourcePartyEffects.HasLivingMember(party, WildBattleSide.Player) || State.Opponent.Pokemon.CurrentHp == 0) return false;
            if (party.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass) return true;
            if (State.Player.Pokemon.CurrentHp > 0) return false;
            if (party.PendingSwitch is null)
            {
                var pending = new WildBattleCalledPendingSwitch(WildBattleSide.Player, WildBattleCalledSwitchKind.FaintedReplacement,
                    WildBattleSourcePartyEffects.LegalReserveIndices(party, WildBattleSide.Player));
                State = State with { CalledParty = party with { PendingSwitch = pending } };
                Add(n => new WildBattleCalledPartySwitchRequiredEvent(n, State.Turn, pending));
            }
            if (!State.AwaitingReplacement)
            {
                State = State with { AwaitingReplacement = true };
                Add(n => new WildBattleReplacementRequiredEvent(n, State.Turn));
            }
            if (State.SourceWild!.Turn is { } turn) SetSourceWildTurn(turn with { NextAction = 2 });
            else SetSourceWildTurn(new(WildBattleSide.Player, State.ForestPlayerMoveSlot ?? StruggleSlot, State.ForestOpponentMoveSlot ?? StruggleSlot, 2));
            return true;
        }
        internal void FinishSourceWildTurn(PokemonCatalog catalog)
        {
            if (State.Outcome is WildBattleOutcome.Caught or WildBattleOutcome.Escaped)
            { CompleteSourceWildTurn(); return; }
            if (SourceWildPauseForReplacement()) { SourceWildCommitRevision(); return; }
            var turn = State.SourceWild!.Turn ?? new(WildBattleSide.Player, State.ForestPlayerMoveSlot ?? StruggleSlot, State.ForestOpponentMoveSlot ?? StruggleSlot, 2);
            SetSourceWildTurn(turn with { NextAction = 2 });
            while (State.SourceWild!.Turn!.EndTurn?.Boundary != WildBattleSourceEndTurnBoundary.Complete)
            {
                SourceCalledSynchronizeParties();
                var party = State.CalledParty!;
                bool playerLives = WildBattleSourcePartyEffects.HasLivingMember(party, WildBattleSide.Player);
                State = State with { Outcome = (WildBattleOutcome)((State.Opponent.Pokemon.CurrentHp == 0 ? 1 : 0) | (!playerLives ? 2 : 0)),
                    AwaitingReplacement = false, CalledParty = party with { PendingSwitch = null } };
                var cursor = AdvanceSourceEndTurn(catalog, State.SourceWild.Turn!.EndTurn);
                SetSourceWildTurn(State.SourceWild.Turn with { EndTurn = cursor });
                if (SourceWildPauseForReplacement()) { SourceWildCommitRevision(); return; }
            }
            CompleteSourceWildTurn();
        }
        private void CompleteSourceWildTurn()
        {
            SourceCalledSynchronizeParties();
            var party = State.CalledParty!;
            var outcome = State.Outcome;
            if (outcome is not (WildBattleOutcome.Caught or WildBattleOutcome.Escaped))
                outcome = (WildBattleOutcome)((State.Opponent.Pokemon.CurrentHp == 0 ? 1 : 0) |
                    (!WildBattleSourcePartyEffects.HasLivingMember(party, WildBattleSide.Player) ? 2 : 0));
            State = State with { Outcome = outcome, AwaitingReplacement = false, ForestPlayerMoveSlot = null, ForestOpponentMoveSlot = null,
                ForestResidualsFinished = false, CalledParty = party with { PendingSwitch = null } };
            SetSourceWildTurn(null);
            if (outcome == WildBattleOutcome.Ongoing) Ready(true);
            else if (!Events.OfType<WildBattleEndedEvent>().Any(e => e.Outcome == outcome)) Add(n => new WildBattleEndedEvent(n, State.Turn, outcome));
            SourceWildCommitRevision();
        }
    }
}
