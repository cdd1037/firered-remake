using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Godot;
using Direction=Game.Domain.World.Direction;

/// <summary>Authenticated city service assets and source-clock field presentation.
/// Commands, inventory, fishing RNG and encounters remain application-owned.</summary>
public partial class CampaignView
{
    private readonly Dictionary<int,SpriteAsset> _cityCycleSprites=[];
    private readonly Dictionary<int,SpriteAsset> _cityFishingSprites=[];
    private readonly Dictionary<string,(PretFishingAnimCommand[] Frames,bool Repeats)> _fishingAnimations=new(StringComparer.Ordinal);
    private double _fishingFrameRemainder;
    private int _fishingPoseFrames;
    private int _cityCycleStepPhase,_cityCycleNextStepPhase;
    private SpriteAsset CityCycleSprite=>_cityCycleSprites.TryGetValue(_session.State.PlayerGender,out var sprite)
        ?sprite:throw new InvalidDataException("Bicycle player art was not loaded.");
    private bool BikeShopMenuActive=>_catalog.CityServicesEnabled&&_session.State.World.Player.MapId.Value=="map.cerulean-city-bike-shop"&&
        _session.State.Script.Wait?.Kind==StoryWaitKind.MenuChoice&&_session.State.MoneyBox is not null;
    private bool BikeShopMenuVisible=>BikeShopMenuActive&&FieldTextComplete&&
        _session.State.Dialogue?.Page+1==_session.State.Dialogue?.Pages.Length;

    private void ExtendCityServicesQuestPocket(ref QuestItemUiDocument document)
    {
        var items=_catalog.CityServiceItems;
        if(items.IsDefaultOrEmpty)return;
        var icons=(_catalog.CityServices?.Icons??[]).AddRange(_catalog.GoodRodService?.Icons??[]).AddRange(_catalog.SuperRodService.Icons).AddRange(_catalog.GameCornerCoins.Icons);
        string firstIcon=document.Items[0].Icon;
        var position=document.Assets.Single(a=>a.Id==firstIcon);
        foreach(var item in items)
        {
            var icon=icons.Single(a=>a.Item==item.Item.Id);
            string id="city-service-"+item.Item.SourceItemSymbol;
            
            _questPocketTextures.Add(id,ItemTexture(icon.Item.Value switch { "pret:ITEM_GOOD_ROD" => "good-rod-service", "pret:ITEM_SUPER_ROD" => "super-rod-service", "pret:ITEM_COIN_CASE" => "game-corner-coins", _ => "city-services" }, icon));
            document=document with
            {
                Assets=document.Assets.Add(new(id,icon.PngPath,icon.Width,icon.Height,position.X,position.Y)),
                Items=document.Items.Add(new(item.Item.Id.Value,item.Item.SourceItemSymbol,item.Item.Name,item.Description,id,item.Item.FieldUseFunction))
            };
        }
    }

