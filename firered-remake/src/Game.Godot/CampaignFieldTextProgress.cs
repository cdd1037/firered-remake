using Game.Application;
using Game.Domain.Options;
using Godot;

public partial class CampaignView
{
    private readonly SourceTextProgress _fieldTextProgress=new();
    private CampaignDialogue? _fieldPrintedDialogue;
    private string? _fieldPrintedText;
    private double _fieldTextFrameRemainder;
    private bool _fieldTextHeld,_fieldTextNewPress;
    private bool FieldTextProgressEnabled=>_catalog.PokemonStorageEnabled&&!SeviiRubyBrailleVisible;
    private bool FieldTextComplete=>!FieldTextProgressEnabled||_fieldPrintedText is null||_fieldTextProgress.Complete;
    private string FieldRenderedText(string complete)=>FieldTextProgressEnabled&&_fieldPrintedText is not null?_fieldTextProgress.VisibleText:complete;
    private void SynchronizeFieldText()
    {
        if(!FieldTextProgressEnabled)return;
        var dialogue=_session.State.Dialogue;var text=PresentedFieldText;
        if(text is not null&&_catalog.PewterEnabled)text=PewterDisplayText(text);
        if(dialogue==_fieldPrintedDialogue&&text==_fieldPrintedText)return;
        _fieldPrintedDialogue=dialogue;_fieldPrintedText=text;_fieldTextFrameRemainder=0;_fieldTextHeld=false;_fieldTextNewPress=false;
        int speed=_session.EffectiveOptions.TextSpeed switch{SourceTextSpeed.Slow=>8,SourceTextSpeed.Mid=>4,_=>1};
        _fieldTextProgress.Reset(text??"",speed);
    }
    private void ProcessFieldText(double delta)
    {
        if(!FieldTextProgressEnabled||_fieldPrintedText is null||_fieldTextProgress.Complete||!FieldDialogueVisible)return;
        _fieldTextFrameRemainder+=delta*60;
        int frames=(int)Math.Min(int.MaxValue,Math.Floor(_fieldTextFrameRemainder));if(frames<=0)return;
        _fieldTextFrameRemainder-=frames;
        _fieldTextProgress.AdvanceFrames(frames,_fieldTextHeld,_fieldTextNewPress);_fieldTextNewPress=false;
        ProcessSourceLeaderIntroAudio();
    }
    private bool FieldTextInput(InputEventKey key)
    {
        if(key.Keycode is not (Key.Enter or Key.Backspace))return false;
        if(!key.Pressed){_fieldTextHeld=false;return false;}
        if(!FieldTextProgressEnabled||!FieldDialogueVisible||FieldTextComplete)return false;
        _fieldTextHeld=true;_fieldTextNewPress=true;_held=null;return true;
    }
    private void ReleaseFieldTextInput(){_fieldTextHeld=false;_fieldTextNewPress=false;}
}
