using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Godot;

public partial class CampaignView
{
    private string? _mailComposerTicket;
    private CampaignMailComposePhase? _mailComposerPhase;
    private int _mailEditRow, _mailEditColumn, _mailBrowseMode, _mailGroupCursor, _mailWordCursor, _mailGroupId, _mailAlphaBucket;
    private int _mailGroupStart, _mailWordStart, _mailConfirm = 1;
    private bool _mailAlphaMode;
    private ImmutableArray<SourceEasyChatGroup> MailGroups => SourceEasyChatRules.SelectableGroups(_catalog.MailComposer!, _session.MailSelectionFacts);
    private ImmutableArray<SourceEasyChatWord> MailWords => _mailAlphaMode
        ? SourceEasyChatRules.AlphabeticalWords(_catalog.MailComposer!, _session.MailSelectionFacts, _mailAlphaBucket)
        : SourceEasyChatRules.SelectableWords(_catalog.MailComposer!, _session.MailSelectionFacts, _mailGroupId);
    private int MailEditSlot => _mailEditRow * 2 + _mailEditColumn;
    private static readonly int[][] MailAlphabet = [[1,2,3,4,5,6,0], [7,8,9,10,11,12], [13,14,15,16,17,18,19], [20,21,22,23,24,25,26]];
    private static readonly int[] MailAlphaOffsets = [0,12,24,56,68,80,92];
    private void SynchronizeMailComposer()
    {
        var draft = _session.State.MailComposer;
        if (draft?.Ticket != _mailComposerTicket)
        {
            _mailComposerTicket = draft?.Ticket; _mailEditRow = _mailEditColumn = _mailBrowseMode = 0;
            _mailGroupCursor = _mailWordCursor = _mailGroupStart = _mailWordStart = 0; _mailAlphaMode = false;
        }
        if (_mailComposerPhase != draft?.Phase)
        { _mailComposerPhase = draft?.Phase; _mailConfirm = 1; _mailBrowseMode = 0; }
        if (draft is not null) { _held = null; _repeat = 0; }
    }
    private void LoadMailStationeryIcons()
    {
        if (!_session.MailComposerEnabled) return;
        foreach (var paper in MailSourceScreen.Content.Stationery.Where(s => s.ItemId < 130))
        {
            var id = new ItemId("pret:" + paper.ItemSymbol);
            if (!_catalog.EarlyShop!.Items.Any(i => i.Id == id)) throw new InvalidDataException("mail_inventory_icon_binding");
            using var image = MailSourceScreen.Texture(paper.ItemIconId).GetImage();
            _earlyItemIcons.Add(id, ImageTexture.CreateFromImage(image));
        }
    }
    private void MailComposeAction(string kind, int? choice = null, int quantity = 0)
    {
        var draft = _session.State.MailComposer ?? throw new InvalidOperationException("No Mail composition is active.");
        MailAction(new(kind, Choice: choice, Quantity: quantity, Text: draft.WaitId));
    }
    private void MailChooseWord(ushort word)
    {
        long revision = _session.Revision; MailComposeAction("mail_compose_word", MailEditSlot, word);
        if (_session.Revision != revision) _mailBrowseMode = 0;
    }
    private bool MailComposerInput(InputEventKey key)
    {
        if (!key.Pressed || key.Echo) return true;
        if (_itemNotice is not null)
        { if (key.Keycode is Key.Enter or Key.Backspace) _itemNotice = null; return true; }
        var draft = _session.State.MailComposer!;
        if (draft.Phase == CampaignMailComposePhase.Preview)
        { if (key.Keycode is Key.Enter or Key.Backspace) MailComposeAction("mail_compose_back"); return true; }
        if (draft.Phase != CampaignMailComposePhase.Edit)
        {
            if (key.Keycode == Key.Backspace) MailComposeAction("mail_compose_back");
            else if (key.Keycode is Key.Up or Key.Down) _mailConfirm = 1 - _mailConfirm;
            else if (key.Keycode == Key.Enter) MailComposeAction("mail_compose_confirm", _mailConfirm);
            return true;
        }
        if (_mailBrowseMode == 2) return MailWordsInput(key);
        if (_mailBrowseMode == 1) return MailGroupsInput(key);
        if (key.Keycode == Key.Backspace) { MailComposeAction("mail_compose_cancel"); return true; }
        if (key.Keycode == Key.Delete && _mailEditRow < 5) { MailChooseWord(ushort.MaxValue); return true; }
        if (key.Keycode == Key.End) { MailComposeAction("mail_compose_done"); return true; }
        if (key.Keycode == Key.Up) _mailEditRow = (_mailEditRow + 5) % 6;
        if (key.Keycode == Key.Down) _mailEditRow = (_mailEditRow + 1) % 6;
        int columns = _mailEditRow == 5 ? 3 : _mailEditRow == 4 ? 1 : 2;
        if (key.Keycode == Key.Left) _mailEditColumn = (_mailEditColumn + columns - 1) % columns;
        if (key.Keycode == Key.Right) _mailEditColumn = (_mailEditColumn + 1) % columns;
        _mailEditColumn = Math.Min(_mailEditColumn, columns - 1);
        if (key.Keycode == Key.Enter)
        {
            if (_mailEditRow == 5) MailComposeAction(_mailEditColumn switch { 0 => "mail_compose_delete_all", 1 => "mail_compose_cancel", _ => "mail_compose_done" });
            else { _mailBrowseMode = 1; _mailGroupCursor = _mailGroupStart = 0; }
        }
        return true;
    }
    private bool MailGroupsInput(InputEventKey key)
    {
        if (key.Keycode == Key.Backspace) { _mailBrowseMode = 0; return true; }
        if (key.Keycode == Key.Tab) { _mailAlphaMode = !_mailAlphaMode; _mailGroupCursor = _mailGroupStart = 0; return true; }
        int entries = _mailAlphaMode ? 27 : MailGroups.Length;
        int count = entries + 3;
        if (_mailAlphaMode)
        {
            // A-Z keeps the original four keyboard rows; blue-box actions occupy the right edge.
            int cursor = Math.Min(_mailGroupCursor, count - 1);
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
                    column = Math.Min(column, MailAlphabet[row].Length - 1);
                    cursor = (row switch { 0 => 0, 1 => 7, 2 => 13, _ => 20 }) + column;
                }
                if (key.Keycode == Key.Left) cursor = column == 0 ? 27 + Math.Min(row,2) : cursor - 1;
                if (key.Keycode == Key.Right) cursor = column + 1 == MailAlphabet[row].Length ? 27 + Math.Min(row,2) : cursor + 1;
            }
            _mailGroupCursor = cursor;
        }
        else
        {
            if (_mailGroupCursor >= entries)
            {
                if (key.Keycode == Key.Up) _mailGroupCursor = entries + (_mailGroupCursor - entries + 2) % 3;
                if (key.Keycode == Key.Down) _mailGroupCursor = entries + (_mailGroupCursor - entries + 1) % 3;
                if (key.Keycode is Key.Left or Key.Right) _mailGroupCursor = Math.Min(entries - 1, _mailGroupStart * 2 + (_mailGroupCursor - entries) * 2 + (key.Keycode == Key.Left ? 1 : 0));
            }
            else
            {
                if (key.Keycode == Key.Up) _mailGroupCursor = Math.Max(0, _mailGroupCursor - 2);
                if (key.Keycode == Key.Down) _mailGroupCursor = Math.Min(entries - 1, _mailGroupCursor + 2);
                if (key.Keycode == Key.Left) _mailGroupCursor = _mailGroupCursor % 2 == 0 ? entries : _mailGroupCursor - 1;
                if (key.Keycode == Key.Right) _mailGroupCursor = _mailGroupCursor % 2 == 1 || _mailGroupCursor + 1 == entries ? entries : _mailGroupCursor + 1;
                if (key.Keycode is Key.Pageup or Key.Pagedown) _mailGroupCursor = Math.Clamp(_mailGroupCursor + (key.Keycode == Key.Pageup ? -8 : 8), 0, entries - 1);
                if (_mailGroupCursor < entries) _mailGroupStart = Math.Clamp(_mailGroupStart, Math.Max(0, _mailGroupCursor / 2 - 3), _mailGroupCursor / 2);
            }
        }
        if (key.Keycode != Key.Enter) return true;
        if (_mailGroupCursor == entries) { _mailAlphaMode = !_mailAlphaMode; _mailGroupCursor = _mailGroupStart = 0; }
        else if (_mailGroupCursor == entries + 1) MailChooseWord(ushort.MaxValue);
        else if (_mailGroupCursor == entries + 2) _mailBrowseMode = 0;
        else
        {
            if (_mailAlphaMode) _mailAlphaBucket = MailAlphabet.SelectMany(row => row).ElementAt(_mailGroupCursor);
            else _mailGroupId = MailGroups[_mailGroupCursor].Id;
            if (MailWords.Length > 0) { _mailBrowseMode = 2; _mailWordCursor = _mailWordStart = 0; }
        }
        return true;
    }
    private bool MailWordsInput(InputEventKey key)
    {
        if (key.Keycode == Key.Backspace) { _mailBrowseMode = 1; return true; }
        var words = MailWords;
        if (key.Keycode == Key.Up) _mailWordCursor = Math.Max(0, _mailWordCursor - 2);
        if (key.Keycode == Key.Down) _mailWordCursor = Math.Min(words.Length - 1, _mailWordCursor + 2);
        if (key.Keycode == Key.Left) _mailWordCursor = _mailWordCursor % 2 == 1 ? _mailWordCursor - 1 : Math.Min(words.Length - 1, _mailWordCursor + 1);
        if (key.Keycode == Key.Right) _mailWordCursor = _mailWordCursor % 2 == 0 ? Math.Min(words.Length - 1, _mailWordCursor + 1) : _mailWordCursor - 1;
        if (key.Keycode is Key.Pageup or Key.Pagedown) _mailWordCursor = Math.Clamp(_mailWordCursor + (key.Keycode == Key.Pageup ? -8 : 8), 0, words.Length - 1);
        _mailWordStart = Math.Clamp(_mailWordStart, Math.Max(0, _mailWordCursor / 2 - 3), _mailWordCursor / 2);
        if (key.Keycode == Key.Enter) MailChooseWord(words[_mailWordCursor].Value);
        return true;
    }
    private bool DrawMailComposerScreen()
    {
        var draft = _session.State.MailComposer!; var screen = MailSourceScreen; var content = screen.Content;
        if (draft.Phase == CampaignMailComposePhase.Preview)
        { screen.DrawReader(this, draft.ItemId, _session.State.PlayerGender, null, _session.MailWordText, MailSpeciesIcon); return true; }
        DrawTexture(screen.Texture("easy-chat-" + (_mailBrowseMode == 2 ? "words" : _mailBrowseMode == 1 ? "groups" : "fields")), Vector2.Zero);
        screen.DrawEditorFields(this, draft.Words, _session.MailWordText);
        if (_mailBrowseMode == 0)
        {
            foreach (var (label,x) in new[] { ("DEL. ALL",32), ("CANCEL",119), ("OK",196) }) SourceMailScreen.Text(this, screen.EditorFont, new(x,90), label);
            var center = _mailEditRow == 5 ? new Vector2(new[] {28,115,191}[_mailEditColumn],97)
                : screen.FieldPosition(draft.Words, _session.MailWordText, MailEditSlot) + new Vector2(-4,7);
            screen.DrawTriangle(this, center);
            var symbols = draft.Phase switch {
                CampaignMailComposePhase.ConfirmDone => new[] { "gText_TheMailMessage", "gText_IsAsShownOkay" },
                CampaignMailComposePhase.ConfirmCancel => ["gText_StopGivingPkmnMail"],
                CampaignMailComposePhase.ConfirmDelete => ["gText_AllTextBeingEditedWill", "gText_BeDeletedThatOkay"],
                _ => ["gText_CombineNineWordsOrPhrases", "gText_AndMakeAMessage"] };
            _battleSkin.DrawStandardFrame(this, new(24,112,192,48));
            for (int i=0;i<symbols.Length;i++) SourceMailScreen.Text(this,screen.EditorFont,new(32,122+i*16),content.Texts[symbols[i]]);
            if (draft.Phase != CampaignMailComposePhase.Edit)
            {
                _battleSkin.DrawStandardFrame(this,new(168,64,56,48));
                SourceMailScreen.Text(this,screen.EditorFont,new(184,74),"YES");SourceMailScreen.Text(this,screen.EditorFont,new(184,90),"NO");
                screen.DrawTriangle(this,new(176,81+_mailConfirm*16));
            }
        }
        else if (_mailBrowseMode == 1) DrawMailGroups();
        else DrawMailWords();
        if (_itemNotice is not null) ItemMessageBox(_itemNotice);
        return true;
    }
    private void DrawMailGroups()
    {
        var screen = MailSourceScreen; int entries = _mailAlphaMode ? 27 : MailGroups.Length;
        if (_mailAlphaMode)
        {
            for (int row=0;row<4;row++) for (int column=0;column<MailAlphabet[row].Length;column++)
            {
                int bucket=MailAlphabet[row][column]; string text=bucket==0?"others":((char)('A'+bucket-1)).ToString();
                SourceMailScreen.Text(this,screen.EditorFont,new(bucket==0?141:29+MailAlphaOffsets[column],90+row*16),text);
            }
        }
        else
        {
            var groups=MailGroups;
            for(int i=_mailGroupStart*2;i<Math.Min(groups.Length,(_mailGroupStart+4)*2);i++)
                SourceMailScreen.Text(this,screen.EditorFont,new(18+i%2*84,90+(i/2-_mailGroupStart)*16),groups[i].Name);
        }
        DrawTexture(screen.Texture("easy-chat-button-window"),new(176,96));
        DrawTextureRectRegion(screen.Texture("easy-chat-mode"),new(176,64,64,32),new(0,_mailAlphaMode?0:32,64,32));
        Vector2 center; int frame;
        if(_mailGroupCursor>=entries){center=new(216,112+(_mailGroupCursor-entries)*16);frame=1;}
        else if(!_mailAlphaMode){center=new(58+_mailGroupCursor%2*84,96+(_mailGroupCursor/2-_mailGroupStart)*16);frame=0;}
        else
        {
            int row=_mailGroupCursor<7?0:_mailGroupCursor<13?1:_mailGroupCursor<20?2:3;
            int column=_mailGroupCursor-(row switch{0=>0,1=>7,2=>13,_=>20});
            bool others=row==0&&column==6;center=new(others?157:32+MailAlphaOffsets[column],96+row*16);frame=others?2:3;
        }
        screen.DrawRectangle(this,center,frame);
        if(!_mailAlphaMode) DrawMailScroll(_mailGroupStart,(entries+1)/2,false);
    }
    private void DrawMailWords()
    {
        var screen=MailSourceScreen;var words=MailWords;
        for(int i=_mailWordStart*2;i<Math.Min(words.Length,(_mailWordStart+4)*2);i++)
            SourceMailScreen.Text(this,screen.EditorFont,new(32+i%2*104,90+(i/2-_mailWordStart)*16),words[i].Text);
        screen.DrawTriangle(this,new(28+_mailWordCursor%2*104,97+(_mailWordCursor/2-_mailWordStart)*16));
        DrawMailScroll(_mailWordStart,(words.Length+1)/2,true);
    }
    private void DrawMailScroll(int start,int rows,bool words)
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
    private bool MailComposerMouse(Vector2 point, int? direction)
    {
        var draft=_session.State.MailComposer!;
        if(draft.Phase==CampaignMailComposePhase.Preview)return direction is null&&MouseConfirm(point,new(0,0,240,160));
        if(_itemNotice is not null)return direction is null&&MouseConfirm(point,MouseMessage);
        if(draft.Phase!=CampaignMailComposePhase.Edit)
            return direction is {} move ? PointerRows(point,move,ref _mailConfirm,2,172,74,44,16):MouseSelect(point,ref _mailConfirm,2,172,74,44,16);
        if(direction is {} wheel && wheel!=0)
        {
            using var input=new InputEventKey{Pressed=true,Keycode=wheel<0?Key.Up:Key.Down};return MailComposerInput(input);
        }
        bool select=direction is null;
        if(_mailBrowseMode==0)
        {
            for(int slot=0;slot<9;slot++)
            {
                Vector2 p=MailSourceScreen.FieldPosition(draft.Words,_session.MailWordText,slot);
                float width=draft.Words[slot]==ushort.MaxValue?70:SourceMailScreen.TextWidth(MailSourceScreen.EditorFont,_session.MailWordText(draft.Words[slot]));
                if(new Rect2(p-new Vector2(8,0),new Vector2(width+12,16)).HasPoint(point))
                { _mailEditRow=slot/2;_mailEditColumn=slot%2;if(select){_mailBrowseMode=1;_mailGroupCursor=_mailGroupStart=0;}return true; }
            }
            for(int i=0;i<3;i++) if(new Rect2(new[]{24,111,188}[i],88,new[]{84,72,36}[i],20).HasPoint(point))
            { _mailEditRow=5;_mailEditColumn=i;if(select)MailComposeAction(i==0?"mail_compose_delete_all":i==1?"mail_compose_cancel":"mail_compose_done");return true; }
            return false;
        }
        if(_mailBrowseMode==2)
        {
            int row=(int)(point.Y-88)/16,column=point.X<128?0:1,index=(_mailWordStart+row)*2+column;
            if(point.Y>=88&&point.Y<152&&point.X>=16&&point.X<232&&index<MailWords.Length)
            { _mailWordCursor=index;if(select)MailChooseWord(MailWords[index].Value);return true; }
            return false;
        }
        int entries=_mailAlphaMode?27:MailGroups.Length;
        if(point.X>=192&&point.X<240&&point.Y>=104&&point.Y<152) _mailGroupCursor=entries+(int)(point.Y-104)/16;
        else if(_mailAlphaMode)
        {
            int row=(int)(point.Y-88)/16;
            if(point.Y<88||point.Y>=152)return false;
            int column=Enumerable.Range(0,MailAlphabet[row].Length).OrderBy(i=>Math.Abs(point.X-(row==0&&i==6?157:32+MailAlphaOffsets[i]))).First();
            if(point.X<16||point.X>=184)return false;
            _mailGroupCursor=(row switch{0=>0,1=>7,2=>13,_=>20})+column;
        }
        else
        {
            int index=(_mailGroupStart+(int)(point.Y-88)/16)*2+(point.X<100?0:1);
            if(point.Y<88||point.Y>=152||point.X<16||point.X>=184||index>=entries)return false;
            _mailGroupCursor=index;
        }
        if(select){using var enter=new InputEventKey{Pressed=true,Keycode=Key.Enter};MailGroupsInput(enter);}return true;
    }
}
