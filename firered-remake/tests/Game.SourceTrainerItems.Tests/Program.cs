using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass project root.");
string root = Path.GetFullPath(args[0]); int checks = 0, groups = 0, failed = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Test(string name, Action run) { groups++; try { run(); Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + " " + ex); } }
var campaign = PretCampaignCatalog.LoadFuchsiaGymProfile(root);
var content = campaign.PokemonContent!; var catalog = content.Catalog;
var shop = campaign.EarlyShop!; var opening = campaign.OpeningItems!;
var gymData = PretFuchsiaGymTrainers.Load(root);
var gym = new FuchsiaGymTrainerCatalog(gymData.SourceCommit, gymData.Profile, gymData.CompleteSourceRoster, gymData.Trainers, gymData.Battles);
MoveId Move(string name) => new("pret:MOVE_" + name);
ItemId Item(string name) => new("pret:ITEM_" + name);
PokemonInstance Mon(string species = "BLASTOISE", uint pid = 0, params string[] moves)
{
    var mon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), 70, 42,
        IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    return mon with { Moves = (moves.Length == 0 ? new[] { "SPLASH", "TACKLE" } : moves)
        .Select(m => new PokemonMoveSlot(Move(m), catalog.Moves[Move(m)].Pp - 1, catalog.Moves[Move(m)].Pp)).ToImmutableArray() };
}
WildBattleCombatant Combat(string species = "BLASTOISE", uint pid = 0, uint status = 0, string held = "ITEM_NONE", params string[] moves)
{
    var mon = Mon(species, pid, moves);
    return new(mon with { CurrentHp = mon.Stats.MaxHp - 40 }, HeldItem: held, Status: status, AttackBadgeBoost: true)
        { SpeedBadgeBoost = true, DefenseBadgeBoost = true };
}
WildBattleCalledPartyMember Member(WildBattleCombatant mon, WildBattleCalledPartyMember? prior = null) =>
    (prior ?? new(WildBattleTransformationRules.PersistentPokemon(mon))) with {
        Pokemon = WildBattleTransformationRules.PersistentPokemon(mon), HeldItem = mon.HeldItem,
        Status = WildBattleTransformationRules.PersistentStatus(mon), KnockedOffItem = mon.KnockedOffItem };
ForestTrainerCombatState Sync(ForestTrainerCombatState state)
{
    var p = state.Battle.CalledParty!;
    var players = state.SsAnnePlayerParty!.Value.SetItem(p.PlayerActiveIndex, state.Battle.Player);
    var opponents = state.OpponentParty.SetItem(p.OpponentActiveIndex, state.Battle.Opponent);
    p = p with { Player = players.Select((m, i) => Member(m!, p.Player[i])).ToImmutableArray(),
        Opponent = opponents.Select((m, i) => Member(m, p.Opponent[i])).ToImmutableArray() };
    return state with { Battle = state.Battle with { CalledParty = p }, SsAnnePlayerParty = players, OpponentParty = opponents,
        HasPlayerReserves = WildBattleSourcePartyEffects.LegalReserveIndices(p, WildBattleSide.Player).Length > 0 };
}
ForestTrainerCombatState Require(ForestTrainerCombatResult result, string name)
{
    Check(result.Succeeded, name + ": " + result.Fault);
    var error = ForestTrainerCombatRules.Validate(catalog, result.State);
    Check(error is null, name + " final validation: " + error);
    return result.State!;
}
ForestTrainerCombatState Start(ImmutableArray<WildBattleCombatant> players = default, string name = "JUGGLER_NATE", uint seed = 1)
{
    if (players.IsDefault) players = [Combat(), Combat("PIDGEOT", 2)];
    return Require(ForestTrainerCombatRules.Start(gym, catalog, new("pret:TRAINER_" + name), players, 0, new(seed), ForestTrainerBattleStyle.Set), "start");
}
ImmutableArray<PartyPokemon> Owned(ForestTrainerCombatState state) => state.Battle.CalledParty!.Player.Select((m, i) =>
    new PartyPokemon(i + 101, m.Pokemon, "PARTNER", "RED", 0, 2, "MAPSEC_FUCHSIA_CITY", 30, 4, "ITEM_POKE_BALL",
        WildBattleSourcePartyEffects.PersistentHeldItem(m)) { Status = m.Status }).ToImmutableArray();
