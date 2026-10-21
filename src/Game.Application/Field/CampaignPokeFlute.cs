using Game.Domain;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public enum CampaignPokeFlutePhase { PlayedText, Fanfare }
/// <summary>Transient, unsaveable source bag task. The music's complete MIDI length is not its wait.</summary>
public sealed record CampaignPokeFluteEffect(CampaignPokeFlutePhase Phase, string WaitId);

public sealed partial class CampaignSession
{

    private string? ValidatePokeFluteCommand(CampaignState state, CampaignCommand command)
    {
        if (command.Text != PokemonTowerItemRules.PokeFluteId.Value) return "poke_flute_item_invalid";
        var error = ValidateCampaignInventory(state.Inventory);
        if (error is not null) return error;
        return OpeningInventoryRules.Quantity(state.Inventory!.KeyItems ?? [], PokemonTowerItemRules.PokeFluteId) == 1
            ? null : "poke_flute_item_missing";
    }

    private Decision<CampaignState> UseFieldPokeFlute(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        var error = ValidatePokeFluteCommand(state, command);
        if (error is not null) return No(error);
        if (state.ItemMenu is not { Origin: "bag", View: "bag" } menu || state.PokeFlute is not null ||
            !IsStableFieldForParty(state with { ItemMenu = null }) || state.Fishing is not null ||
            state.FlashEffect is not null || state.CutEffect is not null || state.Repel is not null ||
            state.Pc is not null || state.TownMap is not null || state.FieldEffectArguments is not null)
            return No("field_poke_flute_context_invalid");
        var use = PokeFluteRules.UseField(ShopCatalog, _content.PokemonContent!, state.Inventory!, state.Party);
        if (!use.Succeeded) return No(use.Error!);
        // Resolve all required pages before any status is committed.
        _ = _content.TextPages(PokeFluteRules.FieldNoEffectMessage);
        _ = _content.TextPages(PokeFluteRules.FieldPlayedMessage);
        _ = _content.TextPages(PokeFluteRules.FieldAwakenedMessage);
        if (!use.WokeSomeone)
            return new(CommandStatus.Accepted, state with { ItemMenu = menu with
                { View = "message", MessageSymbol = PokeFluteRules.FieldNoEffectMessage, ReturnView = "bag" } });
        long sequence = checked(state.Script.EffectSequence + 1);
        return new(CommandStatus.Accepted, state with
        {
            Party = use.Party,
            PokeFlute = new(CampaignPokeFlutePhase.PlayedText, "poke-flute-field:" + sequence),
            Script = state.Script with { EffectSequence = sequence },
            ItemMenu = menu with { View = "message", MessageSymbol = PokeFluteRules.FieldPlayedMessage, ReturnView = "bag" }
        });
    }

    private Decision<CampaignState> DecidePokeFlute(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        var flute = state.PokeFlute!;
        if (command.Text != flute.WaitId) return No("poke_flute_callback_mismatch");
        if (state.ItemMenu is not { Origin: "bag", View: "message", MessageSymbol: PokeFluteRules.FieldPlayedMessage } menu)
            throw new InvalidDataException("poke_flute_task_context_invalid");
        if (flute.Phase == CampaignPokeFlutePhase.PlayedText)
        {
            if (command.Kind != "poke_flute_text_presented") return No("poke_flute_text_pending");
            return new(CommandStatus.Accepted, state with
            {
                PokeFlute = flute with { Phase = CampaignPokeFlutePhase.Fanfare },
                PresentationTicks = PokeFluteRules.SourceFanfareFrames,
                PresentationEffects = state.PresentationEffects.Add(new(state.Script.EffectSequence,
                    "playfanfare", state.World.Player.MapId, [PokeFluteRules.Fanfare]))
            });
        }
        if (flute.Phase != CampaignPokeFlutePhase.Fanfare || command.Kind != "poke_flute_presented")
            return No("poke_flute_fanfare_pending");
        return new(CommandStatus.Accepted, state with
        {
            PokeFlute = null, PresentationTicks = 0,
            PresentationEffects = state.PresentationEffects.RemoveAll(e => e.Sequence == state.Script.EffectSequence &&
                e.Opcode == "playfanfare" && e.Args.SequenceEqual(new[] { PokeFluteRules.Fanfare })),
            ItemMenu = menu with { MessageSymbol = PokeFluteRules.FieldAwakenedMessage }
        });
    }

    private Decision<CampaignState> DecideWildPokeFlute(CampaignState state, CampaignCommand command)
    {
        var error = ValidatePokeFluteCommand(state, command);
        if (error is not null) return new(CommandStatus.Rejected, state, error);
        var active=state.WildBattle!.ActivePartyIndex;
        return DecideWildMedicine(state,command with{Kind="battle_medicine",PartyIndex=active,
            TargetInstanceId=state.Party[active].InstanceId,MoveSlot=-1});
    }

    private Decision<CampaignState> DecideTrainerPokeFlute(CampaignState state, CampaignCommand command)
    {
        var error = ValidatePokeFluteCommand(state, command);
        if (error is not null) return new(CommandStatus.Rejected, state, error);
        var trainer = state.ForestTrainerBattle!;
        return DecideTrainerMedicine(state, command with { Kind = "battle_medicine", Text = "pret:ITEM_POKE_FLUTE",
            PartyIndex = trainer.Presentation.ActivePartyIndex, TargetInstanceId = state.Party[trainer.Presentation.ActivePartyIndex].InstanceId });
    }
}
