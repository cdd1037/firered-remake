using Game.Application;
using Godot;

public partial class CampaignView
{
    private readonly FieldReturnPresentation _fieldDisplayReturn=new();
    private readonly FieldReturnPresentation.BagNavigation _fieldBagNavigation=new();
    private FieldSnapshot? _fieldReturnSnapshot;
    private double _fieldReturnFrames;
    private int _fieldReturnProcessed;
    private FieldReturnPresentation.MenuReturn? _menuReturn;
    private byte[]? _menuReturnPixels;
    private ImageTexture? _menuReturnTexture;
    private int _menuReturnBlend=-1;
    private double _menuReturnFrames;
    private int _menuReturnProcessed;

    // Immutable composed outgoing page, never a copy of authoritative campaign state.
    private Image CaptureMenuReturnPage()=>GetViewport().GetTexture().GetImage();
    private void ReturnFieldMenu(FieldReturnPresentation.MenuScreen screen,Func<bool>? commit=null)
    {
        using var image=CaptureMenuReturnPage();
        if(commit is not null&&!commit())return;
        _fieldMenuMode="start";BeginMenuReturn(screen,image);
    }
    private void BeginMenuReturn(FieldReturnPresentation.MenuScreen screen,Image image)
    {
        ClearMenuReturnPage();
        image.Convert(Image.Format.Rgba8);
        if(image.GetWidth()!=240||image.GetHeight()!=160)
            throw new InvalidOperationException("Menu return requires the logical 240x160 viewport.");
        _menuReturnPixels=image.GetData();
        _menuReturnTexture=ImageTexture.CreateFromImage(image);
        var state=_session.State;
        var weather=FieldArrivalPalettePresentation.ForWeather(state.FieldWeather??_catalog.MapDefinition(state.World.Player.MapId).Weather);
        _menuReturn=new(screen,FieldMenuEntries().Length,weather);
        _menuReturnFrames=0;_menuReturnProcessed=0;_menuReturnBlend=0;
        _fieldDisplayReturn.Reset();_fieldReturnSnapshot=null;_held=null;QueueRedraw();
    }
    private void ClearMenuReturnPage()
    {
        _menuReturnTexture?.Dispose();_menuReturnTexture=null;_menuReturnPixels=null;_menuReturnBlend=-1;
    }
    private void PrepareReturnedField()
    {
        if(_fieldFogScene is {} old)_fieldFogScene=new(old.AnchorCameraX,old.AnchorCameraY);
        ReconstructFieldGrass(_session.State);
        _fieldReturnSnapshot=CaptureFieldSnapshot(_session.State,new object(),0,true);
    }
    private void AdvanceReturnedField()
    {
        var camera=OrdinaryFieldCamera(_poses[CampaignQuestLogRecorder.PlayerId].Position);
        if(_fieldFogScene is {} fog)_fieldFogScene=fog.Advance(1,(int)camera.X,(int)camera.Y);
        AdvanceFieldJumpEffects(1d/60);
    }
    private bool ProcessMenuReturn(double delta)
    {
        if(_menuReturn is not {} transition)return false;
        _menuReturnFrames+=delta*60;
        while(_menuReturnProcessed<(int)Math.Floor(_menuReturnFrames+1e-7)&&!transition.Complete)
        {
            int arrivalFrame=transition.Arrival?.Frames??0;
            transition.Advance();_menuReturnProcessed++;
            if(transition.ReturnFrame==1)PrepareReturnedField();
            if((transition.Arrival?.Frames??0)!=arrivalFrame)AdvanceReturnedField();
        }
        if(_fieldReturnSnapshot is {} snapshot)
            _fieldReturnSnapshot=snapshot with{Fog=_fieldFogScene,GrassCovers=_fieldGrass.Covers};
        if(transition.Complete){_menuReturn=null;_fieldReturnSnapshot=null;_fieldSnapshotReleasePending=true;}
        _held=null;QueueRedraw();return true;
    }
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
        if(_menuReturn is not null)return;
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
        PrepareReturnedField();
        _fieldReturnFrames=0;_fieldReturnProcessed=0;_held=null;
    }

    private bool ProcessFieldReturn(double delta)
    {
        if(ProcessMenuReturn(delta))return true;
        ObserveFieldDisplayOwner();
        if(_help.Active||_fieldDisplayReturn.Arrival is not {Complete:false} arrival)return false;
        _fieldReturnFrames+=delta*60;
        while(_fieldReturnProcessed<(int)Math.Floor(_fieldReturnFrames+1e-7)&&!arrival.Complete)
        {
            AdvanceReturnedField();
            arrival.Advance();_fieldReturnProcessed++;
        }
        _fieldReturnSnapshot=_fieldReturnSnapshot! with{Fog=_fieldFogScene,GrassCovers=_fieldGrass.Covers};
        if(arrival.Complete){_fieldReturnSnapshot=null;_fieldSnapshotReleasePending=true;}
        _held=null;QueueRedraw();return true;
    }

    private bool DrawFieldReturn()
    {
        if(_menuReturn is {} transition)
        {
            if(transition.ShowsDepartingMenu)
            {
                if(_menuReturnBlend!=transition.BackgroundBlend)
                {
                    byte[] pixels=(byte[])_menuReturnPixels!.Clone();
                    Game.Presentation.QuestLogPalettePresentation.TransformRgba(pixels,0,transition.BackgroundBlend);
                    using var image=Image.CreateFromData(240,160,false,Image.Format.Rgba8,pixels);
                    _menuReturnTexture!.Update(image);_menuReturnBlend=transition.BackgroundBlend;
                }
                DrawTexture(_menuReturnTexture!,Vector2.Zero);
                if(transition.BagBlackWipeHeight>0)
                    DrawRect(new(0,0,240,transition.BagBlackWipeHeight),Colors.Black);
            }
            else if(transition.Arrival is {} incoming&&_fieldReturnSnapshot is {} returned)
            {
                DrawFieldSnapshot(returned,0,incoming.Palette.BackgroundBlend,incoming.Palette.ObjectBlend);
                DrawFieldMenu(incoming.Palette.BackgroundBlend);
            }
            else DrawRect(new(0,0,240,160),Colors.Black);
            return true;
        }
        // A fresh _Draw retires prior canvas commands before freeing their texture.
        if(_menuReturnTexture is not null)ClearMenuReturnPage();
        if(_fieldReturnSnapshot is not {} snapshot||_fieldDisplayReturn.Arrival is not {} arrival)return false;
        DrawFieldSnapshot(snapshot,0,arrival.Palette.BackgroundBlend,arrival.Palette.ObjectBlend);
        if(_fieldMenuMode=="start")DrawFieldMenu(arrival.Palette.BackgroundBlend);
        return true;
    }

    private void ResetFieldReturn()
    {
        _menuReturn=null;ClearMenuReturnPage();
        _fieldDisplayReturn.Reset();_fieldBagNavigation.Reset();_fieldReturnSnapshot=null;
        _fieldReturnFrames=0;_fieldReturnProcessed=0;
    }

    private bool FieldReturnInputLocked=>_menuReturn is {Complete:false}||_fieldDisplayReturn.Arrival is {Complete:false};
}
