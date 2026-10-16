using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private static int MachineDiscIndex(MachineDefinition definition)
    {
        string id=definition.Machine.Item.Id.Value;
        bool hm=id.StartsWith("pret:ITEM_HM",StringComparison.Ordinal);
        if ((!hm&&!id.StartsWith("pret:ITEM_TM",StringComparison.Ordinal)) || !int.TryParse(id[^2..],out int number) ||
            number<1 || number>(hm?8:50)) throw new InvalidDataException("Invalid machine disc identifier.");
        return (hm?50:0)+number-1;
    }

    /// <summary>Original five-row TM Case, shared with the tutorial's explicit temporary inventory.</summary>
    private void DrawMachineList(InventoryStack[] stacks, int cursor, int start, int visibleCount, bool shaded=false, bool tutorial=false)
    {
        ArgumentNullException.ThrowIfNull(stacks);
        if (start<0 || start>stacks.Length || visibleCount is <1 or >5) throw new ArgumentOutOfRangeException(nameof(start));
        EnsureInventoryPresentation();
        int selected=Math.Clamp(cursor,0,stacks.Length);
        var definition=selected<stacks.Length?MachineRules.Definition(_catalog.Machines!,stacks[selected].Item):null;
        _machineDisc.Select(definition is null?-1:MachineDiscIndex(definition));
        string gender=_session.State.PlayerGender==1?"female":"male";
        DrawTexture(_inventoryTextures["case-"+gender+(shaded?"-shaded":"")],Vector2.Zero);
        if (_machineDisc.Displayed>=0)
        {
            var shown=_catalog.Machines!.Machines.Single(m=>MachineDiscIndex(m)==_machineDisc.Displayed);
            var move=_catalog.PokemonContent!.Catalog.Moves[shown.Move];
            int type=Array.IndexOf(_inventoryPresentation!.DiscTypes,move.Type);
            if(type<0)throw new InvalidDataException("Machine disc type has no palette.");
            var position=MachineDiscPresentation.Position(_machineDisc.Displayed);
            DrawTextureRectRegion(_inventoryTextures[tutorial?"discs-tutorial":"discs"],new(position.X-16,position.Y-16+_machineDisc.OffsetY,32,32),
                new(type*32,_machineDisc.Displayed>=50?32:0,32,32));
        }
        DrawTexture(_inventoryTextures["case-"+gender+"-front"],Vector2.Zero);
        string title=_catalog.TextPages("gText_TMCase")[0];
        float titleWidth=_sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title),fontSize:_sourceWhite.Size).X;
        SourceText(_sourceWhite,new((int)((72-titleWidth)/2),9),title);
        for(int i=start;i<Math.Min(stacks.Length,start+visibleCount);i++)
        {
            var row=MachineRules.Definition(_catalog.Machines!,stacks[i].Item)??throw new InvalidDataException("machine_list_definition_missing");
            int y=MachineListY+(i-start)*MachineListRowHeight,index=MachineDiscIndex(row);
            bool hm=index>=50;
            if(hm)DrawTexture(_inventoryTextures["hm"],new(88,y));
            int numberX=hm?106:88;
            DrawTexture(_inventoryTextures["number"],new(numberX,y));
            string number=(hm?index-49:index+1).ToString(hm?"0":"00");
            SourceText(_sourceSmall,new(numberX+9,y),number);
            float width=_sourceSmall.Font.GetStringSize(number,fontSize:_sourceSmall.Size).X;
            SourceText(_sourceNormal,new(numberX+9+width+6,y),MoveName(row.Move));
            if(!hm)SourceText(_sourceSmall,new(206,y),_itemSkin.FormatText("gText_TimesStrVar1",stacks[i].Quantity.ToString().PadLeft(3)));
        }
        if(stacks.Length<start+visibleCount)SourceText(_sourceNormal,new(88,MachineListY+(stacks.Length-start)*16),_catalog.TextPages("gText_Close")[0]);
        SourceText(_sourceNormal,new(80,MachineListY+(selected-start)*16),_itemSkin.Text("gText_SelectorArrow2"));
        DrawTexture(_inventoryTextures["move-labels"],new(8,104));
        string[] values=["---","---","---","---"];
        if(definition is not null)
        {
            var move=_catalog.PokemonContent!.Catalog.Moves[definition.Move];
            DrawPokedexVisual(move.Type,56,104);
            values=["",move.Power<2?"---":move.Power.ToString().PadLeft(3),move.Accuracy==0?"---":move.Accuracy.ToString().PadLeft(3),move.Pp.ToString().PadLeft(3)];
        }
        for(int i=0;i<values.Length;i++)SourceText(_machineStatFont!,new(63,104+i*12),values[i]);
        ItemLines(_sourceWhite,new(98,99),definition?.Machine.Description??_catalog.TextPages("gText_TMCaseWillBePutAway")[0],14);
        if(!shaded && start>0)DrawMachineScrollArrow(160,8,true);
        if(!shaded && start+visibleCount<stacks.Length+1)DrawMachineScrollArrow(160,88,false);
    }

    private void DrawMachineScrollArrow(int x,int y,bool up)
    {
        int bounce=_inventoryPresentation!.ArrowOffsets[(int)(_inventoryTick%32)];
        DrawTexture(_inventoryTextures[up?"scroll-up":"scroll-down"],new(x-8,y-8+(up?bounce:-bounce)));
    }
}
