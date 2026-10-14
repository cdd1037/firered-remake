using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.Tutorials;

public enum TeachyTvFieldScreen { Bag, BagExit, TmCase, Completed }
public enum TeachyTvBagPocket { Items, KeyItems, Balls }
public sealed record TeachyTvFieldMachine(PewterAdditionalMachine Machine, PokemonMoveDefinition Move);
public sealed record TeachyTvFieldDemoData(InventoryState BagInventory, InventoryState TmInventory,
    QuestItemDefinition TeachyTvItem, ImmutableArray<TeachyTvFieldMachine> Machines);
public sealed record TeachyTvFieldDemoInput(bool Confirm = false, bool Cancel = false,
    bool PaletteFadeActive = false, bool TextPrinterActive = false, bool MenuTransitionActive = false);
public sealed record TeachyTvFieldDemoEffect(string Kind, string? Value = null,
    uint PaletteMask = 0, int Delay = 0, int From = 0, int To = 0);
public sealed record TeachyTvFieldDemoState(string Ticket, TeachyTvDemonstrationKind Kind,
    TeachyTvFieldDemoData Data, long Revision = 0, TeachyTvFieldScreen Screen = TeachyTvFieldScreen.Bag,
    int BagFrame = 0, int TmState = 0, int Timer = 0,
    TeachyTvBagPocket Pocket = TeachyTvBagPocket.Items, int Cursor = 0,
    bool ContextOpen = false, int ContextCursor = 0, string? SelectedItem = null,
    string? RegisteredItem = null, InventoryState? Inventory = null,
    bool DescriptionShaded = false, string? MessageSymbol = null, bool Interrupted = false)
{
    public string WaitId => Ticket + ":field:" + Revision.ToString(CultureInfo.InvariantCulture);
    public bool Completed => Screen == TeachyTvFieldScreen.Completed;
    public bool AwaitingConfirm => Screen == TeachyTvFieldScreen.TmCase && TmState is 10 or 20;
    public TeachyTvFieldMachine? SelectedMachine => Screen == TeachyTvFieldScreen.TmCase && Cursor < Data.Machines.Length
        ? Data.Machines[Cursor] : null;
}
public sealed record TeachyTvFieldDemoDecision(bool Accepted, TeachyTvFieldDemoState State,
    ImmutableArray<TeachyTvFieldDemoEffect> Effects, string? Error = null);

/// <summary>
/// The two original field demonstrations. No player state enters this owner:
/// every bag, cursor and registered item belongs exclusively to the clip.
/// The source TM lesson browses categories/descriptions; it never teaches a move,
/// opens the party/summary screen or consumes a machine. Do not add a teaching step.
/// One Step is one active source controller frame, not one elapsed wall-clock frame.
/// UI passes its real fade/text/pocket-animation gates and renders returned effects.
/// </summary>
public static class TeachyTvFieldDemoRules
{
    public const string RulesVersion = "firered-teachy-tv-field-v1";
    public const int PokedudeInputDelay = 101;
    private static readonly ItemId[] TutorialMachines = [new("pret:ITEM_TM01"), new("pret:ITEM_TM03"),
        new("pret:ITEM_TM09"), new("pret:ITEM_TM35")];

    public static TeachyTvFieldDemoData CreateData(QuestItemsCatalog quests, EarlyShopCatalog items,
        PewterMachineCatalog machines, PokemonCatalog pokemon)
    {
        QuestItemRules.ValidateCatalog(quests);
        EarlyShopInventoryRules.ValidateCatalog(items);
        PewterMachineRules.ValidateCatalog(machines);
        if (pokemon.SourceCommit != OpeningInventoryRules.SourceCommit)
            throw new ArgumentException("teachy_tv_pokemon_source", nameof(pokemon));
        var tv = quests.Items.Single(i => i.Id == QuestItemRules.TeachyTvId);
        if (!KeyItemRegistrationRules.CanRegister(tv)) throw new ArgumentException("teachy_tv_registration_source");
        foreach (string symbol in new[] { "ITEM_POTION", "ITEM_ANTIDOTE", "ITEM_POKE_BALL", "ITEM_GREAT_BALL", "ITEM_NEST_BALL" })
            if (!items.Items.Any(i => i.SourceItemSymbol == symbol)) throw new ArgumentException("teachy_tv_item_source:" + symbol);
        // InitPokedudeBag's seven AddBagItem calls retain pocket-local insertion order.
        var bag = new InventoryState([new(new("pret:ITEM_POTION"), 1), new(new("pret:ITEM_ANTIDOTE"), 1)], [], items.Limits)
        {
            KeyItems = [new(QuestItemRules.TeachyTvId, 1), new(PewterMachineRules.TmCaseId, 1)],
            Balls = [new(QuestItemRules.PokeBallId, 5), new(new("pret:ITEM_GREAT_BALL"), 1), new(new("pret:ITEM_NEST_BALL"), 1)]
        };
        var tms = new InventoryState([], [], items.Limits);
        var definitions = ImmutableArray.CreateBuilder<TeachyTvFieldMachine>();
        foreach (var id in TutorialMachines)
        {
            var result = PewterMachineRules.AddMachine(machines, tms, id);
            if (!result.Succeeded) throw new ArgumentException("teachy_tv_machine_source:" + result.Error);
            tms = result.State;
            var definition = PewterMachineRules.Definition(machines, id)!;
            if (!pokemon.Moves.TryGetValue(definition.Move, out var move) ||
                move.SourceMoveId != definition.SourceMoveId || move.Pp != definition.MovePp)
                throw new ArgumentException("teachy_tv_machine_move_source");
            definitions.Add(new(definition, move));
        }
        return new(bag, tms, tv, definitions.ToImmutable());
    }