    private void LoadCityServicesPresentation()
    {
        if(!_catalog.CityServicesEnabled)return;
        var source=PretCityCycleArt.Load(_root);
        foreach(var asset in source.Assets)
        {
            var pixels=NativeImages.ReadPixels(source.ReadTexture(asset.Gender));
            // Match the original 5-bit display expansion already used by the walking avatar.
            ExpandCityPlayerPixels(pixels);
            using var image=Image.CreateFromData(asset.Width,asset.Height,false,Image.Format.Rgba8,pixels);
            _cityCycleSprites.Add(asset.Gender,new(ImageTexture.CreateFromImage(image),asset.FrameWidth,asset.FrameHeight,
                asset.FrameCount,new(asset.TileCenterAnchor.X,asset.TileCenterAnchor.Y),false));
        }
        foreach(var asset in source.Fishing.Assets)
        {
            var pixels = NativeImages.ReadPixels(source.ReadFishingTexture(asset.Gender), asset.Width, asset.Height);
            ExpandCityPlayerPixels(pixels);
            using var image = Image.CreateFromData(asset.Width, asset.Height, false, Image.Format.Rgba8, pixels);
            _cityFishingSprites.Add(asset.Gender,new(ImageTexture.CreateFromImage(image),asset.FrameWidth,asset.FrameHeight,
                asset.FrameCount,new(8-asset.DrawOffsetFromTileOrigin.X,8-asset.DrawOffsetFromTileOrigin.Y),false));
        }
        foreach(var animation in source.Fishing.Animations)
        {
            List<PretFishingAnimCommand> frames=[];bool repeats=false;
            foreach(var command in animation.Commands)
            {
                if(command.Opcode=="frame")for(int i=0;i<command.Duration;i++)frames.Add(command);
                else if(command.Opcode=="loop")
                {
                    var block=frames.ToArray();for(int i=0;i<command.Value;i++)frames.AddRange(block);
                }
                else if(command.Opcode=="jump"&&command.Value==0)repeats=true;
                else if(command.Opcode!="end")throw new InvalidDataException("Unsupported fishing animation command.");
            }
            if(frames.Count==0)throw new InvalidDataException("Fishing animation is empty.");
            _fishingAnimations.Add(animation.Id,(frames.ToArray(),repeats));
        }
    }

