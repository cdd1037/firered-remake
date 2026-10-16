using Game.Content;
using Godot;

public partial class CampaignView
{
    private PretTrainerCard? _trainerCard;
    private readonly Dictionary<string,Texture2D> _trainerCardTextures=new(StringComparer.Ordinal);
    private void LoadTrainerCardPresentation()
    {
        _trainerCard=PretTrainerCard.Load(_root);
        foreach(var asset in _trainerCard.Assets)
        {
            using var image=new Image();if(image.LoadPngFromBuffer(_trainerCard.ReadAssetTexture(asset.Id))!=Error.Ok||image.GetWidth()!=asset.Width||image.GetHeight()!=asset.Height)throw new InvalidDataException("Trainer Card PNG differs.");
            image.Convert(Image.Format.Rgba8);var rgba=image.GetData();
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)for(int c=0;c<3;c++){int v=rgba[i+c]>>3;rgba[i+c]=(byte)((v<<3)|(v>>2));}
            using var display=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,rgba);_trainerCardTextures.Add(asset.Id,ImageTexture.CreateFromImage(display));
        }
    }
    private void OpenTrainerCard()
    {
        if(_trainerCard is null){_fieldMenuNotice="The TRAINER CARD cannot be\nopened here.";return;}
        if(!CommitPlayTime(true)){_fieldMenuNotice=_status;return;}
        _fieldMenuMode="trainer-card";
    }
    private bool TrainerCardInput(InputEventKey key)
    {
        if(_fieldMenuMode!="trainer-card")return false;
        if(key.Pressed&&key.Keycode is Key.Enter or Key.Backspace)_fieldMenuMode="start";
        return true;
    }
    private void DrawTrainerCard()
    {
        var card=_trainerCard!;var s=_session.State;var layout=card.Layout;
        DrawTexture(_trainerCardTextures[s.PlayerGender==0?"front-male":"front-female"],Vector2.Zero);
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
        // Authoritative source fields, including all three hour digits. The marker
        // distinguishes a checkpoint observation origin from whole-adventure time.
        Text(layout.TimeLabel,card.Texts.Time);Text(layout.TimeHours,s.PlayTime.Hours.ToString());Text(layout.TimeColon,card.Texts.Colon);Text(layout.TimeMinutes,s.PlayTime.Minutes.ToString("D2")+(s.PlayTime.SinceCheckpoint?"+":""));
        if(s.PlayTime.SinceCheckpoint)SourceText(_sourceSmall,new(8,146),CheckpointTimeNote);
        foreach(var badge in card.Badges)if(s.Script.Flags.GetValueOrDefault(badge.Flag))DrawTexture(_trainerCardTextures[badge.AssetId],new(badge.X,badge.Y));
        SourceText(_sourceSmall,new(190,146),"BACK");
    }
}
