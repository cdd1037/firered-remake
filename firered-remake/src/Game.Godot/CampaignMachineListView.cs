using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    /// <summary>Shared current TM list pixels, reading an explicit inventory/cursor projection.
    /// A tutorial supplies its temporary stacks; no player menu cursor or selected TM is changed.
    /// The original TM CASE background remains outside this existing bounded white-window skin.</summary>
    private void DrawMachineList(InventoryStack[] stacks, int cursor, int start, int visibleCount)
    {
        ArgumentNullException.ThrowIfNull(stacks);
        if (start < 0 || start > stacks.Length || visibleCount < 1)
            throw new ArgumentOutOfRangeException(nameof(start));
        DrawTexture(_questPocketTextures[MachineCaseIcon], new(8, 8));
        SourceText(_sourceNormal, new(40, 12), _catalog.TextPages("gText_TMCase")[0]);
        for (int i = start; i < Math.Min(stacks.Length, start + visibleCount); i++)
        {
            var definition = MachineRules.Definition(_catalog.PewterMachines!, stacks[i].Item)
                ?? throw new InvalidDataException("machine_list_definition_missing");
            SourceText(_sourceNormal, new(118, 40 + (i - start) * 16), definition.Machine.Item.Name + " " + MoveName(definition.Move));
        }
        if (stacks.Length < start + visibleCount)
            SourceText(_sourceNormal, new(118, 40 + (stacks.Length - start) * 16), _catalog.TextPages("gText_Close")[0]);
        int selected = Math.Clamp(cursor, 0, stacks.Length);
        SourceText(_sourceNormal, new(106, 40 + (selected - start) * 16), _itemSkin.Text("gText_SelectorArrow2"));
        if (selected < stacks.Length)
        {
            var stack = stacks[selected];
            if (_earlyItemIcons.TryGetValue(stack.Item, out var icon)) DrawTexture(icon, new(8, 40));
            else DrawTexture(_questPocketTextures[MachineIcon], new(8, 40));
            SourceText(_sourceSmall, new(44, 50), _itemSkin.FormatText("gText_TimesStrVar1", stack.Quantity.ToString()));
            ItemLines(_sourceSmall, new(8, 80), MachineRules.Definition(_catalog.PewterMachines!, stack.Item)!.Machine.Description, 13);
        }
        else ItemLines(_sourceSmall, new(8, 128), _catalog.TextPages("gText_TMCaseWillBePutAway")[0], 13);
    }
}
