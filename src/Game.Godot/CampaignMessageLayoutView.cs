using Godot;

public partial class CampaignView
{
    private static SourceMessageSample MessageSample(SourceMessageCursor cursor, object identity,
        string text, SourceGameFont font, int width)
    {
        cursor.Synchronize((identity, font), text, width,
            value => font.Font.GetStringSize(font.PrepareText(value), fontSize: font.Size).X);
        return cursor.Sample;
    }

    private readonly SourceMessageCursor _battleMessage = new(), _wildMessage = new(), _doubleMessage = new();
    private SourceMessageSample BattleMessageSample
    {
        get
        {
            bool oak = _battleOakPages.Contains(_battlePage);
            if (NativeBattleMessageEnabled)
                return NativeBattleSample(_nativeBattleMessage, (_battlePresentationKey, _battlePages, _battlePage),
                    _battlePages[_battlePage], oak ? _sourceNormal : _battleWhite, oak ? 198 : 212, oak ? 15 : 16);
            return MessageSample(_battleMessage, (_battlePresentationKey, _battlePages, _battlePage),
                _battlePages[_battlePage], oak ? _sourceNormal : _battleWhite, oak ? 198 : 212);
        }
    }
    private SourceMessageSample WildMessageSample => NativeWildMessageEnabled
        ? NativeBattleSample(_nativeWildMessage, (_wildPresentationKey, _wildPages, _wildPage), _wildPages[_wildPage], _battleWhite, 212, 16)
        : MessageSample(_wildMessage,
        (_wildPresentationKey, _wildPages, _wildPage), _wildPages[_wildPage], _battleWhite, 212);
    private SourceMessageSample DoubleMessageSample => NativeDoubleMessageEnabled
        ? NativeBattleSample(_nativeDoubleMessage, (_doubleKey, _doublePages, _doublePage), _doublePages[_doublePage], _battleWhite, 212, 16)
        : MessageSample(_doubleMessage,
        (_doubleKey, _doublePages, _doublePage), _doublePages[_doublePage], _battleWhite, 212);
    private bool TryAdvanceBattleVisualMessage()
    { if (_battlePages.IsEmpty) return false; _ = BattleMessageSample; return NativeBattleMessageEnabled ? AdvanceNativeBattleVisual(_nativeBattleMessage) : _battleMessage.TryAdvance(); }
    private bool TryAdvanceWildVisualMessage()
    { if (_wildPages.IsEmpty) return false; _ = WildMessageSample; return NativeWildMessageEnabled ? AdvanceNativeBattleVisual(_nativeWildMessage) : _wildMessage.TryAdvance(); }
    private bool TryAdvanceDoubleVisualMessage()
    { if (_doublePages.IsEmpty) return false; _ = DoubleMessageSample; return NativeDoubleMessageEnabled ? AdvanceNativeBattleVisual(_nativeDoubleMessage) : _doubleMessage.TryAdvance(); }
    private readonly SourceMessageCursor _battleNoticeMessage = new(), _wildNoticeMessage = new(),
        _doubleNoticeMessage = new(), _itemNoticeMessage = new(), _itemAckMessage = new();
    private string? _battleNoticeValue;
    private string? _battleNotice
    {
        get => _battleNoticeValue;
        set { _battleNoticeValue = value; _battleNoticeMessage.Clear(); }
    }
    private string? _wildNoticeValue;
    private string? _wildNotice
    {
        get => _wildNoticeValue;
        set { _wildNoticeValue = value; _wildNoticeMessage.Clear(); }
    }
    private string? _doubleNoticeValue;
    private string? _doubleNotice
    {
        get => _doubleNoticeValue;
        set { _doubleNoticeValue = value; _doubleNoticeMessage.Clear(); }
    }
    private string? _itemNoticeValue;
    private string? _itemNotice
    {
        get => _itemNoticeValue;
        set { _itemNoticeValue = value; _itemNoticeMessage.Clear(); }
    }
    private SourceMessageSample BattleNoticeSample => MessageSample(_battleNoticeMessage, "battle-notice",
        _battleNotice!, _oakNotice ? _sourceNormal : _battleWhite,
        _battleMenu is "bag" or "bag-actions" ? 174 : _oakNotice ? 198 : 212);
    // Wild notices are also shown in field/party/capture boxes, so reserve that box's cursor budget.
    private SourceMessageSample WildNoticeSample => MessageSample(_wildNoticeMessage, "wild-notice",
        _wildNotice!, _sourceNormal, _wildMenu is "bag" or "bag-actions" ? 174 : 198);
    private SourceMessageSample DoubleNoticeSample => MessageSample(_doubleNoticeMessage, "double-notice",
        _doubleNotice!, _sourceNormal, 198);
    private SourceMessageSample ItemNoticeSample => MessageSample(_itemNoticeMessage, "item-notice",
        _itemNotice!, _sourceNormal, 198);
    // The shared item-result owner can be drawn in the narrower PC/bag result panel.
    // Its 184 px text interior leaves 174 px after reserving the source cursor.
    private SourceMessageSample ItemAckMessageSample => MessageSample(_itemAckMessage,
        _session.State.ItemMenu!, _session.VisibleItemMessage!, _sourceNormal, 174);

    private bool TryAdvanceBattleNotice()
    { _ = BattleNoticeSample; return _battleNoticeMessage.TryAdvance(); }
    private bool TryAdvanceWildNotice()
    { _ = WildNoticeSample; return _wildNoticeMessage.TryAdvance(); }
    private bool TryAdvanceDoubleNotice()
    { _ = DoubleNoticeSample; return _doubleNoticeMessage.TryAdvance(); }
    private bool TryItemVisualContinuationInput(InputEventKey key)
    {
        if(!key.Pressed||key.Echo||key.Keycode is not (Key.Enter or Key.Backspace)||
            _session.State.ItemMenu is not {} menu||menu.Toss is not null)return false;
        if(_itemNotice is not null)
        { _ = ItemNoticeSample; return _itemNoticeMessage.TryAdvance(); }
        if(menu.View!="message")return false;
        _ = ItemAckMessageSample; return _itemAckMessage.TryAdvance();
    }
    private void DrawMessageContinuation(SourceMessageSample sample, SourceGameFont font,
        Vector2 origin, int step, double elapsed, bool blue = false)
    { if(!sample.IsFinal)DrawTextCursor(font,origin,sample.Text.Split('\n'),step,elapsed,blue); }
    private void DrawItemNotice()
    {
        var sample=ItemNoticeSample;ItemMessageBox(sample.Text);
        DrawMessageContinuation(sample,_sourceNormal,LabBattleUiSkin.DialogueText,15,_waitElapsed);
    }
    private void DrawWildNoticeBox()
    {
        var sample=WildNoticeSample;ItemMessageBox(sample.Text);
        DrawMessageContinuation(sample,_sourceNormal,LabBattleUiSkin.DialogueText,15,_wildElapsed);
    }
    private void DrawDoubleNoticeBox()
    {
        var sample=DoubleNoticeSample;ItemMessageBox(sample.Text);
        DrawMessageContinuation(sample,_sourceNormal,LabBattleUiSkin.DialogueText,15,_doubleElapsed);
    }
    private void DrawItemAckMessageBox()
    {
        var sample=ItemAckMessageSample;ItemMessageBox(sample.Text);
        DrawMessageContinuation(sample,_sourceNormal,LabBattleUiSkin.DialogueText,15,_waitElapsed);
    }
}
