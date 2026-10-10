using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

var results = new List<object>(); int failed = 0, checks = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
void Test(string name, Action action)
{
    try { action(); results.Add(new { name, passed = true }); }
    catch (Exception ex) { failed++; results.Add(new { name, passed = false, error = ex.ToString() }); }
}
string Json<T>(T value) => JsonSerializer.Serialize(value);
string root = args.Length == 1 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
if (args.Length > 1) throw new ArgumentException("Expected optional project root");
string Read(string name) => File.ReadAllText(Path.Combine(root, "content/generated", name, "catalog.json"));
var loaded = PretPewterPokemon.LoadContent(Read("route1-pokemon"), Read("old-man-tutorial"), Read("forest-pokemon"), Read("pewter-pokemon"));
var content = new CampaignPokemonContent(loaded.Catalog, loaded.SpeciesNames, PokemonStateValidator.NicknameAlphabet) { AllowSleepStatus = true };
var catalog = content.Catalog;
var shop = PretEarlyShop.Load(File.ReadAllText(Path.Combine(root, "content/generated/early-shop/early-shop.json"))).Catalog;
var opening = PretOpeningItems.Load(File.ReadAllText(Path.Combine(root, "content/generated/opening-items/opening-items.json")));
var gym = PretPewterTrainers.Load(root); var forest = PretForestTrainers.Load(root);
ItemId Item(string name) => new("pret:ITEM_" + name);
PokemonInstance Mon(string species = "BULBASAUR", int level = 20, string move = "TACKLE")
{
    var mon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), level, 17, IvPolicy.Fixed(0), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
    var m = catalog.Moves[new("pret:MOVE_" + move)];
    return mon with { Moves = [new(m.Id, m.Pp - 1, m.Pp)] };
}
PartyPokemon Own(long id, PokemonInstance? mon = null, uint status = 0) =>
    new(id, mon ?? Mon(), "Leaf", "RED", 0, 2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE") { Status = status };
WildBattleCombatant Combat(PartyPokemon p) => new(p.Pokemon, HeldItem: p.HeldItem, Status: p.Status);
InventoryState Bag(string name, int count = 2) => new([new(Item(name), count)], [new(Item("POTION"), 1)], shop.Limits)
    { Balls = [new(Item("POKE_BALL"), 5)], KeyItems = [new(QuestItemRules.TeachyTvId, 1)] };
WildBattleState Wild(PartyPokemon p, string enemyMove = "GROWL", uint seed = 0) =>
    new(Combat(p), new(Mon("PIDGEY", 10, enemyMove)), 2, 4, 10, new(seed), 123, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.EarlyPartyRulesVersion);
ForestTrainerCombatState Trainer(PartyPokemon p, bool brock = false)
{
    var start = brock
        ? ForestTrainerCombatRules.Start(gym, catalog, PewterTrainerPartyFactory.Brock, Combat(p), new(0), earlyPartyRules: true)
        : ForestTrainerCombatRules.Start(forest, catalog, ForestTrainerPartyFactory.SupportedTrainers[0], Combat(p), new(0), earlyPartyRules: true);
    Check(start.Succeeded, start.Fault ?? "trainer start"); return start.State!;
}
WildBattleMedicineResult Use(WildBattleState b, ImmutableArray<PartyPokemon> p, string name, long target,
    InventoryState? bag = null, int active = 0) => WildBattleRules.ResolveMedicine(shop, opening, content, b, bag ?? Bag(name), p, active, new(Item(name), target));
ForestTrainerMedicineResult UseTrainer(ForestTrainerCombatState b, ImmutableArray<PartyPokemon> p, string name,
    long target, InventoryState? bag = null) => ForestTrainerCombatRules.ResolveMedicine(shop, opening, content, b, bag ?? Bag(name), p, 0, new(Item(name), target));
void Accepted(WildBattleMedicineResult r)
{
    Check(r.Succeeded, r.Fault ?? "wild failure"); Check(WildBattleRules.Validate(catalog, r.Battle.State) is null, "wild invalid after");
    Check(r.Party[0].Pokemon == r.Battle.State!.Player.Pokemon && r.Party[0].Status == r.Battle.State.Player.Status, "active projection stale");
}
void AcceptedTrainer(ForestTrainerMedicineResult r)
{
    Check(r.Succeeded, r.Fault ?? "trainer failure"); Check(ForestTrainerCombatRules.Validate(catalog, r.Battle.State) is null, "trainer invalid after");
    Check(r.Party[0].Pokemon == r.Battle.State!.Battle.Player.Pokemon && r.Party[0].Status == r.Battle.State.Battle.Player.Status, "trainer active projection stale");
}
void Atomic(WildBattleMedicineResult r, WildBattleState b, InventoryState inv, ImmutableArray<PartyPokemon> p)
{
    Check(!r.Succeeded && ReferenceEquals(b, r.Battle.State) && ReferenceEquals(inv, r.Inventory) && r.Party == p, "wild failure changed snapshots");
    Check(r.Battle.Events.IsEmpty && r.Battle.Trace.IsEmpty, "wild failure leaked events or RNG");
}
void AtomicTrainer(ForestTrainerMedicineResult r, ForestTrainerCombatState b, InventoryState inv, ImmutableArray<PartyPokemon> p)
{
    Check(!r.Succeeded && ReferenceEquals(b, r.Battle.State) && ReferenceEquals(inv, r.Inventory) && r.Party == p, "trainer failure changed snapshots");
    Check(r.Battle.Events.IsEmpty && r.Battle.TrainerEvents.IsEmpty && r.Battle.Trace.IsEmpty && r.Battle.LastAiDecision is null, "trainer failure leaked execution");
}

Test("pinned-source-identities-and-runtime", () =>
{
    Check(Environment.Version.Major == 10, "runtime");
    var reference = File.ReadAllText(Path.Combine(root, ".tools/reference-root")).Trim();
    using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "source-references.json")));
    Check(source.RootElement.GetProperty("sourceCommit").GetString() == WildBattleRules.SourceCommit, "source commit");
    var inputs = source.RootElement.GetProperty("inputs").EnumerateArray().ToArray(); Check(inputs.Length == 8, "missing source inputs");
    foreach (var input in inputs)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(reference, input.GetProperty("path").GetString()!));
        Check(bytes.Length == input.GetProperty("byteLength").GetInt32(), "source bytes");
        Check(Convert.ToHexStringLower(SHA256.HashData(bytes)) == input.GetProperty("sha256").GetString(), "source SHA256");
        Check(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0"), .. bytes])) == input.GetProperty("gitBlobSha1").GetString(), "source blob identity");
    }
});

