using System.Collections.Immutable;
using System.Globalization;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignFieldPoisonPhase { FaintedPokemon, WhiteOutMessage, MusicFade, ScreenFade }
public sealed record CampaignFieldPoison(int NextPartyIndex, CampaignFieldPoisonPhase Phase);
public enum FieldPoisonEffect { None, Poisoned, Fainted }
public sealed record FieldPoisonStep(int Counter, ImmutableArray<PartyPokemon> Party, FieldPoisonEffect Effect);

/// <summary>
/// Source field poison rules from pret/pokefirered 037335f4.
/// Canonical Toxic is opt-in for the Fuchsia Gym successor; field damage is always one HP.
/// </summary>
public static class CampaignFieldPoisonRules
{
    public const string CounterVariable = "VAR_POISON_STEP_COUNTER";
    public const uint Poison = 8;
    public static FieldPoisonStep Walk(int counter, ImmutableArray<PartyPokemon> party, bool allowSleep=false, bool allowFreeze=false, bool allowToxic=false)
    {
        if (counter is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(counter));
        ValidateParty(party,allowSleep,allowFreeze,allowToxic);
        counter = (counter + 1) % 5;
        if (counter != 0) return new(counter, party, FieldPoisonEffect.None);
        bool poisoned = false, fainted = false;
        for (int i = 0; i < party.Length; i++)
        {
            var mon = party[i];
            if (!IsUsableSpecies(mon) || !SourcePartyStatusRules.IsPoison(mon.Status,allowToxic)) continue;
            int hp = Math.Max(0, mon.Pokemon.CurrentHp - 1);
            poisoned = true;
            fainted |= hp == 0;
            party = party.SetItem(i, mon with { Pokemon = mon.Pokemon with { CurrentHp = hp } });
        }
        return new(counter, party, fainted ? FieldPoisonEffect.Fainted : poisoned ? FieldPoisonEffect.Poisoned : FieldPoisonEffect.None);
    }
    public static bool IsPoisonFaint(PartyPokemon mon, bool allowToxic=false) => IsUsableSpecies(mon) && mon.Pokemon.CurrentHp == 0 && SourcePartyStatusRules.IsPoison(mon.Status,allowToxic);
    public static bool HasUsablePokemon(ImmutableArray<PartyPokemon> party, bool allowSleep=false, bool allowFreeze=false, bool allowToxic=false)
    {
        ValidateParty(party,allowSleep,allowFreeze,allowToxic);
        return party.Any(mon => IsUsableSpecies(mon) && mon.Pokemon.CurrentHp > 0);
    }
    public static PartyPokemon ResolveFaint(PartyPokemon mon, bool allowToxic=false)
    {
        if (!IsPoisonFaint(mon,allowToxic)) throw new ArgumentException("field_poison_faint_required", nameof(mon));
        int friendship = mon.Pokemon.Friendship;
        if (friendship is < 0 or > 255) throw new ArgumentException("field_poison_friendship_invalid", nameof(mon));
        return mon with { Status = 0, Pokemon = mon.Pokemon with { Friendship = Math.Max(0, friendship - (friendship >= 200 ? 10 : 5)) } };
    }
    private static bool IsUsableSpecies(PartyPokemon mon) => !mon.IsEgg && mon.Pokemon.SourceSpeciesId > 0;
    private static void ValidateParty(ImmutableArray<PartyPokemon> party,bool allowSleep,bool allowFreeze,bool allowToxic)
    {
        if (party.IsDefault || party.Length > 6) throw new ArgumentException("field_poison_party_invalid", nameof(party));
        foreach (var mon in party)
        {
            if (mon is null || mon.Pokemon is null || mon.Pokemon.CurrentHp < 0 || mon.Pokemon.CurrentHp > mon.Pokemon.Stats.MaxHp)
                throw new ArgumentException("field_poison_party_invalid", nameof(party));
            if (!SourcePartyStatusRules.IsSupported(mon.Status,allowSleep,allowFreeze,allowToxic)) throw new NotSupportedException("field_poison_status_unsupported");
        }
    }
}

