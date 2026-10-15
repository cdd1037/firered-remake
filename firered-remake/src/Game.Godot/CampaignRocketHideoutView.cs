using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Items;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.WildBattle;
using Godot;
using Direction=Game.Domain.World.Direction;

public partial class CampaignView
{
    private sealed record RocketTile(string Tileset,Rect2I Region);
    private readonly Dictionary<(string Tileset,int Id),RocketTile> _rocketTiles=[];
    private readonly Dictionary<string,int> _rocketTileSymbols=new(StringComparer.Ordinal);
    private readonly Dictionary<string,Texture2D[]> _rocketTileLayers=new(StringComparer.Ordinal);
    private Texture2D? _rocketIndoorTerrain;
    private string? _rocketMenuWait;
    private int _rocketElevatorCursor;
    private long _rocketElevatorEffect;
    private double _rocketElevatorTicks;
    private int _rocketElevatorDistance,_rocketElevatorDirection;
    private bool _rocketElevatorDing;
    private string? _rocketSpinWait,_rocketSpinBlocked;
    private double _rocketSpinTicks;
    private int _rocketSpinDurationTicks;
    private bool RocketInputLocked=>_session?.State.RocketSpin is not null||RocketElevatorAnimating;
    private static readonly string[] RocketElevatorRows=["B1F","B2F","B4F","EXIT"];
    private bool RocketElevatorMenuActive=>_catalog.RocketHideoutEnabled&&_session.State.World.Player.MapId==PretRocketHideoutRegion.ElevatorMapId&&
        _session.State.Script.Wait?.Kind==StoryWaitKind.MenuChoice&&_session.State.RocketElevator?.MenuDefault is not null;
    private bool RocketElevatorMenuVisible=>RocketElevatorMenuActive&&FieldTextComplete&&_session.State.Dialogue?.Page+1==_session.State.Dialogue?.Pages.Length;
    private int RocketElevatorMenuWidth=>(((int)RocketElevatorRows.Max(row=>_sourceNormal.Font.GetStringSize(row,fontSize:_sourceNormal.Size).X)+9)/8+1)*8;
    private int RocketElevatorDuration=>_rocketElevatorDistance==0?0:24*(_rocketElevatorDistance+1);
    private bool RocketElevatorAnimating=>_rocketElevatorDistance>0&&_rocketElevatorTicks<RocketElevatorDuration;
    private int RocketElevatorWindowPhase
    {
        get
        {
            if(_rocketElevatorDistance==0)return 0;
            int count=Math.Min(3*(_rocketElevatorDistance+1),Math.Max(0,((int)Math.Floor(_rocketElevatorTicks)-1)/6));
            int phase=count%3;return _rocketElevatorDirection==0?phase:(3-phase)%3;
        }
    }
    private Vector2 RocketCameraOffset=>!RocketElevatorAnimating?Vector2.Zero:
        new(0,_rocketElevatorTicks<3?0:((int)Math.Floor(_rocketElevatorTicks/3)%2==0?1:-1));
    private void LoadRocketHideoutPresentation()
    {
        if(!_catalog.RocketHideoutEnabled)return;
        var source=_catalog.RocketHideoutPokemon??throw new InvalidDataException("Missing RocketHideout Pokemon.");
        Image Decode(byte[] bytes,int width,int height)
        {
            var image=new Image();
            if(image.LoadPngFromBuffer(bytes)!=Error.Ok){image.Dispose();throw new InvalidDataException("Invalid RocketHideout PNG.");}
            image.Convert(Image.Format.Rgba8);
            if(image.GetWidth()!=width||image.GetHeight()!=height)
            {image.Dispose();throw new InvalidDataException("RocketHideout source pixels differ.");}
            return image;
        }
        foreach(var sprite in source.Sprites)
        {
            using var image=Decode(source.ReadSpriteTexture(new(sprite.SpeciesId),sprite.Side,sprite.PaletteVariant=="shiny"),64,64);
            _battleArt.Add((sprite.SourceSpeciesId,sprite.Side,sprite.PaletteVariant=="shiny"),new(ImageTexture.CreateFromImage(image),new(sprite.SingleBattleTopLeft.X,sprite.SingleBattleTopLeft.Y)));
        }
        foreach(var footprint in source.Footprints)
        {
            using var image=Decode(source.ReadFootprintTexture(footprint.SourceSpeciesId),16,16);
            _route22Footprints.Add(footprint.SourceSpeciesId,ImageTexture.CreateFromImage(image));
        }
        foreach(var icon in source.Icons)
        {
            using var image=Decode(source.ReadIconTexture(new(icon.SpeciesId)),32,32);
            var rgba=image.GetData();
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)for(int c=0;c<3;c++){int value=rgba[i+c]>>3;rgba[i+c]=(byte)((value<<3)|(value>>2));}
            using var display=Image.CreateFromData(32,32,false,Image.Format.Rgba8,rgba);
            _storageIcons.Add(new(icon.SpeciesId),ImageTexture.CreateFromImage(display));
        }
        var art=RocketHideoutPresentationAssets.Load(_root,portraits:true).Files;
        foreach(var id in new[]{"rocket-grunt-m-front","giovanni-front"})
        {
            using var image=Image.CreateFromData(64,64,false,Image.Format.Rgba8,PngRgba8.Decode(art[id+".png"]));
            _forestTrainerPortraits.Add(id,ImageTexture.CreateFromImage(image));
        }
        var items=PretRocketHideoutItems.LoadProject(_root);
        foreach(var icon in items.Icons)
        {
            using var image=Image.CreateFromData(icon.Width,icon.Height,false,Image.Format.Rgba8,ReadItemIconRgba("rocket-hideout-items", icon));
            if(!(_earlyItemIcons.TryGetValue(icon.Item,out var existing)))_earlyItemIcons.Add(icon.Item,ImageTexture.CreateFromImage(image));
        }
        var document=_questPocketUi??throw new InvalidDataException("Rocket key/ball pocket missing.");
        var position=document.Assets.Single(a=>a.Id==document.Items[0].Icon);
        foreach(var value in items.Items.Where(x=>x.Metadata.Item.Id.Value is "pret:ITEM_LIFT_KEY" or "pret:ITEM_SILPH_SCOPE" or "pret:ITEM_NET_BALL" or "pret:ITEM_NEST_BALL"))
        {
            var item=value.Metadata;var icon=items.Icons.Single(i=>i.Item==item.Item.Id);string id="rocket-"+item.Item.SourceItemSymbol.ToLowerInvariant();
            using var image=Image.CreateFromData(icon.Width,icon.Height,false,Image.Format.Rgba8,ReadItemIconRgba("rocket-hideout-items", icon));
            _questPocketTextures.Add(id,ImageTexture.CreateFromImage(image));
            document=document with
            {
                Items=document.Items.Add(new(item.Item.Id.Value,item.Item.SourceItemSymbol,item.Item.Name,item.Description,id,item.Item.FieldUseFunction)),
                Assets=document.Assets.Add(new(id,icon.PngPath,icon.Width,icon.Height,position.X,position.Y))
            };
        }
        _questPocketUi=document;
        _=RocketHideoutPresentationAssets.LoadTerrain(_root);
        _rocketIndoorTerrain=SceneTextures.Texture(System.IO.Path.Combine(_root,"content/generated/rocket-hideout-terrain/indoor2-scene.png"));
        if(_rocketIndoorTerrain.GetWidth()!=240||_rocketIndoorTerrain.GetHeight()!=112)throw new InvalidDataException("Rocket Indoor2 terrain dimensions differ.");
        LoadRocketMetatilePresentation();
    }
    private void LoadRocketMetatilePresentation()
    {
        var art=RocketHideoutPresentationAssets.Load(_root);
        foreach(var tileset in new[]{"game_corner","silph_co"})
        {
            var layers=new Texture2D[3];int index=0;
            foreach(var layer in new[]{"bottom","middle","top"})
            {
                string file=tileset+"-"+layer+".png";
                layers[index++]=SceneTextures.Texture(System.IO.Path.Combine(_root,"content/generated/rocket-hideout-presentation",file));
            }
            _rocketTileLayers.Add(tileset,layers);
        }
        foreach(var tile in art.Manifest.GetProperty("tiles").EnumerateArray())
        {
            string tileset=tile.GetProperty("tileset").GetString()!;
            _rocketTileSymbols.Add(tile.GetProperty("symbol").GetString()!,tile.GetProperty("metatileId").GetInt32());
            _rocketTiles.Add((tileset,tile.GetProperty("metatileId").GetInt32()),new(tileset,
                new(tile.GetProperty("x").GetInt32(),tile.GetProperty("y").GetInt32(),tile.GetProperty("width").GetInt32(),tile.GetProperty("height").GetInt32())));
        }
    }
    private bool TryRocketPatch(Image canvas,Game.Domain.World.FieldMetatileEdit edit,int layer,string mapId)
    {
        if(!_catalog.RocketHideoutEnabled||_catalog.RocketHideoutRegion?.NewMaps.ContainsKey(new(mapId))!=true)return false;
        string tileset=mapId=="map.celadon-city-game-corner"?"game_corner":"silph_co";
        if(!_rocketTiles.TryGetValue((tileset,edit.SourceMetatileId),out var source))throw new InvalidDataException("Rocket metatile art missing: "+edit.SourceMetatileId);
        using var atlas=_rocketTileLayers[tileset][layer].GetImage();
        canvas.BlitRect(atlas,source.Region,new(edit.X*16,edit.Y*16));return true;
    }
    private void SynchronizeRocketPresentation(CampaignState? before,CampaignState after)
    {
        if(!_catalog.RocketHideoutEnabled)return;
        if(after.RocketSpin?.PresentationId!=_rocketSpinWait)
        {
            _rocketSpinWait=after.RocketSpin?.PresentationId;_rocketSpinTicks=0;_rocketSpinBlocked=null;
            if(after.RocketSpin is {} pending)
            {
                // Entering an arrow is an ordinary walk/run; only the forced cue is FAST_1.
                _rocketSpinDurationTicks=pending.Cue?.MovementTicks??_fieldStepTicks;
                if(pending.Cue is {} cue)
                {
                    if(cue.MovementTicks!=8||cue.PixelsPerTick!=2||cue.AnimationFrames.Length!=4||cue.AnimationFrames.Any(f=>f.Duration!=2)||cue.SoundEffectId!=153)
                        throw new InvalidDataException("Rocket forced-spin source cue differs.");
                    SourceSound("SE_M_RAZOR_WIND2");
                }
                _tracks.RemoveAll(t=>t.Actor=="LOCALID_PLAYER");_held=null;ApplyRocketSpinPose(pending,0);
            }
        }
        if(before is null||before.World.Player.MapId!=after.World.Player.MapId)
        {_rocketElevatorDistance=0;_rocketElevatorTicks=0;_rocketElevatorDing=false;_rocketElevatorEffect=before is null?after.Script.EffectSequence:0;}
        string? wait=RocketElevatorMenuActive?after.Script.Wait!.CorrelationId:null;
        if(wait!=_rocketMenuWait){_rocketMenuWait=wait;_rocketElevatorCursor=after.RocketElevator?.MenuDefault??0;}
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>_rocketElevatorEffect&&e.MapId==PretRocketHideoutRegion.ElevatorMapId&&e.Opcode=="special"&&e.Args.SequenceEqual(new[]{"AnimateElevator"})))
        {
            _rocketElevatorEffect=effect.Sequence;
            int from=after.Script.Variables.GetValueOrDefault("VAR_0x8005"),to=after.Script.Variables.GetValueOrDefault("VAR_0x8006");
            _rocketElevatorDistance=Math.Abs(from-to);_rocketElevatorDirection=from>to?1:0;_rocketElevatorTicks=0;_rocketElevatorDing=false;
            if(_rocketElevatorDistance is <1 or >3||after.PresentationTicks!=RocketElevatorDuration)throw new InvalidDataException("Rocket elevator source presentation differs.");
            SourceSound("SE_ELEVATOR");
        }
    }
    private void ProcessRocketPresentation(double delta)
    {
        if(!_catalog.RocketHideoutEnabled||_session.State.World.Player.MapId!=PretRocketHideoutRegion.ElevatorMapId||_rocketElevatorDistance==0)return;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        _rocketElevatorTicks=Math.Min(RocketElevatorDuration,_rocketElevatorTicks+delta*60);
        if(!_rocketElevatorDing&&_rocketElevatorTicks+1e-8>=RocketElevatorDuration){_rocketElevatorDing=true;SourceSound("SE_DING_DONG");}
    }
    private bool DrawRocketElevatorLayer(Texture2D original,Vector2 camera,int layer)
    {
        if(!_catalog.RocketHideoutEnabled||_session.State.World.Player.MapId!=PretRocketHideoutRegion.ElevatorMapId||_rocketElevatorDistance==0)return false;
        // The source animation changes the nine window cells, preserving all other layers.
        int phase=RocketElevatorWindowPhase;
        for(int y=0;y<_art.Source.Height;y++)for(int x=0;x<_art.Source.Width;x++)
        {
            var destination=new Rect2(new Vector2(x*16,y*16)-camera,new(16,16));
            if(x is >=1 and <=3&&y is >=0 and <=2)
            {
                string symbol="METATILE_SilphCo_ElevatorWindow_"+(y==0?"Top":y==1?"Mid":"Bottom")+phase;
                var tile=_rocketTiles[("silph_co",_rocketTileSymbols[symbol])];
                DrawTextureRectRegion(_rocketTileLayers[tile.Tileset][layer],destination,new(tile.Region.Position,tile.Region.Size));
            }
            else DrawTextureRectRegion(original,destination,new(x*16,y*16,16,16));
        }
        return true;
    }
    private void ApplyRocketSpinPose(CampaignRocketSpin pending,int tick)
    {
        var pose=_poses["LOCALID_PLAYER"];var from=new Vector2(pending.From.X*16,pending.From.Y*16);var to=new Vector2(pending.To.X*16,pending.To.Y*16);
        pose.Position=from.Lerp(to,(float)Math.Clamp((double)tick/_rocketSpinDurationTicks,0,1));pose.Visible=_session.State.PlayerVisible;
        if(pending.Cue is {} cue)
        {
            // sprite.c SeekSpriteAnim compensates AnimCmd_frame's first decrement:
            // literal duration2 supplies two visible movement ticks per frame.
            var frame=cue.AnimationFrames[Math.Clamp((Math.Max(1,tick)-1)/2,0,3)];
            pose.Facing=frame.SourceFrame switch{0=>Direction.South,1=>Direction.North,2=>frame.HorizontalFlip?Direction.East:Direction.West,_=>throw new InvalidDataException("Rocket source sprite frame differs.")};
            pose.Frame=0;
        }
        else {pose.Facing=pending.To.Facing;pose.Frame=tick>=_rocketSpinDurationTicks?0:(tick/8)%4+1;}
    }
    private bool ProcessRocketSpin(double delta)
    {
        if(_session.State.RocketSpin is not {} pending)return false;
        _held=null;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        // Leave the eighth movement frame on screen. The next frame acknowledges
        // completion, then can draw the first two pixels of the next source step.
        if(_rocketSpinTicks+1e-8>=_rocketSpinDurationTicks&&_rocketSpinBlocked!=pending.PresentationId)
        {
            Apply(new("rocket_spin_ack",Text:pending.PresentationId));
            if(_session.State.RocketSpin is not {} next)return true;
            if(next.PresentationId==pending.PresentationId){_rocketSpinBlocked=pending.PresentationId;return true;}
            pending=next;
        }
        _rocketSpinTicks=Math.Min(_rocketSpinDurationTicks,_rocketSpinTicks+delta*60);
        ApplyRocketSpinPose(pending,(int)Math.Floor(_rocketSpinTicks+1e-8));return true;
    }
    private ForestMessage? RocketHideoutWildMessage(WildBattleEvent current)=>!_catalog.RocketHideoutEnabled?null:current switch
    {
        WildBattleMoveFailedEvent {Reason:"not-first-turn"}=>new("sText_ButItFailed"),
        WildBattleAbilityPreventedEvent {Ability:"ABILITY_INNER_FOCUS"} ability=>new("sText_PkmnsXPreventsFlinching",
            ("B_EFF_NAME_WITH_PREFIX",WildPrefixedName(ability.Target)),("B_EFF_ABILITY","INNER FOCUS")),
        _=>null
    };
    private bool TryRocketInput(InputEventKey key)
    {
        if(!_catalog.RocketHideoutEnabled)return false;
        if(_session.State.World.Player.MapId.Value=="map.rocket-hideout-b4f"&&_session.State.Script.Wait?.Kind==StoryWaitKind.ButtonPress&&
            _session.State.Dialogue?.TextSymbol=="RocketHideout_B4F_Text_GiovanniIntro"&&key.Keycode is Key.Enter or Key.Backspace)
        {
            _held=null;
            if(key.Pressed&&FieldTextComplete&&_tracks.All(t=>t.Time>=t.Duration)&&_waitElapsed>=_waitDuration)Apply(new("button_press",Text:key.Keycode==Key.Enter?"A":"B"));
            return true;
        }
        if(!RocketElevatorMenuActive||key.Keycode is not(Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace))return false;
        _held=null;
        if(!key.Pressed||!RocketElevatorMenuVisible||_tracks.Any(t=>t.Time<t.Duration)||_waitElapsed<_waitDuration)return true;
        // menu.c Menu_ProcessInput and script_menu.c's B-cancel branch each play this once.
        if(key.Keycode is Key.Up or Key.Down or Key.Enter or Key.Backspace)SourceSound("SE_SELECT");
        if(key.Keycode==Key.Up)_rocketElevatorCursor=(_rocketElevatorCursor+3)%4;
        else if(key.Keycode==Key.Down)_rocketElevatorCursor=(_rocketElevatorCursor+1)%4;
        else if(key.Keycode==Key.Enter)Apply(new("ack",Choice:_rocketElevatorCursor));
        else if(key.Keycode==Key.Backspace)Apply(new("ack",Choice:127));
        return true;
    }
    private bool NavigateRocketPointer(Vector2 point,int direction)=>RocketElevatorMenuVisible&&
        PointerRows(point,direction,ref _rocketElevatorCursor,4,8,8,RocketElevatorMenuWidth,14);
    private bool MouseRocketElevator(Vector2 point)=>RocketElevatorMenuVisible&&
        MouseSelect(point,ref _rocketElevatorCursor,4,8,8,RocketElevatorMenuWidth,14);
    private void DrawRocketElevatorMenu()
    {
        if(!_catalog.RocketHideoutEnabled||_session.State.World.Player.MapId!=PretRocketHideoutRegion.ElevatorMapId)return;
        if(_session.State.RocketElevator?.FloorWindowVisible==true)
        {
            _battleSkin.DrawUserFrame(this,new(168,0,72,48));
            SourceText(_sourceNormal,new(176,10),"Now on:");
            string floor=_session.State.Script.Variables.GetValueOrDefault("VAR_0x8005") switch{3=>"B1F",2=>"B2F",0=>"B4F",_=>throw new InvalidDataException("Rocket elevator floor display differs.")};
            float width=_sourceNormal.Font.GetStringSize(floor,fontSize:_sourceNormal.Size).X;SourceText(_sourceNormal,new(232-width,24),floor);
        }
        if(!RocketElevatorMenuVisible)return;
        _battleSkin.DrawUserFrame(this,new(0,0,RocketElevatorMenuWidth+16,72));
        for(int row=0;row<RocketElevatorRows.Length;row++)SourceText(_sourceNormal,new(16,10+row*14),RocketElevatorRows[row]);
        SourceText(_sourceNormal,new(8,10+_rocketElevatorCursor*14),"▶");
    }
}
