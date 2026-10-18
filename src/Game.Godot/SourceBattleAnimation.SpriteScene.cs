using System.Collections;
namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    /// <summary>Battle-scene sprite lifetime, allocation and OAM order. A script owns
    /// its callbacks/materials; ending that script releases only its controller.</summary>
    public sealed class SpriteScene
    {
        internal readonly SourceSpriteRegistry<VisualSprite> Sprites=new(s=>s.Slot);
        private SourceBattleAnimation? _current;
        internal int[] Order=Enumerable.Range(0,64).ToArray();
        internal readonly int[] Y=Enumerable.Repeat(160,64).ToArray();
        private AnimationBattler[]? _idleBattlers;
        public int Count=>Sprites.Count;
        public bool HasResidualSprites=>Sprites.Any(s=>!s.Owner!.Active&&!s.Counted);
        internal void Attach(SourceBattleAnimation owner)
        {
            if(_current is {Active:true})throw new InvalidOperationException("A scene already has an active animation controller.");
            _current=owner;_idleBattlers=null;
        }
        internal bool Reserved(int slot)=>_idleBattlers is {} idle?idle.Any(b=>b.Slot.Value==slot&&InitialSourceVisibility(b).Present):
            _current is {} owner&&owner.IsSourceBattlerPresent(slot);
        internal void EnsureController(SourceBattleAnimation owner)
        {if(!ReferenceEquals(_current,owner))throw new InvalidOperationException("Only the current controller may advance scene sprites.");}
        internal void Advance()=>Sprites.Advance(s=>s.Owner!.StepSourceSprite(s),s=>s.AdvanceAnimation(s.Owner!));
        public SourceAnimationFrame TickIdle(AnimationBattler[] battlers,bool doubles,SourceCompletedBattlerState? completedState=null)
        {
            if(_current is {Active:true})throw new InvalidOperationException("The active controller owns the scene tick.");
            _idleBattlers=battlers;
            Advance();return CaptureIdle(battlers,doubles,completedState);
        }
        public SourceAnimationFrame CaptureIdle(AnimationBattler[] battlers,bool doubles,SourceCompletedBattlerState? completedState=null)
        {
            if(_current is {Active:true})throw new InvalidOperationException("The active controller owns the scene frame.");
            _idleBattlers=battlers;
            var frame=new SourceAnimationFrame(-1,false,new PixelPoint[4],[],0,0,[],[]){Battlers=battlers,Doubles=doubles};
            frame=completedState?.Apply(frame)??frame;
            _current?.SortSourceSprites(frame.Battlers,doubles,frame);
            return frame with{Sprites=_current?.SpriteOamSnapshot()??[],
                BattlerOamRanks=battlers.Select(b=>Array.IndexOf(Order,b.Slot.Value)).ToArray()};
        }
        public void Cancel()
        {
            var owners=Sprites.Select(s=>s.Owner!).Append(_current).OfType<SourceBattleAnimation>().Distinct().ToArray();
            foreach(var owner in owners)owner.Cancel();
            Sprites.Clear();_current=null;_idleBattlers=null;
            Order=Enumerable.Range(0,64).ToArray();Array.Fill(Y,160);
        }
    }
    // The callback's view is owner-scoped; allocation and stepping are scene-scoped.
    // This preserves existing callback counters without giving an old script authority
    // over a newer script's objects or material state.
    private sealed class OwnedSprites(SourceBattleAnimation owner,SpriteScene scene):IEnumerable<VisualSprite>
    {
        public void Add(VisualSprite sprite){sprite.Owner=owner;scene.Sprites.Add(sprite);}
        public bool Remove(VisualSprite sprite)=>ReferenceEquals(sprite.Owner,owner)&&scene.Sprites.Remove(sprite);
        public void Clear(){foreach(var sprite in this.ToArray())Remove(sprite);}
        public int FindFreeSlot(Func<int,bool>? reserved=null)=>scene.Sprites.FindFreeSlot(reserved);
        public int CreateAndAnimate(Func<int,VisualSprite> factory,Action<VisualSprite> callback,Action<VisualSprite> animate,Func<int,bool>? reserved=null)
        {
            int slot=FindFreeSlot(reserved);if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)return slot;
            var sprite=factory(slot);if(sprite.Slot!=slot)throw new InvalidOperationException("Source sprite factory changed its allocated slot.");
            Add(sprite);scene.Sprites.AdvanceSlot(slot,callback,animate);return slot;
        }
        public IEnumerator<VisualSprite> GetEnumerator()=>scene.Sprites.Where(s=>ReferenceEquals(s.Owner,owner)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private readonly Dictionary<int,ushort[]> _retiredSpritePalettes=[];
    private void CaptureRetiredSpritePalettes()
    {
        if(!_sprites.Any(s=>!s.Counted))return;
        foreach(var (tag,palette) in SpritePaletteTagSnapshot())_retiredSpritePalettes[tag]=palette;
    }
    // OAM geometry is fixed before RunTasks. Palette transfer is later, so bind
    // each captured object's owning palette only when the complete frame is read.
    private SourceVisualSpriteFrame[] SpriteOamSnapshot()=>_spriteOam.Select((frame,i)=>_spriteOamOwners[i].MaterialFrame(frame)).ToArray();
    private SourceVisualSpriteFrame MaterialFrame(SourceVisualSpriteFrame frame)
    {
        // Battler copies resolve their body and detached palette through the battler composer,
        // not the effect-sheet catalog. Missing ordinary effect assets still fail closed.
        if(frame.BattlerCopySlot>=0)return frame;
        SourcePixelAsset? material=frame.Material??(frame.Asset is {} id&&_spriteMaterials is not null?_spriteMaterials[id]:null);
        ushort[]? palette=null;
        if(frame.PaletteSlot is int slot)palette=(ushort[])_spritePaletteBanks[slot].Clone();
        else if(material is {PaletteTag:>=0})
        {
            int bank=FindSpritePalette(material.PaletteTag);
            palette=bank!=255?(ushort[])_spritePaletteBanks[bank].Clone():
                _retiredSpritePalettes.TryGetValue(material.PaletteTag,out var retained)?retained:material.Palette;
        }
        return frame with{Material=material,MaterialPalette=palette};
    }
}