    private static void ExpandCityPlayerPixels(byte[] pixels)
    {
        for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]!=0)for(int c=0;c<3;c++)
        {int five=pixels[i+c]>>3;pixels[i+c]=(byte)((five<<3)|(five>>2));}
    }
    private void DisposeCityServicesPresentation()
    {
        foreach(var sprite in _cityCycleSprites.Values)sprite.Texture.Dispose();
        _cityCycleSprites.Clear();
        foreach(var sprite in _cityFishingSprites.Values)sprite.Texture.Dispose();
        _cityFishingSprites.Clear();_fishingAnimations.Clear();
    }

    private bool TryCityServiceItem(string item)
    {
        if(!_catalog.CityServicesEnabled||item is not ("pret:ITEM_BICYCLE" or "pret:ITEM_OLD_ROD" or "pret:ITEM_GOOD_ROD" or "pret:ITEM_SUPER_ROD"))return false;
        if(item=="pret:ITEM_SUPER_ROD"&&!_catalog.SuperRodEnabled)return false;
        if(item=="pret:ITEM_GOOD_ROD"&&_catalog.GoodRodService is null)return false;
        Apply(new(item=="pret:ITEM_BICYCLE"?"field_bicycle":"field_fish",Text:item));
        if(_session.State.ItemMenu is not null)
            _questPocketNotice=_questPocketUi!.Texts["gText_OakForbidsUseOfItemHere"].Replace("{PLAYER}",_session.State.PlayerName,StringComparison.Ordinal);
        return true;
    }

    private void SynchronizeCityServicesPresentation(CampaignState? before,CampaignState after)
    {
        if(before is null||!before.IsCycling||!after.IsCycling||before.World.Player.MapId!=after.World.Player.MapId||
            before.World.Player.X==after.World.Player.X&&before.World.Player.Y==after.World.Player.Y&&before.World.Player.Facing!=after.World.Player.Facing)
            _cityCycleStepPhase=_cityCycleNextStepPhase=0;
        if(before is not null&&!before.IsCycling&&after.IsCycling)SourceSound("SE_BIKE_BELL");
        if(before?.Fishing?.WaitId!=after.Fishing?.WaitId)
        {_fishingFrameRemainder=0;_fishingPoseFrames=0;}
        if(after.Fishing is {} fishing)
        {
            _held=null;_repeat=0;
            if(before?.Fishing?.State.SourceStep!=fishing.State.SourceStep&&fishing.State.SourceStep is 2 or 9 or 14)
                _fishingPoseFrames=0;
        }
    }

    private void BeginCityCycleStep()
    {
        // SetStepAnimHandleAlternation with source standard animPos {1,3,0,2}.
        // Each completed land step leaves the idle command before the opposite pedal.
        _cityCycleStepPhase=_cityCycleNextStepPhase;_cityCycleNextStepPhase^=2;
    }

    // event_object_movement.c: sStep2, six source updates [2,3,3,2,3,3].
    // Stepping stays pixel-based and does not interpolate uniformly over six ticks.
    private static float CityCycleStepFraction(double seconds)
    {
        int frame=Math.Clamp((int)Math.Floor(seconds*60+1e-9),0,6);
        return new[]{0,2,5,8,10,13,16}[frame]/16f;
    }

    private bool ProcessFishingPresentation(double delta)
    {
        if(_session.State.Fishing is null)return false;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        _fishingFrameRemainder+=delta*SourceAudioFramesPerSecond;
        int frames=(int)Math.Min(int.MaxValue,Math.Floor(_fishingFrameRemainder+1e-9));
        _fishingFrameRemainder-=frames;
        for(int frame=0;frame<frames&&_session.State.Fishing is {} fishing;frame++)
        {
            ProcessFieldText(1d/60);
            _fishingPoseFrames++;
            _waitElapsed+=1d/SourceAudioFramesPerSecond;
            if(fishing.State.SourceStep is 10 or 14 or 15&&_session.State.Dialogue is not null&&FieldTextComplete&&!fishing.TextPrinted)
                Apply(new("fishing_text_finished",Text:fishing.WaitId));
            if(fishing.State.SourceStep==14&&!fishing.NoCatchAnimationFinished&&FishingNoCatchAnimationComplete)
                Apply(new("fishing_animation_finished",Text:fishing.WaitId));
            // Fishing11/16 only advance the sprite/text printers until the correlated
            // result acknowledgement arrives. Keep those presentation clocks above,
            // without committing an identical gameplay snapshot on every source frame.
            var current=_session.State.Fishing;
            bool waitingForResult=current is {MessageFinished:false}&&
                (current.State is {SourceStep:10,FrameCounter:0} or {SourceStep:15});
            if(!waitingForResult)Apply(new("fishing_tick"));
        }
        return true;
    }

    private bool TryFishingInput(InputEvent input)
    {
        if(_session?.State.Fishing is not {} fishing)return false;
        if(input is InputEventKey {Keycode:Key.Escape or Key.F12})return false;
        _held=null;_repeat=0;
        Key? confirm=input switch
        {
            InputEventKey {Echo:false,Keycode:Key.Enter or Key.Backspace} key=>key.Keycode,
            InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left}=>Key.Enter,
            InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Right}=>Key.Backspace,
            _=>null
        };
        if(confirm is {} button)
        {
            bool pressed=input is not InputEventKey {Pressed:false};
            using var key=new InputEventKey{Keycode=button,Pressed=pressed};
            if(!FieldTextInput(key)&&pressed&&fishing.TextPrinted&&!fishing.MessageFinished)
                Apply(new("ack",Text:fishing.WaitId));
        }
        QueueRedraw();return true;
    }

    private void DrawFishingDots()
    {
        if(_session.State.Fishing?.State is not {} fishing||fishing.SourceStep is not (4 or 5 or 9))return;
        _battleSkin.DrawDialogue(this);
        for(int i=0;i<fishing.NumDots;i++)SourceText(_sourceNormal,LabBattleUiSkin.DialogueText+new Vector2(i*12,0),"·");
    }

    private string FishingDirection=>_session.State.World.Player.Facing switch
    {Direction.South=>"south",Direction.North=>"north",Direction.West=>"west",_=>"east"};
    private bool FishingNoCatchAnimationComplete=>_fishingPoseFrames>=_fishingAnimations["retract."+FishingDirection].Frames.Length;
    private PretFishingAnimCommand? FishingPlayerFrame
    {
        get
        {
            var fishing=_session.State.Fishing?.State;
            if(fishing is null||fishing.SourceStep is 1 or 15||fishing.SourceStep==10&&fishing.FrameCounter!=0)return null;
            string action=fishing.SourceStep==14?"retract":fishing.SourceStep is 9 or 10?"hook":"cast";
            var animation=_fishingAnimations[action+"."+FishingDirection];
            int frame=animation.Repeats?_fishingPoseFrames%animation.Frames.Length:Math.Min(_fishingPoseFrames,animation.Frames.Length-1);
            return animation.Frames[frame];
        }
    }
    private void DrawCityServicesActor(string id,SpriteAsset asset,Pose pose,Vector2 camera)
    {
        if(DrawVsSeekerActor(id,asset,pose,camera))return;
        if(DrawFieldFlyTravelActor(id,asset,pose,camera))return;
        if(DrawEventIslandActor(id,asset,pose,camera))return;
        if(id=="LOCALID_PLAYER"&&FishingPlayerFrame is {} frame)
        {
            var sprite=_cityFishingSprites[_session.State.PlayerGender];
            var position=pose.Position.Floor()+new Vector2(8+frame.OffsetX,8+frame.OffsetY)-sprite.Anchor-camera;
            if(frame.FlipH)
            {
                DrawSetTransform(position+new Vector2(sprite.Width,0),0,new(-1,1));
                DrawTextureRectRegion(sprite.Texture,new(0,0,sprite.Width,sprite.Height),new(frame.Value*sprite.Width,0,sprite.Width,sprite.Height));
                DrawSetTransform(Vector2.Zero);
            }
            else DrawTextureRectRegion(sprite.Texture,new(position,new(sprite.Width,sprite.Height)),new(frame.Value*sprite.Width,0,sprite.Width,sprite.Height));
            return;
        }
        if (DrawFourIslandActor(id,asset,pose,camera)) return;
        if (DrawFieldSurfActor(id,pose,camera)) return;
        DrawFieldCutActor(id,asset,pose,camera);
    }

    // script_menu.c uses the source left=11, top=0, then clamps width to 28 tiles.
    // CLEAR_TO 0x49 and FONT_SMALL place the price independently of the normal label.
    private Rect2 BikeShopMenuFrame
    {
        get
        {
            float priceWidth=_sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText("¥1,000,000"),fontSize:_sourceSmall.Size).X;
            int width=((int)(0x49+priceWidth)+9)/8+1;
            int left=Math.Min(11,28-width);
            return new(left*8,0,width*8+16,48);
        }
    }
    private void DrawBikeShopMenu()
    {
        if(!BikeShopMenuVisible)return;
        var frame=BikeShopMenuFrame;
        _battleSkin.DrawUserFrame(this,frame);
        SourceText(_sourceNormal,frame.Position+new Vector2(16,10),"BICYCLE");
        SourceText(_sourceSmall,frame.Position+new Vector2(16+0x49,10),"¥1,000,000");
        SourceText(_sourceNormal,frame.Position+new Vector2(16,24),"NO THANKS");
        SourceText(_sourceNormal,frame.Position+new Vector2(8,_choiceYes?10:24),"▶");
    }
    private bool NavigateBikeShopPointer(Vector2 point,int direction)
    {
        if(!BikeShopMenuVisible)return false;
        var frame=BikeShopMenuFrame;
        if(!new Rect2(frame.Position+new Vector2(0,8),new(frame.Size.X,28)).HasPoint(point))return false;
        if(direction!=0)return MouseNavigationKey(direction);
        bool first=point.Y<22;if(first==_choiceYes)return false;_choiceYes=first;return true;
    }
    private bool MouseBikeShop(Vector2 point)
    {
        if(!BikeShopMenuVisible)return MouseConfirm(point,MouseMessage);
        var frame=BikeShopMenuFrame;
        if(!new Rect2(frame.Position+new Vector2(0,8),new(frame.Size.X,28)).HasPoint(point))return false;
        _choiceYes=point.Y<22;return MouseKey();
    }
}
