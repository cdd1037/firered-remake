using Game.Application;
using Game.Content;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private ItemfinderResponse? _itemfinderResponse;
    private double _itemfinderElapsed;
    private int _itemfinderSounds;
    private Texture2D? _itemfinderSprites;

    private void SynchronizeItemfinderPresentation()
    {
        var response = _session.State.Itemfinder?.Response;
        if (ReferenceEquals(response, _itemfinderResponse)) return;
        _itemfinderResponse = response; _itemfinderElapsed = 0; _itemfinderSounds = 0;
    }

    private int ItemfinderTick => (int)Math.Floor(_itemfinderElapsed * 60 + 1e-9);
    private bool ItemfinderMessageReady => _itemfinderResponse is null || ItemfinderTick >= ItemfinderPresentation.MessageFrame(_itemfinderResponse);

    private void ProcessItemfinderPresentation(double delta)
    {
        SynchronizeItemfinderPresentation();
        if (_itemfinderResponse is not { } response) return;
        _itemfinderElapsed += delta;
        while (_itemfinderSounds < response.Dings && ItemfinderTick >= _itemfinderSounds * ItemfinderPresentation.PulseFrames)
        {
            SourceSound("SE_ITEMFINDER"); _itemfinderSounds++;
        }
    }

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
            SynchronizeItemfinderPresentation();
            if (!ItemfinderMessageReady)
            {
                if (input is InputEventKey { Keycode: Key.Escape or Key.F12 }) return false;
                GetViewport().SetInputAsHandled(); return true;
            }
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
        SynchronizeItemfinderPresentation();
        if (task.Response.Target is not null)
        {
            _itemfinderSprites ??= Texture(System.IO.Path.Combine(_root, "content/generated/sprites/overworld/itemfinder.png"));
            for (int pulse = 0; pulse < task.Response.Dings; pulse++)
            {
                if (ItemfinderPresentation.Sprite(task.Response, _session.State.World.Player.Facing, ItemfinderTick, pulse) is not { } sprite) continue;
                DrawSetTransform(new(sprite.X, sprite.Y), Mathf.DegToRad(sprite.RotationDegrees));
                DrawTextureRectRegion(_itemfinderSprites, new(-8, -8, 16, 16), new(0, sprite.Frame * 16, 16, 16));
                DrawSetTransform(Vector2.Zero);
            }
        }
        if (ItemfinderMessageReady && _session.VisibleItemfinderMessage is {} text) ItemMessageBox(text);
    }
}
