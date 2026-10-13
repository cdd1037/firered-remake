using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.World;

if (args.Length != 3) throw new ArgumentException("ROOT CURRENT_SAVE OUTPUT");
var catalog = PretCampaignCatalog.LoadCurrent(args[0]);
var fresh = CampaignSession.CreateCurrent(catalog);
var originalBytes = File.ReadAllBytes(args[1]);
var checkpoint = SaveCodec.Decode<CampaignState>(originalBytes, CurrentGameProfile.RulesVersion,
    catalog.ContentVersion, validate: fresh.ValidatePlayerSave);
var checks = new List<string>();
var commands = new List<object>();
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidDataException(name);
    checks.Add(name);
}
void Same<T>(T expected, T actual, string name) => Check(CanonicalJson.Hash(expected) == CanonicalJson.Hash(actual), name);
ItemId Item(string symbol) => new("pret:ITEM_" + symbol);
int Count(CampaignState state, string symbol) => OpeningInventoryRules.Quantity(state.Inventory!.BagItems, Item(symbol));
CampaignState Supply(CampaignState state, params (string Symbol, int Quantity)[] supplies)
{
    var bag = state.Inventory!.BagItems;
    foreach (var (symbol, quantity) in supplies)
    {
        var id = Item(symbol);
        bag = bag.RemoveAll(s => s.Item == id).Add(new(id, quantity));
    }
    return state with { Inventory = state.Inventory with { BagItems = bag } };
}
CampaignSession Restore(CampaignState state)
{
    Check(fresh.ValidatePlayerSave(state) is null, "controlled checkpoint strictly valid");
    var session = CampaignSession.RestoreCurrent(catalog, state, checkpoint.Generation);
    Same(state, session.State, "Restore preserves full state");
    return session;
}
CommandOutcome<CampaignState> Run(CampaignSession session, string label, CampaignCommand command,
    CommandStatus expected, string? error = null)
{
    var before = session.State;
    long revision = session.Revision;
    var outcome = session.Execute(label, revision, command);
    commands.Add(new { label, command, outcome.Status, outcome.Error, outcome.Revision,
        outcome.StateHash, outcomeHash = CanonicalJson.Hash(outcome),
        inventoryHash = CanonicalJson.Hash(outcome.State.Inventory),
        partyHash = CanonicalJson.Hash(outcome.State.Party),
        uiHash = CanonicalJson.Hash(new { outcome.State.ItemMenu, outcome.State.Repel,
            outcome.State.RareCandy, outcome.State.TextBuffers, outcome.State.PresentationEffects }) });
    Check(outcome.Status == expected && outcome.Error == error, label + ": expected status/error, actual " + outcome.Status + "/" + outcome.Error);
    if (expected != CommandStatus.Accepted)
    {
        Same(before, session.State, label + ": rejected/faulted state is atomic");
        Check(session.Revision == revision, label + ": revision unchanged");
    }
    return outcome;
}
void Accepted(CampaignSession s, string label, CampaignCommand command) => Run(s, label, command, CommandStatus.Accepted);
void Reject(CampaignSession s, string label, CampaignCommand command, string error) => Run(s, label, command, CommandStatus.Rejected, error);
void OtherPockets(CampaignState before, CampaignState after, string label)
{
    Same(before.Inventory!.KeyItems, after.Inventory!.KeyItems, label + ": all existing key items preserved");
    Same(before.Inventory.Machines, after.Inventory.Machines, label + ": machines preserved");
    Same(before.Inventory.Berries, after.Inventory.Berries, label + ": berries preserved");
    Same(before.Inventory.PcItems, after.Inventory.PcItems, label + ": PC preserved");
}