foreach (var (name, status) in new[] { ("POTION", 0u), ("ANTIDOTE", 8u), ("PARALYZE_HEAL", 64u), ("AWAKENING", 3u), ("BURN_HEAL", 16u) })
foreach (bool reserve in new[] { false, true })
Test($"wild-{name}-{(reserve ? "reserve" : "active")}-whole-record", () =>
{
    var target = Own(reserve ? 22 : 11, Mon() with { CurrentHp = 10 }, status);
    var active = reserve ? Own(11) : target;
    ImmutableArray<PartyPokemon> party = reserve ? [active, target with { HeldItem = "ITEM_ORAN_BERRY" }] : [active, Own(22)];
    var bag = Bag(name); var b = Wild(active); var r = Use(b, party, name, target.InstanceId, bag); Accepted(r);
    int index = reserve ? 1 : 0; var after = r.Party[index];
    Check(OpeningInventoryRules.Quantity(r.Inventory.BagItems, Item(name)) == 1, "medicine quantity");
    Check(r.Inventory.PcItems == bag.PcItems && r.Inventory.Balls == bag.Balls && r.Inventory.KeyItems == bag.KeyItems, "unrelated pockets");
    Check(Json(after with { Pokemon = party[index].Pokemon, Status = status }) == Json(party[index]), "owned metadata changed");
    Check(Json(after.Pokemon with { CurrentHp = 10 }) == Json(target.Pokemon), "identity/PP/EV/stats changed");
    Check(after.Pokemon.CurrentHp == (name == "POTION" ? 30 : 10) && after.Status == 0, "medicine effect");
    var e = r.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single();
    Check(e.TargetInstanceId == target.InstanceId && e.PartyIndex == index && e.ActiveTarget == !reserve && e.StatusBefore == status, "typed target");
    Check(r.Battle.Events.OfType<WildBattleMoveUsedEvent>().Count() == 1 && r.Battle.Events.OfType<WildBattleMoveUsedEvent>().All(x => x.Actor == WildBattleSide.Opponent), "extra/player move invented");
    Check(r.Battle.State!.Turn == b.Turn + 1 && r.Battle.State.Revision == b.Revision + 1, "turn commits once");
    Check(Json(JsonSerializer.Deserialize<WildBattleEvent>(Json<WildBattleEvent>(e))) == Json<WildBattleEvent>(e), "event roundtrip");
});

