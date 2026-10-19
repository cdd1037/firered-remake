using Game.Application;
using Godot;

public partial class CampaignView
{
    private FieldScreenFadePresentation.Timeline? _fieldScreenFade;
    private FieldSnapshot? _fieldScreenFadeSnapshot;
    private long _fieldScreenFadeEffect;

    private void SynchronizeFieldScreenFade(CampaignState? before,CampaignState after)
    {
        if(before is null||before.World.Player.MapId!=after.World.Player.MapId)
        {
            _fieldScreenFade=null;_fieldScreenFadeSnapshot=null;
            _fieldScreenFadeEffect=before is null?after.Script.EffectSequence:0;
        }
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>_fieldScreenFadeEffect&&
            e.Opcode=="fadescreen"&&e.Args.Length==1&&e.Args[0] is "FADE_TO_WHITE" or "FADE_FROM_WHITE"))
        {
            bool waitForPlayer=after.PresentationEffects.Any(e=>e.Sequence>_fieldScreenFadeEffect&&
                e.Sequence<effect.Sequence&&e.Opcode=="lockall");
            int movement=(int)Math.Ceiling(60*(_tracks.Count==0?0:_tracks.Max(t=>Math.Max(0,t.Duration-t.Time)))-1e-7);
            var palette=FieldArrivalPalettePresentation.ForWeather(after.FieldWeather??_catalog.MapDefinition(after.World.Player.MapId).Weather);
            _fieldScreenFade=new(effect.Args[0]=="FADE_FROM_WHITE",palette,movement,waitForPlayer);
            _fieldScreenFadeEffect=effect.Sequence;
            _fieldScreenFadeSnapshot=_fieldScreenFade.Fade is null?null:CaptureFieldSnapshot(after,new object(),0,true);
        }
    }

    private void ProcessFieldScreenFade(double delta)
    {
        if(_fieldScreenFade is not {} timeline)return;
        timeline.Advance(delta*60);
        if(timeline.Fade is {} fade)
        {
            _fieldScreenFadeSnapshot=CaptureFieldSnapshot(_session.State,_fieldScreenFadeSnapshot?.Owner??new object(),0,true);
            if(fade.FromWhite&&fade.VisualComplete&&timeline.ScriptReady)
            {
                _fieldScreenFade=null;_fieldScreenFadeSnapshot=null;
                _fieldSnapshotReleasePending=true;
            }
        }
    }

    private bool DrawFieldScreenFade()
    {
        if(_fieldScreenFadeSnapshot is not {} snapshot||_fieldScreenFade?.Fade is not {} fade)return false;
        DrawFieldSnapshot(snapshot,0,fade.BackgroundBlend,fade.ObjectBlend,0x7fff);
        return true;
    }

    private SourceGameFont FieldDialogueFont=>_fieldScreenFade?.Fade is {} fade?
        FieldPaletteUiFont(fade.BackgroundBlend,0x7fff):_sourceNormal;
    private Texture2D FieldDialoguePalette(Texture2D texture)=>FieldSnapshotPaletteTexture(texture,true,applyShade:false);
    private bool FieldScreenFadeWait=>_session.State.Script.Wait?.Kind==Game.Domain.Events.StoryWaitKind.Presentation&&
        _session.State.PresentationEffects.Any(e=>e.Sequence==_fieldScreenFadeEffect&&e.Opcode=="fadescreen");
}