InventoryState Bag(string medicine = "POTION") => new([new(Item(medicine), 2)], [], shop.Limits)
    { KeyItems = [new(PokemonTowerItemRules.PokeFluteId, 1)] };
ForestTrainerMedicineResult Medicine(ForestTrainerCombatState state, int target = 0, string item = "POTION", ImmutableArray<PartyPokemon> party = default)
{
    if (party.IsDefault) party = Owned(state);
    var result = ForestTrainerCombatRules.ResolveMedicine(shop, opening, content, state, Bag(item), party,
        state.CeruleanPlayerPartyIndex, new(Item(item), party[target].InstanceId));
    Require(result.Battle, "medicine " + item); VerifyParty(result.Party, result.Battle.State!); return result;
}
ForestTrainerPokeFluteResult Flute(ForestTrainerCombatState state)
{
    var result = ForestTrainerCombatRules.ResolvePokeFlute(shop, content, state, Bag(), Owned(state), state.CeruleanPlayerPartyIndex);
    Require(result.Battle, "flute"); VerifyParty(result.Party, result.Battle.State!); return result;
}
void VerifyParty(ImmutableArray<PartyPokemon> owned, ForestTrainerCombatState state)
{
    var actual = state.Battle.CalledParty!;
    for (int i = 0; i < owned.Length; i++)
    {
        Check(owned[i].Pokemon with { Moves = actual.Player[i].Pokemon.Moves } == actual.Player[i].Pokemon &&
            owned[i].Pokemon.Moves.SequenceEqual(actual.Player[i].Pokemon.Moves), "all-member HP/PP/identity " + i);
        Check(owned[i].Status == actual.Player[i].Status && owned[i].HeldItem == WildBattleSourcePartyEffects.PersistentHeldItem(actual.Player[i]), "all-member status/permanent item " + i);
        Check(owned[i].InstanceId == i + 101 && owned[i].Nickname == "PARTNER", "owned metadata " + i);
        Check(state.SsAnnePlayerParty!.Value[i]!.DefenseBadgeBoost, "Defense badge preserved " + i);
    }
}
void Atomic(ForestTrainerCombatState state, ImmutableArray<PartyPokemon> party, string? expected = null)
{
    var bag = Bag();
    var r = ForestTrainerCombatRules.ResolveMedicine(shop, opening, content, state, bag, party, state.CeruleanPlayerPartyIndex, new(Item("POTION"), party[0].InstanceId));
    Check(!r.Succeeded && ReferenceEquals(r.Battle.State, state) && ReferenceEquals(r.Inventory, bag) && r.Party == party, "medicine rejection atomic: " + r.Fault);
    Check(r.Battle.Events.IsEmpty && r.Battle.TrainerEvents.IsEmpty && r.Battle.Trace.IsEmpty, "medicine rejection has no execution");
    if (expected is not null) Check(r.Fault == expected, "fault expected " + expected + " actual " + r.Fault);
    var flute = ForestTrainerCombatRules.ResolvePokeFlute(shop, content, state, bag, party, state.CeruleanPlayerPartyIndex);
    Check(!flute.Succeeded && ReferenceEquals(flute.Battle.State, state) && flute.Party == party && flute.Battle.Events.IsEmpty && flute.Battle.Trace.IsEmpty, "flute rejection atomic: " + flute.Fault);
}

