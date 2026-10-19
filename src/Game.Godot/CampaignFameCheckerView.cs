using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Options;
using Game.Domain.World;
using Godot;

/// <summary>Read-only source viewer. Cursors, printer and source-layer animation never mutate collected facts.</summary>
public partial class CampaignView
{
    private enum FameMode { List, Facts, Pick }
    private FameCheckerPresentationData? _fameArt;
    private readonly Dictionary<string, Texture2D> _fameTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<(int,int), SourceGameFont> _fameFonts = [];
    private readonly FameCheckerTextPresentation _famePrinter = new();
    private readonly Dictionary<SourceGameFont, ImageTexture> _fameFontAtlases = [];
    private ImmutableArray<FameCheckerPersonView> _famePeople = [];
    private ImmutableArray<FameCheckerTextPage> _famePages = [];
    private CampaignFameChecker? _fameOwner;
    private FameMode _fameMode;
    private int _famePerson, _fameTop, _fameFact, _fameFrame, _fameOffset, _fameSlide, _fameInfo = 4, _fameInfoFrames;
    private bool _fameRestored, _fameInfoOpen, _fameTextHeld, _fameTextPress;
    private Key? _fameHeldDirection;
    private int _fameRepeat;
    private double _fameTime;
    private ulong _fameClosedFrame = ulong.MaxValue;
    private Texture2D? _fameBase, _fameOverlay;
    private CampaignResourceCache.Owner? _fameImages;
    private string? _fameBackgroundKey;
    private bool FameCheckerActive => _session?.State.FameChecker is not null;
    private FameCheckerPersonView? FamePerson => _famePerson < _famePeople.Length ? _famePeople[_famePerson] : null;
    private bool FamePickLayer => _fameMode == FameMode.Pick || _fameSlide != 0;
    private FameCheckerFact? FameFact => FamePerson?.Facts[_fameFact].Fact;