Check(checkpoint.Generation == 47819 && Environment.Version.Major == 10, "explicit current generation and .NET 10");
foreach (var (symbol, duration) in new[] { ("REPEL", 100), ("SUPER_REPEL", 200), ("MAX_REPEL", 250) })
{
    var start = Supply(checkpoint.State, (symbol, 2));
    var s = Restore(start);
    Accepted(s, symbol + " open bag", new("bag_open"));
    Reject(s, symbol + " unsupported item", new("field_repel", Text: Item("POTION").Value), "field_repel_context_invalid");
    Accepted(s, symbol + " request", new("field_repel", Text: Item(symbol).Value, Quantity: 2));
    Check(Count(s.State, symbol) == 2 && s.RepelSteps == 0, symbol + ": sound wait defers all consumption");
    Check(s.State.Repel?.Phase == CampaignRepelPhase.UseSound &&
        s.State.PresentationEffects.Any(e => e.Opcode == "playse" && e.Args.SequenceEqual(new[] { "SE_REPEL" })), symbol + ": sound and wait exposed");
    string wait = s.State.Repel!.WaitId;
    Reject(s, symbol + " wrong acknowledgement", new("repel_presented", Text: wait + "-wrong"), "repel_sound_pending");
    Reject(s, symbol + " cannot cancel sound", new("menu_back"), "repel_sound_pending");
    Accepted(s, symbol + " matching acknowledgement", new("repel_presented", Text: wait));
    Check(Count(s.State, symbol) == 1 && s.RepelSteps == duration && s.State.Repel is null, symbol + ": exactly one consumed at acknowledgement");
    Check(s.State.ItemMenu?.MessageSymbol == "gText_PlayerUsedVar2" &&
        s.State.PresentationEffects.All(e => e.Opcode != "playse" || !e.Args.SequenceEqual(new[] { "SE_REPEL" })), symbol + ": original message and sound retirement");
    OtherPockets(start, s.State, symbol);
    Reject(s, symbol + " repeated acknowledgement", new("repel_presented", Text: wait), "item_menu_active");
    Accepted(s, symbol + " dismiss used message", new("menu_ack"));
    Accepted(s, symbol + " effects linger", new("field_repel", Text: Item(symbol).Value));
    Check(Count(s.State, symbol) == 1 && s.RepelSteps == duration &&
        s.State.ItemMenu?.MessageSymbol == "gText_RepelEffectsLingered", symbol + ": lingering use preserves inventory/counter");
    Accepted(s, symbol + " dismiss lingering", new("menu_ack"));
    Accepted(s, symbol + " close bag", new("menu_back"));
    Accepted(s, symbol + " walking step", new("move", Direction.East));
    Check(s.RepelSteps == duration - 1, symbol + ": one successful public walking step");
}
var expiring = Restore(checkpoint.State with { Script = checkpoint.State.Script with {
    Variables = checkpoint.State.Script.Variables.SetItem(CampaignSession.RepelCounterVariable, 1) } });
Accepted(expiring, "repel expiry walking step", new("move", Direction.East));
Check(expiring.RepelSteps == 0 && expiring.State.Repel?.Phase == CampaignRepelPhase.WoreOff,
    "final repel step exposes expiry modal");
Reject(expiring, "repel expiry cannot move", new("move", Direction.West), "repel_expiration_message_pending");
Accepted(expiring, "repel expiry acknowledgement", new("ack"));

var medicineStart = Supply(checkpoint.State, ("REVIVE", 2), ("ETHER", 2), ("MAX_ELIXIR", 2));
var medicine = Restore(medicineStart);
Accepted(medicine, "medicine open bag", new("bag_open"));
Reject(medicine, "revive invalid slot", new("field_medicine", Text: Item("REVIVE").Value, MoveSlot: 0), "medicine_invalid_slot");
Accepted(medicine, "revive use", new("field_medicine", Text: Item("REVIVE").Value, Quantity: 2));
Check(Count(medicine.State, "REVIVE") == 1 && medicine.State.Party[0].Pokemon.CurrentHp == medicineStart.Party[0].Pokemon.Stats.MaxHp / 2,
    "revive consumes one and restores half HP");