foreach (var (name, status) in new[] { ("POTION", 0u), ("ANTIDOTE", 8u), ("PARALYZE_HEAL", 64u), ("AWAKENING", 2u), ("BURN_HEAL", 16u) })
foreach (bool reserve in new[] { false, true })
Test($"trainer-{name}-{(reserve ? "reserve" : "active")}-one-response", () =>
{
    var active = Own(11, Mon() with { CurrentHp = 30 }, reserve ? 0 : status);
    var target = reserve ? Own(22, Mon() with { CurrentHp = 10 }, status) : active;
    ImmutableArray<PartyPokemon> p = [active, reserve ? target : Own(22)];
    var b = Trainer(active); var r = UseTrainer(b, p, name, target.InstanceId); AcceptedTrainer(r);
    var e = r.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single();
    Check(e.ActiveTarget == !reserve && e.StatusAfter == 0 && e.HpAfter == (name == "POTION" ? Math.Min(target.Pokemon.Stats.MaxHp, target.Pokemon.CurrentHp + 20) : target.Pokemon.CurrentHp), "trainer medicine projection");
    Check(r.Battle.Events.OfType<WildBattleMoveUsedEvent>().Count() == 1 && r.Battle.Events.OfType<WildBattleMoveUsedEvent>().All(x => x.Actor == WildBattleSide.Opponent), "trainer response count");
    Check(r.Battle.TrainerEvents.OfType<ForestTrainerAiChosenEvent>().Single().Sequence < e.Sequence, "AI event after medicine");
    Check(r.Battle.State!.Battle.Revision == b.Battle.Revision + 1 && r.Battle.State.Battle.Turn == b.Battle.Turn + 1, "trainer turn count");
    if (reserve) Check(r.Party[1] == (name == "POTION" ? target with { Pokemon = target.Pokemon with { CurrentHp = 30 } } : target with { Status = 0 }), "reserve whole record");
});

Test("active-potion-retains-legacy-result-and-rng", () =>
{
    var p = Own(11, Mon() with { CurrentHp = 1 }); ImmutableArray<PartyPokemon> party = [p, Own(22)];
    var b = Wild(p, "TACKLE"); var r = Use(b, party, "POTION", 11); Accepted(r);
    var old = WildBattleRules.ResolvePotion(catalog, b, p.Pokemon with { CurrentHp = 21 }, true);
    Check(old.Succeeded && Json(old.State) == Json(r.Battle.State) && old.Trace.SequenceEqual(r.Battle.Trace), "legacy wild result drift");
    var trainer = Trainer(p, true); var tr = UseTrainer(trainer, party, "POTION", 11); AcceptedTrainer(tr);
    var legacy = ForestTrainerCombatRules.ResolvePotion(catalog, trainer, p.Pokemon with { CurrentHp = 21 }, true);
    Check(legacy.Succeeded && Json(legacy.State) == Json(tr.Battle.State) && legacy.Trace.SequenceEqual(tr.Battle.Trace), "legacy trainer result drift");
});

Test("trainer-ai-observes-before-hp-and-paralysis", () =>
{
    foreach (string name in new[] { "POTION", "PARALYZE_HEAL" })
    {
        var p = Own(11, Mon("PIDGEOTTO", 20) with { CurrentHp = name == "POTION" ? 1 : 40 }, name == "PARALYZE_HEAL" ? 64u : 0u);
        var b = Trainer(p, true);
        if (name == "PARALYZE_HEAL") b = b with { OpponentIndex = 1, Battle = b.Battle with { Opponent = b.OpponentParty[1] } };
        var r = UseTrainer(b, [p, Own(22)], name, 11); AcceptedTrainer(r);
        var expected = PewterTrainerAi.Choose(catalog, new(b.Battle.Opponent, b.Battle.Player, b.KnownPlayerAbility, 7), b.Battle.Rng, true);
        var cured = b.Battle.Player with { Pokemon = p.Pokemon with { CurrentHp = name == "POTION" ? 21 : 40 }, Status = 0 };
        var incorrect = PewterTrainerAi.Choose(catalog, new(b.Battle.Opponent, cured, b.KnownPlayerAbility, 7), b.Battle.Rng, true);
        Check(expected.Succeeded && incorrect.Succeeded && Json(expected) != Json(incorrect), "fixture does not distinguish before/after AI");
        Check(Json(r.Battle.LastPewterAiDecision) == Json(expected), "AI used post-medicine target");
        Check(r.Battle.Trace.Take(expected.Trace.Length).SequenceEqual(expected.Trace), "AI RNG prefix differs");
    }
});