public sealed partial class CampaignSession
{
    private string? ValidateFieldPoisonState(CampaignState state)
    {
        if (state.FieldPoison is not null) return "save_event_active";
        int counter = state.Script.Variables.GetValueOrDefault(CampaignFieldPoisonRules.CounterVariable);
        return counter is < 0 or >= 5 ? "save_poison_counter_invalid" : null;
    }

    // Call only after an actual, unpreempted walking step and AFTER walking friendship.
    // A Fainted result owns the field command before encounters are considered.
    private CampaignState ApplyFieldPoisonWalkingStep(CampaignState state)
    {
        if (!_content.ForestEnabled) return state;
        if (state.FieldPoison is not null || !state.Script.Completed || state.Script.Locked || state.Script.Wait is not null || !state.Script.Stack.IsEmpty || state.Dialogue is not null)
            throw new InvalidDataException("field_poison_step_event_active");
        var step = CampaignFieldPoisonRules.Walk(state.Script.Variables.GetValueOrDefault(CampaignFieldPoisonRules.CounterVariable), state.Party,_content.EarlyStarterEnabled,_content.Route11Enabled,_content.FuchsiaGymEnabled);
        state = state with { Party = step.Party, Script = state.Script with { Variables = state.Script.Variables.SetItem(CampaignFieldPoisonRules.CounterVariable, step.Counter) } };
        if (step.Effect == FieldPoisonEffect.None) return state;
        // Source FldEffPoison_Start is asynchronous; surviving poison does not stop walking or encounters.
        state = EmitFieldPoisonEffect(state, "fieldpoison", []);
        if (step.Effect != FieldPoisonEffect.Fainted) return state;
        if (!_content.TryConstant("NPC_TEXT_COLOR_NEUTRAL", out var neutral)) throw new InvalidDataException("field_poison_text_color_missing");
        state = state with
        {
            FieldPoison = new(0, CampaignFieldPoisonPhase.FaintedPokemon), TextColor = "NPC_TEXT_COLOR_NEUTRAL",
            Script = state.Script with
            {
                Locked = true, Completed = false,
                Variables = state.Script.Variables.SetItem("VAR_PREV_TEXT_COLOR", state.Script.Variables.GetValueOrDefault("VAR_TEXT_COLOR")).SetItem("VAR_TEXT_COLOR", neutral)
            }
        };
        return AdvanceFieldPoisonFaints(state);
    }

