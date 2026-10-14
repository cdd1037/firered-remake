using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Slots;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideSlotMachine(CampaignState state, CampaignCommand command)
    {
        var result=CampaignSlotMachineTransactions.Tick(state,command,_content);
        if(result.Status!=CommandStatus.Accepted||result.State.SlotMachine is not null)return result;
        // Only the settled owner can resume this exact original playslotmachine callback.
        var continued=StoryRules.Continue(result.State.Script,state.SlotMachine!.CallbackId,_content);
        return new(CommandStatus.Accepted,Pump(Advance(result.State,continued)));
    }

    private bool TryApplySlotMachineEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next=state;
        if(effect.Source.Path!=GameCornerCoinSource.Path||effect.Source.Symbol!=SlotMachineSource.Common||
            effect.Opcode is not ("setfieldeffectargument" or "dofieldeffect" or "waitfieldeffect" or "specialvar" or "playslotmachine"))return false;
        var contract=_content.GameCornerSlots??throw new InvalidDataException("slot_initial_state_policy_pending");
        contract.Validate();
        int id=state.Script.Variables.GetValueOrDefault("VAR_0x8004",-1);
        if(!CampaignSlotMachineTransactions.AtOriginalSign(state,_content,id))throw new InvalidDataException("slot_original_sign_context");
        RequireCoinEffect(state,effect);
        var instruction=GameCornerCoinSource.Instruction(state.Script,_content)!;
        void Require(params string[] args)
        {
            if(!instruction.Args.SequenceEqual(args))throw new InvalidDataException("slot_source_operands");
        }
        switch(effect.Opcode)
        {
            case "setfieldeffectargument":
                int index=int.Parse(effect.Args[0],System.Globalization.CultureInfo.InvariantCulture);
                string token=index switch {0=>"LOCALID_PLAYER",1=>"MAP_GROUP(MAP_CELADON_CITY_GAME_CORNER)",2=>"MAP_NUM(MAP_CELADON_CITY_GAME_CORNER)",_=>throw new InvalidDataException("slot_field_argument_index")};
                Require(index.ToString(System.Globalization.CultureInfo.InvariantCulture),token);
                // Preserve the original non-BUGFIX map/group ordering, which LOCALID_PLAYER ignores.
                int value=index switch {0=>255,1=>10,_=>14};
                next=state with {FieldEffectArguments=(state.FieldEffectArguments??ImmutableSortedDictionary<int,int>.Empty).SetItem(index,value)};
                return true;
            case "dofieldeffect":
                Require("FLDEFF_SMILEY_FACE_ICON");
                if(state.FieldEffectArguments is not {} args||args.GetValueOrDefault(0)!=255||args.GetValueOrDefault(1)!=10||args.GetValueOrDefault(2)!=14)
                    throw new InvalidDataException("slot_smiley_arguments");
                return true;
            case "waitfieldeffect":
                Require("FLDEFF_SMILEY_FACE_ICON");
                next=state with {PresentationTicks=60}; return true;
            case "specialvar":
                Require("VAR_RESULT","GetRandomSlotMachineId");
                if(state.SlotMachine is not null||state.Script.Variables.GetValueOrDefault("VAR_RESULT",-1)!=1)
                    throw new InvalidDataException("slot_selection_confirmation_context");
                var selected=SlotMachineCore.SelectMachine(contract.Rules,state.World.Rng);
                next=state with {World=state.World with {Rng=selected.Rng},FieldEffectArguments=null,
                    Script=state.Script with {Variables=state.Script.Variables.SetItem("VAR_RESULT",selected.MachineIndex)},
                    SlotMachine=new(id,selected.MachineIndex,"",CampaignSlotPhase.Selected,contract.InitialBias,SlotReels.Initial)};
                return true;
            case "playslotmachine":
                Require("VAR_RESULT");
                if(state.SlotMachine is not {Phase:CampaignSlotPhase.Selected} pending||pending.SignId!=id||
                    state.Script.Wait is not {Kind:StoryWaitKind.NativeEffect} wait||
                    state.Script.Variables.GetValueOrDefault("VAR_RESULT",-1)!=pending.MachineIndex)
                    throw new InvalidDataException("slot_entry_callback_context");
                next=state with {SlotMachine=pending with {CallbackId=wait.CorrelationId,Phase=CampaignSlotPhase.Initialize},
                    Script=state.Script with {Wait=wait with {Kind=StoryWaitKind.SlotMachine}},
                    Dialogue=null,SignMessage=null,PresentationTicks=0,PresentationEffects=[],Motions=[]};
                return true;
        }
        return false;
    }
}
