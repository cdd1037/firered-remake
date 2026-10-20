using Game.Application;
using Game.Domain.Options;
using Godot;

public partial class CampaignView
{
    private readonly SourceTextProgress _fieldTextProgress=new();
    private SourceDialogSurface _fieldTextSurface = null!;
    private SourceDialogText FieldText => _fieldTextSurface.Text;
    private int _fieldPrintedTowerSpeechPage;
    private CampaignDialogue? _fieldPrintedDialogue;
    private string? _fieldPrintedText;
    private double _fieldTextFrameRemainder;
    private bool _fieldTextHeld,_fieldTextNewPress;
    private bool FieldTextProgressEnabled=>!SeviiRubyBrailleVisible;
    private bool FieldTextComplete=>!FieldTextProgressEnabled||_fieldPrintedText is null||
        _fieldTextProgress.Complete&&FieldText.IsFinal;
    private bool FieldVisualContinuationPending=>FieldTextProgressEnabled&&_fieldPrintedText is not null&&
        _fieldTextProgress.Complete&&!FieldText.IsFinal;
    private void InitializeFieldText()
    {
        _fieldTextSurface = new();
        AddChild(_fieldTextSurface);
    }
    private void DrawFieldDialogueText(SourceGameFont font) =>
        _fieldTextSurface.Draw(this, LabBattleUiSkin.DialogueText, font);
    private void DrawFieldDialogueCursor() =>
        _textCursor.Draw(this, (LabBattleUiSkin.DialogueText + FieldText.ContinuationPosition).Floor(),
            (long)Math.Max(0, _waitElapsed * 60));
    private void SynchronizeFieldText()
    {
        if(!FieldTextProgressEnabled){FieldText.Clear();_fieldPrintedText=null;return;}
        var dialogue=_session.State.Dialogue;var text=PresentedFieldText;
        if(dialogue==_fieldPrintedDialogue&&text==_fieldPrintedText&&_trainerTowerSpeechPage==_fieldPrintedTowerSpeechPage)return;
        _fieldPrintedDialogue=dialogue;_fieldPrintedText=text;_fieldPrintedTowerSpeechPage=_trainerTowerSpeechPage;_fieldTextFrameRemainder=0;_fieldTextHeld=false;_fieldTextNewPress=false;
        int speed=_session.EffectiveOptions.TextSpeed switch{SourceTextSpeed.Slow=>8,SourceTextSpeed.Mid=>4,_=>1};
        if(text is null){FieldText.Clear();return;}
        string prepared = _sourceNormal.PrepareText(text);
        FieldText.Visible = true;
        FieldText.Bind(prepared, _sourceNormal.Font, _sourceNormal.Size, 15, 198,
            wrap: SourceHelpTextLayout.ContainsCjk(prepared));
        _fieldTextProgress.Reset(FieldText.CurrentVisualText,speed);
    }
    private void ProcessFieldText(double delta)
    {
        if(!FieldTextProgressEnabled||_fieldPrintedText is null||_fieldTextProgress.Complete||!FieldDialogueVisible)return;
        _fieldTextFrameRemainder+=delta*60;
        int frames=(int)Math.Min(int.MaxValue,Math.Floor(_fieldTextFrameRemainder));if(frames<=0)return;
        _fieldTextFrameRemainder-=frames;
        _fieldTextProgress.AdvanceFrames(frames,_fieldTextHeld,_fieldTextNewPress);_fieldTextNewPress=false;
        FieldText.RevealCodepoints(_fieldTextProgress.VisibleCodepoints);
        ProcessSourceLeaderIntroAudio();
    }
    // A visual continuation is consumed before field choice owners that intentionally
    // swallow keys until the whole semantic message has completed.
    private bool FieldVisualContinuationAcceptsInput=>FieldVisualContinuationPending&&FieldDialogueVisible&&
        _fade<=0&&!FieldMenuOpen&&!TownMapActive&&!MoveTutorActive&&!NpcTradeChoosing&&!NpcTradePresenting&&
        !DaisyGroomingChoosing&&!SizeRecordChoosing&&!NameRaterChoosing&&_regionalServiceNotice is null&&
        _session.EasyChatEditor is null&&!_session.State.TrainerTowerRecordsVisible&&_wildNotice is null&&
        _session.State.ItemMenu is null&&_session.State.Pc is null&&_session.State.Shop is null&&
        _session.State.Battle is null&&_session.State.WildBattle is null&&_session.State.SafariBattle is null&&
        _session.State.Evolution is null&&_session.State.RareCandy is null&&_session.State.OldManTutorial is null&&
        !DoubleBattleVisible&&!ForestTrainerCombatVisible;
    private bool TryFieldVisualContinuationInput(InputEventKey key)
    {
        if(!key.Pressed||key.Echo||key.Keycode is not (Key.Enter or Key.Backspace)||
            !FieldVisualContinuationAcceptsInput)return false;
        return TryAdvanceFieldVisualMessage();
    }
    private bool TryAdvanceFieldVisualMessage()
    {
        if(!FieldVisualContinuationPending)return false;
        if(!FieldText.TryAdvanceVisual(_fieldTextProgress.Complete))return false;
        _fieldTextProgress.ContinuePage(FieldText.CurrentVisualText);
        _fieldTextHeld=_fieldTextNewPress=false;_held=null;_repeat=0;
        return true;
    }
    private bool FieldTextInput(InputEventKey key)
    {
        if(key.Keycode is not (Key.Enter or Key.Backspace))return false;
        if(!key.Pressed){_fieldTextHeld=false;return false;}
        if(!FieldTextProgressEnabled||!FieldDialogueVisible||FieldTextComplete)return false;
        if(!key.Echo&&TryAdvanceFieldVisualMessage())return true;
        _fieldTextHeld=true;_fieldTextNewPress=true;_held=null;return true;
    }
    private void ReleaseFieldTextInput(){_fieldTextHeld=false;_fieldTextNewPress=false;}
}
