using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.World;

// Reusable current-contract cases; all field cases use strict-valid controlled fixtures from
// the real current checkpoint. This does not claim natural acquisition, native UI or audio.
public static class StoneEvolutionCases
{
    public static void Run(ICampaignCatalog catalog, CampaignState original, long generation,
        string output, Action<bool, string> check)
    {
        var content = catalog.PokemonContent!;
        var ownership = OwnedBattlePokemonCatalog.For(content.Catalog)!;
        var inspector = CampaignSession.CreateCurrent(catalog);
        var trace = new List<object>(); int commands = 0;
        string Hash<T>(T value) => CanonicalJson.Hash(value);
        int Quantity(CampaignState state, string item) => OpeningInventoryRules.Quantity(state.Inventory!.BagItems, new(item));
        void Refuses(Action action, string name)
        {
            try { action(); } catch (Exception e) when (e is ArgumentException or NotSupportedException)
            { check(true, name + ": " + e.Message); return; }
            check(false, name);
        }
        PartyPokemon Factory(string species, int level)
        {
            var mon = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_" + species), level, 42,
                IvPolicy.Fixed(17), PersonalityPolicy.Fixed(324468760)), new RngState(0)).Pokemon;
            return new(91, mon, content.SpeciesNames[mon.SourceSpeciesId], "RED", 0, 2, "MAPSEC_ROUTE_1", level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        }
        foreach (var (before, after) in new[] { ("NIDORINA", "NIDOQUEEN"), ("NIDORINO", "NIDOKING"), ("CLEFAIRY", "CLEFABLE"), ("JIGGLYPUFF", "WIGGLYTUFF") })
        {
            var mon = Factory(before, 22);
            var proposal = ItemEvolutionRules.Eligibility(content, ownership, mon)!;
            check(proposal.TargetSpecies.Value == "pret:SPECIES_" + after && !proposal.CanCancel, "source Moon Stone family " + before);
            var result = ItemEvolutionRules.Apply(content, ownership, mon, proposal);
            check(result.Pokemon.Pokemon.Species == proposal.TargetSpecies && result.Pokemon.InstanceId == mon.InstanceId, "apply exact owned family " + before);
        }
        var nidorino = Factory("NIDORINO", 22);
        var full = nidorino.Pokemon.Moves;
        var cut = content.Catalog.Moves[new("pret:MOVE_CUT")];
        full = full.SetItem(0, new(cut.Id, 7, cut.Pp * 6 / 5, 1));
        nidorino = nidorino with { Nickname = "ROYAL", Pokemon = nidorino.Pokemon with { CurrentHp = nidorino.Pokemon.Stats.MaxHp - 9, Moves = full } };
        var nidProposal = ItemEvolutionRules.Eligibility(content, ownership, nidorino)!;
        var nidResult = ItemEvolutionRules.Apply(content, ownership, nidorino, nidProposal);
        check(nidResult.Pokemon.Nickname == "ROYAL" && Hash(nidResult.Pokemon.Pokemon.Moves) == Hash(full), "Nidorino keeps nickname, full HM moves and depleted PP Ups");
        check(nidResult.PendingMoves.Any(m => m.Move.Value == "pret:MOVE_THRASH") && nidResult.Pokemon.Pokemon.Stats.MaxHp - nidResult.Pokemon.Pokemon.CurrentHp == 9, "Nidoking exact level22 Thrash decision and HP deficit");
        Refuses(() => ItemEvolutionRules.Apply(content, ownership, nidorino with { InstanceId = 92 }, nidProposal), "stale different-member proposal refused");
        Refuses(() => ItemEvolutionRules.Apply(content, ownership, nidorino, nidProposal with { TargetSpecies = new("pret:SPECIES_CLEFABLE") }), "forged target refused");
        Refuses(() => ItemEvolutionRules.Eligibility(content with { Catalog = content.Catalog with { } }, ownership, nidorino), "cloned owned catalog refused");
        Refuses(() => ItemEvolutionRules.Eligibility(content, OwnedBattlePokemonCatalog.For(catalog.SixIslandOwnership!.Catalog)!, nidorino), "mismatched authenticated predecessor proof refused");
        Refuses(() => ItemEvolutionRules.Eligibility(content with { Catalog = catalog.CelioOpponents!.Catalog }, ownership, nidorino), "opponent catalog cannot borrow ownership");
        Refuses(() => ItemEvolutionRules.Eligibility(content, ownership, nidorino, "ITEM_FIRE_STONE"), "unexposed stone refused");
        Refuses(() => ItemEvolutionRules.SourceEvolution(content, ownership, new("pret:SPECIES_SUNKERN"), ItemEvolutionRules.SunStone), "trainer-only Sunkern cannot use source edge as ownership");
        check(SixIslandItemEvolutionRules.SourceEdge(catalog.SixIslandOwnership!) is not null, "original Six source proof retained");

