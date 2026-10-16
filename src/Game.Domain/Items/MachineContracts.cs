using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record MachineItem(QuestItemDefinition Item, string Description);
public sealed record MachineCompatibility(SpeciesId Species, int SourceSpeciesId, bool CanLearn);
public sealed record MachineDefinition(MachineItem Machine, MoveId Move, int SourceMoveId, int MovePp,
    ImmutableArray<MachineCompatibility> Compatibility);
public sealed record MachineCatalog(MachineItem TmCase, ImmutableArray<MachineDefinition> Machines,
    int MachineSlots, int KeyItemSlots, int MaximumStack);
public sealed record MachineTeachResult(InventoryState Inventory, PartyPokemon Pokemon,
    bool Succeeded, string? Error, int? MoveSlot = null, int ItemsConsumed = 0, int FriendshipDelta = 0);
