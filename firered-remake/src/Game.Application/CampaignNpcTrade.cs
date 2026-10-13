using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Application;

public enum CampaignNpcTradePhase { ChoosingParty, Selected, Prepared, Presenting }
/// <summary>Transient field callback ownership. Receipts retain completed exchanges separately.</summary>
public sealed record CampaignNpcTrade(int TradeId, CampaignNpcTradePhase Phase, int PartyIndex = -1,
    long TargetInstanceId = 0, PartyPokemon? Outgoing = null, PartyPokemon? Incoming = null,
    RngState? RngBefore = null, RngState? RngAfter = null);

public sealed partial class CampaignSession
{
    private const string NpcTradeSharedSource = "data/event_scripts.s";
    // This is a readable functional presentation duration, not source trade-scene frame parity.
    public const int NpcTradePresentationTicks = 360;

    private NpcTradePokemonDefinition? NpcTradeDefinition(CampaignState state)
    {
        if (_content.NpcTrades is not {} catalog || state.LastTalked is not {} id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.Invisible ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId) return null;
        return catalog.Definitions.Values.SingleOrDefault(d => catalog.IsEnabled(d.TradeId) &&
            d.Map == state.World.Player.MapId && d.LocalId == actor.SourceLocalId && d.ScriptLabel == actor.ScriptId);
    }
    private bool NpcTradeSource(NpcTradePokemonDefinition definition, StorySource source, params string[] shared) =>
        source.Path == definition.ScriptSource.Path && source.Symbol == definition.ScriptLabel ||
        source.Path == NpcTradeSharedSource && shared.Contains(source.Symbol, StringComparer.Ordinal);

    private bool NpcTradeWait(CampaignState state, StoryWaitKind kind)
    {
        if (NpcTradeDefinition(state) is not {} definition || state.Script.Wait?.Kind != kind || state.Script.Stack.IsEmpty) return false;
        var frame = state.Script.Stack[^1]; var program = _content.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length &&
            program.Instructions[index] is { Op: StoryOp.WaitState, Opcode: "waitstate" } instruction &&
            instruction.Args.IsEmpty && NpcTradeSource(definition, instruction.Source,
                kind == StoryWaitKind.PartySelection ? "EventScript_ChooseMonForInGameTrade" : "EventScript_DoInGameTrade");
    }