Accepted(medicine, "revive dismiss", new("menu_ack"));
Accepted(medicine, "revive no effect", new("field_medicine", Text: Item("REVIVE").Value));
Check(Count(medicine.State, "REVIVE") == 1 && medicine.State.ItemMenu?.MessageSymbol == "gText_WontHaveEffect", "revive no effect is not consumed");
Accepted(medicine, "revive no effect dismiss", new("menu_ack"));
Reject(medicine, "ether omitted slot", new("field_medicine", Text: Item("ETHER").Value), "medicine_invalid_slot");
Accepted(medicine, "ether use", new("field_medicine", Text: Item("ETHER").Value, MoveSlot: 1));
Check(Count(medicine.State, "ETHER") == 1 && medicine.State.Party[0].Pokemon.Moves[1].CurrentPp == 10,
    "ether restores selected depleted move");
Accepted(medicine, "ether dismiss", new("menu_ack"));
Accepted(medicine, "max elixir use", new("field_medicine", Text: Item("MAX_ELIXIR").Value));
Check(Count(medicine.State, "MAX_ELIXIR") == 1 && medicine.State.Party[0].Pokemon.Moves.All(m => m.CurrentPp == m.MaximumPp),
    "max elixir restores every move once");
OtherPockets(medicineStart, medicine.State, "medicine");

var candyStart = Supply(checkpoint.State, ("RARE_CANDY", 2));
var candy = Restore(candyStart);
Accepted(candy, "candy open bag", new("bag_open"));
Reject(candy, "candy quantity guard", new("field_rare_candy", Text: Item("RARE_CANDY").Value, Quantity: 2), "field_rare_candy_context_invalid");
Reject(candy, "candy move slot guard", new("field_rare_candy", Text: Item("RARE_CANDY").Value, MoveSlot: 0), "field_rare_candy_context_invalid");
Reject(candy, "candy party index guard", new("field_rare_candy", Text: Item("RARE_CANDY").Value, PartyIndex: 6), "item_party_index_invalid");
Accepted(candy, "candy current source admission", new("field_rare_candy", Text: Item("RARE_CANDY").Value));
Check(ReferenceEquals(catalog.PokemonContent!.Catalog, catalog.CeruleanCaveOwnership!.Catalog), "candy source remains exact current owned catalog");
Check(Count(candy.State, "RARE_CANDY") == 1 && candy.State.Party[0].Pokemon.Level == 61,
    "current authenticated candy raises original Gyarados exactly once");
Same(candyStart.Party[0].Pokemon.Moves, candy.State.Party[0].Pokemon.Moves, "candy preserves existing PP and moves");
Same(candyStart.Party[0], candy.State.Party[0] with { Pokemon = candyStart.Party[0].Pokemon }, "candy preserves full owned identity");
Same(candyStart.World.Rng, candy.State.World.Rng, "candy draws no world RNG");
Same(candyStart.Exploration, candy.State.Exploration, "candy draws no encounter RNG");
OtherPockets(candyStart, candy.State, "candy");
DrainCandy(candy, "candy current source");
Check(candy.State.ItemMenu?.View == "bag" && candy.State.RareCandy is null, "candy finishes original bag UI");

void DrainCandy(CampaignSession session, string label)
{
    for (int n = 0; n < 12 && session.State.RareCandy is { Stage: not CampaignRareCandyStage.LearnMove }; n++)
        Accepted(session, label + " page " + n, new("ack"));
    Check(session.State.RareCandy is null or { Stage: CampaignRareCandyStage.LearnMove }, label + ": finite candy pages");
}
void ProofRejected(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership, string label)
{
    try { RareCandyRules.Apply(content, ownership, candyStart.Party[0], "MAPSEC_ONE_ISLAND"); }
    catch (ArgumentException ex) when (ex.Message.Contains("rare_candy_owned_catalog_identity", StringComparison.Ordinal))
    { Check(true, label); return; }
    throw new InvalidDataException(label + ": unauthenticated identity accepted");
}
var ownership = OwnedBattlePokemonCatalog.For(catalog.PokemonContent!.Catalog)!;
ProofRejected(catalog.PokemonContent with { Catalog = catalog.PokemonContent.Catalog with { } }, ownership,
    "candy rejects reconstructed current catalog identity");
ProofRejected(catalog.PokemonContent, OwnedBattlePokemonCatalog.For(catalog.FiveIslandOwnership!.Catalog)!,
    "candy rejects wrong predecessor proof");

