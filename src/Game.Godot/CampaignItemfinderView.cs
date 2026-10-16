using Game.Application;
using Game.Content;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private void ExtendItemfinderQuestPocket(ref QuestItemUiDocument document)
    {
        if(_catalog.ItemfinderDocument is not {} source)return;
        foreach(var entry in source.Icons.Where(i=>i.Item!="pret:ITEM_ITEMFINDER"))
        {
            var pixels=System.IO.File.ReadAllBytes(System.IO.Path.Combine(_root,"content/generated/data/items/itemfinder-source",entry.File));
            using var itemImage=Image.CreateFromData(24,24,false,Image.Format.Rgba8,pixels);
            _earlyItemIcons.Add(new(entry.Item),ImageTexture.CreateFromImage(itemImage));
        }
        const string iconId="itemfinder-source-icon";
        var firstIcon=document.Items[0].Icon;
        var position=document.Assets.Single(a=>a.Id==firstIcon);
        var rgba=System.IO.File.ReadAllBytes(System.IO.Path.Combine(_root,"content/generated/data/items/itemfinder-source/itemfinder.rgba"));
        using var image=Image.CreateFromData(24,24,false,Image.Format.Rgba8,rgba);
        _questPocketTextures.Add(iconId,SceneTextures.FromImage(image));
        document=document with{Items=document.Items.Add(new(ItemfinderRules.Item.Value,"ITEM_ITEMFINDER","ITEMFINDER",source.ItemfinderDescription,iconId,"ItemUseOutOfBattle_Itemfinder")),
            Assets=document.Assets.Add(new(iconId,"content/generated/data/items/itemfinder-source/itemfinder.rgba",24,24,position.X,position.Y))};
    }
    private bool TryItemfinderInput(InputEvent input)
    {
        if(_session is null)return false;
        if(_session.State.Itemfinder is not null)
        {
            _held=null;
            if(input is InputEventKey {Pressed:true,Echo:false,Keycode:Key.Enter or Key.Backspace})Apply(new("itemfinder_ack"));
            else if(input is InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left or MouseButton.Right})Apply(new("itemfinder_ack"));
            else if(input is InputEventJoypadButton {Pressed:true,ButtonIndex:JoyButton.A or JoyButton.B})Apply(new("itemfinder_ack"));
            else if(input is InputEventKey {Keycode:Key.Escape or Key.F12})return false;
            QueueRedraw();GetViewport().SetInputAsHandled();return true;
        }
        bool use=input is InputEventKey {Pressed:true,Echo:false,Keycode:Key.R}||input is InputEventJoypadButton {Pressed:true,ButtonIndex:JoyButton.Back};
        if(!use||FieldMenuOpen||_session.State.ItemMenu is not null||_session.State.RegisteredItem is null)return false;
        _held=null;Apply(new("use_registered"));QueueRedraw();GetViewport().SetInputAsHandled();return true;
    }
    private void DrawItemfinder()
    {
        if(_session.State.Itemfinder is not {} task)return;
        // Direction is decoded from the original arrow index; player facing is unchanged.
        if(task.Response.Target is not null)
        {
            string response=task.Response.Target.Underfoot?"★":task.Response.Direction switch
                {Direction.North=>"↑",Direction.South=>"↓",Direction.West=>"←",Direction.East=>"→",_=>"HERE"};
            SourceText(_sourceNormal,new(116,72),response);
        }
        if(_session.VisibleItemfinderMessage is {} text)ItemMessageBox(text);
    }
}
