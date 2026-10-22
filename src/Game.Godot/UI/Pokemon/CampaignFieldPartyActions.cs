using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

internal readonly record struct FieldPartyActionEntry(string Id, string Label);

/// <summary>Display labels do not identify actions. Known moves retain their existing stable IDs and slot order.</summary>
internal static class CampaignFieldPartyActions
{
    public static FieldPartyActionEntry[] Build(PresentationTextCatalog text, PartyPokemon? member, Func<MoveId, string> moveName,
        bool flyAvailable, bool rockSmashAvailable, bool heldItemsAvailable)
    {
        FieldPartyActionEntry Menu(string id) => new(id, text.Get("party." + id));
        if (member is { IsEgg: true }) return [Menu("summary"), Menu("switch"), Menu("cancel")];

        List<FieldPartyActionEntry> actions = [Menu("summary")];
        if (member is not null)
            foreach (var slot in member.Pokemon.Moves)
                if (VisibleMove(slot.Move.Value, flyAvailable, rockSmashAvailable))
                    actions.Add(new(slot.Move.Value, moveName(slot.Move)));
        actions.Add(Menu("switch"));
        if (heldItemsAvailable)
            actions.Add(Menu(member is not null && SourceMailRules.IsMailItem(member.HeldItem) ? "mail" : "item"));
        actions.Add(Menu("cancel"));
        return actions.ToArray();
    }

    private static bool VisibleMove(string move, bool flyAvailable, bool rockSmashAvailable) => move switch
    {
        FieldFlySourceRules.FlyMove => flyAvailable,
        FieldRockSmashSourceRules.Move => rockSmashAvailable,
        "pret:MOVE_CUT" or "pret:MOVE_FLASH" or FieldHmSourceRules.StrengthMove or FieldHmSourceRules.SurfMove or
            "pret:MOVE_WATERFALL" => true,
        _ => NoncombatFieldMoveRules.IsSupported(move)
    };
}
