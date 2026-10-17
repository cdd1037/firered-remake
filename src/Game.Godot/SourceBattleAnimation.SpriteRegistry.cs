using System.Collections;
namespace Game.Presentation.BattleAnimations;

// Replaces the runtime's sprite list; this array is the sole effect-sprite authority.
// Resource allocation, callbacks, counted ownership and OAM ranking stay with the caller.
internal sealed class SourceSpriteRegistry<T>(Func<T,int> slotOf):IReadOnlyCollection<T> where T:class
{
    public const int Capacity=64;
    private readonly T?[] _slots=new T?[Capacity];
    public int Count{get;private set;}
    public T? this[int slot]=>_slots[CheckedSlot(slot)];
    private static int CheckedSlot(int slot)=>slot is >=0 and <Capacity?slot:throw new ArgumentOutOfRangeException(nameof(slot));
    public int FindFreeSlot(Func<int,bool>? reserved=null,bool atEnd=false)
    {
        for(int i=0;i<Capacity;i++)
        {
            int slot=atEnd?Capacity-1-i:i;
            if(_slots[slot] is null&&!(reserved?.Invoke(slot)??false))return slot;
        }
        return Capacity; // Source failure sentinel, never a live effect-sprite cell.
    }
    public void Add(T sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);int slot=CheckedSlot(slotOf(sprite));
        if(_slots[slot] is not null)throw new InvalidOperationException("Source sprite slot is already occupied.");
        _slots[slot]=sprite;Count++;
    }
    public bool Remove(T sprite)
    {
        int slot=CheckedSlot(slotOf(sprite));
        if(!ReferenceEquals(_slots[slot],sprite))return false;
        _slots[slot]=null;Count--;return true;
    }
    public T? RemoveAt(int slot)
    {
        slot=CheckedSlot(slot);var sprite=_slots[slot];
        if(sprite is not null){_slots[slot]=null;Count--;}
        return sprite;
    }
    public void Clear(){Array.Clear(_slots);Count=0;}
    // Factory only constructs initial data. Source callbacks run after insertion.
    // Existing eager constructors keep their reviewed Add path until explicitly migrated.
    public int Create(Func<int,T> factory,Func<int,bool>? reserved=null,bool atEnd=false)
    {
        int slot=FindFreeSlot(reserved,atEnd);if(slot==Capacity)return slot;
        var sprite=factory(slot);
        if(slotOf(sprite)!=slot)throw new InvalidOperationException("Source sprite factory changed its allocated slot.");
        Add(sprite);return slot;
    }
    public int CreateAndAnimate(Func<int,T> factory,Action<T> callback,Action<T> animate,Func<int,bool>? reserved=null)
    {
        int slot=Create(factory,reserved);if(slot!=Capacity)AdvanceSlot(slot,callback,animate);return slot;
    }
    public void Advance(Action<T> callback,Action<T> animate)
    {
        // The live cell is read as the cursor reaches it. No callback snapshot.
        for(int slot=0;slot<Capacity;slot++)AdvanceSlot(slot,callback,animate);
    }
    public void AdvanceSlot(int slot,Action<T> callback,Action<T> animate)
    {
        slot=CheckedSlot(slot);
        if(_slots[slot] is not {} sprite)return;
        callback(sprite);
        // The callback may destroy/reallocate this very cell. The source advances
        // the replacement's player, without invoking its callback a second time.
        if(_slots[slot] is {} current)animate(current);
    }
    public IEnumerator<T> GetEnumerator()
    {
        for(int i=0;i<Capacity;i++)if(_slots[i] is {} sprite)yield return sprite;
    }
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}
