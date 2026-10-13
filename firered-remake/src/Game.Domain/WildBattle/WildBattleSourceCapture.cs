using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>
/// External ordinary-capture facts, supplied by the authoritative world/Pokédex owner.
/// CaughtSpecies uses canonical species identities, not internal species IDs mistaken for National
/// Dex numbers. The caller maps its caught flags to species. IsUnderwater is the source map type;
/// battle terrain, fishing and a battler using Dive do not imply this flag.
/// </summary>
public sealed record WildBattleCaptureContext(bool IsUnderwater, ImmutableHashSet<SpeciesId> CaughtSpecies,
    CaptureEncounterKind Encounter = CaptureEncounterKind.OrdinaryWild);

/// <summary>
/// A capture does not generate a replacement Pokémon. These delivery values are the exact persistent
/// enemy party member after the successful throw, including its HP, PP, identity, status and held item.
/// The caller remains responsible for inventory, capture metadata, Pokédex and party/PC delivery.
/// </summary>
public sealed record WildBattleCaptureActionResult(WildBattleResult Battle, PokemonInstance? CapturedPokemon,
    uint? CapturedStatus, string? CapturedHeldItem)
{
    public bool Succeeded => Battle.Succeeded;
}

public static partial class WildBattleRules
{
    /// <summary>
    /// Authenticated source ordinary-wild capture. This overload does not enlarge historical regional
    /// ResolveBall contracts or admit Safari, story, trainer, link or double battle modes.
    /// The explicit Celio origin alone admits Roamer capture with its source result receipt.
    /// The source battle's own Turn is the saturating completed-turn counter used by Timer Ball.
    /// </summary>
    public static WildBattleCaptureActionResult ResolveBall(SourceMechanicsCatalog mechanics,
        WildBattleState state, CaptureBall ball, WildBattleCaptureContext captureContext)
    {
        WildBattleCaptureActionResult Reject(string code) => new(Fail(state, code), null, null, null);
        if (mechanics is null || !SourceBattleAdmission.IsCanonicalSourceState(mechanics.Catalog, state) || !IsSourceWild(state))
            return Reject("source_capture_authenticated_wild_required");
        if (captureContext is null || captureContext.CaughtSpecies is null ||
            captureContext.CaughtSpecies.Any(s => !mechanics.Catalog.Species.ContainsKey(s)))
            return Reject("source_capture_context");
        if (!Enum.IsDefined(captureContext.Encounter)) return Reject("source_capture_encounter_kind");
        if (captureContext.Encounter != (state.SourceWild?.CelioRoamer is not null ? CaptureEncounterKind.Roamer : CaptureEncounterKind.OrdinaryWild) || state.ScriptedSnorlax || state.TowerBattleKind is not null)
            return Reject("source_capture_encounter_unsupported");
        if (!Enum.IsDefined(ball) || ball == CaptureBall.Safari) return Reject("source_capture_ball_unsupported");
        var catalog = mechanics.Catalog;
        var error = ValidateAction(catalog, state) ?? ValidateRoute3NonMoveAction(state);
        if (error is not null) return Reject(error);
        var result = Execute(state, state, context =>
        {
            int slot = context.ChooseSourceWildOpponent(catalog);
            context.PrepareMtMoonTurn(catalog, StruggleSlot, slot);
            context.SetSourceWildTurn(new(WildBattleSide.Player, StruggleSlot, slot, 1));
            context.PlayerFirst();
            SourceBattleItemRules.BeginItem(context);
            context.Add(n => new WildBattleBallThrownEvent(n, state.Turn,
                WildBattleSide.Player, WildBattleSide.Opponent, ball));
            var target = context.State.Opponent;
            var pokemon = target.Pokemon;
            var status = WildBattleSourceFieldEffects.IsValidToxic(target.Status)
                ? CaptureStatus.BadPoison : ForestCaptureStatus(target.Status);
            var ballContext = new CaptureBallContext(pokemon.Level,
                WildBattleEffective.HasType(catalog, target, "TYPE_WATER") || WildBattleEffective.HasType(catalog, target, "TYPE_BUG"),
                captureContext.IsUnderwater, captureContext.CaughtSpecies.Contains(pokemon.Species), context.State.Turn);
            var capture = CaptureRules.Resolve(new(pokemon.Stats.MaxHp, pokemon.CurrentHp,
                catalog.Species[pokemon.Species].CatchRate, ball, status), context.State.Rng, ballContext);
            if (capture.Outcome is not (CaptureOutcome.Caught or CaptureOutcome.Escaped))
                throw new UnsupportedAction("source_capture_" + capture.Diagnostic);
            context.State = context.State with { Rng = capture.Rng };
            context.Trace.AddRange(capture.RandomTrace);
            context.Add(n => new WildBattleCaptureResultEvent(n, state.Turn, ball, capture.Outcome,
                capture.Branch!.Value, capture.Calculation!, capture.ChecksPassed, capture.AnimationCode!.Value));
            if (capture.Outcome == CaptureOutcome.Caught)
            {
                context.State = context.State with { Outcome = WildBattleOutcome.Caught };
                context.FinishSourceWildTurn(catalog);
            }
            else context.ResumeSourceWildMoves(catalog);
        });
        if (!result.Succeeded || result.State!.Outcome != WildBattleOutcome.Caught)
            return new(result, null, null, null);
        var captured = result.State.CalledParty!.Opponent[0];
        return new(result, captured.Pokemon, captured.Status, WildBattleSourcePartyEffects.PersistentHeldItem(captured));
    }
}
