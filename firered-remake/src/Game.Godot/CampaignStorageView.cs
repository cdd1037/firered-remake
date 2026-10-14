using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Godot;

// Storage commands own all Pokemon mutations. This adapter only tracks cursor/popup state.
public partial class CampaignView
{
    private readonly Dictionary<SpeciesId,Texture2D> _storageIcons=[];
    private readonly Dictionary<string,Texture2D> _storageAssets=new(StringComparer.Ordinal);
    private void LoadStoragePresentation()
    {
        if(!_catalog.PokemonStorageEnabled)return;
        var source=PretStorageArt.Load(_root);
        Texture2D Decode(byte[] png)
        {
            using var image=new Image();if(image.LoadPngFromBuffer(png)!=Error.Ok)throw new InvalidDataException("Invalid authenticated storage PNG.");
            image.Convert(Image.Format.Rgba8);var rgba=image.GetData();
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)for(int c=0;c<3;c++){int v=rgba[i+c]>>3;rgba[i+c]=(byte)((v<<3)|(v>>2));}
            using var display=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,rgba);return ImageTexture.CreateFromImage(display);
        }
        foreach(var icon in source.Icons)_storageIcons.Add(new SpeciesId(icon.SpeciesId),Decode(source.ReadIconTexture(new(icon.SpeciesId))));
        if(_catalog.Route22Pokemon is {} route22)
            foreach(var icon in route22.Icons)_storageIcons.Add(new SpeciesId(icon.SpeciesId),Decode(route22.ReadIconTexture(new(icon.SpeciesId))));
        if(_catalog.Route3Pokemon is {} route3)
            foreach(var icon in route3.Icons)_storageIcons.Add(new SpeciesId(icon.SpeciesId),Decode(route3.ReadIconTexture(new(icon.SpeciesId))));
        foreach(var id in new[]{"box-background","box-arrow-left","box-arrow-right"})_storageAssets.Add(id,Decode(source.ReadAssetTexture(id)));
    }
    private CampaignPcView? _pcView;
    private int _pcCursor, _boxCursor;
    private string? _pcNotice;
    private bool _storagePartyPeek;
    private string PcText(string symbol,string? name=null)=>string.Join('\n',_catalog.TextPages(symbol))
        .Replace("{PLAYER}",_session.State.PlayerName,StringComparison.Ordinal)
        .Replace("{DYNAMIC 0x00}",name??"",StringComparison.Ordinal);
    private void SynchronizeStorage()
    {
        var view=_session.State.Pc?.View;
        if(view!=_pcView)
        {
            _pcView=view;_pcCursor=view==CampaignPcView.StorageMenu?_session.State.Pc!.StorageMenuSelection:0;
            _boxCursor=0;_pcNotice=null;_storagePartyPeek=false;
        }
        if(view is not null){_held=null;_repeat=0;}
        if(view==CampaignPcView.Deposit)_pcCursor=Math.Clamp(_pcCursor,0,_session.State.Party.Length);
    }
    private void StorageAction(CampaignCommand command,string? successSymbol=null,string? name=null)
    {
        var before=_session.Revision;Apply(command);
        if(_session.Revision!=before){if(successSymbol is not null)_pcNotice=PcText(successSymbol,name);return;}
        _pcNotice=StorageError(_status);
    }
    private string StorageError(string error)
    {
        if(error.Contains("storage_party_full",StringComparison.Ordinal))return PcText("gText_YourPartysFull");
        if(error.Contains("storage_box_full",StringComparison.Ordinal))return PcText("gText_BoxIsFull2");
        if(error.Contains("storage_last_usable",StringComparison.Ordinal))return PcText("gText_ThatsYourLastPkmn");
        if(error.Contains("mail",StringComparison.Ordinal))return PcText("gText_PleaseRemoveTheMail");
        if(error.Contains("not_implemented",StringComparison.Ordinal))return "This PC option is not available yet.\nChoose another option or go back.";
        if(error.Contains("storage_slot_empty",StringComparison.Ordinal))return "There is no POKéMON in this slot.";
        return "This action could not be completed.\nChoose another option or go back.";
    }
    private bool StorageInput(InputEventKey key)
    {
        if(key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape)return false;
        if(!key.Pressed)return true;
        if(_storagePartyPeek){if(key.Keycode is Key.Enter or Key.Backspace)_storagePartyPeek=false;return true;}
        if(_pcNotice is not null){if(key.Keycode is Key.Enter or Key.Backspace)_pcNotice=null;return true;}
        var pc=_session.State.Pc!;
        if(key.Keycode==Key.Backspace){StorageAction(new("pc_back"));return true;}
        if(pc.View is CampaignPcView.Main or CampaignPcView.StorageMenu)
        {
            int count=pc.View==CampaignPcView.Main?_session.PcMenuTextSymbols.Length:5;
            bool wrap=pc.View==CampaignPcView.Main&&count>3;
            if(key.Keycode==Key.Up)_pcCursor=wrap?(_pcCursor+count-1)%count:Math.Max(0,_pcCursor-1);
            if(key.Keycode==Key.Down)_pcCursor=wrap?(_pcCursor+1)%count:Math.Min(count-1,_pcCursor+1);
            if(key.Keycode==Key.Enter)
            {
                if(pc.View==CampaignPcView.StorageMenu&&_pcCursor==0&&_session.State.Party.Length==6){_pcNotice=PcText("gText_PartyFull");return true;}
                if(pc.View==CampaignPcView.StorageMenu&&_pcCursor==1&&_session.State.Party.Length==1){_pcNotice=PcText("gText_JustOnePkmn");return true;}
                StorageAction(new(pc.View==CampaignPcView.Main?"pc_select":"storage_select",Choice:_pcCursor));
            }
            return true;
        }
        if(key.Keycode is Key.Pageup or Key.Pagedown)
        {int box=(_session.State.Storage!.CurrentBox+(key.Keycode==Key.Pagedown?1:13))%14;StorageAction(new("storage_box",Choice:box));return true;}
        if(pc.View==CampaignPcView.Deposit)
        {
            if(key.Keycode==Key.Up)_pcCursor=Math.Max(0,_pcCursor-1);
            if(key.Keycode==Key.Down)_pcCursor=Math.Min(_session.State.Party.Length,_pcCursor+1);
            if(key.Keycode==Key.Enter)
            {
                if(_pcCursor==_session.State.Party.Length)StorageAction(new("pc_back"));
                else{var name=PresentedOwnedName(_session.State.Party[_pcCursor]);StorageAction(new("storage_deposit",PartyIndex:_pcCursor),"gText_PkmnWasDeposited",name);}
            }
        }
        else if(pc.View==CampaignPcView.Withdraw)
        {
            if(key.Keycode==Key.Left)_boxCursor=_boxCursor%6==0?_boxCursor+5:_boxCursor-1;
            if(key.Keycode==Key.Right)_boxCursor=_boxCursor%6==5?_boxCursor-5:_boxCursor+1;
            if(key.Keycode==Key.Up)_boxCursor=(_boxCursor+24)%30;
            if(key.Keycode==Key.Down)_boxCursor=(_boxCursor+6)%30;
            if(key.Keycode==Key.Enter)
            {var owned=_session.State.Storage!.Boxes[_session.State.Storage.CurrentBox].Slots[_boxCursor];if(owned is not null)StorageAction(new("storage_withdraw",MoveSlot:_boxCursor),"gText_PkmnCameBack",PresentedOwnedName(owned));}
        }
        return true;
    }
    private bool MouseStorage(Vector2 point)
    {
        if(_storagePartyPeek)return new Rect2(0,140,80,20).HasPoint(point)&&MouseKey(Key.Backspace);
        if(_pcNotice is not null)return MouseConfirm(point,MouseMessage);
        var pc=_session.State.Pc!;
        if(pc.View is CampaignPcView.Main or CampaignPcView.StorageMenu)
            return MouseSelect(point,ref _pcCursor,pc.View==CampaignPcView.Main?_session.PcMenuTextSymbols.Length:5,8,8,184,16);
        if(pc.View==CampaignPcView.Withdraw&&new Rect2(80,0,72,16).HasPoint(point)){_storagePartyPeek=true;_menuMouseActionFrame=Engine.GetProcessFrames();return true;}
        if(pc.View==CampaignPcView.Withdraw&&new Rect2(168,0,72,16).HasPoint(point))return MouseKey(Key.Backspace);
        if(new Rect2(pc.View==CampaignPcView.Withdraw?84:0,pc.View==CampaignPcView.Withdraw?16:0,24,24).HasPoint(point))return MouseKey(Key.Pageup);
        if(new Rect2(216,pc.View==CampaignPcView.Withdraw?16:0,24,24).HasPoint(point))return MouseKey(Key.Pagedown);
        if(new Rect2(pc.View==CampaignPcView.Withdraw?0:176,140,64,20).HasPoint(point))return MouseKey(Key.Backspace);
        if(pc.View==CampaignPcView.Deposit)return MouseSelect(point,ref _pcCursor,_session.State.Party.Length+1,8,24,224,16);
        if(pc.View==CampaignPcView.Withdraw&&new Rect2(88,32,144,120).HasPoint(point))
        {_boxCursor=(int)((point.X-88)/24)+6*(int)((point.Y-32)/24);return MouseKey();}
        return false;
    }
    private void DrawStorage()
    {
        var pc=_session.State.Pc!;if(pc.View==CampaignPcView.PlayerItems)return;
        if(pc.View is CampaignPcView.Main or CampaignPcView.StorageMenu)
        {
            var labels=pc.View==CampaignPcView.Main?_session.PcMenuTextSymbols.ToArray():_catalog.PokemonStorageSource!.StorageMenu.Select(o=>o.TextSymbol).ToArray();
            _battleSkin.DrawUserFrame(this,new(0,0,200,labels.Length*16+16));
            for(int i=0;i<labels.Length;i++)SourceText(_sourceNormal,new(16,8+i*16),PcText(labels[i]));
            SourceText(_sourceNormal,new(6,8+_pcCursor*16),"▶");
            string text=pc.View==CampaignPcView.Main?PcText("Text_AccessWhichPC"):PcText(_catalog.PokemonStorageSource!.StorageMenu[_pcCursor].DescriptionSymbol);
            ItemMessageBox(_pcNotice??text);return;
        }
        _battleSkin.DrawStandardFrame(this,new(0,0,240,160));
        var storage=_session.State.Storage!;var slots=storage.Boxes[storage.CurrentBox].Slots;
        if(pc.View==CampaignPcView.Deposit)
        {DrawTexture(_storageAssets["box-arrow-left"],new(8,4));SourceText(_sourceNormal,new(92,5),"BOX"+(storage.CurrentBox+1));DrawTexture(_storageAssets["box-arrow-right"],new(220,4));SourceText(_sourceSmall,new(8,144),"PgUp/PgDn: BOX");SourceText(_sourceNormal,new(184,142),"CANCEL");}
        if(pc.View==CampaignPcView.Deposit)
        {
            for(int i=0;i<_session.State.Party.Length;i++)
            {
                var owned=_session.State.Party[i];
                DrawTextureRectRegion(PresentedOwnedIcon(owned),new(18,24+i*16,16,16),new(0,0,32,32));
                SourceText(_sourceNormal,new(36,24+i*16),PresentedOwnedName(owned));
                if(!owned.IsEgg){SourceText(_sourceSmall,new(114,26+i*16),$"Lv{owned.Pokemon.Level}   {owned.Pokemon.CurrentHp}/{owned.Pokemon.Stats.MaxHp}");DrawForestPartyStatus(owned,new(208,26+i*16));}
            }
            SourceText(_sourceNormal,new(18,24+_session.State.Party.Length*16),"CANCEL");SourceText(_sourceNormal,new(6,24+_pcCursor*16),"▶");
        }
        else
        {
            DrawTexture(_storageAssets["box-background"],Vector2.Zero);
            DrawTexture(_storageAssets["box-arrow-left"],new(88,20));DrawTexture(_storageAssets["box-arrow-right"],new(224,20));
            string boxName="BOX"+(storage.CurrentBox+1);float width=_sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(boxName),fontSize:_sourceNormal.Size).X;
            SourceText(_sourceNormal,new(160-(int)width/2,20),boxName);
            for(int i=0;i<30;i++)
            {
                var rect=new Rect2(88+(i%6)*24,32+(i/6)*24,24,24);
                if(i==_boxCursor)DrawRect(rect,new Color("f4db88"),filled:false,width:1);
                var owned=slots[i];if(owned is not null)DrawTexture(PresentedOwnedIcon(owned),new(84+(i%6)*24,28+(i/6)*24));
            }
            var selected=slots[_boxCursor];
            if(selected is not null)
            {
                if(selected.IsEgg) DrawTexture(BreedingEggShell(),new(8,16));
                else if(_battleArt.TryGetValue((PresentedRenderSpecies(selected.Pokemon),"front",selected.Pokemon.IsShiny),out var art))DrawTexture(art.Texture,new(8,16));
                SourceText(_sourceSmall,new(6,88),PresentedOwnedName(selected));
                if(!selected.IsEgg){SourceText(_sourceSmall,new(6,102),_catalog.PokemonContent!.SpeciesNames[selected.Pokemon.SourceSpeciesId]);SourceText(_sourceSmall,new(10,116),$"Lv{selected.Pokemon.Level}");}
            }
            SourceText(_sourceSmall,new(6,132),$"{slots.Count(p=>p is not null)}/30 P{_session.State.Party.Length}");
            SourceText(_sourceSmall,new(6,146),"BACK");
        }
        if(_storagePartyPeek)
        {
            _battleSkin.DrawStandardFrame(this,new(0,0,240,160));
            for(int i=0;i<_session.State.Party.Length;i++)
            {
                var mon=_session.State.Party[i];
                DrawTextureRectRegion(PresentedOwnedIcon(mon),new(12,10+i*20,16,16),new(0,0,32,32));
                SourceText(_sourceNormal,new(30,10+i*20),PresentedOwnedName(mon));
                if(!mon.IsEgg){SourceText(_sourceSmall,new(110,12+i*20),$"Lv{mon.Pokemon.Level}  {mon.Pokemon.CurrentHp}/{mon.Pokemon.Stats.MaxHp}");DrawForestPartyStatus(mon,new(210,12+i*20));}
            }
            SourceText(_sourceNormal,new(8,142),"CANCEL");
        }
        if(_pcNotice is not null)ItemMessageBox(_pcNotice);
    }
}