    private Texture2D FameTexture(FameCheckerTexture texture)
    {
        if (_fameTextures.TryGetValue(texture.Id, out var result)) return result;
        _fameImages ??= _mapResources.CreateOwner("fame-checker");
        var r = texture.Region;
        result = _fameImages.NativeTexture(System.IO.Path.Combine(_root, texture.TexturePath), new Rect2I(r.X,r.Y,r.Width,r.Height));
        _fameTextures.Add(texture.Id, result);
        return result;
    }
    private SourceGameFont FameFont(int color = 2, int shadow = 3)
    {
        if (_fameFonts.TryGetValue((color,shadow), out var font)) return font;
        Color C(int i) { var p=_fameArt!.Palettes.GetProperty("text")[i]; return Color.Color8(p[0].GetByte(),p[1].GetByte(),p[2].GetByte()); }
        font = SourceGameFont.Load(System.IO.Path.Combine(_root,"content/generated/ui/font"),foreground:C(color),shadow:C(shadow));
        _fameFonts.Add((color,shadow),font); return font;
    }
    private string FamePlain(FameCheckerText text) => FameCheckerTextPages.Plain(text,_session.State.PlayerName,_session.State.RivalName);
    private void SynchronizeFameChecker()
    {
        var owner = _session.State.FameChecker;
        if (ReferenceEquals(owner,_fameOwner)) return;
        _fameOwner=owner; _fameHeldDirection=null; _fameTextHeld=_fameTextPress=false; _held=null; _repeat=0;
        if (owner is null) { _famePages=[]; _famePrinter.Reset([],4); return; }
        _fameArt ??= PretFameCheckerPresentation.Load(_root);
        _famePeople=_session.FameCheckerPeople; _fameMode=FameMode.List;
        _famePerson=_fameTop=_fameFact=_fameFrame=_fameOffset=_fameSlide=_fameInfoFrames=0;
        _fameInfo=4;_fameRestored=_fameInfoOpen=false;_fameTime=0;_fameBackgroundKey=null;_famePages=[];_famePrinter.Reset([],4);
        if (_famePeople.IsEmpty) SelectFamePerson();
    }
    private void SetFameText(FameCheckerText? text,bool immediate=false)
    {
        _famePages=text is null?[]:FameCheckerTextPages.Parse(text.EncodedText.EndsWith('$')?text:text with {EncodedText=text.EncodedText+"$"},_session.State.PlayerName,_session.State.RivalName);
        ResetFamePrinter(immediate);
    }
    private void ResetFamePrinter(bool immediate=false)
    {
        _fameTextHeld=_fameTextPress=false;
        _famePrinter.Reset(_famePages,
            _session.EffectiveOptions.TextSpeed switch {SourceTextSpeed.Slow=>8,SourceTextSpeed.Mid=>4,_=>1},immediate);
    }
    private void SetFameInfo(bool opened)
    {
        if (_fameInfoOpen==opened)return;
        _fameInfoOpen=opened;_fameInfoFrames=8;
    }
    private void SelectFamePerson()
    {
        _fameTop=Math.Clamp(_fameTop,0,Math.Max(0,_famePeople.Length+1-5));
        if(_famePerson<_fameTop)_fameTop=_famePerson;
        if(_famePerson>=_fameTop+5)_fameTop=_famePerson-4;
        if(FamePerson is not {} person) SetFameText(_catalog.FameChecker!.UiTexts.Single(t=>t.Symbol=="gFameCheckerText_FameCheckerWillBeClosed"),immediate:true);
        else SetFameText(_fameMode==FameMode.Pick?person.PickText:null);
    }
    private void MoveFamePerson(int delta)
    {
        int next=Math.Clamp(_famePerson+delta,0,_famePeople.Length);if(next==_famePerson)return;
        _famePerson=next;SourceSound("SE_SELECT");SelectFamePerson();
    }
    private void SelectFameFact()
    {
        SetFameText(FameFact?.Text);SetFameInfo(FameFact is not null);
    }
    private void StartFamePick(bool enter)
    {
        if(enter&&FamePerson is null)return;
        SourceSound("SE_M_LOCK_ON");_fameSlide=enter?1:-1;_fameHeldDirection=null;SetFameText(null);
        if(enter){_fameInfo=5;_fameInfoFrames=0;_fameInfoOpen=false;}
    }
    private void CloseFameChecker()
    {
        SourceSound("SE_M_SWIFT");_fameClosedFrame=Engine.GetProcessFrames();
        Apply(new("fame_checker_close"));
    }
    private bool ProcessFameChecker(double delta)
    {
        if(!FameCheckerActive)return false;
        _fameTime+=Math.Max(0,Math.Min(delta,.25))*60;
        int frames=(int)_fameTime;_fameTime-=frames;
        for(int i=0;i<frames;i++)
        {
            _fameFrame++;
            if(_fameSlide!=0)
            {
                _fameOffset=Math.Clamp(_fameOffset+_fameSlide*10,0,240);
                if(_fameOffset is 0 or 240)
                {
                    bool entered=_fameSlide>0;_fameMode=entered?FameMode.Pick:FameMode.List;_fameSlide=0;
                    if(!entered){_fameRestored=true;_fameInfo=4;}
                    SelectFamePerson();
                }
                continue;
            }
            if(_fameInfoFrames>0)
            {
                _fameInfoFrames--;if(_fameInfoFrames==4)_fameInfo=0;
                if(_fameInfoFrames==0)_fameInfo=_fameInfoOpen?1:4;
            }
            if(_famePrinter.AdvanceFrame(_fameTextHeld,_fameTextPress))SourceSound("SE_SELECT");
            _fameTextPress=false;
            if(_fameHeldDirection is {} held&&_fameMode!=FameMode.Facts&&_fameInfoFrames==0&&--_fameRepeat<=0)
            {MoveFamePerson(held==Key.Up?-1:1);_fameRepeat=5;}
        }
        QueueRedraw();return true;
    }
    private void FameKey(Key key,bool pressed)
    {
        bool a=key is Key.Enter or Key.Z,b=key is Key.Backspace or Key.X;
        if(a||b){_fameTextHeld=pressed;if(pressed)_fameTextPress=true;}
        if(!pressed){if(key==_fameHeldDirection)_fameHeldDirection=null;return;}
        if(_fameSlide!=0||(_fameMode!=FameMode.Facts&&_fameInfoFrames>0))return;
        // RunTextPrinters consumes page/scroll prompts before top-menu A handling.
        if(a&&!_famePages.IsEmpty)
        {
            if(!_famePrinter.Complete)return;
        }
        if(_fameMode==FameMode.Facts)
        {
            if(b){SourceSound("SE_SELECT");_fameMode=FameMode.List;SetFameText(null);SetFameInfo(false);return;}
            Direction? direction=key switch{Key.Up=>Direction.North,Key.Down=>Direction.South,Key.Left=>Direction.West,Key.Right=>Direction.East,_=>null};
            if(direction is {} d){_fameFact=FameCheckerRules.MoveFactSelection(_fameFact,d);SourceSound("SE_M_SWAGGER2");SelectFameFact();}
            else if(a&&FameFact is not null)SetFameText(FameFact.Text);
            return;
        }
        if(key==Key.R&&_fameMode!=FameMode.Pick&&_fameOwner?.Origin=="registered"){CloseFameChecker();return;}
        if(key==Key.Tab){StartFamePick(_fameMode!=FameMode.Pick);return;}
        if(b){if(_fameMode==FameMode.Pick)StartFamePick(false);else CloseFameChecker();return;}
        if(key is Key.Up or Key.Down){MoveFamePerson(key==Key.Up?-1:1);_fameHeldDirection=key;_fameRepeat=20;return;}
        if(a)
        {
            if(FamePerson is not {} person){CloseFameChecker();return;}
            if(_fameMode==FameMode.Pick){if(person.HasAllFacts)SetFameText(person.PickText);return;}
            if(person.HasAnyFacts){SourceSound("SE_SELECT");_fameMode=FameMode.Facts;SelectFameFact();}
        }
    }
    private bool TryFameCheckerInput(InputEvent input)
    {
        if(!FameCheckerActive)
        {
            if(_fameClosedFrame!=Engine.GetProcessFrames())return false;
            GetViewport().SetInputAsHandled();return true;
        }
        _held=null;_repeat=0;
        if(input is InputEventKey key&&!key.Echo)
        {
            if(key.Pressed&&key.Keycode == Key.Escape){RequestQuit();return true;}
            FameKey(key.Keycode,key.Pressed);
        }
        else if(input is InputEventJoypadButton joy)
        {
            Key keycode=joy.ButtonIndex switch{JoyButton.A=>Key.Enter,JoyButton.B=>Key.Backspace,JoyButton.Start=>Key.Tab,JoyButton.Back=>Key.R,JoyButton.DpadUp=>Key.Up,JoyButton.DpadDown=>Key.Down,JoyButton.DpadLeft=>Key.Left,JoyButton.DpadRight=>Key.Right,_=>Key.None};
            FameKey(keycode,joy.Pressed);
        }
        else if(input is InputEventMouseButton {Pressed:true} mouse)
        {
            if(mouse.ButtonIndex==MouseButton.Right)FameKey(Key.Backspace,true);
            else if(mouse.ButtonIndex==MouseButton.Left&&_fameSlide==0)
            {
                var point=mouse.Position;
                if(point.Y<16){FameKey(Key.Tab,true);}
                else if(point.X<80&&point.Y>=28&&point.Y<98&&_fameMode!=FameMode.Facts)
                {int row=(int)(point.Y-28)/14;int selected=_fameTop+row;if(selected<=_famePeople.Length){_famePerson=selected;SelectFamePerson();FameKey(Key.Enter,true);}}
                else if(_fameMode==FameMode.Facts&&point.Y>=18&&point.Y<79&&point.X>=98&&point.X<224)
                {int column=Math.Clamp((int)Math.Round((point.X-114)/47),0,2),row=point.Y>=50?1:0;_fameFact=row*3+column;SourceSound("SE_M_SWAGGER2");SelectFameFact();}
                else FameKey(Key.Enter,true);
                _fameTextHeld=false;
            }
        }
        QueueRedraw();GetViewport().SetInputAsHandled();return true;
    }
    private void UpdateFameBackground()
    {
        string key=$"{FamePickLayer}:{_fameRestored}:{_fameInfo}";if(_fameBackgroundKey==key)return;
        _fameBase=FameTexture(_fameArt!.BaseBackground(FamePickLayer,_fameRestored));
        _fameOverlay=FameTexture(_fameArt.Overlay(FamePickLayer?5:_fameInfo));_fameBackgroundKey=key;
    }
    private bool DrawFameChecker()
    {
        if(!FameCheckerActive)return false;
        if(_fameArt is null)throw new InvalidOperationException("fame_checker_presentation_not_loaded");
        UpdateFameBackground();DrawTexture(_fameBase!,Vector2.Zero);
        if(FamePerson is {} person&&_fameOffset<240)
        {
            foreach(var panel in person.Facts)
            {
                int x=114+47*(panel.Index%3),bottom=47+27*(panel.Index/3);
                if(panel.Fact is {} fact){var icon=_fameArt.FactIcon(fact.IconSymbol,_fameMode==FameMode.Facts&&panel.Index!=_fameFact);DrawTexture(FameTexture(icon),new(x-icon.Width/2,bottom-icon.Height));}
                else{var q=_fameArt.Textures["question-mark"];DrawTexture(FameTexture(q),new(x-q.Width/2,31+27*(panel.Index/3)-q.Height/2));}
            }
        }
        DrawTextureRectRegion(_fameOverlay!,new Rect2(0,0,240,160),new Rect2(_fameOffset,0,240,160));
        if(_fameMode==FameMode.Facts)
        {var cursor=_fameArt.CursorFrame(_fameFrame/15%2);DrawTexture(FameTexture(cursor),new(114+47*(_fameFact%3)-16,34+27*(_fameFact/3)-16));}
        if(FamePickLayer&&FamePerson is {} pick)
        {
            int slide=240-_fameOffset;
            DrawTexture(FameTexture(_fameArt.Portrait(pick.Person.SourceIndex,pick.PickState==FameCheckerPickState.Silhouette)),new(116+slide,34));
            DrawSetTransform(new Vector2(226+slide,66),Mathf.Tau*((_fameFrame*4)%256)/256f);
            DrawTexture(FameTexture(_fameArt.Textures["pokeball"]),new(-16,-16));DrawSetTransform(Vector2.Zero);
        }
        var normal=FameFont();
        for(int row=0;row<5&&_fameTop+row<=_famePeople.Length;row++)
        {
            int index=_fameTop+row;string name=index==_famePeople.Length?"CANCEL":FamePlain(_famePeople[index].Person.MenuName);
            SourceText(_fameMode==FameMode.Facts&&index==_famePerson?FameFont(6,7):normal,new(16,28+14*row),name);
        }
        SourceText(_fameMode==FameMode.Facts?_sourceInactiveCursor:normal,new(8,28+14*(_famePerson-_fameTop)),"▶");
        if(_fameMode!=FameMode.Facts)
        {
            var arrow=FameTexture(_fameArt.Textures["scroll-arrows"]);var offsets=_fameArt.Layout.GetProperty("scrollArrows");
            if(_fameTop>0){int dy=offsets.GetProperty("upOffsets")[_fameFrame%32].GetInt32();DrawTextureRectRegion(arrow,new(32,18+dy,16,16),new(0,16,16,16));}
            if(_fameTop+5<_famePeople.Length+1){int dy=offsets.GetProperty("downOffsets")[_fameFrame%32].GetInt32();DrawTextureRectRegion(arrow,new(32,108+dy,16,-16),new(0,16,16,16));}
        }
        string help=_fameMode==FameMode.Facts?"help-facts":FamePickLayer||FamePerson is not {HasAnyFacts:true}?"help-pick":"help-main";
        var strip=_fameArt.Textures[help];DrawTexture(FameTexture(strip),new(236-strip.Width,0));
        if(_fameInfoOpen&&_fameInfoFrames==0&&_fameMode==FameMode.Facts&&FameFact is {} selected)
        {
            void Center(string text,int y){float width=_sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText(text),fontSize:_sourceSmall.Size).X;SourceText(_sourceSmall,new(120+(84-width)/2,y),text);}
            Center(FamePlain(selected.OriginLocation),80);Center(FamePlain(selected.OriginObjectName),90);
        }
        _battleSkin.DrawUserFrame(this,new(8,112,224,48));
        if(!_famePages.IsEmpty)
        {
            var page=_famePages[_famePrinter.PageIndex];int visible=_famePrinter.VisibleLength;
            foreach(var line in page.Lines)foreach(var run in line.Runs)
            {
                int count=Math.Clamp(visible-run.CharacterStart,0,run.Text.Length);if(count==0)continue;
                var font=FameFont((int)run.Style.Foreground,(int)run.Style.Shadow);
                float x=font.Font.GetStringSize(font.PrepareText(line.Text[..run.Column]),fontSize:font.Size).X;
                DrawFameMessageText(font,new(16+x,121+line.LineIndex*FameCheckerTextPresentation.LineStep-_famePrinter.ScrollPixels),run.Text[..count]);
            }
            if(_famePrinter.WaitingForPrompt&&_famePrinter.PromptFrames>0)
            {
                var last=page.Lines[^1];
                float width=normal.Font.GetStringSize(normal.PrepareText(last.Text),fontSize:normal.Size).X;
                _textCursor.Draw(this,new(16+width,121+last.LineIndex*FameCheckerTextPresentation.LineStep),_famePrinter.PromptFrames-1);
            }
        }
        return true;
    }
    // The source scrolls the 208x32 window pixels. Clip glyphs, including a partly scrolled top row,
    // without a viewport or a new texture per frame. All admitted gender fonts share this atlas.
    private void DrawFameMessageText(SourceGameFont font,Vector2 position,string text)
    {
        var size=new Vector2I(font.Size,0);
        if(!_fameFontAtlases.TryGetValue(font,out var atlas))
        {
            using var image=font.Font.GetTextureImage(0,size,0);
            atlas=ImageTexture.CreateFromImage(image);_fameFontAtlases.Add(font,atlas);
        }
        var window=new Rect2(16,120,208,32);
        foreach(var glyph in font.PrepareText(text).EnumerateRunes())
        {
            var uv=font.Font.GetGlyphUVRect(0,size,glyph.Value);
            var destination=new Rect2(position,uv.Size);
            var clipped=destination.Intersection(window);
            if(clipped.HasArea())DrawTextureRectRegion(atlas,clipped,new Rect2(uv.Position+clipped.Position-position,clipped.Size));
            position.X+=font.Font.GetGlyphAdvance(0,font.Size,glyph.Value).X;
        }
    }
    private void DisposeFameCheckerPresentation()
    {
        _fameTextures.Clear();_fameImages?.Dispose();_fameImages=null;
        foreach(var atlas in _fameFontAtlases.Values)atlas.Dispose();_fameFontAtlases.Clear();
        foreach(var font in _fameFonts.Values)font.Dispose();_fameFonts.Clear();
        _fameBase=_fameOverlay=null;_fameArt=null;_fameOwner=null;
    }
}