Test("medicine-does-not-clear-volatiles-or-run-player-cancellation", () =>
{
    var p = Own(11, Mon() with { CurrentHp = 30 }, 3); var b = Wild(p) with
    { Player = Combat(p) with { ConfusionTurnsRemaining = 4, FocusEnergy = true, DefenseCurled = true, SpeedStage = 4, LeechSeedSource = WildBattleSide.Opponent, BindTurnsRemaining = 3 } };
    var r = Use(b, [p, Own(22)], "AWAKENING", 11); Accepted(r); var a = r.Battle.State!.Player;
    Check(a.Status == 0 && a.ConfusionTurnsRemaining == 4 && a.FocusEnergy && a.DefenseCurled && a.SpeedStage == 4 && a.LeechSeedSource == WildBattleSide.Opponent && a.BindTurnsRemaining == 2, "medicine stripped volatiles");
    Check(!r.Battle.Trace.Any(x => x.Purpose.Contains("player.confusion") || x.Purpose == "wild-battle.player.paralysis"), "player cancellation ran for item");
    var potion = Use(Wild(p), [p], "POTION", 11); Accepted(potion);
    Check(potion.Party[0].Status == 3, "item ticked sleep counter");
    var bench = Own(22, Mon() with { CurrentHp = 10 }, 8);
    var reserve = Use(Wild(p), [p, bench], "ANTIDOTE", 22); Accepted(reserve);
    Check(reserve.Party[0].Status == 3 && reserve.Party[1].Status == 0, "bench cure affected active sleep");
});

Test("bench-cure-retains-active-poison-residual-and-fainted-target-does-not-revive", () =>
{
    var active = Own(11, status: 8); var reserve = Own(22, Mon() with { CurrentHp = 0 }, 8);
    var r = Use(Wild(active), [active, reserve], "ANTIDOTE", 22); Accepted(r);
    Check(r.Party[1].Pokemon.CurrentHp == 0 && r.Party[1].Status == 0, "fainted reserve was revived");
    Check(r.Battle.Events.OfType<WildBattleStatusDamageEvent>().Single().Side == WildBattleSide.Player && r.Party[0].Status == 8, "bench cure suppressed active residual");
});

Test("item-priority-allows-exactly-one-blocked-enemy-attempt", () =>
{
    var p = Own(11, Mon() with { CurrentHp = 10 }); var b = Wild(p); b = b with { Opponent = b.Opponent with { Status = 2 } };
    var r = Use(b, [p], "POTION", 11); Accepted(r);
    Check(r.Battle.State!.Opponent.Status == 1 && r.Battle.State.Opponent.Pokemon.Moves == b.Opponent.Pokemon.Moves, "enemy sleep/PP");
    Check(r.Battle.Events.OfType<WildBattleActionBlockedEvent>().Count() == 1 && !r.Battle.Events.OfType<WildBattleMoveUsedEvent>().Any(), "blocked enemy action count");
});

Test("wild-terminal-loss-and-reserve-replacement", () =>
{
    var p = Own(11, Mon() with { CurrentHp = 1 }, 8); var b = Wild(p, "TACKLE");
    b = b with { Opponent = new(Mon("PIDGEY", 50)) };
    var loss = Use(b, [p], "ANTIDOTE", 11); Accepted(loss);
    Check(loss.Battle.State!.Outcome == WildBattleOutcome.Lost && loss.Party[0].Pokemon.CurrentHp == 0 && loss.Battle.Events.OfType<WildBattleEndedEvent>().Count() == 1, "loss terminal");
    var reserve = Own(22, Mon() with { CurrentHp = 1 }); var replacement = Use(b, [p, reserve], "POTION", 22); Accepted(replacement);
    Check(replacement.Battle.State!.AwaitingReplacement && replacement.Battle.State.Outcome == WildBattleOutcome.Ongoing && replacement.Party[1].Pokemon.CurrentHp == 21, "living reserve replacement");
    Check(replacement.Battle.Events.OfType<WildBattleMoveUsedEvent>().Count() == 1, "second enemy action");
});

