using Game.Application;
using Game.Content;
using Godot;

public partial class CampaignView
{
    private PretFirstSeviiPresentation? _firstSeviiPresentation;
    private readonly Dictionary<(string Asset,int Fade),Texture2D> _seagallopTextures=[];
    private SeagallopSourcePresentation? _seagallopClock;
    private string? _seagallopId;
    private double _seagallopRemainder;
    private bool FirstSeviiInputLocked=>_session?.State.Seagallop is not null;
    private void LoadFirstSeviiPresentation()
    {
        if(!_catalog.FirstSeviiEnabled)return;
        var art=_firstSeviiPresentation=_catalog.FirstSeviiPresentation??throw new InvalidDataException("First-Sevii presentation package missing.");
        foreach(var portrait in art.Document.Portraits)
        {
            var asset=art.Asset(portrait.AssetId);
            using var image=NativeImages.LoadPng(art.ReadPng(asset.Id), asset.Width, asset.Height);
            _forestTrainerPortraits.Add(asset.Id,(ImageTexture)SceneTextures.FromImage(image));
        }
        foreach(var item in art.Document.Items)
        {
            var asset=art.Asset(item.AssetId);
            using var image=NativeImages.LoadPng(art.ReadPng(asset.Id), asset.Width, asset.Height);
            if(!(_earlyItemIcons.TryGetValue(new(item.Id),out var existing)))_earlyItemIcons.Add(new(item.Id),(ImageTexture)SceneTextures.FromImage(image));
            if(item.Pocket!="POCKET_KEY_ITEMS")continue;
            var document=_questPocketUi??throw new InvalidDataException("First-Sevii key-item pocket missing.");
            if(document.Items.Any(i=>i.Id==item.Id))throw new InvalidDataException("Duplicated first-Sevii key item.");
            var position=document.Assets.Single(a=>a.Id==document.Items[0].Icon);
            _questPocketTextures.Add(asset.Id,SceneTextures.FromImage(image));
            _questPocketUi=document with
            {
                Items=document.Items.Add(new(item.Id,item.Symbol,item.Name,item.Description,asset.Id,item.FieldUseFunction)),
                Assets=document.Assets.Add(new(asset.Id,PretFirstSeviiPresentation.GeneratedDirectory+asset.TexturePath,asset.Width,asset.Height,position.X,position.Y))
            };
        }
        Write("firstSeviiPresentation",new{trainerTeams=art.Document.Trainers.Length,trainerPortraits=art.Document.Portraits.Length,itemIcons=art.Document.Items.Length,
        originalFerryAssets=true,physicalAudioVerified=false});
    }
    private FirstSeviiPresentationTrainer? FirstSeviiTrainer(string? id)=>id is null?null:_firstSeviiPresentation?.Document.Trainers.SingleOrDefault(t=>t.Id==id);
    private string? VisibleFirstSeviiTrainerClass()=>FirstSeviiTrainer(_session.State.ForestTrainerBattle?.Trainer.Value)?.ClassName;
    private string? VisibleFirstSeviiTrainerPortrait()=>FirstSeviiTrainer(_session.State.ForestTrainerBattle?.Trainer.Value) is {} trainer?
        _firstSeviiPresentation!.Document.Portraits.Single(p=>p.TrainerPic==trainer.TrainerPic).AssetId:null;
    private FirstSeviiPresentationTrainer? FirstSeviiDoubleTrainer=>FirstSeviiTrainer(_session.State.DoubleBattle?.Trainer.Value);
    private Texture2D? FirstSeviiDoublePortrait=>FirstSeviiDoubleTrainer is {} trainer?
        _forestTrainerPortraits[_firstSeviiPresentation!.Document.Portraits.Single(p=>p.TrainerPic==trainer.TrainerPic).AssetId]:null;
    private void SynchronizeFirstSeviiPresentation(CampaignState? before,CampaignState after)
    {
        if(after.Seagallop is not {} voyage){_seagallopClock=null;_seagallopId=null;return;}
        if(_seagallopId==voyage.PresentationId)return;
        if(_firstSeviiPresentation is null||before is null||before.World.Player!=after.World.Player)
            throw new InvalidDataException("First-Sevii ferry presentation context differs.");
        _seagallopId=voyage.PresentationId;_seagallopClock=new(voyage.Origin,voyage.Destination);_seagallopRemainder=0;
        _held=null;_repeat=0;_tracks.Clear();_fade=0;
        Write("seagallopBegin",new{id=_seagallopId,voyage.Origin,voyage.Destination,voyage.To,
            clock=SeagallopSourcePresentation.FramesPerSecond,audioEnabled=_sourceAudio is not null,audioFaulted=_audioFaulted,physicalAudioVerified=false});
    }
    private bool ProcessFirstSeviiPresentation(double delta)
    {
        if(_session.State.Seagallop is null)return false;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        var clock=_seagallopClock??throw new InvalidDataException("Ferry clock missing.");
        _held=null;_repeat=0;_seagallopRemainder+=delta*SeagallopSourcePresentation.FramesPerSecond;
        while(_seagallopRemainder>=1&&_session.State.Seagallop is not null)
        {
            _seagallopRemainder-=1;
            bool audioBypassed=_sourceAudio is null||_audioFaulted;
            var music=_sourceAudio?.Snapshot().Music;
            bool stopped=audioBypassed||music is null||!music.Playing;
            var cue=clock.Advance(dmaReady:true,musicStopped:stopped);
            if(cue.Ship)SourceSound("SE_SHIP");
            if(cue.FadeMusic)
            {
                if(_sourceAudio is SynthSourceAudioPlayer synth)synth.BeginSeagallopMusicFade();
                else if(_sourceAudio is not null&&!_audioFaulted)throw new InvalidDataException("First-Sevii ferry requires source-tick synthesized audio.");
                _audioMusic=null;
                Write("seagallopFade",new{id=_seagallopId,clock.Tick,clock.TravelTicks,sourceFadeSpeed=4,waitsForActualBackendStop=true,audioBypassed});
            }
            if(clock.Phase==SeagallopSourcePresentation.Stage.Complete&&!_busy)
            {
                string id=_seagallopId!;SourceSound("SE_EXIT");
                Write("seagallopReady",new{id,clock.Tick,clock.TravelTicks,clock.BackgroundFade,clock.ObjectFade,clock.PaletteActive,musicStopped=stopped,audioBypassed});
                Apply(new("seagallop_ack",Text:id));
                if(_session.State.Seagallop is not null)throw new InvalidDataException("Correlated ferry acknowledgement rejected.");
            }
        }
        QueueRedraw();return true;
    }
    private Texture2D SeagallopTexture(string id,int fade)
    {
        if(_seagallopTextures.TryGetValue((id,fade),out var texture))return texture;
        var art=_firstSeviiPresentation??throw new InvalidDataException("Ferry resources missing.");var asset=art.Asset(id);byte[] rgba=NativeImages.ReadPixels(art.ReadPng(id));
        for(int i=0;i<rgba.Length;i+=4)for(int c=0;c<3;c++)
        {int value=rgba[i+c]>>3;value+=(-value*fade)>>4;rgba[i+c]=(byte)((value<<3)|(value>>2));}
        using var image=Image.CreateFromData(asset.Width,asset.Height,false,Image.Format.Rgba8,rgba);
        texture=ImageTexture.CreateFromImage(image);_seagallopTextures.Add((id,fade),texture);return texture;
    }
    private bool DrawFirstSeviiFerry()
    {
        if(_session.State.Seagallop is null)return false;
        var clock=_seagallopClock??throw new InvalidDataException("Ferry clock missing.");
        DrawRect(new(0,0,240,160),Colors.Black);
        if(clock.Phase==SeagallopSourcePresentation.Stage.Setup)return true;
        var water=SeagallopTexture("seagallop-water-"+(clock.Eastbound?"eb":"wb"),clock.BackgroundFade);
        int first=Math.Min(240,256-clock.WaterX);
        DrawTextureRectRegion(water,new(0,24,first,112),new(clock.WaterX,24,first,112));
        if(first<240)DrawTextureRectRegion(water,new(first,24,240-first,112),new(0,24,240-first,112));
        // Source subpriority8 wake lies behind the subpriority0 ferry.
        var wake=SeagallopTexture("seagallop-wake",clock.ObjectFade);
        foreach(var row in clock.Wakes.Reverse())
            DrawTextureRectRegion(wake,new(clock.Eastbound?row.X+16:row.X-16,76,clock.Eastbound?-32:32,32),new(0,row.Frame*32,32,32));
        if(clock.FerryVisible)
            DrawTextureRect(SeagallopTexture("seagallop-ferry_sprite",clock.ObjectFade),new(clock.Eastbound?clock.FerryX+32:clock.FerryX-32,60,clock.Eastbound?-64:64,64),false);
        return true;
    }
    private void DisposeFirstSeviiPresentation()
    {foreach(var texture in _seagallopTextures.Values)texture.Dispose();_seagallopTextures.Clear();_seagallopClock=null;_firstSeviiPresentation=null;}
}
