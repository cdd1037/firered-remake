using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private int _mailCursor;
    private string? _mailScreenKey;
    private long[] _mailPartyIds = [];
    private bool MailScreen => _session.State.MailComposer is not null || _session.State.MailInteraction is not null ||
        _session.State.ItemMenu is { Origin: "party", View: "held", TargetPartyIndex: { } index } &&
        index >= 0 && index < _session.State.Party.Length && SourceMailRules.IsMailItem(_session.State.Party[index].HeldItem);
    private string MailText(string symbol) => MailSourceScreen.Content.SourceText.Text(symbol,
        SelectedMailboxMessage?.AuthorName ?? "");
    private SourceMailMessage? SelectedMailboxMessage => _session.State.MailInteraction is { MailboxSlot: >= 0 } pending &&
        pending.MailboxSlot < SourceMailState.MailboxCapacity ? (_session.State.Mail ?? SourceMailState.Empty).Mailbox[pending.MailboxSlot] : null;
    private int[] MailboxSlots => (_session.State.Mail ?? SourceMailState.Empty).Mailbox
        .Select((message, index) => (message, index)).Where(pair => pair.message is not null).Select(pair => pair.index).ToArray();
    private void SynchronizeMail()
    {
        var pending = _session.State.MailInteraction;
        string? key = pending is null ? MailScreen ? "party:held" : null :
            $"{pending.Origin}:{pending.Phase}:{pending.PartyIndex}:{pending.TargetInstanceId}:{pending.MailboxSlot}";
        if (key != _mailScreenKey)
        {
            _mailScreenKey = key; _mailCursor = 0; _partyLastRightSlot = 1;
            _mailPartyIds = pending?.Phase == CampaignMailPhase.MailboxGive ? _session.State.Party.Select(p => p.InstanceId).ToArray() : [];
        }
        if (MailScreen) { _held = null; _repeat = 0; }
    }
    private CampaignCommand PartyMailCommand(string kind, int? choice = null)
    {
        var pending = _session.State.MailInteraction;
        var menu = _session.State.ItemMenu!;
        return new(kind, Choice: choice, PartyIndex: pending?.PartyIndex ?? menu.TargetPartyIndex ?? -1) {
            TargetInstanceId = pending?.TargetInstanceId ?? menu.TargetInstanceId };
    }
    private bool MailAction(CampaignCommand command)
    {
        bool accepted = Apply(command);
        if (!accepted) _itemNotice = MailError(_status);
        return accepted;
    }
    private string MailError(string error) => error switch {
        "mail_mailbox_full" => MailText("gText_PCMailboxFull"),
        "inventory_destination_full" or "inventory_stack_full" => MailText("gText_BagIsFull"),
        "mail_target_holding_item" => MailText("gText_PkmnHoldingItemCantHoldMail"),
        "mail_egg" => _inventoryText.Get("notice.mail-egg"),
        "held_item_target_stale" or "item_party_index_invalid" => _inventoryText.Get("notice.target-changed"),
        _ => _inventoryText.Get("notice.mail-transfer-failed") };
    private void LoadMailStationeryIcons()
    {
        if (!_session.MailComposerEnabled) return;
        foreach (var paper in MailSourceScreen.Content.Stationery.Where(s => s.ItemId < 130))
        {
            var id = new ItemId("pret:" + paper.ItemSymbol);
            if (!_catalog.EarlyShop!.Items.Any(i => i.Id == id)) throw new InvalidDataException("mail_inventory_icon_binding");
            _earlyItemIcons.Add(id, MailSourceScreen.Texture(paper.ItemIconId));
        }
    }
    private bool MailInput(InputEventKey key)
    {
        if (!MailScreen) return false;
        if (_session.State.MailComposer is { Phase: CampaignMailComposePhase.Preview } preview)
        {
            if (key.Pressed && !key.Echo && key.Keycode is Key.Enter or Key.Backspace)
                MailAction(new("mail_compose_back", Text: preview.WaitId));
            return true;
        }
        if (!key.Pressed || key.Echo) return true;
        var pending = _session.State.MailInteraction;
        if (pending?.Phase is CampaignMailPhase.Reader or CampaignMailPhase.Message)
        {
            if (key.Keycode is Key.Enter or Key.Backspace) MailAction(new("mail_ack"));
            return true;
        }
        if (key.Keycode == Key.Backspace)
        { MailAction(new(pending is null ? "menu_back" : "mail_back")); return true; }
        int count = pending?.Phase switch {
            CampaignMailPhase.MailboxList => MailboxSlots.Length + 1,
            CampaignMailPhase.MailboxActions => 4,
            CampaignMailPhase.MailboxGive => _mailPartyIds.Length + 1,
            CampaignMailPhase.SendToPc or CampaignMailPhase.EraseAttached or CampaignMailPhase.EraseMailbox => 2,
            _ => 3 };
        if(pending?.Phase==CampaignMailPhase.MailboxGive)NativePartyMove(key.Keycode,ref _mailCursor,count-1);
        else FieldMenuMoveCursor(key, ref _mailCursor, count);
        if (key.Keycode != Key.Enter) return true;
        switch (pending?.Phase)
        {
            case null:
                MailAction(_mailCursor == 2 ? new("menu_back") : PartyMailCommand(_mailCursor == 0 ? "mail_read" : "mail_take"));
                break;
            case CampaignMailPhase.SendToPc:
                MailAction(PartyMailCommand("mail_send_pc", _mailCursor)); break;
            case CampaignMailPhase.EraseAttached:
                MailAction(PartyMailCommand("mail_erase_confirm", _mailCursor)); break;
            case CampaignMailPhase.EraseMailbox:
                MailAction(new("mail_erase_confirm", Choice: _mailCursor)); break;
            case CampaignMailPhase.MailboxList:
                var slots = MailboxSlots;
                MailAction(_mailCursor >= slots.Length ? new("mail_back") : new("mailbox_select", Choice: slots[_mailCursor])); break;
            case CampaignMailPhase.MailboxActions:
                MailAction(new(_mailCursor switch { 0 => "mailbox_read", 1 => "mailbox_erase", 2 => "mailbox_give", _ => "mail_back" })); break;
            case CampaignMailPhase.MailboxGive:
                MailAction(_mailCursor >= _mailPartyIds.Length ? new("mail_back") :
                    new("mailbox_give_target", PartyIndex: _mailCursor) { TargetInstanceId = _mailPartyIds[_mailCursor] }); break;
        }
        return true;
    }
    private SourceMailScreen? _mailSourceScreen;
    private SourceMailScreen MailSourceScreen => _mailSourceScreen ??= new(_catalog.MailPresentation ?? PretMailPresentation.Load(_root),
        System.IO.Path.Combine(_root, "content/generated/ui/font"), _root, SceneTextures);
    private void DisposeMailPresentation() { _mailSourceScreen?.Dispose(); _mailSourceScreen = null; }
    private Texture2D? MailSpeciesIcon(int renderSpecies)
    {
        if (renderSpecies == 201 || renderSpecies is >= 413 and <= 439) return _unownIcons.GetValueOrDefault(renderSpecies);
        var species = _catalog.PokemonContent?.Catalog.Species.Values.SingleOrDefault(s => s.SourceSpeciesId == renderSpecies);
        return species is null ? null : _storageIcons.GetValueOrDefault(species.Id);
    }
    private bool DrawMailScreen()
    {
        if (!MailScreen) return false;
        if (_session.State.MailComposer is { Phase: CampaignMailComposePhase.Preview } preview)
        { MailSourceScreen.DrawReader(this, preview.ItemId, _session.State.PlayerGender, null, _session.EasyChatWordText, MailSpeciesIcon); return true; }
        var pending = _session.State.MailInteraction;
        if (pending?.Phase == CampaignMailPhase.Reader)
        {
            var mail = _session.MailReader ?? throw new InvalidDataException("Mail reader no longer owns its message.");
            MailSourceScreen.DrawReader(this, mail.ItemId, _session.State.PlayerGender, mail, _session.EasyChatWordText, MailSpeciesIcon);
        }
        else if (pending?.Origin == "pc") DrawMailbox(pending);
        else
        {
            _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
            int index = pending?.PartyIndex ?? _session.State.ItemMenu!.TargetPartyIndex!.Value;
            var owned = _session.State.Party[index];
            SourceText(_sourceNormal, new(12, 10), _catalog.PokemonDisplayName(owned));
            SourceText(_sourceSmall, new(12, 32), _inventoryText.Get("held.header", ("item", _catalog.DisplayNames.HeldItemName(owned.HeldItem))));
            if (pending is null)
            {
                DrawMailRows([MailText("gText_Read2"), MailText("gText_Take2"), MailText("gFameCheckerText_Cancel")], 176, 58);
                ItemMessageBox(MailText("gText_DoWhatWithMail"));
            }
        }
        if (pending?.Phase is CampaignMailPhase.SendToPc or CampaignMailPhase.EraseAttached or CampaignMailPhase.EraseMailbox)
        {
            _battleSkin.DrawStandardFrame(this, new(160, 56, 80, 48));
            DrawMailRows([InventorySourceText("gText_Yes"), InventorySourceText("gText_No")], 176, 62);
            ItemMessageBox(MailText(pending.Phase switch { CampaignMailPhase.SendToPc => "gText_SendMailToPC",
                CampaignMailPhase.EraseAttached => "gText_MailMessageWillBeLost", _ => "gText_MessageWillBeLost" }));
        }
        if (pending?.Phase == CampaignMailPhase.Message) ItemMessageBox(MailText(pending.MessageSymbol!));
        if (_itemNotice is not null) ItemMessageBox(_itemNotice);
        return true;
    }
    private void DrawMailRows(string[] labels, int x, int y, int step = 16, int start = 0)
    {
        for (int i = 0; i < labels.Length; i++) SourceText(_sourceNormal, new(x, y + i * step), labels[i]);
        SourceText(_sourceNormal, new(x - 12, y + (_mailCursor - start) * step), "▶");
    }
    private void DrawMailbox(CampaignMailInteraction pending)
    {
        if (pending.Phase == CampaignMailPhase.MailboxGive)
        {
            DrawNativeParty(_session.State.Party,_mailCursor,MailText("gText_GiveToWhichPokemon"));return;
        }
        var slots = MailboxSlots;
        _battleSkin.DrawStandardFrame(this, new(0, 0, 96, 32));
        SourceText(_sourceNormal, new(15, 10), MailText("gText_Mailbox"));
        _battleSkin.DrawStandardFrame(this, new(144, 0, 96, 160));
        var labels = slots.Select(slot => (_session.State.Mail ?? SourceMailState.Empty).Mailbox[slot]!.AuthorName)
            .Append(MailText("gFameCheckerText_Cancel")).ToArray();
        int selected = pending.Phase == CampaignMailPhase.MailboxList ? _mailCursor : Math.Max(0, Array.IndexOf(slots, pending.MailboxSlot));
        int start = Math.Max(0, selected - 7);
        for (int i = start; i < Math.Min(start + 8, labels.Length); i++) SourceText(_sourceNormal, new(160, 18 + (i - start) * 16), labels[i]);
        SourceText(_sourceNormal, new(148, 18 + (selected - start) * 16), "▶");
        if (pending.Phase == CampaignMailPhase.MailboxActions)
        {
            _battleSkin.DrawStandardFrame(this, new(0, 0, 136, 80));
            DrawMailRows([MailText("gOtherText_Read"), MailText("gOtherText_MoveToBag"), MailText("gOtherText_Give2"), MailText("gOtherText_Exit")], 20, 10);
            ItemMessageBox(MailText("gText_WhatWouldYouLikeToDoWithPlayersMail"));
        }
    }
    private bool MailMouse(Vector2 point, int? direction = null)
    {
        if (_session.State.MailComposer is { Phase: CampaignMailComposePhase.Preview })
            return direction is null && MouseConfirm(point, new(0,0,240,160));
        var phase = _session.State.MailInteraction?.Phase;
        if (phase is CampaignMailPhase.Reader) return direction is null && MouseConfirm(point, new(0, 0, 240, 160));
        if (phase is CampaignMailPhase.Message) return direction is null && MouseConfirm(point, MouseMessage);
        int count, x, y, width, step = 16, start = 0;
        switch (phase)
        {
            case CampaignMailPhase.MailboxList: count = Math.Min(8, MailboxSlots.Length + 1); x = 148; y = 18; width = 84; start = Math.Max(0, _mailCursor - 7); break;
            case CampaignMailPhase.MailboxActions: count = 4; x = 8; y = 10; width = 120; break;
            case CampaignMailPhase.MailboxGive: return direction is {} d ? PointerNativeParty(point,d,ref _mailCursor,_mailPartyIds.Length) : MouseNativeParty(point,ref _mailCursor,_mailPartyIds.Length);
            case CampaignMailPhase.SendToPc:
            case CampaignMailPhase.EraseAttached:
            case CampaignMailPhase.EraseMailbox: count = 2; x = 164; y = 62; width = 68; break;
            default: count = 3; x = 164; y = 58; width = 68; break;
        }
        return direction is { } move ? PointerRows(point, move, ref _mailCursor, count, x, y, width, step, start)
            : MouseSelect(point, ref _mailCursor, count, x, y, width, step, start);
    }
}
