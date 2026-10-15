using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record PewterMachineItem(QuestItemDefinition Item, string Description);
public sealed record PewterMachineCompatibility(SpeciesId Species, int SourceSpeciesId,
    bool CanLearn);
public sealed record PewterMachineCatalog(string SourceCommit, string Profile, bool CompleteItemCatalog,
    int MachineSlots, int KeyItemSlots, int MaximumStack, MoveId Move, int SourceMoveId, int MovePp,
    PewterMachineItem Tm39, PewterMachineItem TmCase,
    ImmutableArray<PewterMachineCompatibility> Compatibility)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<PewterAdditionalMachine>? AdditionalMachines { get; init; }
}
public sealed record PewterAdditionalMachine(PewterMachineItem Machine, MoveId Move, int SourceMoveId, int MovePp,
    ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record PewterMachineTeachResult(InventoryState Inventory, PartyPokemon Pokemon,
    bool Succeeded, string? Error, int? MoveSlot = null, int ItemsConsumed = 0, int FriendshipDelta = 0);
