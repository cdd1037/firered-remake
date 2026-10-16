using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class PokedexAideCatalogTests
{
    private const string Entry = "NewTown_Researcher", Request = "ResearchRequest", Flag = "FLAG_RESEARCH_REWARD";
    private const string Symbol = "ITEM_RESEARCH_TOKEN";
    private static readonly MapId Map = new("map.new-town");
    private static readonly QuestItemDefinition Item = new(new("pret:" + Symbol), Symbol, 777, "RESEARCH TOKEN", 100,
        0, 0, "POCKET_ITEMS", "ITEM_TYPE_BAG_MENU", "HOLD_EFFECT_NONE", 0, "NULL", 0, "NULL", 0, true, true, true);

    [TestMethod]
    [DataRow(7, 1)]
    [DataRow(47, 3)]
    public void BindsThresholdRewardAndActorFromDataWithoutOriginalRegionNames(int caught, int quantity)
    {
        var source = Source(caught, quantity);
        var catalog = Bind(source);
        var aide = catalog.ByActor[(Map, "LOCALID_RESEARCHER")];
        Assert.AreEqual(caught, aide.RequiredCaught);
        Assert.AreEqual(quantity, aide.Quantity);
        Assert.AreEqual(Item, aide.Item);
        Assert.AreEqual(Flag, aide.RewardFlag);
        Assert.AreEqual(Entry, aide.Actor.ScriptId);
        var buffer = source.Programs[Request].Instructions[0];
        Assert.AreEqual(buffer, aide.Effects[new(buffer.Source, buffer.Opcode)]);
        var add = source.Programs[Entry].Instructions.Single(i => i.Opcode == "additem");
        Assert.AreEqual(add, aide.Effects[new(add.Source, add.Opcode)]);
    }

    [TestMethod]
    public void ClonedAndUnrelatedActorsDoNotRegisterAnotherService()
    {
        var source = Source(); var map = MapDefinition();
        var clone = map.Actors[0] with { LocalId = "CLONE", IsClone = true };
        var unrelated = map.Actors[0] with { LocalId = "UNRELATED", ScriptId = "Greeting" };
        var catalog = PokedexAideCatalog.Bind(source, [map with { Actors = [map.Actors[0], clone, unrelated] }], Inventory());
        Assert.AreEqual(1, catalog.ByActor.Count);
        Assert.IsFalse(catalog.ByActor.ContainsKey((Map, "CLONE")));
        Assert.IsFalse(catalog.ByActor.ContainsKey((Map, "UNRELATED")));
    }

    [TestMethod]
    [DataRow("count-selector")]
    [DataRow("missing-flag-guard")]
    [DataRow("mismatched-capacity-item")]
    [DataRow("mismatched-capacity-quantity")]
    [DataRow("missing-capacity-failure-branch")]
    [DataRow("reward-before-count")]
    [DataRow("threshold-buffer-mismatch")]
    [DataRow("unsupported-operation")]
    [DataRow("missing-branch-target")]
    public void RejectsMalformedSourceRewardFlowAtBinding(string mutation)
    {
        var source = Source();
        var entry = source.Programs[Entry];
        int Find(string opcode) => entry.Instructions.Select((instruction, index) => (instruction, index))
            .Single(pair => pair.instruction.Opcode == opcode).index;
        void Change(int index, ImmutableArray<string> args) => entry = entry with
            { Instructions = entry.Instructions.SetItem(index, entry.Instructions[index] with { Args = args }) };
        switch (mutation)
        {
            case "count-selector": Change(Find("setvar"), ["VAR_0x8004", "1"]); break;
            case "missing-flag-guard": Change(Find("goto_if_set"), ["FLAG_OTHER", "Already"]); break;
            case "mismatched-capacity-item": Change(Find("checkitemspace"), ["ITEM_OTHER", "1"]); break;
            case "mismatched-capacity-quantity": Change(Find("checkitemspace"), [Symbol, "2"]); break;
            case "missing-capacity-failure-branch":
                int index = Find("checkitemspace") + 1;
                entry = entry with { Instructions = entry.Instructions.SetItem(index,
                    entry.Instructions[index] with { Op = StoryOp.Dialog, Opcode = "msgbox", Args = ["Wrong"] }) };
                break;
            case "reward-before-count":
                var items = entry.Instructions.ToArray();
                int add = Find("additem"), count = Find("specialvar");
                (items[add], items[count]) = (items[count], items[add]);
                entry = entry with { Instructions = items.ToImmutableArray() };
                break;
            case "threshold-buffer-mismatch":
                var request = source.Programs[Request];
                source.Programs[Request] = request with { Instructions = request.Instructions.SetItem(0,
                    request.Instructions[0] with { Args = ["STR_VAR_1", "48"] }) };
                break;
            case "unsupported-operation":
                source.Programs["Already"] = Program("Already", (StoryOp.Unsupported, "unsupported", []));
                break;
            case "missing-branch-target": source.Programs.Remove("NoRoom"); break;
        }
        source.Programs[Entry] = entry;
        Assert.ThrowsExactly<InvalidDataException>(() => Bind(source));
    }

    [TestMethod]
    public void RejectsMissingOrMismatchedRuntimeItemDefinition()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => PokedexAideCatalog.Bind(Source(), [MapDefinition()],
            new([], new(42, 50, 999), new(30, 13, 999), 58, [])));
        Assert.ThrowsExactly<InvalidDataException>(() => PokedexAideCatalog.Bind(Source(), [MapDefinition()],
            new([Item with { SourceItemId = 778 }], new(42, 50, 999), new(30, 13, 999), 58, [])));
    }

    private static PokedexAideCatalog Bind(ScriptCatalog source)
        => PokedexAideCatalog.Bind(source, [MapDefinition()], Inventory());
    private static InventoryCatalog Inventory() => new([Item], new(42, 50, 999), new(30, 13, 999), 58, []);
    private static FieldMapDefinition MapDefinition() => new(Map, "MAP_NEW_TOWN",
        [new("LOCALID_RESEARCHER", 3, "OBJ_RESEARCHER", 2, 3, 3, "MOVEMENT_TYPE_FACE_DOWN", Entry, "0")],
        [], [], new(null, null, null), "MUS_TOWN");

    private static StoryProgram Program(string id, params (StoryOp Op, string Opcode, string[] Args)[] instructions)
        => new(id, instructions.Select((i, index) => new StoryInstruction(i.Op, i.Opcode, i.Args.ToImmutableArray(),
            new("data/maps/NewTown/scripts.inc", id, index + 1))).ToImmutableArray());

    private static ScriptCatalog Source(int caught = 47, int quantity = 1)
    {
        string required = caught.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string amount = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var source = new ScriptCatalog();
        source.Programs[Entry] = Program(Entry,
            (StoryOp.Lock, "lock", []),
            (StoryOp.Call, "call", [Request]),
            (StoryOp.FlagGoto, "goto_if_set", [Flag, "Already"]),
            (StoryOp.Dialog, "msgbox", ["Question", "MSGBOX_YESNO"]),
            (StoryOp.ConditionalGoto, "goto_if_eq", ["VAR_RESULT", "NO", "Decline"]),
            (StoryOp.SetVar, "setvar", ["VAR_0x8004", "0"]),
            (StoryOp.Special, "specialvar", ["VAR_RESULT", "GetPokedexCount"]),
            (StoryOp.Special, "buffernumberstring", ["STR_VAR_3", "VAR_0x8006"]),
            (StoryOp.Call, "call", [Request]),
            (StoryOp.ConditionalGoto, "goto_if_lt", ["VAR_0x8006", required, "TooFew"]),
            (StoryOp.Special, "checkitemspace", [Symbol, amount]),
            (StoryOp.ConditionalGoto, "goto_if_eq", ["VAR_RESULT", "FALSE", "NoRoom"]),
            (StoryOp.Special, "additem", [Symbol, amount]),
            (StoryOp.SetFlag, "setflag", [Flag]),
            (StoryOp.Release, "release", []),
            (StoryOp.End, "end", []));
        source.Programs[Request] = Program(Request,
            (StoryOp.Special, "buffernumberstring", ["STR_VAR_1", required]),
            (StoryOp.Special, "bufferitemname", ["STR_VAR_2", Symbol]),
            (StoryOp.Return, "return", []));
        foreach (string id in new[] { "Already", "Decline", "TooFew", "NoRoom", "Greeting" })
            source.Programs[id] = Program(id, (StoryOp.Dialog, "msgbox", [id + "Text"]),
                (StoryOp.Release, "release", []), (StoryOp.End, "end", []));
        return source;
    }

    private sealed class ScriptCatalog : IStoryCatalog
    {
        public Dictionary<string, StoryProgram> Programs { get; } = new(StringComparer.Ordinal);
        public StoryProgram? FindProgram(string id) => Programs.GetValueOrDefault(id);
        public bool TryConstant(string symbol, out int value)
        {
            value = symbol switch { Symbol => 777, Flag => 900, "POCKET_ITEMS" => 1, "FALSE" or "NO" => 0, _ => -1 };
            return value != -1;
        }
    }
}