// Controlled level-100 variant retains the existing individual's origin and moves.
var member = candyStart.Party[0];
var pokemon = member.Pokemon;
var species = catalog.PokemonContent.Catalog.Species[pokemon.Species];
var basis = species.BaseStats;
int Other(int b, int iv, int ev, Stat stat) => StatRules.CalculateOther(b, iv, ev, 100, pokemon.Nature, stat);
var maximum = pokemon with { Level = 100, Experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, 100),
    StatCalculationEvs = null, Stats = new(
        StatRules.CalculateHp(basis.Hp, pokemon.Ivs.Hp, pokemon.Evs.Hp, 100),
        Other(basis.Attack, pokemon.Ivs.Attack, pokemon.Evs.Attack, Stat.Attack),
        Other(basis.Defense, pokemon.Ivs.Defense, pokemon.Evs.Defense, Stat.Defense),
        Other(basis.SpecialAttack, pokemon.Ivs.SpecialAttack, pokemon.Evs.SpecialAttack, Stat.SpecialAttack),
        Other(basis.SpecialDefense, pokemon.Ivs.SpecialDefense, pokemon.Evs.SpecialDefense, Stat.SpecialDefense),
        Other(basis.Speed, pokemon.Ivs.Speed, pokemon.Evs.Speed, Stat.Speed)) };
var cappedStart = candyStart with { Party = candyStart.Party.SetItem(0, member with { Pokemon = maximum }) };
var capped = Restore(cappedStart);
Accepted(capped, "level100 open bag", new("bag_open"));
Accepted(capped, "level100 candy no effect", new("field_rare_candy", Text: Item("RARE_CANDY").Value));
Same(cappedStart.Party, capped.State.Party, "level100 preserves fainted HP, PP, stats and identity");
Same(cappedStart.Inventory, capped.State.Inventory, "level100 consumes no candy");
Check(capped.State.ItemMenu?.MessageSymbol == "gText_WontHaveEffect", "level100 no-effect UI");

// Existing Charizard reaches its source level-64 move with a disclosed PP-Up fixture.
var charizard = candyStart.Party[1];
var cut = charizard.Pokemon.Moves[0] with { CurrentPp = 7, MaximumPp = 48, PpUps = 3 };
var learningStart = candyStart with { Party = candyStart.Party.SetItem(1,
    charizard with { Pokemon = charizard.Pokemon with { Moves = charizard.Pokemon.Moves.SetItem(0, cut) } }) };
var learning = Restore(learningStart);
Accepted(learning, "candy learning open bag", new("bag_open"));
Accepted(learning, "candy Charizard level64", new("field_rare_candy", Text: Item("RARE_CANDY").Value, PartyIndex: 1));
DrainCandy(learning, "candy learning");
Check(learning.State.RareCandy?.Stage == CampaignRareCandyStage.LearnMove &&
    learning.State.RareCandy.PendingMoves[0].Move.Value == "pret:MOVE_FIRE_SPIN", "source level64 Fire Spin awaits explicit replacement");
Reject(learning, "candy learning protects HM", new("rare_candy_learn", MoveSlot: 0), "hm_move_cannot_be_forgotten");
Accepted(learning, "candy learns Fire Spin", new("rare_candy_learn", MoveSlot: 2));
DrainCandy(learning, "candy learned move");
Check(learning.State.Party[1].Pokemon.Moves[2].Move.Value == "pret:MOVE_FIRE_SPIN" &&
    learning.State.Party[1].Pokemon.Moves[2].CurrentPp == learning.State.Party[1].Pokemon.Moves[2].MaximumPp,
    "learned move receives full PP");
Same(cut, learning.State.Party[1].Pokemon.Moves[0], "existing HM depletion and PP Ups preserved");
Same(learningStart.Party[1], learning.State.Party[1] with { Pokemon = learningStart.Party[1].Pokemon }, "learning preserves owned identity");
Check(Count(learning.State, "RARE_CANDY") == 1, "learning pages never consume candy again");

