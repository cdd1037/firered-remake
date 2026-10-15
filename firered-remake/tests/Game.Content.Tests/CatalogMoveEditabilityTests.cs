using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class CatalogMoveEditabilityTests
{
    [TestMethod]
    public void EditedSharedMoveSetExtendsByActualIdsRatherThanHistoricalTotal()
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var starter = PretPokemonCatalog.Load(canonical, File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/pokemon/starters.json")));
        var extra = canonical.Catalog.Moves.Values.First(move => !starter.Moves.ContainsKey(move.Id));
        var selected = starter.Moves.Add(extra.Id, extra);
        using var output = TestAssets.TemporaryOutput();
        WriteMoves(output.Path, selected.Values, canonical.MoveCatalog);
        var edited = PretMetronomeMoveCatalog.Load(output.Path);
        var expanded = edited.Extend(starter);
        Assert.AreEqual(selected.Count, expanded.Moves.Count);
        Assert.IsTrue(expanded.Moves.Keys.ToHashSet().SetEquals(selected.Keys));
        foreach (var pair in expanded.Moves)
            Assert.IsTrue(PretMetronomeMoveCatalog.DefinitionsEqual(selected[pair.Key], pair.Value));

        var tackle = new MoveId("pret:MOVE_TACKLE");
        var conflict = starter with { Moves = starter.Moves.SetItem(tackle, starter.Moves[tackle] with { Pp = 34 }) };
        Assert.ThrowsExactly<InvalidDataException>(() => edited.Extend(conflict));
        var unknown = extra with { Id = new("pret:MOVE_UNKNOWN") };
        Assert.ThrowsExactly<InvalidDataException>(() => edited.Extend(starter with { Moves = starter.Moves.Add(unknown.Id, unknown) }));
        Assert.ThrowsExactly<ArgumentException>(() => edited.Extend(starter with { Moves = starter.Moves.Remove(tackle) }));
    }

    [TestMethod]
    public void EditedSharedMoveSetStillRejectsInvalidDefinitionsNamesAndDuplicateIds()
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var tackle = canonical.Catalog.Moves[new("pret:MOVE_TACKLE")];
        using var output = TestAssets.TemporaryOutput();
        WriteMoves(output.Path, [tackle with { Pp = 0 }], canonical.MoveCatalog);
        Assert.ThrowsExactly<InvalidDataException>(() => PretMetronomeMoveCatalog.Load(output.Path));
        WriteDocument(output.Path, new([tackle], []));
        Assert.ThrowsExactly<InvalidDataException>(() => PretMetronomeMoveCatalog.Load(output.Path));
        WriteDocument(output.Path, new([tackle, tackle], [new(tackle.Id, "TACKLE")]));
        Assert.ThrowsExactly<ArgumentException>(() => PretMetronomeMoveCatalog.Load(output.Path));
    }

    private static void WriteMoves(string root, IEnumerable<PokemonMoveDefinition> moves, PretMetronomeMoveCatalog names)
    {
        var definitions = moves.ToImmutableArray();
        WriteDocument(root, new(definitions, definitions.Select(move => new MetronomeMoveName(move.Id, names.MoveNames[move.Id])).ToImmutableArray()));
    }

    private static void WriteDocument(string root, MetronomeMoveDocument document)
    {
        var path = Path.Combine(root, PretMetronomeMoveCatalog.CatalogRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(document, ContentJson.CreateOptions()));
    }
}
