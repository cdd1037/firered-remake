using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Godot;

public partial class CampaignView
{
    private Texture2D? _fieldFogTexture;
    private FieldHorizontalFogPresentation.Scene? _fieldFogScene,_questFogScene;
    private (object Owner,int Scene,int Revision,bool Saved)? _questFogKey;

    private void LoadFieldFogPresentation()
    {
        // Existing imported shared weather texture; its historical package path
        // does not select the maps that use it.
        using var image=NativeImages.LoadPng(GeneratedResourceFile.Read(_root,
            "content/generated/ui/pokemon-tower-presentation/fog-horizontal.png"),64,64);
        _fieldFogTexture=ImageTexture.CreateFromImage(image);
    }

    private static Vector2 FieldCameraAt(Vector2 position)=>(position+new Vector2(8-120,8-80)).Floor();
    private static FieldHorizontalFogPresentation.Scene FreshFieldFog(Vector2 camera)=>new((int)camera.X,(int)camera.Y);
    private bool HasFieldFog(string map,string? weather=null)=>
        (weather??_catalog.MapDefinition(new MapId(map)).Weather)=="WEATHER_FOG_HORIZONTAL";

    private void SynchronizeFieldFog(CampaignState? before,CampaignState after)
    {
        if(before is null||before.World.Player.MapId!=after.World.Player.MapId)ResetFieldReturn();
        if(_fieldWarpSynchronizing)return; // The destination snapshot already owns its exact phase.
        if(!HasFieldFog(after.World.Player.MapId.Value,after.FieldWeather)){_fieldFogScene=null;return;}
        if(_fieldFogScene is null||before is null||before.World.Player.MapId!=after.World.Player.MapId||
            before.FieldWeather!=after.FieldWeather||after.FieldTransition is {} transition&&!ReferenceEquals(before.FieldTransition,transition))
            _fieldFogScene=FreshFieldFog(FieldCameraAt(new(after.World.Player.X*16,after.World.Player.Y*16)));
    }

    private Vector2 OrdinaryFieldCamera(Vector2 hero)=>(BillCameraFocus(EventIslandCameraFocus(hero))+new Vector2(8,8)-new Vector2(120,80)).Floor()+
        RocketCameraOffset+DepartmentCameraOffset+SilphCameraOffset+CinnabarCameraOffset+SixIslandCameraOffset+SevenIslandCameraOffset+EventIslandCameraOffset;

    private void ProcessFieldFog(double delta)
    {
        if(_fieldDisplayReturn.Suspended||_fieldFogScene is not {} fog||!_poses.TryGetValue("LOCALID_PLAYER",out var hero))return;
        // Weather and fog sprites run before this frame's camera movement.
        var camera=OrdinaryFieldCamera(hero.Position);
        _fieldFogScene=fog.Advance(delta*60,(int)camera.X,(int)camera.Y);
    }

    private static Vector2 QuestFogCamera(CampaignQuestLogPlayback playback)
    {
        var player=playback.Actors.Single(a=>a.Id==CampaignQuestLogRecorder.PlayerId);
        var position=new Vector2(player.X*16,player.Y*16);
        if(playback.Movement(player.Id) is {} move)
            position=new Vector2(move.From.X*16,move.From.Y*16).Lerp(position,(float)move.Fraction);
        if(playback.ActiveTransitionTiming is {} timing&&playback.TransitionElapsedFrames<timing.SwitchFrame&&
            timing.EffectiveDeparture==Game.Domain.World.FieldTransitionDeparture.StepOntoWarp&&timing.DepartureDoor is {} step)
        {
            var sample=QuestLogFieldWarpSample(playback);
            position=new Vector2(player.X*16,player.Y*16)+new Vector2(step.X-player.X,step.Y-player.Y)*sample.WalkPixels;
        }
        return FieldCameraAt(position);
    }

    private void EnsureQuestFog(CampaignQuestLogPlayback playback)
    {
        var key=((object)playback,playback.SceneIndex,playback.ArtRevision,playback.SavedGameVisible);
        if(_questFogKey==key)return;
        _questFogKey=key;
        if(!HasFieldFog(playback.Map,playback.SavedGameVisible?_session.State.FieldWeather:null)){_questFogScene=null;return;}
        var origin=playback.MapOriginPlayer;
        _questFogScene=FreshFieldFog(FieldCameraAt(new(origin.X*16,origin.Y*16)));
    }

    private void DrawFieldFogSample(FieldHorizontalFogPresentation.Scene fog,Texture2D texture)=>
        DrawFieldFog(texture,fog.Sprites().Select(sprite=>new Vector2(sprite.X,sprite.Y)),
            FieldHorizontalFogPresentation.Eva,FieldHorizontalFogPresentation.Evb);

    private void DisposeFieldFogPresentation()
    {
        DisposeFieldFogCompositor();
        _fieldFogTexture?.Dispose();_fieldFogTexture=null;
        _fieldFogScene=_questFogScene=null;_questFogKey=null;
    }
}
