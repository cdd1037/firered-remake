using Game.Application;
using Godot;

public partial class CampaignView
{
    private readonly FieldReturnPresentation _fieldDisplayReturn=new();
    private readonly FieldReturnPresentation.BagNavigation _fieldBagNavigation=new();
    private FieldSnapshot? _fieldReturnSnapshot;
    private double _fieldReturnFrames;
    private int _fieldReturnProcessed;

    // One ownership boundary covers nested ordinary menus and battle submenus.
    // Start/save dialogs and personal-PC text menus still belong to the field.
    private FieldReturnPresentation.Owner FieldDisplayOwner()
    {
        if(_session.State.Battle is not null||_session.State.WildBattle is not null||
            _session.State.SafariBattle is not null||DoubleBattleVisible||ForestTrainerCombatVisible)
            return FieldReturnPresentation.Owner.Battle;
        if(_session.State.ItemMenu is {Origin:"bag" or "party"}||
            _fieldMenuMode is not ("closed" or "start" or "save-confirm" or "save-overwrite"))
            return FieldReturnPresentation.Owner.FullScreenMenu;
        return _fieldMenuMode=="start"?FieldReturnPresentation.Owner.StartMenu:FieldReturnPresentation.Owner.Field;
    }

    private void ObserveFieldDisplayOwner()
    {
        if(_help.Active||PlayerEntryActive||_fieldWarp is not null||QuestLogPlaybackActive)return;
        var state=_session.State;
        // Existing field-move controllers own their return/warp program.
        if(state.NoncombatFieldEffect is not null||state.CutEffect is not null||state.RockSmashEffect is not null||
            state.FlashEffect is not null||state.StrengthEffect is not null||state.SurfEffect is not null||state.FlyEffect is not null)
        { _fieldDisplayReturn.Reset();_fieldReturnSnapshot=null;return; }
        var palette=_fieldFogScene is null?FieldArrivalPalette.Normal:FieldArrivalPalette.FogHorizontal;
        if(!_fieldDisplayReturn.Observe(FieldDisplayOwner(),palette))return;
        var old=_fieldFogScene!;
        _fieldFogScene=new(old.AnchorCameraX,old.AnchorCameraY);
        _fieldReturnSnapshot=CaptureFieldSnapshot(_session.State,new object(),0,true);
        _fieldReturnFrames=0;_fieldReturnProcessed=0;_held=null;_repeat=0;
    }

    private bool ProcessFieldReturn(double delta)
    {
        ObserveFieldDisplayOwner();
        if(_help.Active||_fieldDisplayReturn.Arrival is not {Complete:false} arrival)return false;
        _fieldReturnFrames+=delta*60;
        while(_fieldReturnProcessed<(int)Math.Floor(_fieldReturnFrames+1e-7)&&!arrival.Complete)
        {
            var camera=OrdinaryFieldCamera(_poses[CampaignQuestLogRecorder.PlayerId].Position);
            _fieldFogScene=_fieldFogScene!.Advance(1,(int)camera.X,(int)camera.Y);
            arrival.Advance();_fieldReturnProcessed++;
        }
        _fieldReturnSnapshot=_fieldReturnSnapshot! with{Fog=_fieldFogScene};
        if(arrival.Complete){_fieldReturnSnapshot=null;_fieldSnapshotReleasePending=true;}
        _held=null;QueueRedraw();return true;
    }

    private bool DrawFieldReturn()
    {
        if(_fieldReturnSnapshot is not {} snapshot||_fieldDisplayReturn.Arrival is not {} arrival)return false;
        DrawFieldSnapshot(snapshot,0,arrival.Palette.BackgroundBlend,arrival.Palette.ObjectBlend);
        if(_fieldMenuMode=="start")DrawFieldMenu();
        return true;
    }

    private void ResetFieldReturn()
    {
        _fieldDisplayReturn.Reset();_fieldBagNavigation.Reset();_fieldReturnSnapshot=null;
        _fieldReturnFrames=0;_fieldReturnProcessed=0;
    }

    private bool FieldReturnInputLocked=>_fieldDisplayReturn.Arrival is {Complete:false};
}
