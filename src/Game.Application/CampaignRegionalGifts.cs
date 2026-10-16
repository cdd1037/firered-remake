using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed record CampaignEeveeGift(long InstanceId, uint Personality, SixStats Ivs);

public sealed partial class CampaignSession
{
    private const string EeveeMap = "map.celadon-city-condominiums-roof-room";
    private const string EeveeSource = "data/maps/CeladonCity_Condominiums_RoofRoom/scripts.inc";
    private const string EeveeBallScript = "CeladonCity_Condominiums_RoofRoom_EventScript_EeveeBall";

    private string? ValidateGiftIdentityUniqueness(CampaignState state)
    {
        var gifts = new List<long>();
        foreach (var prize in state.PrizePokemon ?? []) gifts.Add(prize.InstanceId);
        if (state.MagikarpGift is { } magikarp) gifts.Add(magikarp.InstanceId);
        if (state.EeveeGift is { } eevee) gifts.Add(eevee.InstanceId);
        if (state.LaprasGift is { } lapras) gifts.Add(lapras.InstanceId);
        if (state.DojoGift is { } dojo) gifts.Add(dojo.InstanceId);
        foreach (var fossil in state.FossilRevivals ?? []) gifts.Add(fossil.InstanceId);
        if (gifts.Distinct().Count() != gifts.Count || gifts.Contains(1) ||
            state.Exploration?.Captured is { } captures && !captures.IsDefault &&
            captures.Any(c => c is not null && gifts.Contains(c.InstanceId))) return "save_gift_identity_conflict";
        return null;
    }
    private bool InEeveeScript(CampaignState state) => state.World.Player.MapId.Value == EeveeMap &&
        state.LastTalked is { } actor && state.Actors.TryGetValue(actor, out var value) && value.ScriptId == EeveeBallScript;

    private string? ValidateEeveeGift(CampaignState state)
    {
        bool got = state.Script.Flags.GetValueOrDefault("FLAG_GOT_EEVEE"), hidden = state.Script.Flags.GetValueOrDefault("FLAG_HIDE_EEVEE_BALL");
        if ((state.EeveeGift is not null) != got || hidden != got) return "save_eevee_flag_inconsistent";
        if (state.EeveeGift is not { } gift) return null;
        if (gift.InstanceId < 2 || gift.InstanceId >= state.NextPokemonId || gift.Ivs is null || gift.Ivs.Values.Any(v => v is < 0 or > 31)) return "save_eevee_origin_invalid";
        var matching = OwnedPokemon(state).Where(p => p.InstanceId == gift.InstanceId).ToArray();
        if (matching.Length > 1) return "save_eevee_instance_mismatch";
        if (!state.CaughtSpecies.Contains(133) || !state.SeenSpecies.Contains(133)) return "save_eevee_instance_mismatch";
        if (matching.Length == 1)
        {
            var mon = matching[0];
            // Evolution validation uses the catalog's admitted evolution ledger; this method opens none itself.
            if (!EvolutionSpeciesMatchesOrigin(state, mon, 133) || mon.Pokemon.Personality != gift.Personality || mon.Pokemon.Ivs != gift.Ivs ||
                mon.MetLocation != "MAPSEC_CELADON_CITY" || mon.MetLevel != 25 || mon.Ball != "ITEM_POKE_BALL" ||
                !state.CaughtSpecies.Contains(133) || !state.SeenSpecies.Contains(133) ||
                state.Exploration?.Captured is not { } captures || captures.IsDefault || captures.Any(c => c is null || c.InstanceId == gift.InstanceId) ||
                state.MagikarpGift?.InstanceId == gift.InstanceId) return "save_eevee_instance_mismatch";
        }
        return null;
    }

    private bool TryApplyRegionalGiftEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "givemon" || effect.Args.IsDefaultOrEmpty ||
            effect.Args[0] is not ("SPECIES_EEVEE" or "133")) return false;
        if (!InEeveeScript(state) || effect.Source.Path != EeveeSource || effect.Source.Symbol != EeveeBallScript ||
            !effect.Args.SequenceEqual(new[] { "SPECIES_EEVEE", "25" }) || state.EeveeGift is not null ||
            state.Script.Flags.GetValueOrDefault("FLAG_GOT_EEVEE") || state.Script.Flags.GetValueOrDefault("FLAG_HIDE_EEVEE_BALL"))
            throw new InvalidDataException("eevee_gift_context_invalid");
        var result = GiveScriptGift(state, 133, 25, "MAPSEC_CELADON_CITY");
        next = result.State;
        if (result.Gift is { } mon) next = next with { EeveeGift = new(mon.InstanceId, mon.Pokemon.Personality, mon.Pokemon.Ivs) };
        return true;
    }

    /// <summary>Shared ScriptGiveMon construction, RNG, storage routing and dex effects for source gifts.</summary>
    private (CampaignState State, PartyPokemon? Gift) GiveScriptGift(CampaignState state, int sourceSpecies, int level, string met)
    {
        if (state.GiftDelivery is not null || state.NextPokemonId is < 2 or long.MaxValue || state.Storage is null)
            throw new InvalidDataException("script_gift_delivery_context_invalid");
        var species = RequireSpecies(sourceSpecies);
        // Source constructs before GiveMonToPlayer: PID low/high then two packed IV draws even on full storage.
        var created = PokemonFactory.Create(_content.PokemonContent!.Catalog,
            new(species.Definition.Id, level, state.TrainerId, IvPolicy.Random, PersonalityPolicy.Random), state.World.Rng);
        var owned = new PartyPokemon(state.NextPokemonId, created.Pokemon, species.Name, state.PlayerName, state.PlayerGender,
            2, met, level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        var receive = PokemonStorageRules.Receive(_content.PokemonContent, state.Party, state.Storage, owned);
        var next = state with { World = state.World with { Rng = created.Rng } };
        if (!receive.Succeeded)
        {
            if (receive.Diagnostic?.Code != "storage_full") throw new InvalidDataException(receive.Diagnostic?.Code);
            return (next with { Script = next.Script with { Variables = next.Script.Variables.SetItem("VAR_RESULT", 2) } }, null);
        }
        int previous = state.Script.Variables.GetValueOrDefault("VAR_PC_BOX_TO_SEND_MON");
        var variables = next.Script.Variables.SetItem("VAR_RESULT", receive.Address is null ? 0 : 1);
        var flags = next.Script.Flags;
        if (receive.Address is { } address)
        {
            if (previous != address.BoxIndex) flags = flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE", false);
            variables = variables.SetItem("VAR_PC_BOX_TO_SEND_MON", address.BoxIndex);
        }
        next = next with { Party = receive.Party, Storage = receive.Storage, NextPokemonId = checked(state.NextPokemonId + 1),
            GiftDelivery = new(receive.Address, previous), SeenSpecies = next.SeenSpecies.Add(sourceSpecies),
            CaughtSpecies = next.CaughtSpecies.Add(sourceSpecies), Script = next.Script with { Variables = variables, Flags = flags } };
        return (next, owned);
    }

    /// <summary>The authenticated source gift currently owned by the shared nickname workflow.</summary>
    public long? PendingGiftInstanceId => State.GiftDelivery is null ? null : PendingScriptGiftInstanceId(State);

    private long? PendingScriptGiftInstanceId(CampaignState state) => InGameCornerPrizeScript(state) ? PendingPrizeGiftInstanceId(state) : InFossilLabScript(state) ? PendingFossilGiftInstanceId(state) : InDojoGiftScript(state) ? state.DojoGift?.InstanceId : InLaprasScript(state) ? state.LaprasGift?.InstanceId : InEeveeScript(state) ? state.EeveeGift?.InstanceId :
        InMagikarpScript(state) ? state.MagikarpGift?.InstanceId : null;

    private bool TryApplySharedScriptGiftEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state; var a = effect.Args;
        bool handles = effect.Opcode is "getpartysize" or "bufferboxname" or "subvar" ||
            effect.Opcode == "special" && a[0] == "ChangeBoxPokemonNickname" ||
            effect.Opcode == "specialvar" && a[1] is "ShouldShowBoxWasFullMessage" or "GetPCBoxToSendMon";
        if (!handles || !(InMagikarpScript(state) || InEeveeScript(state) || InLaprasScript(state) || InDojoGiftScript(state) || InFossilLabScript(state) || InGameCornerPrizeScript(state))) return false;
        if (state.GiftDelivery is null || PendingScriptGiftInstanceId(state) is not { } instanceId)
            throw new InvalidDataException("script_gift_pending_missing");
        int Value(string token) => int.TryParse(token, out int n) ? n : state.Script.Variables.TryGetValue(token, out int v) ? v :
            _content.TryConstant(token, out int c) ? c : throw new InvalidDataException("script_gift_operand_unknown: " + token);
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        switch (effect.Opcode)
        {
            case "getpartysize": next = Result(state.Party.Length); break;
            case "subvar":
                if (a[0] != "VAR_RESULT" || Value(a[1]) != 1 || state.GiftDelivery.BoxAddress is not null || state.Party.IsEmpty ||
                    state.Party[^1].InstanceId != instanceId) throw new InvalidDataException("script_gift_party_slot_context");
                next = Result((state.Script.Variables.GetValueOrDefault("VAR_RESULT") - 1) & 65535); break;
            case "bufferboxname":
                int index = Value(a[1]);
                if (a[0] is not ("STR_VAR_1" or "STR_VAR_3") || index is < 0 or >= PokemonStorageState.BoxCount)
                    throw new InvalidDataException("script_gift_box_name_invalid");
                next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], "BOX" + (index + 1)) }; break;
            case "specialvar":
                if (a[0] != "VAR_RESULT" || state.GiftDelivery is not { BoxAddress: { } address } pending)
                    throw new InvalidDataException("script_gift_pc_delivery_missing");
                if (a[1] == "GetPCBoxToSendMon") next = Result(pending.PreviousBox);
                else
                {
                    bool full = state.Storage!.CurrentBox != address.BoxIndex && !state.Script.Flags.GetValueOrDefault("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE");
                    next = Result(full ? 1 : 0);
                    if (full) next = next with { Script = next.Script with { Flags = next.Script.Flags.SetItem("FLAG_SHOWN_BOX_WAS_FULL_MESSAGE", true) } };
                }
                break;
            case "special":
                if (state.GiftDelivery.BoxAddress is not { } slot || state.Storage!.Boxes[slot.BoxIndex].Slots[slot.SlotIndex] is not { } mon ||
                    mon.InstanceId != instanceId) throw new InvalidDataException("script_gift_box_nickname_context");
                next = state with { Nickname = new(-1, mon.Nickname, false) { BoxAddress = slot },
                    TextBuffers = state.TextBuffers.SetItem("STR_VAR_3", mon.Nickname).SetItem("STR_VAR_2", mon.Nickname) }; break;
        }
        return true;
    }

    private Decision<CampaignState> SubmitBoxGiftNickname(CampaignState state, CampaignNickname naming, StoryWait wait, string name)
    {
        if (state.GiftDelivery?.BoxAddress != naming.BoxAddress || state.Storage is not { } storage || PendingScriptGiftInstanceId(state) is not { } instanceId)
            return new(CommandStatus.Rejected, state, "gift_box_nickname_not_pending");
        var address = naming.BoxAddress!; var box = storage.Boxes[address.BoxIndex]; var mon = box.Slots[address.SlotIndex];
        if (mon is null || mon.InstanceId != instanceId) return new(CommandStatus.Rejected, state, "gift_box_nickname_instance_mismatch");
        return new(CommandStatus.Accepted, state with { Storage = storage with { Boxes = storage.Boxes.SetItem(address.BoxIndex,
            box with { Slots = box.Slots.SetItem(address.SlotIndex, mon with { Nickname = name }) }) }, Nickname = naming with { Submitted = true },
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", name), ScreenIsBlack = false, PresentationTicks = 16,
            Script = state.Script with { Wait = wait with { Kind = StoryWaitKind.Presentation } } });
    }
}
