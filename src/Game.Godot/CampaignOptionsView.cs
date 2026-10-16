using Game.Application;
using Game.Content;
using Game.Domain.Options;
using Godot;

public partial class CampaignView
{
    private int _appliedOptionFrame=-1;
    private SourceGameFont _optionValueFont=null!;
    private void LoadOptionsPresentation()
    {
        _optionValueFont=SourceGameFont.Load(System.IO.Path.Combine(_root,"content/generated/ui/font"),foreground:Color.Color8(231,8,8),shadow:Color.Color8(255,189,115));
        var source=PretOptionFrames.Load(_root);
        foreach(var asset in source.Assets)
        {
            Rect2I? region=asset.Region is {} r?new Rect2I(r.X,r.Y,r.Width,r.Height):null;
            var texture=SceneTextures.NativeTexture(System.IO.Path.Combine(_root,asset.TexturePath),region);
            if(texture.GetWidth()!=asset.Width||texture.GetHeight()!=asset.Height)throw new InvalidDataException("Option frame dimensions differ.");
            _optionFrameTextures.Add((asset.FrameIndex,asset.Kind),texture);
        }
        SynchronizeSavedOptions();
    }
    private void DisposeOptionsPresentation()
    {
        _optionValueFont?.Dispose(); _optionValueFont=null!;
        _battleSkin?.SetUserWindowAssets(null);
        // NativeTexture assets (including atlas regions) belong to SceneTextures.
        _optionFrameTextures.Clear();
    }
    // Apply only committed preferences, never the in-menu draft. The same path is
    // used for startup, Continue/new game, load, and an accepted options_save.
    private void SynchronizeSavedOptions() => ApplySavedOptions(_session.EffectiveOptions);
    private void ApplySavedOptions(SourceGameOptions options)
    {
        ApplyOptionFrame(options.Frame);
        _sourceAudio?.SetStereo(options.Sound == Game.Domain.Options.SourceSound.Stereo);
    }
    private void ApplyOptionFrame(int frame)
    {
        if(frame==_appliedOptionFrame)return;
        _appliedOptionFrame=frame;
        if(frame==0){_battleSkin.SetUserWindowAssets(null);return;}
        _battleSkin.SetUserWindowAssets(new Dictionary<string,Texture2D>(StringComparer.Ordinal)
        {
            ["frame"]=_optionFrameTextures[(frame,"frame")],
            ["battle-message"]=_optionFrameTextures[(frame,"battle-message")],
            ["action-menu"]=_optionFrameTextures[(frame,"action-menu")],
            ["move-menu"]=_optionFrameTextures[(frame,"move-menu")]
        });
    }
    private SourceGameOptions _optionsDraft=SourceGameOptions.Defaults;
    private int _optionsCursor;
    private readonly Dictionary<(int Frame,string Kind),Texture2D> _optionFrameTextures=[];
    private static readonly string[] OptionLabels=["TEXT SPEED","BATTLE SCENE","BATTLE STYLE","SOUND","BUTTON MODE","FRAME","CANCEL"];
    private void OpenOptionsMenu()
    {
        if(!_session.CanEditOptions){_fieldMenuNotice="Options cannot be changed\nhere.";return;}
        _optionsDraft=_session.EffectiveOptions;_optionsCursor=0;_fieldMenuMode="options";
    }
    private bool OptionEditable(int row)=>CampaignOptionEditing.IsEditable(row,_optionFrameTextures.Count==40);
    private string OptionValue(int row)=>row switch
    {
        0=>_optionsDraft.TextSpeed.ToString().ToUpperInvariant(),1=>_optionsDraft.BattleScene.ToString().ToUpperInvariant(),
        2=>_optionsDraft.BattleStyle.ToString().ToUpperInvariant(),3=>_optionsDraft.Sound.ToString().ToUpperInvariant(),
        4=>_optionsDraft.ButtonMode switch{SourceButtonMode.Help=>"HELP",SourceButtonMode.Lr=>"LR",_=>"L=A"},
        5=>"TYPE "+(_optionsDraft.Frame+1),_=>""
    };
    private void ChangeOption(int direction)
    {
        if(_optionsCursor==6)return;
        if(!OptionEditable(_optionsCursor)){_fieldMenuNotice="This option's presentation\nis not available yet.";return;}
        _optionsDraft=CampaignOptionEditing.Change(_optionsDraft,_optionsCursor,direction,_optionFrameTextures.Count==40);
    }
    private bool OptionsInput(InputEventKey key)
    {
        if(_fieldMenuMode!="options")return false;
        if(!key.Pressed)return true;
        if(key.Keycode==Key.Up)_optionsCursor=(_optionsCursor+6)%7;
        if(key.Keycode==Key.Down)_optionsCursor=(_optionsCursor+1)%7;
        if(key.Keycode==Key.Left)ChangeOption(-1);
        if(key.Keycode==Key.Right)ChangeOption(1);
        // Original OptionMenu_ProcessInput closes and commits on either A or B from any row.
        if(key.Keycode is Key.Enter or Key.Backspace)
        {
           bool accepted = Apply(new CampaignCommand("options_save"){Options=_optionsDraft});
            if(accepted)_fieldMenuMode="start";
            else _fieldMenuNotice="Options could not be applied.\nReturn to a stable field.";
        }
        return true;
    }
    private bool MouseOptionsMenu(Vector2 point)
    {
        if(_fieldMenuMode!="options")return false;
        if(!new Rect2(16,58,208,91).HasPoint(point))return false;
        _optionsCursor=Math.Clamp((int)((point.Y-58)/13),0,6);
        if(_optionsCursor==6)return MouseKey();
        if(point.X>=216)return MouseKey(Key.Right);
        if(point.X>=124&&point.X<148)return MouseKey(Key.Left);
        // Clicking a label or value selects it without treating the same click as A/exit.
        _menuMouseActionFrame=Engine.GetProcessFrames();return true;
    }
    private void DrawOptionsMenu()
    {
        DrawRect(new(0,0,240,160),Color.Color8(0,123,189));
        _itemSkin.DrawStandardFrame(this,new(8,16,224,32));
        DrawOptionsPreview();
        DrawRect(new(16,56,208,96),new Color("dedede"));
        DrawRect(new(16,58+_optionsCursor*13,208,14),Colors.White);
        SourceText(_sourceNormal,new(24,25),"OPTION");
        for(int i=0;i<OptionLabels.Length;i++)
        {
            int y=58+i*13;
            SourceText(_sourceNormal,new(24,y),OptionLabels[i]);
            if(i<6)
            {
                string value=OptionValue(i);
                SourceText(_optionValueFont,new(146,y),value);
                if(OptionEditable(i)){DrawTextureRect(_storageAssets["box-arrow-left"],new(132,y+1,8,12),false);DrawTextureRect(_storageAssets["box-arrow-right"],new(216,y+1,8,12),false);}
            }
        }
        SourceText(_sourceSmallWhite,new(8,1),"UP/DOWN PICK  LEFT/RIGHT SWITCH  A/B BACK");
    }
    private void DrawOptionsPreview()
    {
        if(!_optionFrameTextures.TryGetValue((_optionsDraft.Frame,"frame"),out var texture)){_battleSkin.DrawStandardFrame(this,new(8,48,224,112));return;}
        for(int y=0;y<112;y+=8)for(int x=0;x<224;x+=8)
        {
            int sx=x==0?0:x==216?16:8,sy=y==0?0:y==104?16:8;
            DrawTextureRectRegion(texture,new Rect2(x+8,y+48,8,8),new Rect2(sx,sy,8,8));
        }
    }
}