var evolving = Restore(candyStart);
Accepted(evolving, "candy evolution open bag", new("bag_open"));
Accepted(evolving, "candy Ekans level56", new("field_rare_candy", Text: Item("RARE_CANDY").Value, PartyIndex: 2));
DrainCandy(evolving, "candy evolution");
Check(evolving.State.Evolution?.Proposal.TargetSpecies.Value == "pret:SPECIES_ARBOK", "legal source Ekans evolution proposed");
Accepted(evolving, "candy accept Arbok evolution", new("evolution_choice", Choice: 1));
for (int n = 0; n < 12 && evolving.State.Evolution is { } scene; n++)
    Accepted(evolving, "candy evolution result " + n,
        scene.Stage == CampaignEvolutionStage.LearnMove ? new("evolution_learn", MoveSlot: -1) : new("ack"));
Check(evolving.State.Evolution is null && evolving.State.ItemMenu?.View == "bag" &&
    evolving.State.Party[2].Pokemon.Species.Value == "pret:SPECIES_ARBOK", "candy evolution returns to bag");
Check(evolving.State.Party[2].InstanceId == candyStart.Party[2].InstanceId && Count(evolving.State, "RARE_CANDY") == 1,
    "evolution preserves identity and once-only consumption");
Same(candyStart.Party[2].Pokemon.Moves, evolving.State.Party[2].Pokemon.Moves, "evolution preserves all existing PP");
Check(evolving.State.EvolutionHistory!.Value[^1].Item == Item("RARE_CANDY").Value,
    "evolution records original candy item");

var machine = Restore(checkpoint.State);
Accepted(machine, "machine open bag", new("bag_open"));
Accepted(machine, "machine open case", new("machine_open"));
Reject(machine, "machine unsupported ID", new("machine_teach", Text: "pret:ITEM_TM99"), "inventory_machine_unsupported");
Reject(machine, "machine default TM39 compatibility", new("machine_teach", MoveSlot: 1), "machine_incompatible");
Reject(machine, "machine negative replacement", new("machine_teach", Text: Item("TM03").Value, MoveSlot: -2), "machine_invalid_slot");
Reject(machine, "machine HM protection", new("machine_teach", Text: Item("TM03").Value, MoveSlot: 0), "machine_cannot_forget_hm");
Accepted(machine, "machine exact TM03 teach", new("machine_teach", Text: Item("TM03").Value, MoveSlot: 1));
Check(OpeningInventoryRules.Quantity(machine.State.Inventory!.Machines!.Value, Item("TM03")) == 0 && OpeningInventoryRules.Quantity(machine.State.Inventory.Machines.Value, PewterMachineRules.Tm39Id) == 1 &&
    machine.State.Party[0].Pokemon.Moves[1].Move.Value == "pret:MOVE_WATER_PULSE", "explicit TM03 consumes precise item and replaces selected move");
Same(checkpoint.State.Inventory!.KeyItems, machine.State.Inventory.KeyItems, "machine keeps every key item");
Check(machine.State.Party[0].InstanceId == checkpoint.State.Party[0].InstanceId, "machine preserves owned identity");
Check(machine.State.ItemMenu?.MessageSymbol == "gText_PkmnLearnedMove3" && machine.State.ItemMenu.ReturnView == "machines", "machine result uses existing UI return");

var stoneOutput = Path.GetFullPath(args[2]) + ".stones";
Directory.CreateDirectory(stoneOutput);
StoneEvolutionCases.Run(catalog, checkpoint.State, checkpoint.Generation, stoneOutput, Check);

Check(File.ReadAllBytes(args[1]).SequenceEqual(originalBytes), "source checkpoint unchanged");
Check(checks.Count > 100 && commands.Count > 50, "nonempty bounded characterization");
File.WriteAllText(args[2], JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), checkpoint.Generation,
    sourceHash = CanonicalJson.Hash(checkpoint.State), catalog.ContentVersion,
    fixture = "Current generation 47819; controlled consumable stacks, level100 Gyarados and Charizard Cut PP Ups are explicit fixtures. Original checkpoint and authenticated current source remain unchanged.",
    passed = checks.Count, commandCount = commands.Count, checks, commands }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Passed {checks.Count} checks over {commands.Count} command observations.");