    private bool TryDecideNpcTrade(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        decision = new(CommandStatus.Rejected, state, "npc_trade_not_pending");
        bool choice = command.Kind is "npc_trade_choose" or "npc_trade_cancel";
        if (state.NpcTrade is not {} pending) return choice;
        if (pending.Phase != CampaignNpcTradePhase.ChoosingParty) return choice;
        if (!NpcTradeWait(state, StoryWaitKind.PartySelection)) return true;
        if (!choice) { decision = new(CommandStatus.Rejected, state, "npc_trade_selection_required"); return true; }
        int index = 7; long instance = 0;
        if (command.Kind == "npc_trade_choose")
        {
            if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length)
            { decision = new(CommandStatus.Rejected, state, "npc_trade_party_index_invalid"); return true; }
            var mon = state.Party[command.PartyIndex];
            if (command.TargetInstanceId != mon.InstanceId)
            { decision = new(CommandStatus.Rejected, state, "npc_trade_target_changed"); return true; }
            index = command.PartyIndex; instance = mon.InstanceId;
        }
        var next = state with
        {
            NpcTrade = pending with { Phase = CampaignNpcTradePhase.Selected, PartyIndex = index, TargetInstanceId = instance },
            Dialogue = null, Motions = [], PresentationEffects = [], PresentationTicks = 0, ScreenIsBlack = false,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", index) }
        };
        decision = new(CommandStatus.Accepted, Pump(Advance(next,
            StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
        return true;
    }

    private PartyPokemon SelectedNpcTradePokemon(CampaignState state, NpcTradePokemonDefinition definition)
    {
        if (state.NpcTrade is not { Phase: CampaignNpcTradePhase.Selected } pending || pending.TradeId != definition.TradeId ||
            pending.PartyIndex < 0 || pending.PartyIndex >= state.Party.Length ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8005", -1) != pending.PartyIndex ||
            state.Party[pending.PartyIndex].InstanceId != pending.TargetInstanceId)
            throw new InvalidDataException("npc_trade_selection_context");
        return state.Party[pending.PartyIndex];
    }

    private bool TryApplyNpcTradeEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (NpcTradeDefinition(state) is not {} definition) return false;
        var a = effect.Args;
        if (effect.Opcode == "waitstate" && state.NpcTrade is {} waiting)
        {
            string label = waiting.Phase == CampaignNpcTradePhase.ChoosingParty ? "EventScript_ChooseMonForInGameTrade" : "EventScript_DoInGameTrade";
            if (!a.IsEmpty || !NpcTradeSource(definition, effect.Source, label) ||
                state.Script.Wait?.Kind != StoryWaitKind.Presentation || waiting.TradeId != definition.TradeId ||
                waiting.Phase is not (CampaignNpcTradePhase.ChoosingParty or CampaignNpcTradePhase.Presenting))
                throw new InvalidDataException("npc_trade_wait_context");
            next = state with { Dialogue = null, Script = state.Script with { Wait = state.Script.Wait with {
                Kind = waiting.Phase == CampaignNpcTradePhase.ChoosingParty ? StoryWaitKind.PartySelection : StoryWaitKind.Presentation } } };
            return true;
        }
        if (effect.Opcode is not ("special" or "specialvar")) return false;
        int functionIndex = effect.Opcode == "special" ? 0 : 1;
        if (a.Length <= functionIndex) return false;
        string function = a[functionIndex];
        if (function is not ("GetInGameTradeSpeciesInfo" or "ChoosePartyMon" or "GetTradeSpecies" or "CreateInGameTradePokemon" or "DoInGameTradeScene")) return false;
        string shared = function switch {
            "GetInGameTradeSpeciesInfo" => "EventScript_GetInGameTradeSpeciesInfo",
            "ChoosePartyMon" => "EventScript_ChooseMonForInGameTrade",
            "GetTradeSpecies" => "EventScript_GetInGameTradeSpecies",
            _ => "EventScript_DoInGameTrade" };
        bool returns = function is "GetInGameTradeSpeciesInfo" or "GetTradeSpecies";
        if (!NpcTradeSource(definition, effect.Source, shared) ||
            a.Length != (returns ? 2 : 1) || effect.Opcode != (returns ? "specialvar" : "special") || returns && a[0] != "VAR_RESULT" ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8008", -1) != definition.TradeId)
            throw new InvalidDataException("npc_trade_effect_context");
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        var content = _content.PokemonContent!;
        switch (function)
        {
            case "GetInGameTradeSpeciesInfo":
                if (state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != definition.TradeId)
                    throw new InvalidDataException("npc_trade_offer_context");
                int requested = content.Catalog.Species[definition.RequestedSpecies].SourceSpeciesId;
                int received = content.Catalog.Species[definition.ReceivedSpecies].SourceSpeciesId;
                next = Result(requested) with { TextBuffers = state.TextBuffers
                    .SetItem("STR_VAR_1", content.SpeciesNames[requested]).SetItem("STR_VAR_2", content.SpeciesNames[received]) };
                break;
            case "ChoosePartyMon":
                if (state.NpcTrade is not null || state.Script.Flags.GetValueOrDefault(definition.CompletionFlag) ||
                    state.Party.IsDefaultOrEmpty || state.Party.Length > 6) throw new InvalidDataException("npc_trade_chooser_context");
                next = state with { NpcTrade = new(definition.TradeId, CampaignNpcTradePhase.ChoosingParty) };
                break;
            case "GetTradeSpecies":
                var selected = SelectedNpcTradePokemon(state, definition);
                next = Result(selected.IsEgg ? 0 : selected.Pokemon.SourceSpeciesId);
                break;
            case "CreateInGameTradePokemon":
                var outgoing = SelectedNpcTradePokemon(state, definition);
                if (outgoing.IsEgg || outgoing.Pokemon.Species != definition.RequestedSpecies ||
                    state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1) != definition.TradeId ||
                    state.Script.Flags.GetValueOrDefault(definition.CompletionFlag) ||
                    (state.NpcTrades ?? []).Any(t => t.TradeId == definition.TradeId))
                    throw new InvalidDataException("npc_trade_creation_context");
                _ = checked(state.NextPokemonId + 1);
                var creation = NpcTradePokemonFactory.Create(_content.NpcTrades!, content, definition.TradeId,
                    outgoing.Pokemon.Level, state.NextPokemonId, state.World.Rng);
                next = state with { World = state.World with { Rng = creation.Rng }, NpcTrade = state.NpcTrade! with {
                    Phase = CampaignNpcTradePhase.Prepared, Outgoing = outgoing, Incoming = creation.Pokemon,
                    RngBefore = state.World.Rng, RngAfter = creation.Rng } };
                break;
            case "DoInGameTradeScene":
                if (state.NpcTrade is not { Phase: CampaignNpcTradePhase.Prepared, Outgoing: {} sent, Incoming: {} incoming,
                    RngBefore: {} before, RngAfter: {} after } prepared || prepared.TradeId != definition.TradeId ||
                    prepared.PartyIndex < 0 || prepared.PartyIndex >= state.Party.Length ||
                    state.Party[prepared.PartyIndex] != sent || incoming.InstanceId != state.NextPokemonId || state.World.Rng != after)
                    throw new InvalidDataException("npc_trade_swap_context");
                next = state with { Party = state.Party.SetItem(prepared.PartyIndex, incoming),
                    NextPokemonId = checked(state.NextPokemonId + 1),
                    NpcTrades = (state.NpcTrades ?? []).Add(new(definition.TradeId, prepared.PartyIndex, sent, incoming, before, after)),
                    SeenSpecies = state.SeenSpecies.Add(incoming.Pokemon.SourceSpeciesId),
                    CaughtSpecies = state.CaughtSpecies.Add(incoming.Pokemon.SourceSpeciesId),
                    NpcTrade = prepared with { Phase = CampaignNpcTradePhase.Presenting },
                    Dialogue = null, ScreenIsBlack = false, PresentationTicks = NpcTradePresentationTicks };
                break;
        }
        return true;
    }

    private CampaignState CompleteNpcTradePresentation(CampaignState before, CampaignState next)
    {
        if (before.NpcTrade is not { Phase: CampaignNpcTradePhase.Presenting, Incoming: {} incoming } pending) return next;
        if (!NpcTradeWait(before, StoryWaitKind.Presentation) || before.Party[pending.PartyIndex].InstanceId != incoming.InstanceId ||
            before.NpcTrades is not { Length: > 0 } receipts || receipts[^1].Incoming.InstanceId != incoming.InstanceId)
            throw new InvalidDataException("npc_trade_presentation_context");
        // BufferInGameTradeMonName runs after the source swap and therefore reads the received nickname.
        return next with { NpcTrade = null, TextBuffers = next.TextBuffers
            .SetItem("STR_VAR_1", incoming.Nickname).SetItem("STR_VAR_2", _content.PokemonContent!.SpeciesNames[incoming.Pokemon.SourceSpeciesId]) };
    }
}
