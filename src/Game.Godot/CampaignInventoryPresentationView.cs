using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Pokemon;
using Game.Domain.Tutorials;
using Godot;

public partial class CampaignView
{
    private InventoryPresentationAssets? _inventoryPresentation;
    private readonly Dictionary<string, Texture2D> _inventoryTextures = [];
    private SourceGameFont? _machineStatFont;
    private readonly MachineDiscPresentation _machineDisc = new();
    private readonly StorageHandPresentation _storageHand = new();
    private double _inventoryClock;
    private long _inventoryTick;
    private ImmutableArray<PartyPokemon> _storageHandParty = [];
    private ImmutableArray<PartyPokemon?> _storageHandBox = [];
    private PokemonStorageMoveLocation? _storageHandLocation;
    private PokemonStorageRectangle? _storageHandRectangle;
    private ImmutableArray<PartyPokemon?> _storageHandGroup = [];
    private PartyPokemon? _storageHandMoving, _storageHandTarget;
    private Vector2 _storageHandOrigin;

    private void EnsureInventoryPresentation()
    {
        if (_inventoryPresentation is not null) return;
        var assets = InventoryPresentationAssets.Load(_root);
        foreach (var asset in assets.Assets)
        {
            using var image = new Image();
            var path = Game.Content.GeneratedResourceFile.SafePath(_root, InventoryPresentationAssets.Directory + asset.TexturePath);
            if (image.LoadPngFromBuffer(System.IO.File.ReadAllBytes(path)) != Error.Ok || image.GetWidth() != asset.Width || image.GetHeight() != asset.Height)
                throw new InvalidDataException("Inventory presentation image geometry differs: " + asset.Id);
            _inventoryTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        static Color Rgb(int[] rgb) => new(rgb[0]/255f, rgb[1]/255f, rgb[2]/255f);
        _machineStatFont = SourceGameFont.Load(System.IO.Path.Combine(_root,"content/generated/ui/font"),
            foreground: Rgb(assets.StatForeground), shadow: Rgb(assets.StatShadow));
        _inventoryPresentation = assets;
    }

    private void DisposeInventoryPresentation()
    {
        _machineStatFont?.Dispose(); _machineStatFont = null;
        foreach (var texture in _inventoryTextures.Values) texture.Dispose();
        _inventoryTextures.Clear(); _inventoryPresentation = null; _machineDisc.Reset();
        ResetStorageHandPresentation(); _inventoryClock = 0; _inventoryTick = 0;
    }

    private void AdvanceInventoryPresentation(double delta)
    {
        if (!MachineActive && TeachyField?.Screen != TeachyTvFieldScreen.TmCase) _machineDisc.Reset();
        _inventoryClock += Math.Max(0,delta) * 60;
        long now = (long)Math.Floor(_inventoryClock + 1e-8);
        for (; _inventoryTick < now; _inventoryTick++)
        {
            _machineDisc.AdvanceFrame();
            bool active = _storageHand.Active;
            _storageHand.AdvanceFrame();
            if (active && !_storageHand.Active) ClearStorageHandSnapshot();
        }
        if (MachineActive || TeachyField?.Screen == TeachyTvFieldScreen.TmCase || _session.State.Pc?.View == CampaignPcView.Move)
            QueueRedraw();
    }

    private void ResetStorageHandPresentation() { _storageHand.Reset(); ClearStorageHandSnapshot(); }
    private void ClearStorageHandSnapshot()
    {
        _storageHandParty = []; _storageHandBox = []; _storageHandLocation = null; _storageHandRectangle = null;
        _storageHandMoving = _storageHandTarget = null; _storageHandGroup = [];
    }

    private Vector2 StorageHandPosition => _storageTitleSelected ? new(162,12)
        : _storageMovePartyOpen ? _storageMovePartyCursor == 0 ? new(104,52)
            : _storageMovePartyCursor == 6 ? new(152,132) : new(152,(_storageMovePartyCursor-1)*24+4)
        : new(100+_boxCursor%6*24,32+_boxCursor/6*24);

    private void BeginStorageHandPresentation(CampaignCommand command, CampaignState before)
    {
        var action = command.Kind switch
        {
            "storage_move_grab" => StorageHandAction.Grab,
            "storage_move_place" => StorageHandAction.Place,
            "storage_move_shift" => StorageHandAction.Shift,
            "storage_group_grab" => StorageHandAction.GroupGrab,
            "storage_group_place" => StorageHandAction.GroupPlace,
            _ => StorageHandAction.None
        };
        if (action == StorageHandAction.None) return;
        EnsureInventoryPresentation();
        // Retain only the visible immutable slot arrays; never retain the campaign or other boxes.
        _storageHandParty = before.Party; _storageHandBox = before.Storage!.Boxes[before.Storage.CurrentBox].Slots;
        _storageHandLocation = command.StorageLocation;
        _storageHandOrigin = StorageHandPosition;
        if (command.StorageLocation is {} location)
        {
            _storageHandTarget = location.IsParty ? before.Party.ElementAtOrDefault(location.SlotIndex)
                : before.Storage!.Boxes[location.BoxIndex].Slots[location.SlotIndex];
        }
        _storageHandMoving = action == StorageHandAction.Grab ? _storageHandTarget : before.Pc?.MoveCursor?.Pokemon;
        if (action == StorageHandAction.GroupGrab)
        {
            var group = _session.State.Pc!.GroupCursor!;
            _storageHandRectangle = group.Origin; _storageHandGroup = group.Cells;
            _storageHandOrigin = new(100+group.Origin.Left*24,32+group.Origin.Top*24);
        }
        else if (action == StorageHandAction.GroupPlace)
        {
            _storageHandRectangle = before.Pc!.GroupCursor!.Origin;
            _storageHandGroup = before.Pc.GroupCursor.Cells;
        }
        _storageHand.Begin(action);
    }

    private bool StorageHandHidesSlot(bool party, int slot)
    {
        if (!_storageHand.Active) return false;
        if (_storageHand.Action == StorageHandAction.GroupGrab && !party && _storageHandRectangle is {} rect)
            return slot%6 >= rect.Left && slot%6 < rect.Left+rect.Width && slot/6 >= rect.Top && slot/6 < rect.Top+rect.Height;
        return _storageHandLocation is {} location && location.IsParty == party && location.SlotIndex == slot &&
            _storageHand.Action is StorageHandAction.Grab or StorageHandAction.Shift;
    }

    private void DrawStorageHand()
    {
        EnsureInventoryPresentation();
        var held = _session.State.Pc!.MoveCursor?.Pokemon;
        bool group = _session.State.Pc.GroupCursor is not null;
        Vector2 center = _storageHand.Active ? _storageHandOrigin : StorageHandPosition;
        var pose = _storageHand.Pose(_inventoryPresentation!.ShiftOffsets);
        void Icon(PartyPokemon? pokemon, int x, int y)
        {
            if (pokemon is not null) DrawTexture(PresentedOwnedIcon(pokemon),center+new Vector2(x-16,y-4));
        }
        if (_storageHand.Active)
        {
            if (_storageHand.Action == StorageHandAction.Shift)
            {
                if (pose.TargetOnTop) { Icon(_storageHandMoving,pose.MovingX,pose.MovingY); Icon(_storageHandTarget,pose.TargetX,pose.TargetY); }
                else { Icon(_storageHandTarget,pose.TargetX,pose.TargetY); Icon(_storageHandMoving,pose.MovingX,pose.MovingY); }
            }
            else if (_storageHandRectangle is {} rectangle)
            {
                for (int i=0;i<_storageHandGroup.Length;i++)
                    Icon(_storageHandGroup[i],i%rectangle.Width*24,i/rectangle.Width*24+pose.MovingY);
            }
            else Icon(_storageHandMoving,0,pose.MovingY);
        }
        else if (held is not null) Icon(held,0,-8);
        int frame = _storageHand.Active ? pose.HandFrame : held is not null || group ? 3
            : StorageCreatureOverlayActive || _storageRectangleAnchor is not null ? 0 : (int)(_inventoryTick/30%2);
        DrawTextureRectRegion(_inventoryTextures[_storageMultiMode?"hand-multi":"hand-single"],
            new(center+new Vector2(-16,-16+(_storageHand.Active?pose.HandY:0)),new(32,32)),new(0,frame*32,32,32));
    }

    private void DrawStorageHandShadow()
    {
        if (_session.State.Pc?.View != CampaignPcView.Move || _storageMovePartyOpen || _storageTitleSelected) return;
        EnsureInventoryPresentation();
        DrawTexture(_inventoryTextures["hand-shadow"], (_storageHand.Active ? _storageHandOrigin : StorageHandPosition) + new Vector2(-8,12));
    }
}
