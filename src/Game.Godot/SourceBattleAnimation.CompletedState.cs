namespace Game.Presentation.BattleAnimations;

/// <summary>Completed battler geometry and authenticated replacement pixels, detached
/// from the animation controller. The view owns occupant identity and material-result
/// acknowledgement. Residual sprites, backgrounds and their clocks stay scene-owned.</summary>
public sealed class SourceCompletedBattlerState
{
    private sealed record Pose(PixelPoint Offset,PixelPoint ObjectOffset,
        SourceBattlerVisibilityFrame Visibility,SourceBattlerAffine Affine);
    private sealed record Material(AnimationBattler Geometry,SourcePixelAsset Pixels);
    private readonly Pose?[] _poses=new Pose?[4];
    private readonly Material?[] _materials=new Material?[4];
    private bool _doubles;
    public bool HasState=>_poses.Any(p=>p is not null)||_materials.Any(m=>m is not null);
    public int[] Slots=>Enumerable.Range(0,4).Where(i=>_poses[i] is not null||_materials[i] is not null).ToArray();
    public void Clear(){Array.Clear(_poses);Array.Clear(_materials);}
    public void DropSlot(int slot)
    {
        ValidateSlot(slot);_poses[slot]=null;_materials[slot]=null;
    }
    public bool HasMaterial(int slot){ValidateSlot(slot);return _materials[slot] is not null;}
    /// <summary>The displayed material-result receipt has caught up. Independent terminal
    /// offsets, visibility and affine state remain until the view releases their slot.</summary>
    public void DropMaterial(int slot){ValidateSlot(slot);_materials[slot]=null;}
    private static void ValidateSlot(int slot)
    {if(slot is <0 or >=4)throw new ArgumentOutOfRangeException(nameof(slot));}

    /// <summary>Replace previous holds from the pre-Tick and completed snapshots. Only
    /// changed battler geometry may retain the caller's authenticated invocation material.
    /// A nonterminal, malformed, unrestored or incompletely supplied capture fails closed.</summary>
    public bool Capture(SourceAnimationFrame initial,SourceAnimationFrame completed,
        IReadOnlyDictionary<string,SourcePixelAsset>? materials=null)
    {
        Clear();
        if(completed.Active||!Shape(initial)||!Shape(completed)||initial.Doubles!=completed.Doubles||
            !CleanMaterials(initial)||!CleanMaterials(completed))return false;
        int count=initial.Doubles?4:2;
        var poses=new Pose?[4];var heldMaterials=new Material?[4];
        for(int slot=0;slot<count;slot++)
        {
            var before=initial.Battlers.Single(b=>b.Slot.Value==slot);
            var after=completed.Battlers.Single(b=>b.Slot.Value==slot);
            if(!SameGeometry(before,after))
            {
                if(string.IsNullOrEmpty(after.Asset)||materials is null||!materials.TryGetValue(after.Asset,out var pixels)||!MaterialShape(pixels))return false;
                heldMaterials[slot]=new(after,Detach(pixels));
            }
            var beforePose=Read(initial,slot);var afterPose=Read(completed,slot);
            if(afterPose!=beforePose)poses[slot]=afterPose;
        }
        // Commit only after every changed slot has a complete detached replacement.
        _doubles=initial.Doubles;Array.Copy(poses,_poses,4);Array.Copy(heldMaterials,_materials,4);
        return HasState;
    }

    /// <summary>Inject held named body assets into a fresh idle-scene material map.
    /// Returned arrays are detached too, so a composer cannot mutate the held snapshot.</summary>
    public void AddMaterials(IDictionary<string,SourcePixelAsset> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        foreach(var material in _materials)if(material is not null)
            materials[material.Geometry.Asset]=Detach(material.Pixels);
    }

    /// <summary>Overlay held battler state onto a newly captured idle scene. This does not
    /// advance time, reuse terminal sprites/backgrounds, or mutate either input.</summary>
    public SourceAnimationFrame Apply(SourceAnimationFrame liveIdle)
    {
        if(!HasState)return liveIdle;
        if(liveIdle.Active||!Shape(liveIdle)||liveIdle.Doubles!=_doubles)
            throw new InvalidDataException("completed_battler_state_idle_frame");
        int count=liveIdle.Doubles?4:2;
        var offsets=(PixelPoint[])liveIdle.Offsets.Clone();var objects=(PixelPoint[])liveIdle.ObjectOffsets.Clone();
        var visibility=Enumerable.Range(0,count).Select(i=>Visibility(liveIdle,i)).ToArray();
        var affine=Enumerable.Range(0,count).Select(i=>Affine(liveIdle,i)).ToArray();
        var battlers=liveIdle.Battlers;
        for(int slot=0;slot<count;slot++)
        {
            if(_poses[slot] is {} pose)
            {
                // Offsets include raw x/y plus x2/y2; ObjectOffsets retain the separate
                // built-OAM latch. Neither can replace the other during idle sorting.
                offsets[slot]=pose.Offset;objects[slot]=pose.ObjectOffset;
                visibility[slot]=pose.Visibility with{Present=pose.Visibility.Present&&visibility[slot].Present};
                affine[slot]=pose.Affine;
            }
            if(_materials[slot] is {} material)
            {
                if(ReferenceEquals(battlers,liveIdle.Battlers))battlers=(AnimationBattler[])battlers.Clone();
                int at=Array.FindIndex(battlers,b=>b.Slot.Value==slot);var body=battlers[at];var replacement=material.Geometry;
                // Preserve current occupant visibility/presence; a retained body may
                // never resurrect a battler already removed by faint/switch display.
                battlers[at]=body with{Asset=replacement.Asset,YOffset=replacement.YOffset,Elevation=replacement.Elevation,
                    SourceWidth=replacement.SourceWidth,SourceHeight=replacement.SourceHeight,DisplayCenter=replacement.DisplayCenter};
            }
        }
        return liveIdle with{Offsets=offsets,ObjectOffsets=objects,BattlerVisibility=visibility,BattlerAffine=affine,Battlers=battlers};
    }

