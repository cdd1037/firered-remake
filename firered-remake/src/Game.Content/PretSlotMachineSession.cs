using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Slots;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    // Current production supplies the approved None/0 and circular21 safety policy.
    public SlotMachineSessionContract? GameCornerSlots { get; init; }
}
