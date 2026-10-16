using System.Collections.Immutable;
using System.Text.Json.Nodes;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class EditedLandEncounterTests : WildBattleFixture
{
    [TestMethod]
    [DataRow("route11", "map.route-11")]
    [DataRow("rocktunnel", "map.rock-tunnel-1f")]
    public void EditedGeneratedTableCompletesPublicBattleAndSave(string family, string mapId)
    {
        using var output = TestAssets.TemporaryOutput();
        var source = Path.Combine(TestAssets.Root, "content/generated");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(output.Path, "content/generated", Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        var path = Path.Combine(output.Path, $"content/generated/data/encounters/{family}.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var row = document["tables"]!.AsArray().Single(t => t!["map"]!["value"]!.GetValue<string>() == mapId)!;
        row["rawEncounterRate"] = 22;
        var slots = row["slots"]!.AsArray();
        var first = slots[0]!;
        // Keep the old save's existing acquisition ranges represented elsewhere.
        var inherited = slots.Skip(1).First(s => s!["species"]!["value"]!.GetValue<string>() == first["species"]!["value"]!.GetValue<string>())!;
        inherited["minimumLevel"] = Math.Min(inherited["minimumLevel"]!.GetValue<int>(), first["minimumLevel"]!.GetValue<int>());
        inherited["maximumLevel"] = Math.Max(inherited["maximumLevel"]!.GetValue<int>(), first["maximumLevel"]!.GetValue<int>());
        first["species"]!["value"] = "pret:SPECIES_PIDGEY";
        first["minimumLevel"] = 5;
        first["maximumLevel"] = 30;
        // Preserve the loader's exact coverage contract after editing the slot species.
        var species = document["tables"]!.AsArray().SelectMany(t => t!["slots"]!.AsArray())
            .Select(s => s!["species"]!["value"]!.GetValue<string>()).ToHashSet();
        var held = document["heldItems"]!.AsArray();
        foreach (var item in held.ToArray())
            if (!species.Contains(item!["species"]!.GetValue<string>())) held.Remove(item);
        held.Add(new JsonObject { ["species"] = "pret:SPECIES_PIDGEY", ["common"] = "ITEM_NONE", ["rare"] = "ITEM_NONE" });
        File.WriteAllText(path, document.ToJsonString());
        content = PretCampaignCatalog.LoadCurrent(output.Path);
        validator = CampaignSession.CreateCurrent(content);
        var table = content.LandEncounters![new(mapId)];
        Assert.AreEqual(22, table.RawEncounterRate);
        var map = content.Maps[table.Map];
        var definition = content.MapDefinition(map.Id);
        var cell = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y)))
            .First(p => map.GetTile(p.x, p.y) is { Blocked: false } && map.SourceEncounterTypeAt(p.x, p.y) == 1 &&
                !definition.Actors.Any(a => a.X == p.x && a.Y == p.y));
        var behavior = (byte)map.SourceBehaviorAt(cell.x, cell.y)!;
        if (family == "rocktunnel") Assert.AreNotEqual(EarlyLandEncounterRules.TallGrassBehavior, behavior);
        var process = new EarlyLandEncounterState(new(0), behavior, ushort.MaxValue, 21,
            RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var result = EarlyLandEncounterRules.Check(process, table,
            new(table.Map, EncounterCheckTrigger.TurnInPlaceCompleted, behavior, EncounterTileType.Land),
            new(RulesVersion: process.RulesVersion), new(3));
        Assert.AreEqual(EncounterCheckOutcome.CandidateReady, result.Outcome);
        Assert.AreEqual(new SpeciesId("pret:SPECIES_PIDGEY"), result.Candidate!.Species);
        Assert.AreEqual((byte)15, result.Candidate.Level);
        var generated = SevenIslandWildPokemonFactory.Generate(content.SevenIslandOwnership!, table, result.State, result.GlobalRng, checkpoint.State.TrainerId);
        Assert.AreEqual(result.Candidate.Species, generated.Pokemon.Species);
        Assert.AreEqual(result.Candidate.Level, generated.Pokemon.Level);
        Assert.IsNull(EarlyLandEncounterRules.CompleteCandidate(result.State, table).Pending);
        var unsupported = new SpeciesId("pret:SPECIES_NOT_OWNED");
        var unsupportedTable = table with { Slots = table.Slots.SetItem(result.Candidate.Slot,
            table.Slots[result.Candidate.Slot] with { Species = unsupported }) };
        var unsupportedState = result.State with { Pending = result.State.Pending! with {
            Candidate = result.Candidate with { Species = unsupported } } };
        Assert.Throws<NotSupportedException>(() => SevenIslandWildPokemonFactory.Generate(content.SevenIslandOwnership!,
            unsupportedTable, unsupportedState, result.GlobalRng, checkpoint.State.TrainerId));
        var fixture = FieldFixture();
        fixture = fixture with {
            World = fixture.World with { Player = new(map.Id, cell.x, cell.y, map.GetTile(cell.x, cell.y)!.Elevation, Direction.South), Rng = fixture.World.Rng with { Seed = 3 } },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId, a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation, Direction.South, a.MovementType, a.ScriptId, a.HideFlag, false, false, a.X, a.Y), StringComparer.Ordinal),
            FlashLevel = definition.RequiresFlash ? FieldFlashSourcePresentation.MaximumLevel : 0,
            Exploration = fixture.Exploration! with { EarlyEncounter = process }
        };
        var repelled = EarlyLandEncounterRules.Check(content.PowerPlantOwnership!, process, table,
            new(table.Map, EncounterCheckTrigger.TurnInPlaceCompleted, behavior, EncounterTileType.Land),
            fixture.Party, 10, new(3), Game.Domain.Roaming.RoamerState.Empty, new(0, 1), extendedRepel: true);
        Assert.AreEqual(EncounterCheckOutcome.RepelSuppressed, repelled.Outcome);
        Assert.IsNull(repelled.State.Pending);
        Assert.AreEqual(result.GlobalRng, repelled.GlobalRng);
        Assert.IsNull(validator.ValidatePlayerSave(fixture));
        Assert.AreEqual("save_encounter_pending", validator.ValidatePlayerSave(fixture with { Exploration = fixture.Exploration with { EarlyEncounter = result.State } }));
        var session = CampaignSession.RestoreCurrent(content, fixture, checkpoint.Generation);
        Send(session, new("turn", Direction: Direction.North));
        Drain(session);
        Assert.IsNotNull(session.State.WildBattle);
        Assert.AreEqual(new SpeciesId("pret:SPECIES_PIDGEY"), session.State.WildBattle.State.Opponent.Pokemon.Species);
        Assert.IsTrue(session.State.WildBattle.State.Opponent.Pokemon.Level > 5);
        Assert.IsNull(session.State.Exploration!.EarlyEncounter!.Pending);
        for (int attempt = 0; attempt < 20 && session.State.WildBattle is not null; attempt++)
        {
            Send(session, new("battle_menu", Text: "run"));
            Drain(session);
        }
        Assert.IsNull(session.State.WildBattle, "ordinary battle must finish before save");
        StrictRoundtrip(session);

        // The same resource cannot introduce nonexistent maps or unowned species.
        row["map"]!["value"] = "map.unregistered";
        File.WriteAllText(path, document.ToJsonString());
        Assert.Throws<NotSupportedException>(() => CampaignSession.CreateCurrent(PretCampaignCatalog.LoadCurrent(output.Path)));
        row["map"]!["value"] = mapId;
        first["species"]!["value"] = "pret:SPECIES_NOT_OWNED";
        held.Single(r => r!["species"]!.GetValue<string>() == "pret:SPECIES_PIDGEY")!["species"] = "pret:SPECIES_NOT_OWNED";
        File.WriteAllText(path, document.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CampaignSession.CreateCurrent(PretCampaignCatalog.LoadCurrent(output.Path)));
    }
}