    // Source Task_BattleStart clears this at battle admission, not when returning to the field.
    private CampaignState ResetFieldPoisonCounterForBattle(CampaignState state) => !_content.ForestEnabled ? state :
        state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(CampaignFieldPoisonRules.CounterVariable, 0) } };

    private Decision<CampaignState> DecideFieldPoison(CampaignState state, CampaignCommand command)
    {
        if (command.Kind != "ack") return new(CommandStatus.Rejected, state, "field_poison_event_active");
        if (state.FieldPoison is not { } poison || state.Script.Wait is not { } wait || !state.Script.Locked || state.Script.Completed)
            return new(CommandStatus.Fault, state, "field_poison_wait_invalid");
        if (wait.Kind == StoryWaitKind.Dialog && state.Dialogue is { } dialogue && dialogue.Page + 1 < dialogue.Pages.Length)
            return new(CommandStatus.Accepted, state with { Dialogue = dialogue with { Page = dialogue.Page + 1 } });
        var next = state with { Dialogue = null, PresentationEffects = [], PresentationTicks = 0, Script = state.Script with { Wait = null } };
        switch (poison.Phase)
        {
            case CampaignFieldPoisonPhase.FaintedPokemon:
                return new(CommandStatus.Accepted, AdvanceFieldPoisonFaints(next));
            case CampaignFieldPoisonPhase.WhiteOutMessage:
                next = next with { FieldPoison = poison with { Phase = CampaignFieldPoisonPhase.MusicFade } };
                return new(CommandStatus.Accepted, SetFieldPoisonWait(next, "special", ["Script_FadeOutMapMusic"], StoryWaitKind.Presentation));
            case CampaignFieldPoisonPhase.MusicFade:
                next = next with { FieldPoison = poison with { Phase = CampaignFieldPoisonPhase.ScreenFade }, ScreenIsBlack = true, PresentationTicks = 16 };
                return new(CommandStatus.Accepted, SetFieldPoisonWait(next, "fadescreen", ["FADE_TO_BLACK"], StoryWaitKind.Presentation));
            case CampaignFieldPoisonPhase.ScreenFade:
                next = FinishFieldPoison(next) with { ScreenIsBlack = false };
                return new(CommandStatus.Accepted, WhiteOutFromRoute1(next));
            default:
                return new(CommandStatus.Fault, state, "field_poison_phase_invalid");
        }
    }

    private CampaignState AdvanceFieldPoisonFaints(CampaignState state)
    {
        var poison = state.FieldPoison ?? throw new InvalidDataException("field_poison_not_pending");
        for (int i = poison.NextPartyIndex; i < state.Party.Length; i++)
        {
            var mon = state.Party[i];
            if (!CampaignFieldPoisonRules.IsPoisonFaint(mon,_content.FuchsiaGymEnabled)) continue;
            // Task_TryFieldPoisonWhiteOut changes this one slot before its own message,
            // and waits for dismissal before advancing to the next party slot.
            state = state with
            {
                Party = state.Party.SetItem(i, CampaignFieldPoisonRules.ResolveFaint(mon,_content.FuchsiaGymEnabled)),
                FieldPoison = poison with { NextPartyIndex = i + 1 },
                TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", mon.Nickname)
            };
            return ShowFieldPoisonMessage(state, "gText_PkmnFainted3");
        }
        bool whiteout = !CampaignFieldPoisonRules.HasUsablePokemon(state.Party,_content.EarlyStarterEnabled,_content.Route11Enabled,_content.FuchsiaGymEnabled);
        state = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", whiteout ? 1 : 0) } };
        if (!whiteout) return FinishFieldPoison(state);
        // This profile has no badge yet; the shared respawn routine enforces the same restriction.
        if (HasUnimplementedBadge(state))
            throw new InvalidDataException("whiteout_respawn_profile_not_supported");
        int lost = WhiteOutLoss(state);
        state = state with
        {
            FieldPoison = poison with { Phase = CampaignFieldPoisonPhase.WhiteOutMessage },
            TextBuffers = state.Money > 0 ? state.TextBuffers.SetItem("STR_VAR_1", lost.ToString(CultureInfo.InvariantCulture)) : state.TextBuffers,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.Money > 0 ? 1 : 0) }
        };
        // The script only buffers the loss here. Deduction/healing happens in DoWhiteOut.
        return ShowFieldPoisonMessage(state, state.Money > 0 ? "Text_WhitedOutLostMoney" : "Text_WhitedOut");
    }

    private CampaignState ShowFieldPoisonMessage(CampaignState state, string symbol)
    {
        var pages = _content.TextPages(symbol);
        if (pages.IsDefaultOrEmpty) throw new InvalidDataException("field_poison_text_missing: " + symbol);
        // gText_PkmnFainted3 ends with \p$: the final terminator is not another visible page.
        if (symbol == "gText_PkmnFainted3" && pages.Length > 1 && pages[^1].Length == 0) pages = pages.RemoveAt(pages.Length - 1);
        state = state with { Dialogue = new(symbol, pages, 0, false, true) };
        return SetFieldPoisonWait(state, "message", [symbol], StoryWaitKind.Dialog);
    }
    private static CampaignState FinishFieldPoison(CampaignState state) => state with
    {
        FieldPoison = null, Dialogue = null, PresentationTicks = 0, PresentationEffects = [],
        Script = state.Script with { Wait = null, Locked = false, Completed = true }
    };
    private static CampaignState EmitFieldPoisonEffect(CampaignState state, string opcode, ImmutableArray<string> args)
    {
        long sequence = checked(state.Script.EffectSequence + 1);
        return state with
        {
            Script = state.Script with { EffectSequence = sequence },
            PresentationEffects = state.PresentationEffects.Add(new(sequence, opcode, state.World.Player.MapId, args))
        };
    }
    private static CampaignState SetFieldPoisonWait(CampaignState state, string opcode, ImmutableArray<string> args, StoryWaitKind kind)
    {
        state = EmitFieldPoisonEffect(state, opcode, args);
        return state with { Script = state.Script with { Revision = checked(state.Script.Revision + 1), Wait = new(kind, "field-poison:" + state.Script.EffectSequence) } };
    }
}
