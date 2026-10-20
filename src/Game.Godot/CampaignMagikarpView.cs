using System.Globalization;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Godot;

// Bounded gift presentation. Source scripts own payment, delivery, naming and release.
public partial class CampaignView
{
    private sealed record GiftMoneyLayout(Rect2 Frame, Vector2 LabelPosition, Vector2 AmountPosition, string Label, string Amount);

    private GiftMoneyLayout? MagikarpMoneyLayout()
    {
        if (_session.State.MoneyBox is not {} money) return null;
        // Authenticated src/money.c:73-128: x+1/y+1, 8x3 interior, normal label,
        // small amount at y=12, six decimal cells, right edge of the 64px interior.
        if (money.X != 0 || money.Y != 0 || money.Amount is < 0 or > 999999)
            throw new InvalidDataException("Unsupported source money box.");
        var text = _trainerCard?.Texts ?? throw new InvalidDataException("Money text was not loaded.");
        string digits = money.Amount.ToString(CultureInfo.InvariantCulture);
        string amount = new string(' ', 6 - digits.Length) + text.Currency + digits;
        float width = _sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText(amount), fontSize: _sourceSmall.Size).X;
        return new(new(money.X * 8, money.Y * 8, 80, 40), new(8, 8), new(72 - width, 20), text.Money, amount);
    }

    private void DrawMagikarpMoneyBox()
    {
        if (MagikarpMoneyLayout() is not {} layout) return;
        _itemSkin.DrawStandardFrame(this, layout.Frame);
        SourceText(_sourceNormal, layout.LabelPosition, layout.Label);
        SourceText(_sourceSmall, layout.AmountPosition, layout.Amount);
    }

    private bool GiftNamingActive => _session.State.GiftDelivery is not null &&
        _session.State.Nickname is { Submitted: false } && _session.State.Script.Wait?.Kind == StoryWaitKind.Nickname;

    private PartyPokemon GiftNicknameTarget()
    {
        if (!GiftNamingActive) throw new InvalidDataException("Gift nickname is not pending.");
        var state = _session.State;
        var naming = state.Nickname!;
        PartyPokemon? mon;
        if (naming.BoxAddress is {} address)
        {
            if (state.Storage is not {} storage || address.BoxIndex < 0 || address.BoxIndex >= storage.Boxes.Length ||
                address.SlotIndex < 0 || address.SlotIndex >= storage.Boxes[address.BoxIndex].Slots.Length)
                throw new InvalidDataException("Gift nickname box address differs.");
            mon = storage.Boxes[address.BoxIndex].Slots[address.SlotIndex];
        }
        else mon = naming.PartyIndex >= 0 && naming.PartyIndex < state.Party.Length ? state.Party[naming.PartyIndex] : null;
        long? expectedId = _session.PendingGiftInstanceId;
        if (mon is null || mon.InstanceId != expectedId || naming.BoxAddress != state.GiftDelivery!.BoxAddress)
            throw new InvalidDataException("Gift nickname target differs.");
        return mon;
    }

    private void SubmitCampaignNickname(string text)
    {
        if(!NamingGlyphPreflight(text))return;
        if(SubmitStorageBoxNameFromView(text))return;
        if(SubmitNameRaterNicknameFromView(text))return;
        if (CaptureNamingActive && _wildNotice is not null) { _wildNotice = null; QueueRedraw(); return; }
        if (GiftNamingActive) _ = GiftNicknameTarget();
        Apply(new("nickname", Text: text));
    }

}
