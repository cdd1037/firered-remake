using Game.Application;
using Game.Content;
using Godot;

public partial class CampaignView
{
    private PretTrainerCard? _trainerCard;
    private SourceGameFont? _trainerCardStatFont;
    private void DisposeTrainerCardPresentation(){_trainerCardStatFont?.Dispose();_trainerCardStatFont=null;}
    private readonly Dictionary<string,Texture2D> _trainerCardTextures=new(StringComparer.Ordinal);
    private readonly Dictionary<(int Species,int Tint),Texture2D> _trainerCardPhotoTextures=new();
    private int _trainerCardStars;
    private bool _trainerCardBack,_trainerCardFlipFromBack;
    private double _trainerCardFlipFrames=-1;
    private long _trainerCardFlashSequence=-1;
    private double _trainerCardFlashFrames=32;
    private void LoadTrainerCardPresentation()
    {
        _trainerCard=PretTrainerCard.Load(_root,_battleText);
        var fg=_trainerCard.Layout.StatTextRgb;var shadow=_trainerCard.Layout.StatShadowRgb;
        _trainerCardStatFont=SourceGameFont.Load(System.IO.Path.Combine(_root,"content/generated/ui/font"),
            foreground:Color.Color8(fg[0],fg[1],fg[2]),shadow:Color.Color8(shadow[0],shadow[1],shadow[2]));
        foreach(var asset in _trainerCard.Assets)
            _trainerCardTextures.Add(asset.Id,PresentationTexture(_trainerCard.ReadAssetTexture(asset.Id),asset.Width,asset.Height,replicatePalette:true));
    }
    private void OpenTrainerCard()
    {
        if(_trainerCard is null){_fieldMenuNotice=FieldMenuText.Get("notice.trainer-card-unavailable");return;}
        if(!CommitPlayTime(true)){_fieldMenuNotice=_status;return;}
        _trainerCardBack=false;_trainerCardFlipFrames=-1;_trainerCardStars=_session.TrainerCardStars;
        _fieldMenuMode="trainer-card";SourceSound("SE_CARD_OPEN");
    }
    private bool TrainerCardInput(InputEventKey key)
    {
        if(_fieldMenuMode!="trainer-card")return false;
        if(!key.Pressed || _trainerCardFlipFrames>=0)return true;
        if((!_trainerCardBack && key.Keycode==Key.Enter) || (_trainerCardBack && key.Keycode==Key.Backspace))
        {
            _trainerCardFlipFromBack=_trainerCardBack;_trainerCardFlipFrames=0;SourceSound("SE_CARD_FLIP");
        }
        else if(key.Keycode is Key.Enter or Key.Backspace)_fieldMenuMode="start";
        return true;
    }
    private void ProcessTrainerCardPresentation(double delta)
    {
        _trainerCardFlashFrames=Math.Min(32,_trainerCardFlashFrames+delta*60);
        if(_fieldMenuMode!="trainer-card" || _trainerCardFlipFrames<0)return;
        double before=_trainerCardFlipFrames;_trainerCardFlipFrames+=delta*60;
        if(before<20 && _trainerCardFlipFrames>=20){_trainerCardBack=!_trainerCardFlipFromBack;SourceSound("SE_CARD_FLIPPING");}
        if(_trainerCardFlipFrames>=36){_trainerCardFlipFrames=-1;SourceSound("SE_CARD_OPEN");}
    }
    private void SynchronizeTrainerCardPhoto(CampaignState? before,CampaignState after)
    {
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>_trainerCardFlashSequence &&
            e.Opcode=="dofieldeffect" && e.Args.SequenceEqual(new[]{"FLDEFF_PHOTO_FLASH"})))
        {_trainerCardFlashSequence=effect.Sequence;_trainerCardFlashFrames=0;}
    }
    private void DrawTrainerCardPhotoFlash()
    {
        // Source speed -1 means deltaY=3; BG and OBJ palettes update on alternating frames.
        if(_trainerCardFlashFrames<10)
            DrawRect(new(0,0,240,160),new Color(1,1,1,(float)Math.Max(0,(15-3*Math.Floor(_trainerCardFlashFrames/2))/16)));
    }
    private void DrawTrainerCardFlip(PretTrainerCard card,int gender,int stars)
    {
        int frame=(int)_trainerCardFlipFrames;
        bool rising=frame>=20;
        int top=rising?Math.Max(0,77-(frame-19)*5):Math.Min(77,(frame+1)*7);
        string side=card.BackgroundId(rising?!_trainerCardFlipFromBack:_trainerCardFlipFromBack,gender,stars);
        string suffix=(gender==0?"male":"female")+(stars==0?"":"-"+stars);
        DrawTexture(_trainerCardTextures["background-"+suffix],Vector2.Zero);
        var panel=_trainerCardTextures["panel-"+side];
        // Source unsigned 16.16 scanline deformation, BG0 only. Text, stars, badges and photos are hidden.
        int height=160-2*top;
        uint offset=unchecked((uint)(-top<<16));
        uint step=unchecked(0xa00000u/(uint)height+0xffff0000u);
        uint curvature=step/(uint)height;
        step=rising?step/2:step*2;
        int blend=(top+40)/10;if(blend<=4)blend=0;
        float shade=1-blend/16f;
        for(int y=top;y<160-top;y++)
        {
            int sourceY=(y+(int)(offset>>16))&255;
            if(sourceY<160)DrawTextureRectRegion(panel,new(0,y,240,1),new(0,sourceY,240,1),new Color(shade,shade,shade));
            offset=unchecked(offset+step);step=rising?unchecked(step+curvature):unchecked(step-curvature);
        }
    }
    private void DrawTrainerCard()
    {
        var card=_trainerCard!;var s=_session.State;var layout=card.Layout;int stars=_trainerCardStars;
        if(_trainerCardFlipFrames>=0){DrawTrainerCardFlip(card,s.PlayerGender,stars);return;}
        DrawTexture(_trainerCardTextures[card.BackgroundId(_trainerCardBack,s.PlayerGender,stars)],Vector2.Zero);
        if(_trainerCardBack){DrawTrainerCardBack(card);return;}
        var portrait=card.Asset(s.PlayerGender==0?"portrait-red":"portrait-leaf");DrawTexture(_trainerCardTextures[portrait.Id],new(portrait.X,portrait.Y));
        void Text(TrainerCardPoint at,string value)=>SourceText(_sourceNormal,new(at.X,at.Y),value);
        Text(layout.Name,card.Texts.Name+s.PlayerName);
        Text(layout.Id,card.Texts.Id+PretTrainerCard.FormatTrainerId(s.TrainerId));
        Text(layout.MoneyLabel,card.Texts.Money);Text(card.MoneyValuePosition((uint)s.Money),card.FormatMoney((uint)s.Money));
        if(s.Script.Flags.GetValueOrDefault(card.Rules.PokedexVisibleFlag))
        {
            var numbers=s.CaughtSpecies.Select(id=>_pokedexMenu!.Entries[id].NationalNumber);
            int count=PretTrainerCard.CountCaught(numbers,nationalEnabled:_session.NationalPokedexEnabled);
            Text(layout.PokedexLabel,card.Texts.Pokedex);Text(card.CaughtValuePosition(count),count.ToString());
        }
        Text(layout.TimeLabel,card.Texts.Time);Text(layout.TimeHours,s.PlayTime.Hours.ToString());Text(layout.TimeColon,card.Texts.Colon);Text(layout.TimeMinutes,s.PlayTime.Minutes.ToString("D2")+(s.PlayTime.SinceCheckpoint?"+":""));
        if(s.PlayTime.SinceCheckpoint)SourceText(_sourceSmall,new(8,146),CheckpointTimeNote);
        foreach(var badge in card.Badges)if(s.Script.Flags.GetValueOrDefault(badge.Flag))DrawTexture(_trainerCardTextures[badge.AssetId],new(badge.X,badge.Y));
        var star=card.Asset("star");for(int i=0;i<stars;i++)DrawTexture(_trainerCardTextures[star.Id],new(star.X+i*8,star.Y));
    }
    private void DrawTrainerCardBack(PretTrainerCard card)
    {
        var s=_session.State;
        SourceText(_sourceNormal,new(card.Layout.BackName.X,card.Layout.BackName.Y),s.PlayerName);
        if(s.HallOfFame?.FirstHallOfFamePlayTime is int debut && debut>0)
        {
            var row=card.Rules.BackRows.Single(r=>r.Id=="hallOfFame");
            SourceText(_sourceNormal,new(row.X,row.Y),card.Texts.HallOfFame);
            SourceText(_trainerCardStatFont!,new(row.ValueX,row.Y),PretTrainerCard.FormatHallOfFameTime(debut));
        }
        else if(s.HallOfFame is {FirstHallOfFamePlayTime:null})
            SourceText(_sourceSmall,new(18,43),FieldMenuText.Get("notice.hall-debut-time-unavailable"));
        for(int kind=0;kind<card.Rules.StickerVariables.Length;kind++)
        {
            int tier=s.Script.Variables.GetValueOrDefault(card.Rules.StickerVariables[kind]);
            if(tier==0)continue;
            var sticker=card.Asset("sticker-"+kind+"-"+tier);
            DrawTexture(_trainerCardTextures[sticker.Id],new(sticker.X,sticker.Y));
        }
        int tint=s.Script.Variables.GetValueOrDefault(CampaignSession.TrainerCardPhotoTint);
        for(int slot=0;slot<6;slot++)
        {
            int encoded=s.Script.Variables.GetValueOrDefault(CampaignSession.TrainerCardPhotoPrefix+(slot+1));
            if(encoded==0)continue;
            int render=encoded==412?412:PretMailPresentation.IconRenderSpecies((ushort)encoded)
                ??throw new InvalidDataException("trainer_card_photo_icon");
            if(!_trainerCardPhotoTextures.TryGetValue((render,tint),out var texture))
            {
                var source=render==412?BreedingEggIcon():MailSpeciesIcon(render)
                    ??throw new InvalidDataException("trainer_card_photo_icon_missing");
                using var image=source.GetImage();image.Convert(Image.Format.Rgba8);var rgba=image.GetData();
                for(int i=0;i<rgba.Length;i+=4)
                {
                    var color=PretTrainerCard.TintPhoto(rgba[i],rgba[i+1],rgba[i+2],tint);
                    rgba[i]=color.R;rgba[i+1]=color.G;rgba[i+2]=color.B;
                }
                using var converted=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,rgba);
                texture=SceneTextures.FromImage(converted);_trainerCardPhotoTextures.Add((render,tint),texture);
            }
            DrawTextureRectRegion(texture,new(card.Rules.PhotoX+slot*card.Rules.PhotoSpacing,card.Rules.PhotoY,32,32),new(0,0,32,32));
        }
    }
}