    public static TeachyTvFieldDemoState Begin(string ticket, TeachyTvDemonstrationKind kind, TeachyTvFieldDemoData data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket);
        ArgumentNullException.ThrowIfNull(data);
        if (kind is not (TeachyTvDemonstrationKind.TmCase or TeachyTvDemonstrationKind.RegisterItem))
            throw new ArgumentOutOfRangeException(nameof(kind));
        return new(ticket, kind, data, Inventory: data.BagInventory);
    }

    public static TeachyTvFieldDemoDecision Step(TeachyTvFieldDemoState state, string waitId, TeachyTvFieldDemoInput input)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(input);
        if (state.WaitId != waitId) return new(false, state, [], "teachy_tv_field_stale_input");
        if (state.Completed) return new(false, state, [], "teachy_tv_field_completed");
        var effects = ImmutableArray.CreateBuilder<TeachyTvFieldDemoEffect>();
        var next = state.Screen switch
        {
            TeachyTvFieldScreen.Bag => Bag(state, input, effects),
            TeachyTvFieldScreen.BagExit => ExitBag(state, input, effects),
            TeachyTvFieldScreen.TmCase => TmCase(state, input, effects),
            _ => throw new InvalidOperationException("teachy_tv_field_screen")
        };
        return new(true, next with { Revision = checked(state.Revision + 1) }, effects.ToImmutable());
    }

    private static TeachyTvFieldDemoState Bag(TeachyTvFieldDemoState s, TeachyTvFieldDemoInput input,
        ImmutableArray<TeachyTvFieldDemoEffect>.Builder effects)
    {
        // The bag task is replaced during SwitchPockets. B cannot interrupt that
        // task or an active fade; Task_BButtonInterruptTeachyTv runs afterward.
        if (input.PaletteFadeActive || input.MenuTransitionActive) return s;
        if (input.Cancel) return StartBagExit(s with { Interrupted = true }, effects, false);
        bool register = s.Kind == TeachyTvDemonstrationKind.RegisterItem;
        switch (s.BagFrame)
        {
            case 102:
                effects.Add(new("sound", "SE_BAG_POCKET"));
                effects.Add(new("pocket_switch", "POCKET_KEY_ITEMS"));
                s = s with { Pocket = TeachyTvBagPocket.KeyItems, Cursor = 0 };
                break;
            case 204 when !register:
                s = s with { Cursor = 1 };
                effects.Add(new("cursor", "down"));
                break;
            case 204 when register:
            case 306 when !register:
                string selected = register ? QuestItemRules.TeachyTvId.Value : PewterMachineRules.TmCaseId.Value;
                s = s with { ContextOpen = true, ContextCursor = 0, SelectedItem = selected };
                effects.Add(new("sound", "SE_SELECT"));
                effects.Add(new("context_open", selected));
                break;
            case 306 when register:
                s = s with { ContextCursor = 1 };
                effects.Add(new("sound", "SE_SELECT"));
                effects.Add(new("context_cursor", "register"));
                break;
            case 408 when register:
                var registered = KeyItemRegistrationRules.Toggle(s.Inventory!, s.Data.TeachyTvItem, s.RegisteredItem);
                if (!registered.Succeeded || registered.RegisteredItem != QuestItemRules.TeachyTvId.Value)
                    throw new InvalidOperationException("teachy_tv_temporary_registration:" + registered.Error);
                s = s with { RegisteredItem = registered.RegisteredItem, ContextOpen = false, SelectedItem = null };
                effects.Add(new("sound", "SE_SELECT"));
                effects.Add(new("registered_item", registered.RegisteredItem));
                break;
            case 408:
                effects.Add(new("sound", "SE_SELECT"));
                return StartBagExit(s, effects, false);
            case 510 when register:
            case 612 when register:
                s = s with { Cursor = s.Cursor + 1 };
                effects.Add(new("cursor", "down"));
                break;
            case 714 when register:
                effects.Add(new("sound", "SE_SELECT"));
                return StartBagExit(s, effects, true);
        }
        return s with { BagFrame = s.BagFrame + 1 };
    }

    private static TeachyTvFieldDemoState StartBagExit(TeachyTvFieldDemoState s,
        ImmutableArray<TeachyTvFieldDemoEffect>.Builder effects, bool closeWindow)
    {
        effects.Add(new("release_temporary_bag"));
        if (closeWindow) effects.Add(new("bag_close_window"));
        effects.Add(new("palette_fade", PaletteMask: uint.MaxValue, Delay: -2, From: 0, To: 16));
        return s with { Screen = TeachyTvFieldScreen.BagExit, Inventory = null, RegisteredItem = null,
            ContextOpen = false, SelectedItem = null };
    }

    private static TeachyTvFieldDemoState ExitBag(TeachyTvFieldDemoState s, TeachyTvFieldDemoInput input,
        ImmutableArray<TeachyTvFieldDemoEffect>.Builder effects)
    {
        if (input.PaletteFadeActive || input.MenuTransitionActive) return s;
        if (s.Interrupted || s.Kind == TeachyTvDemonstrationKind.RegisterItem) return Complete(s, effects);
        effects.Add(new("open_tm_case", "Pokedude_InitTMCase"));
        return s with { Screen = TeachyTvFieldScreen.TmCase, Inventory = s.Data.TmInventory,
            Cursor = 0, TmState = -1, Timer = 0 };
    }

    private static TeachyTvFieldDemoState TmCase(TeachyTvFieldDemoState s, TeachyTvFieldDemoInput input,
        ImmutableArray<TeachyTvFieldDemoEffect>.Builder effects)
    {
        // Source B is checked before every TM state, including A/B text prompts,
        // and unlike the bag it can request cleanup during a palette fade.
        if (input.Cancel && s.TmState is >= 0 and < 21) s = s with { TmState = 21, Interrupted = true };
        switch (s.TmState)
        {
            case -1:
                // Task_Pokedude_Start, after InitTMCase's opening fade.
                return input.PaletteFadeActive ? s : s with { TmState = 0 };
            case 0:
                effects.Add(new("palette_fade", PaletteMask: 0xFFFF8405, Delay: 4, From: 0, To: 6));
                return s with { TmState = 1, DescriptionShaded = true };
            case 1:
            case 11:
                return input.PaletteFadeActive ? s : Timed(s);
            case 2 or 3 or 4 or 12 or 13 or 14:
                if (s.Timer == 0)
                {
                    s = s with { Cursor = s.Cursor + 1 };
                    effects.Add(new("cursor", "down"));
                }
                return Timed(s);
            case 5 or 6 or 7 or 15 or 16 or 17:
                if (s.Timer == 0)
                {
                    s = s with { Cursor = s.Cursor - 1 };
                    effects.Add(new("cursor", "up"));
                }
                return Timed(s);
            case 8:
            case 18:
                string symbol = s.TmState == 8 ? "gPokedudeText_TMTypes" : "gPokedudeText_ReadTMDescription";
                effects.Add(new("message", symbol));
                return s with { TmState = s.TmState + 1, DescriptionShaded = true, MessageSymbol = symbol };
            case 9:
            case 19:
                return input.TextPrinterActive ? s : s with { TmState = s.TmState + 1 };
            case 10:
                if (!input.Confirm) return s;
                effects.Add(new("palette_fade", PaletteMask: 0x00000400, Delay: 0, From: 6, To: 0));
                return s with { TmState = 11, DescriptionShaded = false, MessageSymbol = null };
            case 20:
                return input.Confirm ? s with { TmState = 21 } : s;
            case 21:
                if (input.PaletteFadeActive) return s;
                effects.Add(new("release_temporary_tm_case"));
                effects.Add(new("palette_fade", PaletteMask: uint.MaxValue, Delay: -2, From: 0, To: 16));
                return s with { TmState = 22, Inventory = null, MessageSymbol = null };
            case 22:
                return input.PaletteFadeActive ? s : Complete(s, effects);
            default:
                throw new InvalidOperationException("teachy_tv_tm_state");
        }
    }

    private static TeachyTvFieldDemoState Timed(TeachyTvFieldDemoState s) =>
        s.Timer + 1 > PokedudeInputDelay ? s with { Timer = 0, TmState = s.TmState + 1 } : s with { Timer = s.Timer + 1 };

    private static TeachyTvFieldDemoState Complete(TeachyTvFieldDemoState s,
        ImmutableArray<TeachyTvFieldDemoEffect>.Builder effects)
    {
        effects.Add(new("callback", "CB2_ReturnToTeachyTV"));
        return s with { Screen = TeachyTvFieldScreen.Completed, Inventory = null, RegisteredItem = null,
            ContextOpen = false, SelectedItem = null, MessageSymbol = null };
    }
}