Test("source profile, real runtime and active/reserve medicine", () =>
{
    Check(Environment.Version.Major == 10 && catalog.Moves.Count == 354 && catalog.Species.Count == 107, "source metadata/runtime");
    foreach (int target in new[] { 0, 1 })
    {
        var before = Start(); var party = Owned(before); var result = Medicine(before, target);
        var applied = result.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single();
        Check(applied.PartyIndex == target && applied.ActiveTarget == (target == 0) && applied.HpAfter - applied.HpBefore == 20, "genuine medicine target");
        Check(result.Party[0].Pokemon.Moves.SequenceEqual(party[0].Pokemon.Moves) && result.Party[1].Pokemon.Moves.SequenceEqual(party[1].Pokemon.Moves), "item never spends player PP");
        if (target == 1) Check(result.Party[1].Pokemon.CurrentHp == party[1].Pokemon.CurrentHp + 20, "real bench healed");
        Check(result.Battle.Events.OfType<WildBattleMoveUsedEvent>().All(e => e.Actor == WildBattleSide.Opponent), "no invented player move");
        Check(result.Battle.State!.Battle.Turn == before.Battle.Turn + 1 && result.Battle.State.SourceTurn is null, "exactly one complete source turn");
        Check(OpeningInventoryRules.Quantity(result.Inventory.BagItems, Item("POTION")) == 1, "one potion used");
    }
});
Test("authentic status medicine reaches both party positions", () =>
{
    foreach (var (item, status) in new[] { ("ANTIDOTE", 8u), ("AWAKENING", 3u), ("PARALYZE_HEAL", 64u), ("BURN_HEAL", 16u) })
    foreach (int target in new[] { 0, 1 })
    {
        var players = ImmutableArray.Create(Combat(), Combat("PIDGEOT", 2));
        players = players.SetItem(target, players[target] with { Status = status });
        var before = Start(players); var result = Medicine(before, target, item);
        var applied = result.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single();
        Check(applied.StatusBefore == status && applied.StatusAfter == 0, "correct actual status cured");
        if (target == 1) Check(result.Party[target].Status == 0, "bench cure preserved through source sync");
    }
});
Test("source enemy item and switch happen after medicine without extra moves", () =>
{
    var state = Start(name: "LEADER_KOGA");
    state = Sync(state with { Battle = state.Battle with { Opponent = state.Battle.Opponent with { Pokemon = state.Battle.Opponent.Pokemon with { CurrentHp = 1 } } } });
    var result = Medicine(state); var after = result.Battle.State!;
    Check(result.Battle.TrainerEvents.OfType<CeruleanTrainerItemUsedEvent>().Single().Item == "ITEM_HYPER_POTION", "genuine Koga item decision");
    Check(!result.Battle.Events.OfType<WildBattleMoveUsedEvent>().Any() && after.CeruleanItemsUsedMask == 1, "two items consume exactly one turn");
    Check(result.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single().Sequence < result.Battle.TrainerEvents.OfType<CeruleanTrainerItemUsedEvent>().Single().Sequence, "player item before enemy item");
    state = Start(name: "LEADER_KOGA");
    state = Sync(state with { Battle = state.Battle with { Opponent = state.Battle.Opponent with { CalledVolatile = new() { PerishSongTimer = 0 } } } });
    var switched = Medicine(state);
    var e = switched.Battle.TrainerEvents.OfType<ForestTrainerOpponentReplacedEvent>().Single();
    Check(e.Sequence > switched.Battle.Events.OfType<WildBattleMedicineAppliedEvent>().Single().Sequence && switched.Battle.State!.OpponentIndex != state.OpponentIndex, "player item before genuine source AI switch");
    Check(!switched.Battle.Events.OfType<WildBattleMoveUsedEvent>().Any(), "switch never executes reserve's extra move");
});
Test("flute wakes both actual parties while retaining Soundproof and all records", () =>
{
    var state = Start([Combat(status: 3), Combat("PIDGEOT", 2, 3), Combat("ELECTRODE", 0, 4)], name: "TAMER_PHIL");
    Check(state.SsAnnePlayerParty!.Value[2]!.Pokemon.AbilityId == "ABILITY_SOUNDPROOF", "authentic Soundproof member");
    state = state with { OpponentParty = state.OpponentParty.Select(m => m with { Status = 3 }).ToImmutableArray(),
        Battle = state.Battle with { Player = state.Battle.Player with { Nightmare = true, DestinyBond = true, FuryCutterCounter = 3, CalledVolatile = new() { Grudge = true } },
            Opponent = state.Battle.Opponent with { Status = 3, Nightmare = true } } };
    state = Sync(state);
    var result = Flute(state); var after = result.Battle.State!;
    var events = result.Battle.Events.OfType<WildBattlePokeFluteEvent>().ToArray();
    Check(events.Select(e => e.Phase).SequenceEqual(new[] { PokeFluteBattlePhase.Played, PokeFluteBattlePhase.Fanfare, PokeFluteBattlePhase.Awakened }), "source three phase flute action");
    Check(events.All(e => e.PlayerPartyIndices.SequenceEqual(new[] { 0, 1 }) && e.OpponentPartyIndices.SequenceEqual(new[] { 0, 1 })), "real both party masks");
    Check(result.Party[1].Status == 0 && result.Party[2].Status == 4 && after.Battle.CalledParty!.Opponent[1].Status == 0, "bench statuses committed across both teams");
    Check(!after.Battle.Player.Nightmare && !after.Battle.Player.DestinyBond && after.Battle.Player.CalledVolatile?.Grudge != true && after.Battle.Player.FuryCutterCounter == 0, "source item clears active flags");
    Check(result.Battle.Events.OfType<WildBattleMoveUsedEvent>().Single().Sequence > events[^1].Sequence, "woken enemy acts after flute");
});
Test("strict stale full party projections and unsupported markers reject atomically", () =>
{
    var state = Start(); var party = Owned(state);
    Atomic(state, party.SetItem(1, party[1] with { Pokemon = party[1].Pokemon with { CurrentHp = party[1].Pokemon.CurrentHp + 1 } }), "source_item_party_projection");
    Atomic(state, party.SetItem(1, party[1] with { Status = 8 }), "source_item_party_projection");
    Atomic(state, party.SetItem(1, party[1] with { HeldItem = "ITEM_HARD_STONE" }), "source_item_party_projection");
    Atomic(state, party.SetItem(1, party[1] with { InstanceId = party[0].InstanceId }), "source_item_duplicate_identity");
    Atomic(state with { SourceMoveRulesVersion = "unknown" }, party, "source_trainer_rules_version");
    Atomic(state with { Battle = state.Battle with { Revision = long.MaxValue } }, party, "forest_trainer_counter_exhausted");
});
Test("source Toxic party kind and live counter survive healing and flute", () =>
{
    var state = Start(name: "TAMER_PHIL");
    state = Sync(state with { Battle = state.Battle with { Player = state.Battle.Player with { Status = 0x280 },
        Opponent = state.Battle.Opponent with { Status = 5 }, CalledField = new() } });
    Check(Owned(state)[0].Status == 0x80, "real persistent Toxic kind has no live counter");
    int hp = state.Battle.Player.Pokemon.CurrentHp, max = state.Battle.Player.Pokemon.Stats.MaxHp;
    var healed = Medicine(state);
    Check(healed.Battle.State!.Battle.Player.Status == 0x380 && healed.Party[0].Status == 0x80, "healing preserves old Toxic count before real next residual");
    Check(healed.Party[0].Pokemon.CurrentHp == hp + 20 - Math.Max(1, max / 16) * 3, "correct escalating Toxic damage after actual Potion");
    var cured = Medicine(state, item: "ANTIDOTE");
    Check(cured.Battle.State!.Battle.Player.Status == 0 && cured.Party[0].Status == 0, "Antidote clears actual toxic kind and counter");
    var flute = Flute(state);
    Check(flute.Battle.State!.Battle.Player.Status == 0x380 && flute.Party[0].Status == 0x80, "flute preserves live Toxic while waking the other battler");
    state = Start([Combat(), Combat("PIDGEOT", 2)]);
    state = state with { SsAnnePlayerParty = state.SsAnnePlayerParty!.Value.SetItem(1, state.SsAnnePlayerParty.Value[1]! with { Status = 0x80 }) };
    state = Sync(state);
    Check(Medicine(state, 1, "ANTIDOTE").Party[1].Status == 0, "real bench Toxic cured");
});
Test("source flute independently checks swapped battle ability and permanent party ability", () =>
{
    foreach (bool originalSoundproof in new[] { false, true })
    {
        var state = Start(originalSoundproof ? [Combat("ELECTRODE", 0, 3), Combat()] : [Combat(status: 3), Combat("ELECTRODE")], name: "LEADER_KOGA");
        state = Sync(state with { Battle = state.Battle with {
            Player = state.Battle.Player with { CalledOverlay = new() { Ability = originalSoundproof ? "ABILITY_TORRENT" : "ABILITY_SOUNDPROOF" }, Nightmare = true },
            Opponent = state.Battle.Opponent with { Pokemon = state.Battle.Opponent.Pokemon with { CurrentHp = 1 } } } });
        var result = Flute(state); var after = result.Battle.State!;
        Check(after.Battle.Player.Status == (originalSoundproof ? 0u : 3u) && result.Party[0].Status == (originalSoundproof ? 3u : 0u), "true source active and party status divergence");
        Check(after.Battle.Player.Nightmare != originalSoundproof && after.Battle.Player.SourcePartyStatusOverride == result.Party[0].Status, "only effective Soundproof retains Nightmare");
        var corrupted = Sync(after with { Battle = after.Battle with { Player = after.Battle.Player with { SourcePartyStatusOverride = 8 } } });
        Atomic(corrupted, Owned(corrupted), "source_party_status_override");
        var beforeHealing = Sync(after with { Battle = after.Battle with { Opponent = after.Battle.Opponent with { CalledVolatile = new() { PerishSongTimer = 0 } } } });
        var healing = Medicine(beforeHealing);
        Check(healing.Battle.State!.Battle.Player.Status == after.Battle.Player.Status && healing.Party[0].Status == result.Party[0].Status,
            "HP-only medicine preserves both independent source statuses");
        Check(!healing.Battle.Events.OfType<WildBattleTowerVolatileEvent>().Any(e => e.Effect == "nightmare" && e.Reason == "medicine"),
            "HP-only medicine emits no false Nightmare cure");
        var mon = after.Battle.CalledParty!.Player[1];
        var switched = Require(ForestTrainerCombatRules.SwitchPlayer(catalog, after,
            new(mon.Pokemon, HeldItem: WildBattleSourcePartyEffects.PersistentHeldItem(mon), Status: mon.Status), true, 1), "switch after flute divergence");
        Check(switched.Battle.CalledParty!.Player[0].Status == result.Party[0].Status && switched.SsAnnePlayerParty!.Value[0]!.SourcePartyStatusOverride is null, "departure retains permanent status and clears battler override");
    }
});
Test("permanent held item overrides survive medicine and flute for active and bench", () =>
{
    foreach (int index in new[] { 0, 1 })
    foreach (bool flute in new[] { false, true })
    {
        var state = Start(); var actual = state.Battle.CalledParty!;
        var member = actual.Player[index] with { HeldItem = "ITEM_NONE", KnockedOffItem = "ITEM_HARD_STONE", PersistentHeldItemOverride = "ITEM_PECHA_BERRY" };
        var player = state.SsAnnePlayerParty!.Value[index]! with { HeldItem = member.HeldItem, KnockedOffItem = member.KnockedOffItem };
        state = state with { Battle = state.Battle with { CalledParty = actual with { Player = actual.Player.SetItem(index, member), PlayerUsedHeldItem = "ITEM_ORAN_BERRY" },
            Player = index == 0 ? player : state.Battle.Player }, SsAnnePlayerParty = state.SsAnnePlayerParty.Value.SetItem(index, player) };
        Check(ForestTrainerCombatRules.Validate(catalog, state) is null, "genuine KnockOff then Recycle item state is valid");
        var before = Owned(state);
        var afterParty = flute ? Flute(state).Party : Medicine(state, index).Party;
        Check(afterParty[index].HeldItem == "ITEM_PECHA_BERRY", "permanent Recycle write preserved rather than old KnockOff original");
        Atomic(state, before.SetItem(index, before[index] with { HeldItem = "ITEM_HARD_STONE" }), "source_item_party_projection");
    }
    var trickState = Start([Combat(held: "ITEM_HARD_STONE", moves: ["TRICK"]), Combat("PIDGEOT", 2)]);
    trickState = Require(ForestTrainerCombatRules.ResolveMove(catalog, trickState, 0, true), "actual learned Trick");
    Check(trickState.Battle.Player.HeldItem == "ITEM_NONE" && trickState.Battle.Opponent.HeldItem == "ITEM_HARD_STONE", "actual source Trick moved held item");
    var trickMedicine = Medicine(trickState);
    Check(trickMedicine.Party[0].HeldItem == "ITEM_NONE" && trickMedicine.Battle.State!.Battle.CalledParty!.Opponent[trickState.OpponentIndex].HeldItem == "ITEM_HARD_STONE", "item action cannot resurrect pre-Trick ownership");
});
Test("executed Knock Off and Recycle remain permanent through source item and switches", () =>
{
    var players = ImmutableArray.Create(Combat(status: 8, held: "ITEM_PECHA_BERRY"), Combat("PIDGEOT", 2, held: "ITEM_HARD_STONE", moves: ["RECYCLE"]));
    var state = Require(ForestTrainerCombatRules.StartSource(campaign.FuchsiaRouteTrainers!, catalog,
        new("pret:TRAINER_BEAUTY_SHEILA"), players, 0, new(1), ForestTrainerBattleStyle.Set), "actual Metronome trainer entry");
    state = Require(ForestTrainerCombatRules.ResolveMove(catalog, state, 0, true), "consume real Pecha Berry");
    Check(state.Battle.CalledParty!.PlayerUsedHeldItem == "ITEM_PECHA_BERRY" && state.Battle.Player.HeldItem == "ITEM_NONE", "real held cure recorded in source battler slot");
    var reserve = state.Battle.CalledParty.Player[1];
    state = Require(ForestTrainerCombatRules.SwitchPlayer(catalog, state, new(reserve.Pokemon, HeldItem: reserve.HeldItem, Status: reserve.Status), true, 1), "switch real Hard Stone holder");
    int slot = Enumerable.Range(0, state.Battle.Opponent.Pokemon.Moves.Length).Single(i => state.Battle.Opponent.Pokemon.Moves[i].Move == Move("METRONOME"));
    var knocked = WildBattleRules.ResolveCalledMoveAction(catalog, state.Battle, new(WildBattleSide.Opponent, slot, Move("KNOCK_OFF")));
    Check(knocked.Succeeded, "genuine source Metronome Knock Off: " + knocked.Fault);
    state = Sync(state with { Battle = knocked.State! });
    Check(state.Battle.Player.HeldItem == "ITEM_NONE" && state.Battle.Player.KnockedOffItem == "ITEM_HARD_STONE", "actual battle item removed");
    state = Require(ForestTrainerCombatRules.ResolveMove(catalog, state, 0, true), "actual learned Recycle");
    Check(WildBattleSourcePartyEffects.PersistentHeldItem(state.Battle.CalledParty!.Player[1]) == "ITEM_PECHA_BERRY" && state.Battle.CalledParty.PlayerUsedHeldItem is null, "actual Recycle replaces permanent item");
    state = Medicine(state, 1).Battle.State!;
    var outgoing = state.Battle.CalledParty!.Player[0];
    state = Require(ForestTrainerCombatRules.SwitchPlayer(catalog, state, new(outgoing.Pokemon, HeldItem: WildBattleSourcePartyEffects.PersistentHeldItem(outgoing), Status: outgoing.Status), true, 0), "depart recycled holder");
    var permanent = state.Battle.CalledParty!.Player[1];
    state = Require(ForestTrainerCombatRules.SwitchPlayer(catalog, state, new(permanent.Pokemon, HeldItem: WildBattleSourcePartyEffects.PersistentHeldItem(permanent), Status: permanent.Status), true, 1), "reenter recycled holder");
    var flute = Flute(state);
    Check(flute.Battle.State!.Battle.Player.HeldItem == "ITEM_NONE" && flute.Party[1].HeldItem == "ITEM_PECHA_BERRY", "source suppression stays separate from permanent Recycle item after flute");
});
Test("genuine forced turn lock and Baton continuation block item bypass", () =>
{
    var state = Start([Combat(moves: ["FLY", "BATON_PASS"]), Combat("PIDGEOT", 2)]);
    state = Require(ForestTrainerCombatRules.ResolveMove(catalog, state, 0, true), "Fly charge");
    Check(state.Battle.Player.CalledTurnState?.Lock?.Kind == WildBattleCalledLockKind.Charge, "real source charge lock");
    Atomic(state, Owned(state), "source_trainer_item_locked");
    state = Start([Combat(moves: ["BATON_PASS"]), Combat("PIDGEOT", 2)]);
    state = Require(ForestTrainerCombatRules.ResolveMove(catalog, state, 0, true), "Baton action");
    Check(state.Battle.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass, "real source Baton boundary");
    Atomic(state, Owned(state), "source_trainer_item_pending");
    state = Require(ForestTrainerCombatRules.ChooseSourceParty(catalog, state, 1), "Baton selection");
    Check(state.SourceTurn is null && state.Battle.Turn == 1 && state.CeruleanPlayerPartyIndex == 1, "original queued enemy resumes once");
});
Test("actual Transform keeps copied move PP and permanent owner through item turns", () =>
{
    var state = Start([Combat(moves: ["TRANSFORM"]), Combat("PIDGEOT", 2)]);
    state = Require(ForestTrainerCombatRules.ResolveMove(catalog, state, 0, true), "actual source Transform");
    Check(state.Battle.Player.Transform is not null && state.Battle.Player.Pokemon.Species == state.Battle.Opponent.Pokemon.Species, "real copied combatant");
    var copied = state.Battle.Player.Pokemon.Moves;
    var original = state.Battle.CalledParty!.Player[0].Pokemon;
    var medicine = Medicine(state);
    Check(medicine.Battle.State!.Battle.Player.Transform is not null && medicine.Battle.State.Battle.Player.Pokemon.Moves.SequenceEqual(copied), "medicine preserves real copied PP");
    Check(medicine.Party[0].Pokemon.Species == original.Species && medicine.Party[0].Pokemon.Moves.SequenceEqual(original.Moves), "medicine returns permanent identity and actual Transform PP debit");
    var flute = Flute(medicine.Battle.State);
    Check(flute.Battle.State!.Battle.Player.Transform is not null && flute.Battle.State.Battle.Player.Pokemon.Moves.SequenceEqual(copied), "flute also preserves copied PP");
    Check(flute.Party[0].Pokemon.Species == original.Species && flute.Party[0].Pokemon.Moves.SequenceEqual(original.Moves), "flute preserves original move history");
});
Test("source end turn faint yields and resumes actual delayed callback once", () =>
{
    var state = Start(name: "TAMER_PHIL");
    state = Sync(state with { Battle = state.Battle with { Player = state.Battle.Player with { Status = WildBattleStatus.Poison, Pokemon = state.Battle.Player.Pokemon with { CurrentHp = 1 } },
        Opponent = state.Battle.Opponent with { Status = 5 }, CalledDelayed = new() { OpponentFutureAttack = new(1, Move("FUTURE_SIGHT"), WildBattleSide.Player, 7) } } });
    var used = Medicine(state, 1); state = used.Battle.State!;
    Check(state.Phase == ForestTrainerCombatPhase.AwaitingPlayerReplacement && state.SourceTurn?.EndTurn is not null, "actual endturn source continuation retained");
    Atomic(state, used.Party);
    var next = state.Battle.CalledParty!.Player[1]; int enemyHp = state.Battle.Opponent.Pokemon.CurrentHp;
    var resumed = ForestTrainerCombatRules.ReplaceFaintedPlayer(catalog, state,
        new(next.Pokemon, HeldItem: WildBattleSourcePartyEffects.PersistentHeldItem(next), Status: next.Status), false, 1);
    state = Require(resumed, "source replace after item");
    var delayedHit = resumed.Events.OfType<WildBattleHpChangedEvent>().Single(e => e.Move == Move("FUTURE_SIGHT"));
    Check(delayedHit.Damage is >= 5 and <= 7 && state.Battle.Opponent.Pokemon.CurrentHp == enemyHp - delayedHit.Damage &&
        state.Battle.CalledDelayed!.OpponentFutureAttack is null, "due Future Sight hits once after replacement with its source damage draw");
    Check(resumed.Events.OfType<WildBattleCalledDelayedEvent>().Count(e => e.Move == Move("FUTURE_SIGHT") && e.Reason == "resolved") == 1, "delayed callback resolved exactly once");
    Check(state.Battle.Turn == 1 && state.SourceTurn is null, "one item turn completed");
    Check(!resumed.Events.OfType<WildBattleMedicineAppliedEvent>().Any(), "no medicine replay");
});
Console.WriteLine(JsonSerializer.Serialize(new { passed = failed == 0 && groups > 0, groups, checks, failed, runtime = Environment.Version.ToString() }));
return failed == 0 && groups > 0 ? 0 : 1;