Test("trainer-terminal-loss-and-experience-pause", () =>
{
    var p = Own(11, Mon() with { CurrentHp = 1 }, 8); var b = Trainer(p, true);
    var r = UseTrainer(b, [p], "ANTIDOTE", 11); AcceptedTrainer(r);
    Check(r.Battle.State!.Phase == ForestTrainerCombatPhase.Ended && r.Battle.State.Battle.Outcome == WildBattleOutcome.Lost, "trainer loss terminal");
    var injuredReserve = Own(22, Mon() with { CurrentHp = 1 });
    var replacement = UseTrainer(b, [p, injuredReserve], "POTION", 22); AcceptedTrainer(replacement);
    Check(replacement.Battle.State!.Phase == ForestTrainerCombatPhase.AwaitingPlayerReplacement &&
        replacement.Battle.State.HasPlayerReserves && replacement.Party[1].Pokemon.CurrentHp == 21, "trainer reserve replacement");
    var healthy = Own(11); var t = Trainer(healthy); var foe = t.Battle.Opponent;
    foe = foe with { Pokemon = foe.Pokemon with { CurrentHp = 1, Moves = foe.Pokemon.Moves.Select(x => x with { CurrentPp = 0 }).ToImmutableArray() } };
    t = t with { Battle = t.Battle with { Opponent = foe }, OpponentParty = t.OpponentParty.SetItem(t.OpponentIndex, foe) };
    var reserve = Own(22, Mon() with { CurrentHp = 1 }); var recoil = UseTrainer(t, [healthy, reserve], "POTION", 22); AcceptedTrainer(recoil);
    Check(recoil.Battle.State!.Phase == ForestTrainerCombatPhase.AwaitingExperience && recoil.Battle.TrainerEvents.OfType<ForestTrainerExperienceRequiredEvent>().Count() == 1, "recoil EXP pause");
    Check(recoil.Battle.Events.OfType<WildBattleMoveUsedEvent>().Single().Move == WildBattleRules.StruggleMove && recoil.Party[1].Pokemon.CurrentHp == 21, "Struggle response/bench medicine");
});

Test("invalid-cancel-no-effect-and-target-drift-are-atomic", () =>
{
    var active = Own(11); var reserve = Own(22, Mon() with { CurrentHp = 1 }, 8); ImmutableArray<PartyPokemon> party = [active, reserve];
    var b = Wild(active); var bag = Bag("ANTIDOTE");
    Atomic(WildBattleRules.ResolveMedicine(shop, opening, content, b, bag, party, 0, null), b, bag, party);
    foreach (var (name, target) in new[] { ("ANTIDOTE", 11L), ("ANTIDOTE", 99L), ("POTION", 11L), ("FULL_RESTORE", 22L), ("REVIVE", 22L), ("REPEL", 22L), ("ESCAPE_ROPE", 22L) })
        Atomic(Use(b, party, name, target, bag), b, bag, party);
    foreach (var badParty in new ImmutableArray<PartyPokemon>[] { [], [active, reserve with { InstanceId = 11 }], [active with { Status = 8 }, reserve], [active with { Pokemon = active.Pokemon with { CurrentHp = 1 } }, reserve], [active with { HeldItem = "ITEM_ORAN_BERRY" }, reserve], [active with { Pokemon = active.Pokemon with { Moves = active.Pokemon.Moves.SetItem(0, active.Pokemon.Moves[0] with { CurrentPp = 1 }) } }, reserve], [active, reserve with { Status = 128 }] })
        Atomic(Use(b, badParty, "ANTIDOTE", 22, bag), b, bag, badParty);
    Atomic(Use(b, party, "ANTIDOTE", 22, bag, -1), b, bag, party);
    var empty = bag with { BagItems = [] }; Atomic(Use(b, party, "ANTIDOTE", 22, empty), b, empty, party);
    var fullHp = Bag("POTION"); Atomic(Use(b, party, "POTION", 11, fullHp), b, fullHp, party);
    var fainted = party.SetItem(1, reserve with { Pokemon = reserve.Pokemon with { CurrentHp = 0 } }); Atomic(Use(b, fainted, "POTION", 22, fullHp), b, fullHp, fainted);
    var t = Trainer(active); AtomicTrainer(ForestTrainerCombatRules.ResolveMedicine(shop, opening, content, t, bag, party, 0, null), t, bag, party);
    AtomicTrainer(UseTrainer(t, party, "ANTIDOTE", 11, bag), t, bag, party);
    var wrongCure = Bag("BURN_HEAL"); Atomic(Use(b, party, "BURN_HEAL", 22, wrongCure), b, wrongCure, party);
    var malformed = shop with { Items = shop.Items.SetItem(2, shop.Items[2] with { CureStatus = 16 }) };
    Atomic(WildBattleRules.ResolveMedicine(malformed, opening, content, b, bag, party, 0, new(Item("ANTIDOTE"), 22)), b, bag, party);
});

