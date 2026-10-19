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
        if(FieldDisplayOwner() is FieldReturnPresentation.Owner.FullScreenMenu or FieldReturnPresentation.Owner.Battle)_fieldJumpCues.Clear();
        // Existing field-move controllers own their return/warp program.
        if(state.NoncombatFieldEffect is not null||state.CutEffect is not null||state.RockSmashEffect is not null||
            state.FlashEffect is not null||state.StrengthEffect is not null||state.SurfEffect is not null||state.FlyEffect is not null)
        { _fieldDisplayReturn.Reset();_fieldReturnSnapshot=null;return; }
        var palette=FieldArrivalPalettePresentation.ForWeather(
            state.FieldWeather??_catalog.MapDefinition(state.World.Player.MapId).Weather);
        if(!_fieldDisplayReturn.Observe(FieldDisplayOwner(),palette))return;
        if(_fieldFogScene is {} old)
            _fieldFogScene=new(old.AnchorCameraX,old.AnchorCameraY);
        ReconstructFieldGrass(state);
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
            if(_fieldFogScene is {} fog)
                _fieldFogScene=fog.Advance(1,(int)camera.X,(int)camera.Y);
            AdvanceFieldJumpEffects(1d/60);
            arrival.Advance();_fieldReturnProcessed++;
        }
        _fieldReturnSnapshot=_fieldReturnSnapshot! with{Fog=_fieldFogScene,GrassCovers=_fieldGrass.Covers};
        if(arrival.Complete){_fieldReturnSnapshot=null;_fieldSnapshotReleasePending=true;}
        _held=null;QueueRedraw();return true;
    }

    private bool DrawFieldReturn()
    {
        if(_fieldReturnSnapshot is not {} snapshot||_fieldDisplayReturn.Arrival is not {} arrival)return false;
        DrawFieldSnapshot(snapshot,0,arrival.Palette.BackgroundBlend,arrival.Palette.ObjectBlend);
        if(_fieldMenuMode=="start")DrawFieldMenu(arrival.Palette.BackgroundBlend);
        return true;
    }

    private void ResetFieldReturn()
    {
        _fieldDisplayReturn.Reset();_fieldBagNavigation.Reset();_fieldReturnSnapshot=null;
        _fieldReturnFrames=0;_fieldReturnProcessed=0;
    }

    private bool FieldReturnInputLocked=>_fieldDisplayReturn.Arrival is {Complete:false};
}