    private static bool SameGeometry(AnimationBattler before,AnimationBattler after)=>
        before.Asset==after.Asset&&before.YOffset==after.YOffset&&before.Elevation==after.Elevation&&
        before.SourceWidth==after.SourceWidth&&before.SourceHeight==after.SourceHeight&&before.DisplayCenter==after.DisplayCenter;
    private static bool MaterialShape(SourcePixelAsset? pixels)=>pixels is {Width:64,Height:64,Pixels.Length:4096}&&
        (pixels.PaletteIndices.Length==0&&pixels.Palette.Length==0||pixels.PaletteIndices.Length==4096&&pixels.Palette.Length==16&&
            pixels.PaletteIndices.All(i=>i<16)&&pixels.Palette.All(c=>c<=32767));
    private static SourcePixelAsset Detach(SourcePixelAsset pixels)=>pixels with{Pixels=(ushort[])pixels.Pixels.Clone(),
        PaletteIndices=(byte[])pixels.PaletteIndices.Clone(),Palette=(ushort[])pixels.Palette.Clone()};
    private static Pose Read(SourceAnimationFrame frame,int slot)=>new(frame.Offsets[slot],frame.ObjectOffsets[slot],Visibility(frame,slot),Affine(frame,slot));
    private static SourceBattlerAffine Affine(SourceAnimationFrame frame,int slot)=>
        frame.BattlerAffine.Length==0?new(false,256,256):frame.BattlerAffine[slot];
    private static SourceBattlerVisibilityFrame Visibility(SourceAnimationFrame frame,int slot)
    {
        if(frame.BattlerVisibility.Length!=0)return frame.BattlerVisibility.Single(v=>v.Slot==slot);
        var body=frame.Battlers.Single(b=>b.Slot.Value==slot);
        var source=body.SourceVisibility??new(body.Visible,!body.Visible,!body.Visible);
        return new(slot,source.Present,source.LogicalInvisible,source.SpriteInvisible,source.SpriteInvisible);
    }
    private static bool Shape(SourceAnimationFrame frame)
    {
        int count=frame.Doubles?4:2;
        return frame.Battlers.Length==count&&frame.Battlers.Select(b=>b.Slot.Value).Order().SequenceEqual(Enumerable.Range(0,count))&&
            frame.Offsets.Length==4&&frame.ObjectOffsets.Length==4&&
            (frame.BattlerVisibility.Length==0||frame.BattlerVisibility.Length==count&&
                frame.BattlerVisibility.Select(v=>v.Slot).Order().SequenceEqual(Enumerable.Range(0,count)))&&
            (frame.BattlerAffine.Length==0||frame.BattlerAffine.Length==count&&frame.BattlerAffine.All(a=>a is not null));
    }
    private static bool CleanMaterials(SourceAnimationFrame frame)=>
        frame.BackgroundSlots.Length==0&&frame.StaticBackgroundCopies.Length==0&&frame.Overlay is null&&
        frame.StatMask is null&&frame.Metallic is null&&!frame.BlendEnabled&&frame.BlendedObjectSlots.Length==0&&
        frame.PaletteBlends.All(p=>p.Coefficient==0&&p.Unfaded is null)&&frame.InvertedPalettes.Length==0&&frame.GreyscaleSlots.Length==0&&
        frame.BackgroundPaletteEntries.Length==0&&frame.BattlerBackgroundMosaic.Length==2&&frame.BattlerBackgroundMosaic.All(v=>!v)&&
        frame.BattlerBackgroundEnabled.Length==2&&frame.BattlerBackgroundEnabled.All(v=>v)&&
        frame.BackgroundMosaicHorizontal==0&&frame.BackgroundMosaicVertical==0&&
        (frame.MoveBackground is null||frame.MoveBackground is {Id:null,ScrollX:0,ScrollY:0,Darkening:0,ScreenSize:1});
}