        CampaignState Fixture(int species, string item)
        {
            var address = original.Storage!.Boxes.SelectMany((box, b) => box.Slots.Select((mon, s) => new { mon, b, s }))
                .Single(x => x.mon?.Pokemon.SourceSpeciesId == species);
            var deposit = PokemonStorageRules.Deposit(content, original.Party, original.Storage, 0, 13);
            check(deposit.Succeeded, "controlled source deposit for " + species);
            var withdrawal = PokemonStorageRules.Withdraw(content, deposit.Party, deposit.Storage, address.b, address.s);
            check(withdrawal.Succeeded, "controlled withdrawal of existing owned " + species);
            var bag = original.Inventory!.BagItems;
            if (!bag.Any(x => x.Item.Value == item)) bag = bag.Add(new(new(item), 2));
            var fixture = original with { Party = withdrawal.Party, Storage = withdrawal.Storage, Inventory = original.Inventory with { BagItems = bag } };
            check(inspector.ValidatePlayerSave(fixture) is null, "strict controlled fixture " + species);
            return fixture;
        }
        CampaignSession Session(CampaignState state)
        {
            check(inspector.ValidatePlayerSave(state) is null, "strict player boundary before test: " + inspector.ValidatePlayerSave(state));
            return CampaignSession.RestoreCurrent(catalog, state, generation);
        }
        void Send(CampaignSession session, CampaignCommand command)
        {
            var revision = session.Revision; var id = "stone:" + commands++;
            var result = session.Execute(id, revision, command);
            trace.Add(new { id, command, result.Status, result.Error, result.StateHash, result.Revision });
            check(result.Status == CommandStatus.Accepted, "public " + command.Kind + " accepted: " + result.Error);
            var retry = session.Execute(id, revision, command);
            check(retry.Status == CommandStatus.Rejected && retry.Error == "stale_revision" && retry.Revision == result.Revision && ReferenceEquals(retry.State, result.State), "stale retry atomic " + command.Kind);
        }
        void Reject(CampaignSession session, CampaignCommand command, string error)
        {
            var before = session.State; var revision = session.Revision;
            var result = session.Execute("negative:" + commands++, revision, command);
            trace.Add(new { command, result.Status, result.Error, result.StateHash, result.Revision });
            check(result.Status == CommandStatus.Rejected && result.Error == error && result.Revision == revision && ReferenceEquals(before, result.State), "atomic refusal " + error);
        }
        void Drain(CampaignSession session)
        {
            for (int i = 0; i < 25 && session.State.Evolution is not null; i++)
                Send(session, session.State.Evolution.Stage == CampaignEvolutionStage.LearnMove ? new("evolution_learn", MoveSlot: -1) : new("ack"));
            check(session.State.Evolution is null && session.State.ItemMenu is { Origin: "bag", View: "bag" }, "evolution returns to original bag");
            Send(session, new("menu_back"));
        }
        void Save(CampaignSession session, string name)
        {
            check(session.ValidatePlayerSave(session.State) is null, name + " strict stable save: " + session.ValidatePlayerSave(session.State));
            var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
            var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
            var restored = CampaignSession.RestoreCurrent(catalog, decoded.State, decoded.Generation);
            check(restored.Revision == session.Revision && Hash(restored.State) == Hash(session.State), name + " exact strict codec and Restore");
            File.WriteAllBytes(Path.Combine(output, name + ".json"), bytes);
        }
        CampaignState Evolve(CampaignState fixture, string item, int target, string name, bool hm = false)
        {
            var session = Session(fixture); int index = fixture.Party.Length - 1; var before = fixture.Party[index];
            Send(session, new("bag_open"));
            Reject(session, new("field_evolution", Text: item, PartyIndex: index, Quantity: 2), "evolution_item_command_invalid");
            Send(session, new("field_evolution", Text: item, PartyIndex: index));
            var after = session.State.Party[index];
            check(after.Pokemon.SourceSpeciesId == target && session.State.Evolution?.ItemUse is not null, name + " actual public stone evolution");
            check(after.InstanceId == before.InstanceId && after.Pokemon.Personality == before.Pokemon.Personality && after.Pokemon.Ivs == before.Pokemon.Ivs &&
                after.Pokemon.OriginalTrainerId == before.Pokemon.OriginalTrainerId && after.MetLevel == before.MetLevel && after.MetLocation == before.MetLocation, name + " original owned identity retained");
            check(Hash(after.Pokemon.Moves) == Hash(before.Pokemon.Moves), name + " existing PP, PP Ups and moves retained");
            check(before.Pokemon.CurrentHp == 0 ? after.Pokemon.CurrentHp == 0 : after.Pokemon.Stats.MaxHp - after.Pokemon.CurrentHp == before.Pokemon.Stats.MaxHp - before.Pokemon.CurrentHp, name + " living HP deficit or fainting retained");
            check(Quantity(session.State, item) == Quantity(fixture, item) - 1, name + " exactly one stone consumed");
            check(Hash(session.State.Exploration) == Hash(fixture.Exploration) && Hash(session.State.Storage) == Hash(fixture.Storage) && session.State.World.Rng == fixture.World.Rng, name + " capture ledger, storage and world RNG unchanged");
            check(session.State.EvolutionHistory!.Value[^1] is { BattleId: null } history && history.InstanceId == before.InstanceId && history.Item == item && history.Level == before.Pokemon.Level && history.LevelBeforeBattle == history.Level, name + " same-level stone history without invented battle");
            check(session.ValidatePlayerSave(session.State) == "save_event_active", name + " modal save refused");
            Reject(session, new("evolution_choice", Choice: 0), "evolution_decision_required");
            Reject(session, new("field_evolution", Text: item, PartyIndex: index), "evolution_decision_required");
            if (hm)
            {
                for (int i = 0; i < 10 && session.State.Evolution!.Stage != CampaignEvolutionStage.LearnMove; i++) Send(session, new("ack"));
                check(session.State.Evolution?.Stage == CampaignEvolutionStage.LearnMove, "Sun Stone exact-level move reaches decision");
                Reject(session, new("evolution_learn", MoveSlot: 0), "hm_move_cannot_be_forgotten");
            }
            Drain(session); Save(session, name);
            var historyEntries = session.State.EvolutionHistory!.Value;
            check(inspector.ValidatePlayerSave(session.State with { EvolutionHistory = historyEntries.RemoveAt(historyEntries.Length - 1) }) is not null, name + " missing evolution lineage rejected");
            check(inspector.ValidatePlayerSave(session.State with { EvolutionHistory = historyEntries.SetItem(historyEntries.Length - 1, historyEntries[^1] with { Item = item == "pret:ITEM_SUN_STONE" ? "pret:ITEM_MOON_STONE" : "pret:ITEM_SUN_STONE" }) }) is not null, name + " wrong stone lineage rejected");
            return session.State;
        }
        var moon = Fixture(35, "pret:ITEM_MOON_STONE");
        var sun = Fixture(44, "pret:ITEM_SUN_STONE");
        Evolve(moon, "pret:ITEM_MOON_STONE", 36, "moon-clefable");
        Evolve(sun, "pret:ITEM_SUN_STONE", 182, "sun-bellossom");
        foreach (var (fixture, item) in new[] { (moon, "pret:ITEM_MOON_STONE"), (sun, "pret:ITEM_SUN_STONE") })
        {
            int index = fixture.Party.Length - 1; var mon = fixture.Party[index] with { HeldItem = "ITEM_EVERSTONE" };
            var held = fixture with { Party = fixture.Party.SetItem(index, mon) };
            check(ItemEvolutionRules.PartyMenuTarget(content, ownership, mon, item[5..]) is not null && ItemEvolutionRules.Eligibility(content, ownership, mon, item[5..]) is null, item + " Everstone menu compatibility differs from actual use");
            var session = Session(held); Send(session, new("bag_open")); var before = session.State;
            Send(session, new("field_evolution", Text: item, PartyIndex: index));
            check(session.State.Evolution is null && session.State.ItemMenu?.MessageSymbol == "gText_WontHaveEffect" && Hash(session.State with { ItemMenu = before.ItemMenu }) == Hash(before), item + " public Everstone no effect and no consumption");
        }
        var wounded = sun.Party[^1]; var pp = content.Catalog.Moves[wounded.Pokemon.Moves[0].Move].Pp;
        wounded = wounded with { Nickname = "PETAL", Pokemon = wounded.Pokemon with { CurrentHp = wounded.Pokemon.Stats.MaxHp - 11,
            Moves = wounded.Pokemon.Moves.SetItem(0, wounded.Pokemon.Moves[0] with { PpUps = 1, MaximumPp = pp * 6 / 5, CurrentPp = 3 }) } };
        var named = Evolve(sun with { Party = sun.Party.SetItem(sun.Party.Length - 1, wounded) }, "pret:ITEM_SUN_STONE", 182, "sun-named-wounded");
        check(named.Party[^1].Nickname == "PETAL", "public custom nickname retained");
        Evolve(sun with { Party = sun.Party.SetItem(sun.Party.Length - 1, sun.Party[^1] with { Pokemon = sun.Party[^1].Pokemon with { CurrentHp = 0 } }) }, "pret:ITEM_SUN_STONE", 182, "sun-fainted");
        // Controlled source factory level/moves only, retaining the original Gloom capture identity.
        var sourceGloom = sun.Party[^1];
        var bellossom = content.Catalog.Species[new("pret:SPECIES_BELLOSSOM")];
        int moveLevel = bellossom.LevelUpMoves.First(m => m.Level >= sourceGloom.MetLevel && m.Level > 1).Level;
        var raised = PokemonFactory.Create(content.Catalog, new(sourceGloom.Pokemon.Species, moveLevel, sourceGloom.Pokemon.OriginalTrainerId,
            IvPolicy.Fixed(17), PersonalityPolicy.Fixed(sourceGloom.Pokemon.Personality)), new RngState(0)).Pokemon;
        check(raised.Ivs == sourceGloom.Pokemon.Ivs, "controlled raised Gloom keeps capture IVs");
        var hmMoves = new[] { "CUT", "TACKLE", "GROWL", "POUND" }.Select(symbol => content.Catalog.Moves[new("pret:MOVE_" + symbol)]).Select(m => new PokemonMoveSlot(m.Id, m.Pp, m.Pp)).ToImmutableArray();
        var hmGloom = sourceGloom with { Pokemon = raised with { Moves = hmMoves } };
        Evolve(sun with { Party = sun.Party.SetItem(sun.Party.Length - 1, hmGloom) }, "pret:ITEM_SUN_STONE", 182, "sun-hm-decision", hm: true);
        File.WriteAllText(Path.Combine(output, "trace.json"), JsonSerializer.Serialize(trace, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "fixture-provenance.json"), JsonSerializer.Serialize(new { originalGeneration = generation,
            originalHash = Hash(original), setup = "Pure storage deposit/withdrawal of existing checkpoint-owned creatures; add two Sun Stones. Domain factory Nidorino family and controlled wounded/nickname/PP/Everstone/fainted/level-and-HM Gloom variants are explicit test inputs. No invented battle or acquisition.",
            hmLevel = moveLevel, scope = "Current Domain/Application only; no natural acquisition or Godot/audio acceptance." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
