using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Actual four-slot storage and KO ownership for the common residual commands.</summary>
internal sealed partial class FrameEndTurnContext(SourceBattleFrame frame) : SourceEndTurnContext
{
    internal SourceBattleFrame Frame { get; private set; } = frame;
    private ImmutableArray<SourceBattleParty> reportedParties = frame.Parties;
    internal readonly ImmutableArray<SourceBattleEvent>.Builder Events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
    internal readonly ImmutableArray<RandomTrace>.Builder Trace = ImmutableArray.CreateBuilder<RandomTrace>();
    internal override int Turn => Frame.Turn;
    internal override long Revision => Frame.Revision;
    internal override long EventSequence => Frame.EventSequence;
    internal override int BattlerCount => Frame.BattlerCount;
    internal override bool Ongoing => Frame.Outcome == WildBattleOutcome.Ongoing && Frame.FourTurn?.OutcomeAfterRewards is null;
    internal override bool AdvancedAbilitiesEnabled => true;
    internal override bool ToxicEnabled => true;
    internal override bool RainTimerEnabled => true;
    internal override WildBattleCombatant Get(BattlerSlot slot) => Frame.Get(slot).Body ?? throw new SourceRecipeUnsupported("source_end_turn_absent_body");
    internal override void Set(BattlerSlot slot, WildBattleCombatant body)
    {
        var cell = Frame.Get(slot); var key = cell.Member!.Value; var party = Frame.Party(key.Side);
        var member = party.Members[key.Index] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(body),
            Status = WildBattleTransformationRules.PersistentStatus(body), HeldItem = body.HeldItem, KnockedOffItem = body.KnockedOffItem };
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Body = body }),
            Parties = Frame.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, member) }) };
    }
    internal override int SureHitTurnsRemaining(BattlerSlot slot) => Frame.Get(slot).Registers.Turn?.SureHitTurnsRemaining ?? 0;
    internal override void SetSureHitTurnsRemaining(BattlerSlot slot, int remaining)
    {
        var cell = Frame.Get(slot); var turn = cell.Registers.Turn!;
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Registers = cell.Registers with {
            Turn = turn.Lock is null && remaining == 0 ? null : turn with {
                SureHitTurnsRemaining = remaining, SureHitSource = remaining == 0 ? null : turn.SureHitSource } } }) };
    }
    internal override void ClearBindingSource(BattlerSlot slot)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Registers = cell.Registers with { WrappedBy = null } }) };
    }
    internal override BattlerSlot? LeechSeedSource(BattlerSlot target) => Frame.Get(target).Registers.LeechSeedSource;
    internal override bool IsAbsent(BattlerSlot slot) => Frame.IsSourceAbsent(slot);
    internal override bool WeatherSuppressed => SourceWeatherRules.IsSuppressed(Frame);
    internal override bool HasWeather(string weather) => weather switch {
        "rain" => SourceWeatherRules.HasRain(Frame.Field), "sandstorm" => SourceWeatherRules.HasSandstorm(Frame.Field),
        "sun" => SourceWeatherRules.HasSun(Frame.Field), "hail" => Frame.Field.HailTurnsRemaining > 0,
        _ => throw new SourceRecipeUnsupported("source_end_turn_weather") };
    internal override bool WeatherDamageEnabled(BattlerSlot slot, string weather) => true;
    internal override bool UnderGroundOrWater(BattlerSlot slot) => SourceChargeRules.SemiInvulnerable(
        Frame.Get(slot).Registers.Turn?.Lock is {Kind:WildBattleCalledLockKind.Charge} charge?charge.Move:null)
        is WildBattleSemiInvulnerable.Underground or WildBattleSemiInvulnerable.Underwater;
    internal override int GetTimer(BattleSide side, string effect)
    {
        var s = Frame.Sides[(int)side]; var f = Frame.Field;
        return effect switch {
            "reflect" => s.ReflectTurnsRemaining, "light-screen" => s.LightScreenTurnsRemaining,
            "mist" => s.MistTurnsRemaining, "safeguard" => s.SafeguardTurnsRemaining,
            "rain" => f.RainTurnsRemaining, "sandstorm" => f.SandstormTurnsRemaining,
            "sun" => f.SunTurnsRemaining, "hail" => f.HailTurnsRemaining,
            _ => throw new SourceRecipeUnsupported("source_end_turn_field") };
    }
    internal override void SetTimer(BattleSide side, string effect, int value)
    {
        var s = Frame.Sides[(int)side]; var f = Frame.Field;
        Frame = effect switch {
            "reflect" => Frame with { Sides = Frame.Sides.SetItem((int)side, s with { ReflectTurnsRemaining = value }) },
            "light-screen" => Frame with { Sides = Frame.Sides.SetItem((int)side, s with { LightScreenTurnsRemaining = value }) },
            "mist" => Frame with { Sides = Frame.Sides.SetItem((int)side, s with { MistTurnsRemaining = value }) },
            "safeguard" => Frame with { Sides = Frame.Sides.SetItem((int)side, s with { SafeguardTurnsRemaining = value }) },
            "rain" => Frame with { Field = f with { RainTurnsRemaining = value } },
            "sandstorm" => Frame with { Field = f with { SandstormTurnsRemaining = value } },
            "sun" => Frame with { Field = f with { SunTurnsRemaining = value } },
            "hail" => Frame with { Field = f with { HailTurnsRemaining = value } },
            _ => throw new SourceRecipeUnsupported("source_end_turn_field") };
    }
    internal override void Add(Func<long, SourceBattleEvent> create)
    {
        var published = SourceBattleEventHooks.Publish(Frame, (n, _) => create(n)); Frame = published.State; Events.Add(published.Event);
    }
    internal override ushort Draw(string purpose, int bound)
    {
        var next = Gen3Rng.Next(Frame.Rng, purpose, bound); Frame = Frame with { Rng = next.State }; Trace.Add(next.Trace); return next.Value;
    }
    internal override string Purpose(BattlerSlot slot, string effect) => $"source-battle.slot-{slot.Value}.{effect}";
    internal override void RecordAbility(BattlerSlot slot, string ability) { } // Events remain available to the future AI owner.
    internal override void ChangeStatus(BattlerSlot slot, uint status, string reason)
    {
        var before = Get(slot);
        if (before.Nightmare && !WildBattleStatus.IsSleeping(status))
            Add(n => new SourceTowerVolatileEvent(n, Turn, slot, "nightmare", false, reason));
        Set(slot, before with { Status = status, Nightmare = WildBattleStatus.IsSleeping(status) && before.Nightmare, SourcePartyStatusOverride = null });
        if (before.Status != status) Add(n => new SourceStatusChangedEvent(n, Turn, slot, before.Status, status, reason));
    }
    internal override void Faint(BattlerSlot slot)
    {
        var occupant = Frame.Get(slot).Occupant!.Value;
        if (Get(slot).Pokemon.CurrentHp != 0 || Frame.FourTurn!.Defeats.Any(d => d.Occupant == occupant))
            throw new SourceRecipeUnsupported("source_end_turn_faint_contract");
        Frame = SourceBattleIdentityRules.ClearDepartureReferences(Frame, slot, SourceDepartureKind.Faint);
        Set(slot, SourceFaintCommands.ClearCanonicalBody(Get(slot)));
        ChangeStatus(slot, WildBattleStatus.None, "faint");
        Add(n => new SourceFaintedEvent(n, Turn, slot));
        Frame = Frame with { DeferredKoVacancyMask = (byte)(Frame.DeferredKoVacancyMask | slot.Mask),
            FourTurn = Frame.FourTurn! with { Defeats = Frame.FourTurn.Defeats.Add(new(occupant, Frame.EventSequence, slot.Side == BattleSide.Opponent)) } };
    }
    internal override void DamageOutcome() { } // The scheduler checks exhaustion after the complete recipe command.
    private void SynchronizePartyEvents()
    {
        foreach (var cell in Frame.Slots.Take(BattlerCount))
        {
            var key = cell.Member!.Value;
            var before = reportedParties[(int)key.Side].Members[key.Index]; var after = Frame.Party(key.Side).Members[key.Index];
            if (!WildBattleSourcePartyEffects.SameMember(before, after))
                Add(n => new SourceCalledPartyChangedEvent(n, Turn, key.Side, key.Index, "active-sync", before, after));
        }
        reportedParties = Frame.Parties;
    }
    internal override void CheckTeamsLost()
    {
        SynchronizePartyEvents();
        bool p = Frame.Party(BattleSide.Player).Members.Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0);
        bool o = Frame.Party(BattleSide.Opponent).Members.Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0);
        if (p && o) return;
        var outcome = !p && !o ? WildBattleOutcome.Draw : p ? WildBattleOutcome.Won : WildBattleOutcome.Lost;
        if (SourceFourActionTurns.HasPendingRewards(Frame))
            Frame = Frame with { FourTurn = Frame.FourTurn! with { OutcomeAfterRewards = outcome } };
        else if (Frame.Outcome != outcome)
        {
            Frame = Frame with { Outcome = outcome };
            Add(n => new SourceEndedEvent(n, Turn, outcome));
        }
    }
    internal override void Prepare()
    {
        // TurnValuesCleanUp(TRUE) resets Follow Me before residuals, retaining Helping Hand.
        Frame = Frame with { Sides = Frame.Sides.Select(s => s with { FollowMeTimer = 0, FollowMeTarget = null }).ToImmutableArray() };
        foreach (var cell in Frame.Slots.Take(BattlerCount))
        {
            // HandleEndTurn_ContinueBattle cancels a sleeping charge before weather.
            if (WildBattleStatus.IsSleeping(cell.Body!.Status) && cell.Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge)
            {
                var turn = cell.Registers.Turn;
                Frame = Frame with { Slots = Frame.Slots.SetItem(cell.Slot.Value, cell with { Registers = cell.Registers with {
                    Turn = turn.SureHitTurnsRemaining == 0 ? null : turn with { Lock = null } } }) };
                Add(n => new SourceMtMoonVolatileEvent(n, Turn, cell.Slot, "called-charge", charge.TurnsRemaining, 0, "sleep"));
            }
            if (WildBattleStatus.IsSleeping(Get(cell.Slot).Status) && RampageRemaining(cell.Slot) != 0)
                SetRampageRemaining(cell.Slot, 0, "sleep");
            Set(cell.Slot, SourceTurnBoundaryRules.PrepareBody(Get(cell.Slot)));
        }
    }
    internal override void ExposeFaintedActions() => CheckTeamsLost();
    internal override void FaintedActionItems()
    {
        if (Frame.Slots.Any(c => !SourceFrameBerryItems.SupportsHeldItem(c.Body!.HeldItem)))
            throw new SourceRecipeUnsupported("source_four_action_fainted_item_phase_not_migrated");
        var settled = SourceFourEntryAbilities.Settle(Frame);
        Frame = settled.State; Events.AddRange(settled.Events); Trace.AddRange(settled.Trace);
    }
    internal override void Wish(PokemonCatalog catalog, BattlerSlot slot)
    {
        if (Frame.Get(slot).Position.Wish is not null) throw new SourceRecipeUnsupported("source_four_action_wish_not_migrated");
    }
    internal override void UnmigratedBattlerStep(SourceBattlerEndTurnStep step, BattlerSlot slot)
    {
        if (step == SourceBattlerEndTurnStep.Thrash) { RampageResidual(slot); return; }
        if (step == SourceBattlerEndTurnStep.Encore) { EncoreResidual(slot); return; }
        if (step == SourceBattlerEndTurnStep.Disable) { Disable(slot); return; }
        if (step is SourceBattlerEndTurnStep.Items1 or SourceBattlerEndTurnStep.Items2)
        {
            var berries = SourceFrameBerryItems.Apply(Frame, step == SourceBattlerEndTurnStep.Items1
                ? SourceBerryPhase.Normal : SourceBerryPhase.NormalMoveTurn, slot);
            Frame = berries.State; Events.AddRange(berries.Events); return;
        }
        var m = Get(slot); var r = Frame.Get(slot).Registers;
        bool nonempty = step switch {
            SourceBattlerEndTurnStep.Uproar or SourceBattlerEndTurnStep.Thrash => r.Turn?.Lock is { Kind: not (WildBattleCalledLockKind.Recharge or WildBattleCalledLockKind.Rollout or WildBattleCalledLockKind.Charge or WildBattleCalledLockKind.Rampage) } || m.RampageTurnsRemaining != 0,
            SourceBattlerEndTurnStep.Disable => m.DisabledMove is not null,
            SourceBattlerEndTurnStep.Encore => m.EncoreMoveSlot is not null,
            SourceBattlerEndTurnStep.Charge => m.ChargeTurnsRemaining != 0,
            SourceBattlerEndTurnStep.Taunt => m.CalledVolatile?.TauntTurnsRemaining > 0,
            _ => throw new SourceRecipeUnsupported("source_end_turn_phase") };
        if (nonempty) throw new SourceRecipeUnsupported("source_four_action_residual_phase_not_migrated");
    }
    internal override bool Future(PokemonCatalog catalog, BattlerSlot slot)
    {
        // HandleWishPerishSongOnTurnEnd skips source-absent positions before decrementing.
        if (!Ongoing || IsAbsent(slot) || Frame.Get(slot).Position.FutureAttack is not { } future) return false;
        int after = future.TurnsRemaining - 1;
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with {
            FutureAttack = after == 0 ? null : future with { TurnsRemaining = after } } }) };
        Add(n => new SourceCalledDelayedEvent(n, Turn, slot, future.Move, future.TurnsRemaining, after,
            after == 0 ? "resolved" : "countdown"));
        if (after != 0 || Get(slot).Pokemon.CurrentHp == 0) return false;
        var hit = new FrameRecipeContext(catalog, Frame,
            new(Frame.Get(future.Attacker).Occupant!.Value, Frame.Revision, 0, slot), catalog.Moves[future.Move]);
        hit.RunFutureHit(future, slot);
        Frame = hit.Frame; Events.AddRange(hit.Events); Trace.AddRange(hit.Trace);
        if (Get(slot).Pokemon.CurrentHp == 0) Faint(slot);
        int priorEvents = hit.Events.Count, priorTrace = hit.Trace.Count;
        hit.CompleteFutureHitItems(Frame);
        Frame = hit.Frame; Events.AddRange(hit.Events.Skip(priorEvents)); Trace.AddRange(hit.Trace.Skip(priorTrace));
        return true;
    }
    internal override bool Perish(BattlerSlot slot)
    {
        if (Get(slot).CalledVolatile?.PerishSongTimer is not null) throw new SourceRecipeUnsupported("source_four_action_perish_not_migrated");
        return false;
    }
    internal override void Cleanup()
    {
        // Final cleanup retains one slot traversal: damage lifetime and the source recharge timer.
        foreach (var prior in Frame.Slots.Take(BattlerCount))
        {
            var cell = prior with { Registers = prior.Registers with { Damage = null, HelpingHand = false } };
            Frame = Frame with { Slots = Frame.Slots.SetItem(cell.Slot.Value, cell) };
            if (WildBattleStatus.IsSleeping(cell.Body!.Status) && cell.Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Rollout } rollout)
            {
                Frame = Frame with { Slots = Frame.Slots.SetItem(cell.Slot.Value, cell with { Registers = cell.Registers with { Turn = null } }) };
                Add(n => new SourceMtMoonVolatileEvent(n, Turn, cell.Slot, "called-rollout", rollout.TurnsRemaining, 0, "sleep"));
            }
            if (cell.Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Recharge } recharge)
            {
                int after = SourceTurnBoundaryRules.RechargeAfterReady(recharge.TurnsRemaining);
                var turn = cell.Registers.Turn;
                Frame = Frame with { Slots = Frame.Slots.SetItem(cell.Slot.Value, cell with { Registers = cell.Registers with {
                    Turn = after == 0 && turn.SureHitTurnsRemaining == 0 ? null : turn with { Lock = after == 0 ? null : recharge with { TurnsRemaining = after } } } }) };
                Add(n => new SourceMtMoonVolatileEvent(n, Turn, cell.Slot, "called-recharge", recharge.TurnsRemaining, after, "recharge-timer"));
            }
        }
        SynchronizePartyEvents();
    }
}
