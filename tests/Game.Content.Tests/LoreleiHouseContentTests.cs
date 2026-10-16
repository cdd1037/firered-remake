using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class LoreleiHouseContentTests
{
    private static readonly MapId House = new("map.four-island-loreleis-house");
    private static readonly MapId Town = new("map.four-island");
    private static PretCampaignCatalog Content => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static readonly (int Minimum, string Flag)[] ExpectedUnlocks =
    [
        (25, "FLAG_HIDE_LORELEI_HOUSE_MEOWTH_DOLL"),
        (50, "FLAG_HIDE_LORELEI_HOUSE_CHANSEY_DOLL"),
        (75, "FLAG_HIDE_LORELEIS_HOUSE_NIDORAN_F_DOLL"),
        (100, "FLAG_HIDE_LORELEI_HOUSE_JIGGLYPUFF_DOLL"),
        (125, "FLAG_HIDE_LORELEIS_HOUSE_NIDORAN_M_DOLL"),
        (150, "FLAG_HIDE_LORELEIS_HOUSE_FEAROW_DOLL"),
        (175, "FLAG_HIDE_LORELEIS_HOUSE_PIDGEOT_DOLL"),
        (200, "FLAG_HIDE_LORELEIS_HOUSE_LAPRAS_DOLL")
    ];

    [TestMethod]
    public void OriginalDoorAndRoomRestrictionsMountThroughExistingFourIslandCatalog()
    {
        var content = Content;
        var map = content.Maps[House];
        Assert.AreEqual((11, 9), (map.Width, map.Height));
        var entry = content.Maps[Town].FieldWarps[4];
        var exit = map.FieldWarps.Single();
        Assert.AreEqual((33, 23, House, 0), (entry.X, entry.Y, entry.DestinationMapId, entry.DestinationWarpId));
        Assert.AreEqual((4, 7, Town, 4), (exit.X, exit.Y, exit.DestinationMapId, exit.DestinationWarpId));
        Assert.IsFalse(content.Maps[Town].GetTile(33, 24)!.Blocked);
        Assert.IsFalse(map.GetTile(4, 7)!.Blocked);
        Assert.AreEqual(0x69, content.Maps[Town].SourceBehaviorAt(33, 23));
        Assert.AreEqual(0x65, map.SourceBehaviorAt(4, 7));
        var definition = content.MapDefinition(House);
        Assert.AreEqual(("MAP_TYPE_INDOOR", "MAPSEC_FOUR_ISLAND", "MUS_SEVII_45"),
            (definition.MapType, definition.RegionMapSection, definition.Music));
        Assert.IsFalse(FieldFlySourceRules.MapTypeAllowsFly(definition.MapType));
        Assert.IsFalse(content.EscapeRoutes.Exits.ContainsKey(House));
        Assert.IsFalse(content.LandEncounters!.ContainsKey(House));
        foreach (var name in new[] { "allow_cycling", "allow_escaping", "allow_running", "requires_flash", "show_map_name" })
            Assert.IsFalse(content.SourceMaps[House].Events.GetProperty(name).GetBoolean(), name);
        Assert.IsTrue(definition.CoordTriggers.IsEmpty);
        Assert.IsTrue(definition.BackgroundTriggers.IsEmpty);
        Assert.AreEqual("FourIsland_LoreleisHouse_OnTransition", definition.Programs.OnTransition);
        var transition = content.FindProgram(definition.Programs.OnTransition!)!;
        Assert.AreEqual(2, transition.Instructions.Length);
        Assert.AreEqual(StoryOp.Special, transition.Instructions[0].Op);
        CollectionAssert.AreEqual(new[] { "UpdateLoreleiDollCollection" }, transition.Instructions[0].Args.ToArray());
        Assert.AreEqual(StoryOp.End, transition.Instructions[1].Op);
        var presentation = PretCampaignMapPresentation.Create(content)[House];
        Assert.AreEqual(content.FourIslandRegion!.NpcSourcePath, presentation.NpcSourcePath);
        Assert.IsTrue(presentation.UsesLocalBorder);
    }

    [TestMethod]
    [DataRow(1, "LORELEI", 7, 4, 3, "FLAG_HIDE_LORELEI_IN_HER_HOUSE")]
    [DataRow(2, "WIGGLYTUFF", 0, 2, 3, "0")]
    [DataRow(3, "SEEL", 10, 3, 0, "0")]
    [DataRow(4, "PIKACHU", 9, 3, 0, "0")]
    [DataRow(5, "SLOWPOKE", 7, 2, 3, "0")]
    [DataRow(6, "SLOWBRO", 6, 2, 3, "0")]
    [DataRow(7, "PSYDUCK", 6, 4, 0, "0")]
    [DataRow(8, "MEOWTH", 9, 4, 0, "FLAG_HIDE_LORELEI_HOUSE_MEOWTH_DOLL")]
    [DataRow(9, "CHANSEY", 10, 4, 0, "FLAG_HIDE_LORELEI_HOUSE_CHANSEY_DOLL")]
    [DataRow(10, "JIGGLYPUFF", 0, 3, 3, "FLAG_HIDE_LORELEI_HOUSE_JIGGLYPUFF_DOLL")]
    [DataRow(11, "NIDORAN_F", 0, 5, 3, "FLAG_HIDE_LORELEIS_HOUSE_NIDORAN_F_DOLL")]
    [DataRow(12, "NIDORAN_M", 1, 5, 3, "FLAG_HIDE_LORELEIS_HOUSE_NIDORAN_M_DOLL")]
    [DataRow(13, "PIDGEOT", 9, 0, 3, "FLAG_HIDE_LORELEIS_HOUSE_PIDGEOT_DOLL")]
    [DataRow(14, "FEAROW", 10, 0, 3, "FLAG_HIDE_LORELEIS_HOUSE_FEAROW_DOLL")]
    [DataRow(15, "LAPRAS_DOLL", 9, 6, 3, "FLAG_HIDE_LORELEIS_HOUSE_LAPRAS_DOLL")]
    public void EverySourceObjectKeepsItsExactPositionFlagScriptAndSharedArt(int localId, string graphics,
        int x, int y, int elevation, string hideFlag)
    {
        var content = Content;
        var definition = content.MapDefinition(House);
        Assert.AreEqual(15, definition.Actors.Length);
        var actor = definition.Actors.Single(a => a.SourceLocalId == localId);
        Assert.AreEqual(("OBJ_EVENT_GFX_" + graphics, x, y, elevation, hideFlag),
            (actor.GraphicsId, actor.X, actor.Y, actor.Elevation, actor.HideFlag));
        Assert.AreEqual(localId == 1 ? "MOVEMENT_TYPE_FACE_LEFT" : "MOVEMENT_TYPE_FACE_DOWN", actor.MovementType);
        Assert.AreEqual("TRAINER_TYPE_NONE", actor.TrainerType);
        Assert.AreEqual("FourIsland_LoreleisHouse_EventScript_" + (localId == 1 ? "Lorelei" : "Doll"), actor.ScriptId);
        Assert.IsFalse(content.FindProgram(actor.ScriptId)!.Instructions.Any(i => i.Op == StoryOp.Unsupported));
        using var shared = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,
            "content/generated/sprites/overworld/catalog.json")));
        var art = shared.RootElement.GetProperty(actor.GraphicsId);
        Assert.IsTrue(art.GetProperty("frameCount").GetInt32() > 0);
        Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, "content/generated/sprites/overworld",
            art.GetProperty("texturePath").GetString()!)));
    }

    [TestMethod]
    public void ConvertedUnlockRulesMatchSourceStatThresholdsAndShareOneDefinition()
    {
        var definition = Content.StatFlagUnlocks["UpdateLoreleiDollCollection"];
        Assert.AreSame(Content.FourIslandRegion!.DollCollection, definition);
        Assert.AreEqual(House, definition.Map);
        Assert.AreEqual("UpdateLoreleiDollCollection", definition.Special);
        CollectionAssert.AreEqual(ExpectedUnlocks.Select(r => new FieldStatFlagUnlock("GAME_STAT_ENTERED_HOF", r.Minimum, r.Flag)).ToArray(),
            definition.Rules.ToArray());
        var objects = Content.MapDefinition(House).Actors;
        foreach (var row in ExpectedUnlocks) Assert.AreEqual(1, objects.Count(a => a.HideFlag == row.Flag));
        Assert.AreEqual(6, objects.Count(a => a.HideFlag == "0"));
    }

    [TestMethod]
    [DataRow(0)] [DataRow(24)] [DataRow(25)] [DataRow(49)] [DataRow(50)]
    [DataRow(74)] [DataRow(75)] [DataRow(99)] [DataRow(100)] [DataRow(124)]
    [DataRow(125)] [DataRow(149)] [DataRow(150)] [DataRow(174)] [DataRow(175)]
    [DataRow(199)] [DataRow(200)] [DataRow(999)]
    public void StatRulesUseInclusiveEdgesWithoutChangingUnrelatedFlagsOrCounters(int count)
    {
        var flags = ExpectedUnlocks.ToImmutableSortedDictionary(r => r.Flag, _ => true)
            .Add("FLAG_HIDE_LORELEI_IN_HER_HOUSE", true).Add("FLAG_TALKED_TO_LORELEI_AFTER_WAREHOUSE", false);
        var stats = ImmutableSortedDictionary<string, int>.Empty.Add("GAME_STAT_ENTERED_HOF", count)
            .Add("GAME_STAT_USED_POKECENTER", 1000);
        var result = Content.StatFlagUnlocks["UpdateLoreleiDollCollection"].Apply(flags, stats);
        Assert.AreEqual(flags.Count, result.Count);
        foreach (var row in ExpectedUnlocks) Assert.AreEqual(count < row.Minimum, result[row.Flag], row.Flag);
        Assert.IsTrue(result["FLAG_HIDE_LORELEI_IN_HER_HOUSE"]);
        Assert.IsFalse(result["FLAG_TALKED_TO_LORELEI_AFTER_WAREHOUSE"]);
        Assert.AreEqual(count, stats["GAME_STAT_ENTERED_HOF"]);
        Assert.AreEqual(1000, stats["GAME_STAT_USED_POKECENTER"]);
    }

    [TestMethod]
    public void MissingStatAndRepeatedVisitsCannotRehideACollectedDoll()
    {
        var definition = Content.StatFlagUnlocks["UpdateLoreleiDollCollection"];
        var flags = ExpectedUnlocks.ToImmutableSortedDictionary(r => r.Flag, _ => true);
        Assert.AreSame(flags, definition.Apply(flags, null));
        Assert.AreSame(flags, definition.Apply(flags, ImmutableSortedDictionary<string, int>.Empty));
        var allVisible = definition.Apply(flags, ImmutableSortedDictionary<string, int>.Empty.Add("GAME_STAT_ENTERED_HOF", 200));
        Assert.IsTrue(allVisible.Values.All(v => !v));
        Assert.AreSame(allVisible, definition.Apply(allVisible, ImmutableSortedDictionary<string, int>.Empty.Add("GAME_STAT_ENTERED_HOF", 0)));
    }

    [TestMethod]
    public void InvalidSharedUnlockDefinitionsAreRejectedAtConstruction()
    {
        FieldStatFlagUnlock valid = new("GAME_STAT_ENTERED_HOF", 25, "FLAG_HIDE_DOLL");
        ImmutableArray<FieldStatFlagUnlock>[] invalidRules =
        [
            default, [], [null!], [valid with { Stat = null! }], [valid with { Flag = null! }],
            [valid with { Stat = "VAR_0x8000" }], [valid with { Flag = "not-a-flag" }],
            [valid with { Minimum = 0 }], [valid with { Minimum = -1 }], [valid, valid]
        ];
        foreach (var rules in invalidRules)
            Assert.ThrowsExactly<ArgumentException>(() => new FieldStatFlagUnlocks(House, "UpdateLoreleiDollCollection", rules));
        Assert.ThrowsExactly<ArgumentException>(() => new FieldStatFlagUnlocks(new(" "), "special", [valid]));
        Assert.ThrowsExactly<ArgumentException>(() => new FieldStatFlagUnlocks(House, " ", [valid]));
    }

    [TestMethod]
    public void ExistingIcefallAndCelioScriptsRemainTheOwnersOfLoreleiVisibility()
    {
        var icefall = Content.FindProgram("FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene")!.Instructions.ToArray();
        int battle = Array.FindIndex(icefall, i => i.Opcode == "trainerbattle_no_intro");
        int clear = Array.FindIndex(icefall, i => i.Opcode == "clearflag" && i.Args.SequenceEqual(new[] { "FLAG_HIDE_LORELEI_IN_HER_HOUSE" }));
        int scene = Array.FindIndex(icefall, i => i.Opcode == "setvar" && i.Args.SequenceEqual(new[] { "VAR_MAP_SCENE_ICEFALL_CAVE_BACK", "1" }));
        Assert.IsTrue(battle >= 0 && clear > battle && scene > clear);
        var celio = Content.FindProgram("OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire")!.Instructions.ToArray();
        int postgame = Array.FindIndex(celio, i => i.Opcode == "special" && i.Args.SequenceEqual(new[] { "SetPostgameFlags" }));
        int hide = Array.FindIndex(celio, i => i.Opcode == "setflag" && i.Args.SequenceEqual(new[] { "FLAG_HIDE_LORELEI_IN_HER_HOUSE" }));
        int roam = Array.FindIndex(celio, i => i.Opcode == "special" && i.Args.SequenceEqual(new[] { "InitRoamer" }));
        Assert.IsTrue(postgame >= 0 && hide > postgame && roam > hide);
        Assert.IsFalse(Content.StatFlagUnlocks["UpdateLoreleiDollCollection"].Rules.Any(r => r.Flag == "FLAG_HIDE_LORELEI_IN_HER_HOUSE"));
    }
}
