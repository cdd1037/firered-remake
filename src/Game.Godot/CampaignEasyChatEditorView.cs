using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Godot;

public partial class CampaignView
{
    private string? _easyChatComposerTicket, _easyChatNotice;
    private SourceEasyChatEditPhase? _easyChatComposerPhase;
    private int _easyChatEditRow, _easyChatEditColumn, _easyChatBrowseMode, _easyChatGroupCursor, _easyChatWordCursor, _easyChatGroupId, _easyChatAlphaBucket;
    private int _easyChatGroupStart, _easyChatWordStart, _easyChatConfirm = 1;
    private bool _easyChatAlphaMode;
    private ImmutableArray<SourceEasyChatGroup> EasyChatGroups => SourceEasyChatRules.SelectableGroups(_catalog.MailComposer!, _session.EasyChatSelectionFacts);
    private ImmutableArray<SourceEasyChatWord> EasyChatWords => _easyChatAlphaMode
        ? SourceEasyChatRules.AlphabeticalWords(_catalog.MailComposer!, _session.EasyChatSelectionFacts, _easyChatAlphaBucket)
        : SourceEasyChatRules.SelectableWords(_catalog.MailComposer!, _session.EasyChatSelectionFacts, _easyChatGroupId);
    private SourceEasyChatTemplate EasyChatTemplate => _catalog.MailComposer!.Templates[_session.EasyChatEditor!.TemplateId];
    private EasyChatPresentationTemplate EasyChatLayout => MailSourceScreen.Content.Templates[_session.EasyChatEditor!.TemplateId];
    private int EasyChatEditSlot => _easyChatEditRow * EasyChatTemplate.Columns + _easyChatEditColumn;
    private static readonly int[][] EasyChatAlphabet = [[1,2,3,4,5,6,0], [7,8,9,10,11,12], [13,14,15,16,17,18,19], [20,21,22,23,24,25,26]];
    private static readonly int[] EasyChatAlphaOffsets = [0,12,24,56,68,80,92];
    private void SynchronizeEasyChatEditor()
    {
        var draft = _session.EasyChatEditor;
        if (draft?.Ticket != _easyChatComposerTicket)
        {
            _easyChatComposerTicket = draft?.Ticket; _easyChatNotice = null; _easyChatEditRow = _easyChatEditColumn = _easyChatBrowseMode = 0;
            _easyChatGroupCursor = _easyChatWordCursor = _easyChatGroupStart = _easyChatWordStart = 0; _easyChatAlphaMode = false;
        }
        if (_easyChatComposerPhase != draft?.Phase)
        { _easyChatComposerPhase = draft?.Phase; _easyChatConfirm = 1; _easyChatBrowseMode = 0; }
        if (draft is not null) { _held = null; _repeat = 0; }
    }
    private bool EasyChatAction(string kind, int? choice = null, int quantity = 0)
    {
        var draft = _session.EasyChatEditor ?? throw new InvalidOperationException("No Easy Chat editor is active.");
        string prefix = draft.Context == CampaignEasyChatContext.Mail ? "mail_compose_" : "profile_compose_";
        bool accepted = Apply(new(prefix + kind, Choice: choice, Quantity: quantity, Text: draft.WaitId));
        if (!accepted) _easyChatNotice = draft.Context == CampaignEasyChatContext.Mail ? MailError(_status) : "The profile couldn't be changed.";
        return accepted;
    }
    private void EasyChatChooseWord(ushort word)
    {
        bool accepted = EasyChatAction("word", EasyChatEditSlot, word);
        if (accepted) _easyChatBrowseMode = 0;
    }
    private bool EasyChatEditorInput(InputEventKey key)
    {
        if (!key.Pressed || key.Echo) return true;
        if (_easyChatNotice is not null)
        { if (key.Keycode is Key.Enter or Key.Backspace) _easyChatNotice = null; return true; }
        var draft = _session.EasyChatEditor!;
        if (draft.Phase != SourceEasyChatEditPhase.Edit)
        {
            if (key.Keycode == Key.Backspace) EasyChatAction("back");
            else if (key.Keycode is Key.Up or Key.Down) _easyChatConfirm = 1 - _easyChatConfirm;
            else if (key.Keycode == Key.Enter) EasyChatAction("confirm", _easyChatConfirm);
            return true;
        }
        if (_easyChatBrowseMode == 2) return EasyChatWordsInput(key);
        if (_easyChatBrowseMode == 1) return EasyChatGroupsInput(key);
        if (key.Keycode == Key.Backspace) { EasyChatAction("cancel"); return true; }
        if (key.Keycode == Key.Delete && _easyChatEditRow < EasyChatTemplate.Rows) { EasyChatChooseWord(ushort.MaxValue); return true; }
        if (key.Keycode == Key.End) { EasyChatAction("done"); return true; }
        (_easyChatEditRow, _easyChatEditColumn) = EasyChatEditorNavigation.Move(EasyChatTemplate,
            _easyChatEditRow, _easyChatEditColumn,
            key.Keycode == Key.Left ? -1 : key.Keycode == Key.Right ? 1 : 0,
            key.Keycode == Key.Up ? -1 : key.Keycode == Key.Down ? 1 : 0);
        if (key.Keycode == Key.Enter)
        {
            if (_easyChatEditRow == EasyChatTemplate.Rows) EasyChatAction(_easyChatEditColumn switch { 0 => "delete_all", 1 => "cancel", _ => "done" });
            else { _easyChatBrowseMode = 1; _easyChatGroupCursor = _easyChatGroupStart = 0; }
        }
        return true;
    }
    private bool EasyChatGroupsInput(InputEventKey key)
    {
        if (key.Keycode == Key.Backspace) { _easyChatBrowseMode = 0; return true; }
        if (key.Keycode == Key.Tab) { _easyChatAlphaMode = !_easyChatAlphaMode; _easyChatGroupCursor = _easyChatGroupStart = 0; return true; }
        int entries = _easyChatAlphaMode ? 27 : EasyChatGroups.Length;
        int count = entries + 3;
        if (_easyChatAlphaMode)
        {
            // A-Z keeps the original four keyboard rows; blue-box actions occupy the right edge.
            int cursor = Math.Min(_easyChatGroupCursor, count - 1);
            if (cursor >= 27)
            {
                if (key.Keycode == Key.Up) cursor = 27 + (cursor - 27 + 2) % 3;
                if (key.Keycode == Key.Down) cursor = 27 + (cursor - 27 + 1) % 3;
                if (key.Keycode == Key.Left) cursor = cursor == 27 ? 6 : cursor == 28 ? 12 : 19;
                if (key.Keycode == Key.Right) cursor = cursor == 27 ? 0 : cursor == 28 ? 7 : 13;
            }
            else
            {
                int row = cursor < 7 ? 0 : cursor < 13 ? 1 : cursor < 20 ? 2 : 3;
                int offset = row switch { 0 => 0, 1 => 7, 2 => 13, _ => 20 }, column = cursor - offset;
                if (key.Keycode is Key.Up or Key.Down)
                {
                    row = (row + (key.Keycode == Key.Up ? 3 : 1)) % 4;
                    column = Math.Min(column, EasyChatAlphabet[row].Length - 1);
                    cursor = (row switch { 0 => 0, 1 => 7, 2 => 13, _ => 20 }) + column;
                }
                if (key.Keycode == Key.Left) cursor = column == 0 ? 27 + Math.Min(row,2) : cursor - 1;
                if (key.Keycode == Key.Right) cursor = column + 1 == EasyChatAlphabet[row].Length ? 27 + Math.Min(row,2) : cursor + 1;
            }
            _easyChatGroupCursor = cursor;
        }
        else
        {
            if (_easyChatGroupCursor >= entries)
            {
                if (key.Keycode == Key.Up) _easyChatGroupCursor = entries + (_easyChatGroupCursor - entries + 2) % 3;
                if (key.Keycode == Key.Down) _easyChatGroupCursor = entries + (_easyChatGroupCursor - entries + 1) % 3;
                if (key.Keycode is Key.Left or Key.Right) _easyChatGroupCursor = Math.Min(entries - 1, _easyChatGroupStart * 2 + (_easyChatGroupCursor - entries) * 2 + (key.Keycode == Key.Left ? 1 : 0));
            }
            else
            {
                if (key.Keycode == Key.Up) _easyChatGroupCursor = Math.Max(0, _easyChatGroupCursor - 2);
                if (key.Keycode == Key.Down) _easyChatGroupCursor = Math.Min(entries - 1, _easyChatGroupCursor + 2);
                if (key.Keycode == Key.Left) _easyChatGroupCursor = _easyChatGroupCursor % 2 == 0 ? entries : _easyChatGroupCursor - 1;
                if (key.Keycode == Key.Right) _easyChatGroupCursor = _easyChatGroupCursor % 2 == 1 || _easyChatGroupCursor + 1 == entries ? entries : _easyChatGroupCursor + 1;
                if (key.Keycode is Key.Pageup or Key.Pagedown) _easyChatGroupCursor = Math.Clamp(_easyChatGroupCursor + (key.Keycode == Key.Pageup ? -8 : 8), 0, entries - 1);
                if (_easyChatGroupCursor < entries) _easyChatGroupStart = Math.Clamp(_easyChatGroupStart, Math.Max(0, _easyChatGroupCursor / 2 - 3), _easyChatGroupCursor / 2);
            }
        }
        if (key.Keycode != Key.Enter) return true;
        if (_easyChatGroupCursor == entries) { _easyChatAlphaMode = !_easyChatAlphaMode; _easyChatGroupCursor = _easyChatGroupStart = 0; }
        else if (_easyChatGroupCursor == entries + 1) EasyChatChooseWord(ushort.MaxValue);
        else if (_easyChatGroupCursor == entries + 2) _easyChatBrowseMode = 0;
        else
        {
            if (_easyChatAlphaMode) _easyChatAlphaBucket = EasyChatAlphabet.SelectMany(row => row).ElementAt(_easyChatGroupCursor);
            else _easyChatGroupId = EasyChatGroups[_easyChatGroupCursor].Id;
            if (EasyChatWords.Length > 0) { _easyChatBrowseMode = 2; _easyChatWordCursor = _easyChatWordStart = 0; }
        }
        return true;
    }
    private bool EasyChatWordsInput(InputEventKey key)
    {
        if (key.Keycode == Key.Backspace) { _easyChatBrowseMode = 1; return true; }
        var words = EasyChatWords;
        if (key.Keycode == Key.Up) _easyChatWordCursor = Math.Max(0, _easyChatWordCursor - 2);
        if (key.Keycode == Key.Down) _easyChatWordCursor = Math.Min(words.Length - 1, _easyChatWordCursor + 2);
        if (key.Keycode == Key.Left) _easyChatWordCursor = _easyChatWordCursor % 2 == 1 ? _easyChatWordCursor - 1 : Math.Min(words.Length - 1, _easyChatWordCursor + 1);
        if (key.Keycode == Key.Right) _easyChatWordCursor = _easyChatWordCursor % 2 == 0 ? Math.Min(words.Length - 1, _easyChatWordCursor + 1) : _easyChatWordCursor - 1;
        if (key.Keycode is Key.Pageup or Key.Pagedown) _easyChatWordCursor = Math.Clamp(_easyChatWordCursor + (key.Keycode == Key.Pageup ? -8 : 8), 0, words.Length - 1);
        _easyChatWordStart = Math.Clamp(_easyChatWordStart, Math.Max(0, _easyChatWordCursor / 2 - 3), _easyChatWordCursor / 2);
        if (key.Keycode == Key.Enter) EasyChatChooseWord(words[_easyChatWordCursor].Value);
        return true;
    }
    private bool DrawEasyChatEditorScreen()
    {
        var draft = _session.EasyChatEditor!; var screen = MailSourceScreen;
        DrawTexture(screen.Texture(EasyChatLayout.BackgroundIds[_easyChatBrowseMode == 2 ? "words" : _easyChatBrowseMode == 1 ? "groups" : "fields"]), Vector2.Zero);
        if (EasyChatTemplate.TitleTextSymbol is {} titleSymbol)
        {
            string title = _catalog.MailComposer!.Texts[titleSymbol];
            var font = screen.TitleFont;
            SourceMailScreen.Text(this, font, new(EasyChatLayout.TitleX + (EasyChatLayout.TitleWidth - SourceMailScreen.TextWidth(font, title)) / 2, EasyChatLayout.TitleY), title);
        }
        screen.DrawEditorFields(this, EasyChatLayout, EasyChatTemplate, draft.Words, _session.EasyChatWordText);
        if (_easyChatBrowseMode == 0)
        {
            foreach (var (label,x) in new[] { ("DEL. ALL",32), ("CANCEL",119), ("OK",196) }) SourceMailScreen.Text(this, screen.EditorFont, new(x,90), label);
            var center = _easyChatEditRow == EasyChatTemplate.Rows ? new Vector2(new[] {28,115,191}[_easyChatEditColumn],97)
                : screen.FieldPosition(EasyChatLayout, EasyChatTemplate, draft.Words, _session.EasyChatWordText, EasyChatEditSlot) + new Vector2(-4,7);
            screen.DrawTriangle(this, center);
            var symbols = draft.Phase switch {
                SourceEasyChatEditPhase.ConfirmDone => EasyChatTemplate.ConfirmTextSymbols,
                SourceEasyChatEditPhase.ConfirmCancel => EasyChatTemplate.CancelTextSymbols,
                SourceEasyChatEditPhase.ConfirmDelete => ["gText_AllTextBeingEditedWill", "gText_BeDeletedThatOkay"],
                _ => EasyChatTemplate.InstructionTextSymbols };
            _battleSkin.DrawStandardFrame(this, new(24,112,192,48));
            for (int i=0;i<symbols.Length;i++) SourceMailScreen.Text(this,screen.EditorFont,new(32,122+i*16),_catalog.MailComposer!.Texts[symbols[i]]);
            if (draft.Phase != SourceEasyChatEditPhase.Edit)
            {
                _battleSkin.DrawStandardFrame(this,new(168,64,56,48));
                SourceMailScreen.Text(this,screen.EditorFont,new(184,74),"YES");SourceMailScreen.Text(this,screen.EditorFont,new(184,90),"NO");
                screen.DrawTriangle(this,new(176,81+_easyChatConfirm*16));
            }
        }
        else if (_easyChatBrowseMode == 1) DrawEasyChatGroups();
        else DrawEasyChatWords();
        if (_easyChatNotice is not null) ItemMessageBox(_easyChatNotice);
        return true;
    }
    private void DrawEasyChatGroups()
    {
        var screen = MailSourceScreen; int entries = _easyChatAlphaMode ? 27 : EasyChatGroups.Length;
        if (_easyChatAlphaMode)
        {
            for (int row=0;row<4;row++) for (int column=0;column<EasyChatAlphabet[row].Length;column++)
            {
                int bucket=EasyChatAlphabet[row][column]; string text=bucket==0?"others":((char)('A'+bucket-1)).ToString();
                SourceMailScreen.Text(this,screen.EditorFont,new(bucket==0?141:29+EasyChatAlphaOffsets[column],90+row*16),text);
            }
        }
        else
        {
            var groups=EasyChatGroups;
            for(int i=_easyChatGroupStart*2;i<Math.Min(groups.Length,(_easyChatGroupStart+4)*2);i++)
                SourceMailScreen.Text(this,screen.EditorFont,new(18+i%2*84,90+(i/2-_easyChatGroupStart)*16),groups[i].Name);
        }
        DrawTexture(screen.Texture("easy-chat-button-window"),new(176,96));
        DrawTextureRectRegion(screen.Texture("easy-chat-mode"),new(176,64,64,32),new(0,_easyChatAlphaMode?0:32,64,32));
        Vector2 center; int frame;
        if(_easyChatGroupCursor>=entries){center=new(216,112+(_easyChatGroupCursor-entries)*16);frame=1;}
        else if(!_easyChatAlphaMode){center=new(58+_easyChatGroupCursor%2*84,96+(_easyChatGroupCursor/2-_easyChatGroupStart)*16);frame=0;}
        else
        {
            int row=_easyChatGroupCursor<7?0:_easyChatGroupCursor<13?1:_easyChatGroupCursor<20?2:3;
            int column=_easyChatGroupCursor-(row switch{0=>0,1=>7,2=>13,_=>20});
            bool others=row==0&&column==6;center=new(others?157:32+EasyChatAlphaOffsets[column],96+row*16);frame=others?2:3;
        }
        screen.DrawRectangle(this,center,frame);
        if(!_easyChatAlphaMode) DrawEasyChatScroll(_easyChatGroupStart,(entries+1)/2,false);
    }
    private void DrawEasyChatWords()
    {
        var screen=MailSourceScreen;var words=EasyChatWords;
        for(int i=_easyChatWordStart*2;i<Math.Min(words.Length,(_easyChatWordStart+4)*2);i++)
            SourceMailScreen.Text(this,screen.EditorFont,new(32+i%2*104,90+(i/2-_easyChatWordStart)*16),words[i].Text);
        screen.DrawTriangle(this,new(28+_easyChatWordCursor%2*104,97+(_easyChatWordCursor/2-_easyChatWordStart)*16));
        DrawEasyChatScroll(_easyChatWordStart,(words.Length+1)/2,true);
    }
    private void DrawEasyChatScroll(int start,int rows,bool words)
    {
        var screen=MailSourceScreen;int x=words?120:96;
        if(start>0)
        {
            DrawTextureRectRegion(screen.Texture("easy-chat-scroll-indicator"),new(x-8,72,16,16),new(0,0,16,16));
            if(words) DrawTextureRectRegion(screen.Texture("easy-chat-start-select-buttons"),new(204,80,32,8),new(0,0,32,8));
        }
        if(start+4<rows)
        {
            DrawTextureRectRegion(screen.Texture("easy-chat-scroll-indicator"),new(x-8,164,16,-16),new(0,0,16,16));
            if(words) DrawTextureRectRegion(screen.Texture("easy-chat-start-select-buttons"),new(204,152,32,8),new(0,8,32,8));
        }
    }
    private bool EasyChatEditorMouse(Vector2 point, int? direction)
    {
        var draft=_session.EasyChatEditor!;
        if(_easyChatNotice is not null)return direction is null&&MouseConfirm(point,MouseMessage);
        if(draft.Phase!=SourceEasyChatEditPhase.Edit)
            return direction is {} move ? PointerRows(point,move,ref _easyChatConfirm,2,172,74,44,16):MouseSelect(point,ref _easyChatConfirm,2,172,74,44,16);
        if(direction is {} wheel && wheel!=0)
        {
            using var input=new InputEventKey{Pressed=true,Keycode=wheel<0?Key.Up:Key.Down};return EasyChatEditorInput(input);
        }
        bool select=direction is null;
        if(_easyChatBrowseMode==0)
        {
            for(int slot=0;slot<EasyChatTemplate.WordCount;slot++)
            {
                Vector2 p=MailSourceScreen.FieldPosition(EasyChatLayout,EasyChatTemplate,draft.Words,_session.EasyChatWordText,slot);
                float width=draft.Words[slot]==ushort.MaxValue?70:SourceMailScreen.TextWidth(MailSourceScreen.EditorFont,_session.EasyChatWordText(draft.Words[slot]));
                if(new Rect2(p-new Vector2(8,0),new Vector2(width+12,16)).HasPoint(point))
                { _easyChatEditRow=slot/EasyChatTemplate.Columns;_easyChatEditColumn=slot%EasyChatTemplate.Columns;if(select){_easyChatBrowseMode=1;_easyChatGroupCursor=_easyChatGroupStart=0;}return true; }
            }
            for(int i=0;i<3;i++) if(new Rect2(new[]{24,111,188}[i],88,new[]{84,72,36}[i],20).HasPoint(point))
            { _easyChatEditRow=EasyChatTemplate.Rows;_easyChatEditColumn=i;if(select)EasyChatAction(i==0?"delete_all":i==1?"cancel":"done");return true; }
            return false;
        }
        if(_easyChatBrowseMode==2)
        {
            int row=(int)(point.Y-88)/16,column=point.X<128?0:1,index=(_easyChatWordStart+row)*2+column;
            if(point.Y>=88&&point.Y<152&&point.X>=16&&point.X<232&&index<EasyChatWords.Length)
            { _easyChatWordCursor=index;if(select)EasyChatChooseWord(EasyChatWords[index].Value);return true; }
            return false;
        }
        int entries=_easyChatAlphaMode?27:EasyChatGroups.Length;
        if(point.X>=192&&point.X<240&&point.Y>=104&&point.Y<152) _easyChatGroupCursor=entries+(int)(point.Y-104)/16;
        else if(_easyChatAlphaMode)
        {
            int row=(int)(point.Y-88)/16;
            if(point.Y<88||point.Y>=152)return false;
            int column=Enumerable.Range(0,EasyChatAlphabet[row].Length).OrderBy(i=>Math.Abs(point.X-(row==0&&i==6?157:32+EasyChatAlphaOffsets[i]))).First();
            if(point.X<16||point.X>=184)return false;
            _easyChatGroupCursor=(row switch{0=>0,1=>7,2=>13,_=>20})+column;
        }
        else
        {
            int index=(_easyChatGroupStart+(int)(point.Y-88)/16)*2+(point.X<100?0:1);
            if(point.Y<88||point.Y>=152||point.X<16||point.X>=184||index>=entries)return false;
            _easyChatGroupCursor=index;
        }
        if(select){using var enter=new InputEventKey{Pressed=true,Keycode=Key.Enter};EasyChatGroupsInput(enter);}return true;
    }
}
