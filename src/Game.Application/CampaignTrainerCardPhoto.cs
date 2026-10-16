using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

/// <summary>The original printer stores Mail-encoded species in six persistent script variables.</summary>
public sealed partial class CampaignSession
{
    public const string TrainerCardPhotoPrefix = "VAR_TRAINER_CARD_MON_ICON_";
    public const string TrainerCardPhotoTint = "VAR_TRAINER_CARD_MON_ICON_TINT_IDX";
    private const string TrainerCardPrinter = "CeladonCity_GameCorner_EventScript_PhotoPrinter";
    private const string TrainerCardPrintPhoto = "CeladonCity_GameCorner_EventScript_PrintPhoto";
    private const string TrainerCardScriptPath = "data/scripts/trainer_card.inc";

    public int TrainerCardStars => (State.HallOfFame?.FirstHallOfFamePlayTime is >0 ? 1 : 0) +
        (KantoPokedexRules.HasAllKantoMons(State.CaughtSpecies) ? 1 : 0) +
        (_content.EvaluateNationalDex(State.SeenSpecies, State.CaughtSpecies).Complete ? 1 : 0);

    private string? ValidateTrainerCardPhoto(CampaignState state)
    {
        if(state.Script.Variables.GetValueOrDefault(TrainerCardPhotoTint) is <0 or >3)return "save_trainer_card_photo_tint";
        bool empty=false;
        for(int slot=1;slot<=6;slot++)
        {
            int species=state.Script.Variables.GetValueOrDefault(TrainerCardPhotoPrefix+slot);
            if(species==0){empty=true;continue;}
            if(empty || !(species==NameRaterRules.EggSpecies || SourceMailComposerRules.IsEncodedUnownSpecies(species) ||
                species!=_content.MailComposer!.UnownSpecies && _content.IsValidPokedexSourceSpecies(species)))return "save_trainer_card_photo_species";
        }
        return null;
    }

    private bool TryApplyTrainerCardPhotoEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(effect.Source.Path!=TrainerCardScriptPath || !effect.Source.Symbol.StartsWith("CeladonCity_GameCorner_",StringComparison.Ordinal) ||
            effect.Opcode is not ("showmoneybox" or "hidemoneybox" or "updatemoneybox" or "checkmoney" or "removemoney" or "multichoice" or "dofieldeffect" or "special"))return false;
        var p=state.World.Player;
        var (dx,dy)=Delta(p.Facing);
        if(!state.Script.Locked ||
            !_content.MapDefinition(p.MapId).BackgroundTriggers.Any(b=>b.X==p.X+dx && b.Y==p.Y+dy &&
                b.ScriptId==TrainerCardPrinter && b.HiddenItem is null && (b.PlayerFacing is null || b.PlayerFacing==p.Facing)) ||
            _content.FindProgram(effect.Source.Symbol)?.Instructions.Any(i=>i.Source==effect.Source && i.Opcode==effect.Opcode && i.Args.SequenceEqual(effect.Args))!=true)
            throw new InvalidDataException("trainer_card_photo_source_context");
        switch(effect.Opcode)
        {
            case "showmoneybox":case "hidemoneybox":case "updatemoneybox":
                next=ApplySourceMoneyBox(state,effect);return true;
            case "checkmoney":case "removemoney":
                if(effect.Source.Symbol!=TrainerCardPrinter || !effect.Args.SequenceEqual(new[]{"50"}) || state.MoneyBox is null)
                    throw new InvalidDataException("trainer_card_photo_payment_context");
                next=effect.Opcode=="checkmoney"?SourceMoneyResult(state,50):DebitSourceMoney(state,50);return true;
            case "multichoice":
                if(effect.Source.Symbol!=TrainerCardPrinter || !effect.Args.SequenceEqual(new[]{"21","0","MULTICHOICE_TRAINER_CARD_ICON_TINT","TRUE"}) || state.MoneyBox is null)
                    throw new InvalidDataException("trainer_card_photo_menu_context");
                return true;
            case "dofieldeffect":
                if(effect.Source.Symbol!=TrainerCardPrintPhoto || !effect.Args.SequenceEqual(new[]{"FLDEFF_PHOTO_FLASH"}))
                    throw new InvalidDataException("trainer_card_photo_flash_context");
                return true; // Preserved presentation effect; the following source delay owns the 60-frame wait.
            case "special":
                if(effect.Source.Symbol!=TrainerCardPrintPhoto || !effect.Args.SequenceEqual(new[]{"UpdateTrainerCardPhotoIcons"}) || state.MoneyBox is null)
                    throw new InvalidDataException("trainer_card_photo_snapshot_context");
                int tint=state.Script.Variables.GetValueOrDefault("VAR_0x8004",-1);
                if(tint is <0 or >3)throw new InvalidDataException("trainer_card_photo_tint");
                var vars=state.Script.Variables.SetItem(TrainerCardPhotoTint,tint);
                for(int slot=0;slot<6;slot++)
                {
                    int species=slot<state.Party.Length?SourceMailComposerRules.MailSpecies(_content.MailComposer!,
                        NameRaterRules.SpeciesOrEgg(state.Party[slot]),state.Party[slot].Pokemon.Personality):0;
                    vars=vars.SetItem(TrainerCardPhotoPrefix+(slot+1),species);
                }
                next=state with{Script=state.Script with{Variables=vars}};return true;
            default:return false;
        }
    }
}