Test("rng-and-event-revision-overflow-roll-back-all-three-records", () =>
{
    var active = Own(11, Mon() with { CurrentHp = 1 }); ImmutableArray<PartyPokemon> party = [active, Own(22)]; var bag = Bag("POTION"); var b = Wild(active, "TACKLE");
    var normal = Use(b, party, "POTION", 11, bag); Accepted(normal);
    for (ulong remaining = 0; remaining < (ulong)normal.Battle.Trace.Length; remaining++)
    {
        var bad = b with { Rng = new(b.Rng.Seed, ulong.MaxValue - remaining) }; Atomic(Use(bad, party, "POTION", 11, bag), bad, bag, party);
    }
    foreach (var bad in new[] { b with { EventSequence = long.MaxValue }, b with { Revision = long.MaxValue } }) Atomic(Use(bad, party, "POTION", 11, bag), bad, bag, party);
    var t = Trainer(active, true); var success = UseTrainer(t, party, "POTION", 11, bag); AcceptedTrainer(success);
    for (ulong remaining = 0; remaining < (ulong)success.Battle.Trace.Length; remaining++)
    {
        var bad = t with { Battle = t.Battle with { Rng = new(t.Battle.Rng.Seed, ulong.MaxValue - remaining) } }; AtomicTrainer(UseTrainer(bad, party, "POTION", 11, bag), bad, bag, party);
    }
    foreach (var bad in new[] { t with { Battle = t.Battle with { EventSequence = long.MaxValue } }, t with { Battle = t.Battle with { Revision = long.MaxValue } } }) AtomicTrainer(UseTrainer(bad, party, "POTION", 11, bag), bad, bag, party);
});

Test("equivalent-deserialized-party-accepted-and-historical-profile-closed", () =>
{
    var p = Own(11, Mon() with { CurrentHp = 10 }); var b = Wild(p);
    var copy = JsonSerializer.Deserialize<PartyPokemon>(Json(p))!; var r = Use(b, [copy], "POTION", 11); Accepted(r);
    var old = b with { RulesVersion = WildBattleRules.RulesVersion }; var bag = Bag("POTION"); ImmutableArray<PartyPokemon> party = [p];
    Atomic(Use(old, party, "POTION", 11, bag), old, bag, party);
    Check(WildBattleRules.ResolvePotion(catalog, old, p.Pokemon with { CurrentHp = 30 }).Succeeded, "old Potion API broken");
});

Test("nonzero-active-index-and-repeated-target-selection", () =>
{
    var bench = Own(11, status: 8); var active = Own(22, status: 16);
    var r = Use(Wild(active), [bench, active], "ANTIDOTE", 11, active: 1);
    Check(r.Succeeded && WildBattleRules.Validate(catalog, r.Battle.State) is null, r.Fault ?? "nonzero active rejected");
    Check(r.Party[0] == bench with { Status = 0 } && r.Party[1].Status == 16 &&
        r.Party[1].Pokemon == r.Battle.State!.Player.Pokemon, "nonzero active synchronization");
    Check(r.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single() is { ActiveTarget: false, PartyIndex: 0, TargetInstanceId: 11 }, "wrong bench event target");
    var next = Use(r.Battle.State!, r.Party, "BURN_HEAL", 22, active: 1);
    Check(next.Succeeded && next.Party[1].Status == 0 && next.Party[0] == r.Party[0] &&
        next.Party[1].Pokemon == next.Battle.State!.Player.Pokemon, "second medicine used stale active");
    Check(next.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single() is { ActiveTarget: true, PartyIndex: 1, TargetInstanceId: 22 }, "wrong active event target");
});

Check(results.Count == 32, "nonempty complete suite: " + results.Count);
Console.WriteLine(JsonSerializer.Serialize(new { suite = "Game.BattleMedicine.Tests", passed = failed == 0, runtime = Environment.Version.ToString(), sourceCommit = WildBattleRules.SourceCommit, checks, count = results.Count, failed, results }, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 ? 0 : 1;
