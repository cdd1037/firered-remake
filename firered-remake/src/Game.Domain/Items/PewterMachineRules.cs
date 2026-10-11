using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Bounded, source-authenticated regional TM profiles. No generic TM/HM service.</summary>
public static class PewterMachineRules
{
    public const string SourceCommit = OpeningInventoryRules.SourceCommit;
    public const string Profile = "firered-pewter-machines-v1";
    public const string Route22Profile = "firered-route22-machines-v1";
    public const string Route3Profile = "firered-route3-machines-v1";
    public const string MtMoonProfile = "firered-mtmoon-machines-v1";
    public const string CeruleanProfile = "firered-cerulean-machines-v1";
    public const string VermilionProfile = "firered-vermilion-machines-v1";
    public const string SsAnneProfile = "firered-ssanne-machines-v1";
    public const string Route11Profile = "firered-route11-machines-v1";
    public static ItemId Hm05Id { get; } = new("pret:ITEM_HM05");
    public const string VermilionGymProfile = "firered-vermilion-gym-machines-v1";
    public static ItemId Tm34Id { get; } = new("pret:ITEM_TM34");
    public static MoveId ShockWaveId { get; } = new("pret:MOVE_SHOCK_WAVE");
    public static ItemId Tm31Id { get; } = new("pret:ITEM_TM31");
    public static ItemId Tm44Id { get; } = new("pret:ITEM_TM44");
    public static ItemId Hm01Id { get; } = new("pret:ITEM_HM01");
    public const string BillProfile = "firered-bill-machines-v1";
    public static ItemId Tm45Id { get; } = new("pret:ITEM_TM45");
    public static ItemId Tm43Id { get; } = new("pret:ITEM_TM43");
    public static ItemId Tm28Id { get; } = new("pret:ITEM_TM28");
    public static ItemId Tm03Id { get; } = new("pret:ITEM_TM03");
    public static MoveId WaterPulseId { get; } = new("pret:MOVE_WATER_PULSE");
    public static ItemId Tm39Id { get; } = new("pret:ITEM_TM39");
    public static ItemId TmCaseId { get; } = new("pret:ITEM_TM_CASE");
    public static MoveId RockTombId { get; } = new("pret:MOVE_ROCK_TOMB");
    private static readonly (string Symbol, int Id, bool CanLearn)[] ExpectedSpecies = [
        ("BULBASAUR",1,false),("IVYSAUR",2,false),("VENUSAUR",3,false),
        ("CHARMANDER",4,false),("CHARMELEON",5,false),("CHARIZARD",6,false),
        ("SQUIRTLE",7,false),("WARTORTLE",8,false),("BLASTOISE",9,false),
        ("CATERPIE",10,false),("METAPOD",11,false),("BUTTERFREE",12,false),
        ("WEEDLE",13,false),("KAKUNA",14,false),("BEEDRILL",15,false),
        ("PIDGEY",16,false),("PIDGEOTTO",17,false),("PIDGEOT",18,false),
        ("RATTATA",19,false),("RATICATE",20,false),("PIKACHU",25,false),("RAICHU",26,false),
        ("SANDSHREW",27,true),("SANDSLASH",28,true),("GEODUDE",74,true),
        ("GRAVELER",75,true),("GOLEM",76,true),("ONIX",95,true),("STEELIX",208,true)
    ];

    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (SafariMachineCatalog.IsAuthenticated(catalog) || SurfMachineCatalog.IsAuthenticated(catalog)) return;
        if (catalog.Profile == FuchsiaGymMachineRules.Profile) { FuchsiaGymMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == FuchsiaMachineRules.Profile) { FuchsiaMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == EeveeMachineRules.Profile) { EeveeMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == SnorlaxMachineRules.Profile) { SnorlaxMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == PokemonTowerMachineRules.Profile) { PokemonTowerMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == RocketHideoutMachineRules.Profile) { RocketHideoutMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == CeladonGymMachineRules.Profile) { CeladonGymMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == CeladonApproachMachineRules.Profile) { CeladonApproachMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == RockTunnelMachineRules.Profile) { RockTunnelMachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile == Route11Profile) { Route11MachineRules.ValidateCatalog(catalog); return; }
        if (catalog.Profile is SsAnneProfile or VermilionGymProfile) { SsAnneMachineRules.ValidateCatalog(catalog); return; }
        var expected = catalog.Profile is Route22Profile or Route3Profile or MtMoonProfile or CeruleanProfile or BillProfile or VermilionProfile ? ExpectedSpecies.Concat(new (string Symbol,int Id,bool CanLearn)[]{("SPEAROW",21,false),("FEAROW",22,false),("MANKEY",56,true)}).ToArray() : ExpectedSpecies;
        if (catalog.Profile is Route3Profile or MtMoonProfile or CeruleanProfile or BillProfile or VermilionProfile)
            expected = expected.Concat(new (string Symbol, int Id, bool CanLearn)[] {
                ("EKANS",23,false),("NIDORAN_F",29,false),("NIDORINA",30,false),("NIDORAN_M",32,false),
                ("NIDORINO",33,false),("JIGGLYPUFF",39,false),("MAGIKARP",129,false),("GYARADOS",130,false)
            }).ToArray();
        if (catalog.Profile is MtMoonProfile or CeruleanProfile or BillProfile or VermilionProfile)
            expected = expected.Concat(new (string Symbol, int Id, bool CanLearn)[] {
                ("ARBOK",24,false),("CLEFAIRY",35,false),("ZUBAT",41,false),("GOLBAT",42,false),("ODDISH",43,false),
                ("PARAS",46,false),("PARASECT",47,false),("BELLSPROUT",69,false),("MAGNEMITE",81,false),("GRIMER",88,true),
                ("VOLTORB",100,false),("KOFFING",109,false),("NIDOQUEEN",31,true),("NIDOKING",34,true),("CLEFABLE",36,false),("WIGGLYTUFF",40,false)
            }).ToArray();
        if (catalog.Profile is CeruleanProfile or BillProfile or VermilionProfile)
            expected = expected.Concat(new (string Symbol, int Id, bool CanLearn)[] {
                ("ABRA",63,false),("KADABRA",64,false),("MACHOP",66,true),("DROWZEE",96,false),
                ("SHELLDER",90,false),("HORSEA",116,false),("GOLDEEN",118,false),("STARYU",120,false),("STARMIE",121,false)
            }).ToArray();
        if (catalog.Profile is BillProfile or VermilionProfile)
            expected = expected.Concat(new (string Symbol, int Id, bool CanLearn)[] {
                ("SLOWPOKE",79,false),("GLOOM",44,false),("WEEPINBELL",70,false)
            }).ToArray();
        if (catalog.Profile == VermilionProfile)
            expected = expected.Concat(new (string Symbol,int Id,bool CanLearn)[]{("MEOWTH",52,false),("PERSIAN",53,false)}).ToArray();
        if (catalog.SourceCommit != SourceCommit || catalog.Profile is not (Profile or Route22Profile or Route3Profile or MtMoonProfile or CeruleanProfile or BillProfile or VermilionProfile) || catalog.CompleteItemCatalog ||
            catalog.MachineSlots != 58 || catalog.KeyItemSlots != 30 || catalog.MaximumStack != 999 ||
            catalog.Move != RockTombId || catalog.SourceMoveId != 317 || catalog.MovePp != 10 ||
            catalog.Compatibility.IsDefault || catalog.Compatibility.Length != expected.Length)
            throw new NotSupportedException("Only the pinned Pewter TM39 profile is supported.");
        for (int n = 0; n < expected.Length; n++)
        {
            var e = expected[n]; var c = catalog.Compatibility[n];
            if (c is null || c.Species != new SpeciesId("pret:SPECIES_" + e.Symbol) ||
                c.SourceSpeciesId != e.Id || c.CanLearn != e.CanLearn || c.Source is null)
                throw new NotSupportedException("Unsupported source TM39 compatibility.");
        }
        ValidateItem(catalog.Tm39, false);
        ValidateItem(catalog.TmCase, true);
        ValidateAdditionalMachines(catalog);
    }

    private static void ValidateItem(PewterMachineItem value, bool isCase)
    {
        if (value is null || value.Item is null || value.DescriptionSource is null ||
            string.IsNullOrWhiteSpace(value.Description) || string.IsNullOrWhiteSpace(value.DeclarationDescription))
            throw new NotSupportedException("Missing machine item metadata.");
        var i = value.Item;
        if (i.Id != (isCase ? TmCaseId : Tm39Id) || i.SourceItemSymbol != (isCase ? "ITEM_TM_CASE" : "ITEM_TM39") ||
            i.SourceItemId != (isCase ? 364 : 327) || i.Name != (isCase ? "TM CASE" : "TM39") ||
            i.Price != (isCase ? 0 : 3000) || i.Importance != (isCase ? 1 : 0) ||
            i.Registrability != (isCase ? 1 : 0) || i.Pocket != (isCase ? "POCKET_KEY_ITEMS" : "POCKET_TM_CASE") ||
            i.Type != (isCase ? "ITEM_TYPE_BAG_MENU" : "ITEM_TYPE_PARTY_MENU") ||
            i.FieldUseFunction != (isCase ? "FieldUseFunc_TmCase" : "NULL") ||
            i.HoldEffect != "HOLD_EFFECT_NONE" || i.HoldEffectParam != 0 || i.BattleUsage != 0 ||
            i.BattleUseFunction != "NULL" || i.SecondaryId != 0 || i.CanToss != !isCase ||
            i.CanSell != !isCase || i.CanGive != !isCase || i.Source is null)
            throw new NotSupportedException("Unsupported TM39 or TM CASE metadata.");
    }

    private static void ValidateAdditionalMachines(PewterMachineCatalog catalog)
    {
        if (catalog.Profile is not (MtMoonProfile or CeruleanProfile or BillProfile or VermilionProfile))
        {
            if (catalog.AdditionalMachines is not null) throw new NotSupportedException("Legacy machine additions are unsupported.");
            return;
        }
        bool bill = catalog.Profile is BillProfile or VermilionProfile;
        bool cerulean = catalog.Profile is CeruleanProfile or BillProfile or VermilionProfile;
        int speciesCount = catalog.Profile == VermilionProfile ? 70 : bill ? 68 : cerulean ? 65 : 56;
        if (catalog.AdditionalMachines is not { IsDefault: false } values || values.Length != (bill ? 7 : cerulean ? 4 : 3))
            throw new NotSupportedException("Missing bounded regional machine additions.");
        var rows = new[] {
            ("TM05",293,1000,"ROAR",46,20,"VENUSAUR CHARIZARD BLASTOISE RATICATE GOLEM ONIX STEELIX GYARADOS NIDOQUEEN NIDOKING"),
            ("TM09",297,3000,"BULLET_SEED",331,30,"BULBASAUR IVYSAUR VENUSAUR ODDISH PARAS PARASECT BELLSPROUT"),
            ("TM46",334,3000,"THIEF",168,10,"BUTTERFREE BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE RAICHU SANDSHREW SANDSLASH SPEAROW FEAROW MANKEY EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO ARBOK ZUBAT GOLBAT PARAS PARASECT BELLSPROUT GRIMER VOLTORB KOFFING NIDOQUEEN NIDOKING")
        };
        if (cerulean)
        {
            rows[2].Item7 += " ABRA KADABRA MACHOP DROWZEE";
            rows = [.. rows, ("TM03",291,3000,"WATER_PULSE",352,20,
                "SQUIRTLE WARTORTLE BLASTOISE NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF GYARADOS CLEFAIRY NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF SHELLDER HORSEA GOLDEEN STARYU STARMIE")];
        }
        if (bill)
        {
            rows[1].Item7 += " GLOOM WEEPINBELL";
            rows[2].Item7 += " WEEPINBELL";
            rows[3].Item7 += " SLOWPOKE";
            rows = [.. rows,
                ("TM45",333,3000,"ATTRACT",213,15,
                    "BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE BUTTERFREE BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX SPEAROW FEAROW MANKEY EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF GYARADOS ARBOK CLEFAIRY ZUBAT GOLBAT ODDISH PARAS PARASECT BELLSPROUT GRIMER KOFFING NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF ABRA KADABRA MACHOP DROWZEE SHELLDER HORSEA GOLDEEN SLOWPOKE GLOOM WEEPINBELL"),
                ("TM43",331,3000,"SECRET_POWER",290,20,
                    "BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE BUTTERFREE BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX SPEAROW FEAROW MANKEY EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF GYARADOS ARBOK CLEFAIRY ZUBAT GOLBAT ODDISH PARAS PARASECT BELLSPROUT MAGNEMITE GRIMER VOLTORB KOFFING NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF ABRA KADABRA MACHOP DROWZEE SHELLDER HORSEA GOLDEEN STARYU STARMIE SLOWPOKE GLOOM WEEPINBELL"),
                ("TM28",316,2000,"DIG",91,10,
                    "CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE RATTATA RATICATE PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX MANKEY EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF ARBOK CLEFAIRY PARAS PARASECT GRIMER NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF MACHOP SLOWPOKE")];
        }
        if (catalog.Profile == VermilionProfile)
        {
            rows[0].Item7 += " PERSIAN";
            foreach(int index in new[]{2,3,4,5,6}) rows[index].Item7 += " MEOWTH PERSIAN";
        }
        for (int n = 0; n < rows.Length; n++)
        {
            var (symbol, id, price, move, moveId, pp, species) = rows[n]; var value = values[n];
            if (value is null || value.Machine is null || value.Machine.Item is null ||
                value.Move != new MoveId("pret:MOVE_" + move) || value.SourceMoveId != moveId || value.MovePp != pp ||
                value.Compatibility.IsDefault || value.Compatibility.Length != speciesCount)
                throw new NotSupportedException("MtMoon machine definition differs.");
            var item = value.Machine.Item;
            // Normalize only the machine identity/name/price fields; every other source property must match TM39 semantics.
            ValidateItem(value.Machine with { Item = item with { Id = Tm39Id, SourceItemSymbol = "ITEM_TM39", SourceItemId = 327, Name = "TM39", Price = 3000 } }, false);
            if (item.Id != new ItemId("pret:ITEM_" + symbol) || item.SourceItemSymbol != "ITEM_" + symbol ||
                item.SourceItemId != id || item.Name != symbol || item.Price != price)
                throw new NotSupportedException("MtMoon machine item differs.");
            var compatible = species.Split(' ').Select(s => new SpeciesId("pret:SPECIES_" + s)).ToHashSet();
            for (int s = 0; s < speciesCount; s++)
            {
                var c = value.Compatibility[s]; var old = catalog.Compatibility[s];
                if (c is null || c.Species != old.Species || c.SourceSpeciesId != old.SourceSpeciesId ||
                    c.CanLearn != compatible.Contains(c.Species) || c.Source != old.Source)
                    throw new NotSupportedException("MtMoon machine source compatibility differs.");
            }
        }
    }

    /// <summary>Read-only lookup on an already validated catalog; null means this profile does not admit the machine.</summary>
    public static PewterAdditionalMachine? Definition(PewterMachineCatalog catalog, ItemId machine)
        => machine == Tm39Id ? new(catalog.Tm39, catalog.Move, catalog.SourceMoveId, catalog.MovePp, catalog.Compatibility) :
            catalog.AdditionalMachines?.FirstOrDefault(x => x.Machine.Item.Id == machine);

    /// <summary>Validates Machines and Key Items physical shape/CASE. Other item membership belongs to legacy validators.</summary>
    public static string? Validate(PewterMachineCatalog catalog, InventoryState? state)
    {
        ValidateCatalog(catalog);
        if (state is null) return "inventory_missing";
        string? error = ValidatePocket(state.Machines, catalog.MachineSlots, true, catalog) ??
            ValidatePocket(state.KeyItems, catalog.KeyItemSlots, false, catalog);
        if (error is not null) return error;
        int cases = OpeningInventoryRules.Quantity(state.KeyItems ?? [], TmCaseId);
        if (cases > 1) return "inventory_tm_case_quantity";
        if ((state.Machines?.Length ?? 0) != 0 && cases != 1) return "inventory_tm_case_missing";
        return null;
    }

    private static string? ValidatePocket(ImmutableArray<InventoryStack>? optional, int capacity, bool machines, PewterMachineCatalog catalog)
    {
        if (optional is null) return null;
        var stacks = optional.Value;
        if (stacks.IsDefault || stacks.Length > capacity)
            return machines ? "inventory_machines_capacity" : "inventory_key_items_capacity";
        var seen = new HashSet<ItemId>();
        foreach (var stack in stacks)
        {
            if (stack is null) return "inventory_stack_missing";
            if (machines && Definition(catalog, stack.Item) is null) return "inventory_machine_unsupported";
            if (stack.Quantity is < 1 or > 999) return "inventory_stack_quantity";
            if (!seen.Add(stack.Item)) return "inventory_duplicate_stack";
        }
        return null;
    }

    /// <summary>Strips only this module's additions, after Validate, for unchanged narrow legacy validators.</summary>
    public static InventoryState WithoutMachines(InventoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with { Machines = null, KeyItems = state.KeyItems is null ? null :
            state.KeyItems.Value.Where(x => x.Item != TmCaseId).ToImmutableArray() };
    }

    /// <summary>Source CheckBagHasSpace checks the TM pocket alone; this is not a promise AddBagItem can insert CASE.</summary>
    public static bool CanAddTm39(PewterMachineCatalog catalog, InventoryState state, int quantity = 1)
        => CanAddMachine(catalog, state, Tm39Id, quantity);

    public static bool CanAddMachine(PewterMachineCatalog catalog, InventoryState state, ItemId machine, int quantity = 1)
    {
        if (Validate(catalog, state) is not null || Definition(catalog, machine) is null || quantity is < 1 or > 999) return false;
        var stacks = state.Machines ?? [];
        int count = OpeningInventoryRules.Quantity(stacks, machine);
        return count != 0 ? count <= catalog.MaximumStack - quantity : stacks.Length < catalog.MachineSlots;
    }

    public static InventoryTransactionResult AddTm39(PewterMachineCatalog catalog, InventoryState state, int quantity = 1)
        => AddMachine(catalog, state, Tm39Id, quantity);

    public static InventoryTransactionResult AddMachine(PewterMachineCatalog catalog, InventoryState state, ItemId machine, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error) => new(state, false, error);
        string? error = Validate(catalog, state);
        if (error is not null) return Fail(error);
        if (Definition(catalog, machine) is null) return Fail("inventory_machine_unsupported");
        if (quantity is < 1 or > 999) return Fail("inventory_transfer_quantity");
        var machines = state.Machines ?? [];
        int count = OpeningInventoryRules.Quantity(machines, machine);
        if (count > catalog.MaximumStack - quantity) return Fail("inventory_stack_full");
        if (count == 0 && machines.Length >= catalog.MachineSlots) return Fail("inventory_destination_full");
        var keys = state.KeyItems ?? [];
        if (count == 0 && OpeningInventoryRules.Quantity(keys, TmCaseId) == 0)
        {
            if (keys.Length >= catalog.KeyItemSlots) return Fail("inventory_key_items_full");
            keys = keys.Add(new(TmCaseId, 1));
        }
        return new(state with { Machines = OpeningInventoryRules.SetQuantity(machines, machine, count + quantity),
            KeyItems = keys }, true, null);
    }

    public static PewterMachineTeachResult TeachTm39(PewterMachineCatalog catalog, PokemonCatalog pokemonCatalog,
        InventoryState inventory, PartyPokemon pokemon, string currentMapSection, int? replacementSlot = null)
        => TeachMachine(catalog, pokemonCatalog, inventory, pokemon, Tm39Id, currentMapSection, replacementSlot);

    public static PewterMachineTeachResult TeachMachine(PewterMachineCatalog catalog, PokemonCatalog pokemonCatalog,
        InventoryState inventory, PartyPokemon pokemon, ItemId machine, string currentMapSection, int? replacementSlot = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        PewterMachineTeachResult Fail(string error) => new(inventory, pokemon, false, error);
        string? error = Validate(catalog, inventory);
        if (error is not null) return Fail(error);
        var definition = Definition(catalog, machine);
        if (definition is null) return Fail("inventory_machine_unsupported");
        var safari = SafariMachineCatalog.For(catalog);
        var surf = SurfMachineCatalog.For(catalog);
        if (surf is not null && !ReferenceEquals(surf.Pokemon.Catalog, pokemonCatalog)) return Fail("machine_pokemon_catalog");
        if (surf is not null && !PokemonMoveRules.IsLearningSupported(pokemonCatalog, definition.Move)) return Fail("machine_move_execution_unsupported");
        if (safari is not null && !ReferenceEquals(safari.Pokemon.Catalog, pokemonCatalog))
            return Fail("machine_pokemon_catalog");
        if (safari is not null && !PokemonMoveRules.IsLearningSupported(pokemonCatalog, definition.Move))
            return Fail("machine_move_execution_unsupported");
        if (catalog.Profile is RocketHideoutMachineRules.Profile or PokemonTowerMachineRules.Profile or SnorlaxMachineRules.Profile or EeveeMachineRules.Profile or FuchsiaMachineRules.Profile or FuchsiaGymMachineRules.Profile && !RocketHideoutMachineRules.IsTeachingSupported(machine))
            return Fail("machine_move_execution_unsupported");
        error = PokemonStateValidator.Validate(pokemonCatalog, pokemon.Pokemon);
        if (error is not null) return Fail(error);
        if (pokemonCatalog.Species.Count != (surf is not null ? 112 : safari is not null ? 110 : catalog.Profile is FuchsiaMachineRules.Profile or FuchsiaGymMachineRules.Profile ? 107 : catalog.Profile == EeveeMachineRules.Profile ? 103 : catalog.Profile == SnorlaxMachineRules.Profile ? 102 : catalog.Profile == PokemonTowerMachineRules.Profile ? 101 : catalog.Profile == RocketHideoutMachineRules.Profile ? 99 : catalog.Profile == CeladonGymMachineRules.Profile ? 98 : catalog.Profile == CeladonApproachMachineRules.Profile ? 94 : catalog.Profile == RockTunnelMachineRules.Profile ? 93 : catalog.Profile == Route11Profile ? 79 : catalog.Profile is SsAnneProfile or VermilionGymProfile ? 73 : catalog.Profile == VermilionProfile ? 70 : catalog.Profile == BillProfile ? 68 : catalog.Profile == CeruleanProfile ? 65 : catalog.Profile == MtMoonProfile ? 56 : catalog.Profile == Route3Profile ? 40 : catalog.Profile == Route22Profile ? 32 : 29) || !pokemonCatalog.Moves.TryGetValue(definition.Move, out var move) ||
            move.SourceMoveId != definition.SourceMoveId || move.Pp != definition.MovePp)
            return Fail("machine_pokemon_catalog");
        if (string.IsNullOrEmpty(currentMapSection) || !currentMapSection.StartsWith("MAPSEC_", StringComparison.Ordinal))
            return Fail("machine_map_section");
        if (pokemon.IsEgg) return Fail("machine_egg");
        int count = OpeningInventoryRules.Quantity(inventory.Machines ?? [], machine);
        if (count == 0) return Fail("inventory_insufficient_quantity");
        var compatibility = definition.Compatibility.FirstOrDefault(x => x.Species == pokemon.Pokemon.Species);
        if (compatibility is null || !compatibility.CanLearn) return Fail("machine_incompatible");
        var before = pokemon.Pokemon;
        if (before.Moves.Any(x => x.Move == definition.Move)) return Fail("machine_already_known");
        int slot;
        if (before.Moves.Length < 4)
        {
            if (replacementSlot is not null) return Fail("machine_invalid_slot");
            slot = before.Moves.Length;
        }
        else
        {
            if (replacementSlot is null) return Fail("machine_replacement_required");
            if (replacementSlot < 0 || replacementSlot >= 4) return Fail("machine_invalid_slot");
            slot = replacementSlot.Value;
            if (PokemonMoveRules.IsHiddenMachineMove(before.Moves[slot].Move)) return Fail("machine_cannot_forget_hm");
        }
        var learned = new PokemonMoveSlot(definition.Move, definition.MovePp, definition.MovePp);
        var moves = slot == before.Moves.Length ? before.Moves.Add(learned) : before.Moves.SetItem(slot, learned);
        // FRIENDSHIP_EVENT_LEARN_TMHM = {1,1,0}. Soothe Bell floor(1*1.5) remains 1.
        int delta = before.Friendship < 200 ? 1 : 0;
        if (delta > 0)
        {
            if (pokemon.Ball == "ITEM_LUXURY_BALL") delta++;
            if (pokemon.MetLocation == currentMapSection) delta++;
        }
        int friendship = Math.Min(255, before.Friendship + delta);
        var after = pokemon with { Pokemon = before with { Moves = moves, Friendship = friendship } };
        int consumed = machine == Hm01Id || machine == Hm05Id ||
            (safari is not null || surf is not null) && (machine == SafariMachineCatalog.Hm03Id || machine == SafariMachineCatalog.Hm04Id) ? 0 : 1;
        var afterInventory = inventory with { Machines = OpeningInventoryRules.SetQuantity(inventory.Machines ?? [], machine, count - consumed) };
        return new(afterInventory, after, true, null, slot, consumed, friendship - before.Friendship);
    }
}
