using Game.Content;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;
public partial class CampaignView
{
    private void LoadBillCreaturePresentation()
    {
        if (_catalog.BillPokemon is not { } source) return;

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
        foreach(var icon in _catalog.BillItems!.Icons.Where(i=>i.Item.Value is "pret:ITEM_NUGGET" or "pret:ITEM_FAME_CHECKER" or "pret:ITEM_SS_TICKET").Concat(PretBillMachines.LoadProject(_root).Icons.Where(i=>i.Item.Value is "pret:ITEM_TM45" or "pret:ITEM_TM43" or "pret:ITEM_TM28")))
        {
            
            _earlyItemIcons.Add(icon.Item,ItemTexture(_catalog.BillItems!.Icons.Contains(icon) ? "data/items/bill" : "data/machine-learning", icon));
        }
        var document=_questPocketUi??throw new InvalidDataException("Bill key pocket missing.");
        var position=document.Assets.Single(a=>a.Id==document.Items[0].Icon);
        foreach(var value in _catalog.BillItems.Items.Where(x=>x.Metadata.Item.Pocket=="POCKET_KEY_ITEMS"&&x.Metadata.Item.Id.Value is "pret:ITEM_FAME_CHECKER" or "pret:ITEM_SS_TICKET").Select(x=>x.Metadata))
        {
            var icon=_catalog.BillItems.Icons.Single(i=>i.Item==value.Item.Id);
            string id="bill-"+value.Item.SourceItemSymbol.ToLowerInvariant();
            
            _questPocketTextures.Add(id,ItemTexture(_catalog.BillItems!.Icons.Contains(icon) ? "data/items/bill" : "data/machine-learning", icon));
            document=document with
            {
                Items=document.Items.Add(new(value.Item.Id.Value,value.Item.SourceItemSymbol,value.Item.Name,value.Description,id,value.Item.FieldUseFunction)),
                Assets=document.Assets.Add(new(id,icon.PngPath,icon.Width,icon.Height,position.X,position.Y))
            };
        }
        _questPocketUi=document;
    }
}
