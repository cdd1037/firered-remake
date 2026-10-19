using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Events;
using Game.Domain.WildBattle;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class AbilityPreventionPresentationTests
{
    private static SourceDisplayNames Names => TestAssets.Cached(() => SourceDisplayNames.LoadProject(TestAssets.Root,
        SharedItemCatalog.LoadProject(TestAssets.Root), PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root).Catalog));

    private static IReadOnlyDictionary<string,string> BattleText => TestAssets.Cached(() => PretBattleText.Load(TestAssets.Root));

    [TestMethod]
    [DataRow("ABILITY_OWN_TEMPO","sText_PkmnPreventsConfusionWith")]
    [DataRow("ABILITY_HYPER_CUTTER","sText_PkmnsXPreventsYLoss")]
    [DataRow("ABILITY_KEEN_EYE","sText_PkmnsXPreventsYLoss")]
    [DataRow("ABILITY_WHITE_SMOKE","sText_PkmnPreventsStatLossWith")]
    [DataRow("ABILITY_CLEAR_BODY","sText_PkmnPreventsStatLossWith")]
    public void SinglesUsesSharedSourceAbilityMessageAndAffectedParticipant(string ability,string symbol)
    {
        foreach(var target in new[]{WildBattleSide.Player,WildBattleSide.Opponent})
        {
            var actor=target==WildBattleSide.Player?WildBattleSide.Opponent:WildBattleSide.Player;
            WildBattleEvent[] events=[new WildBattleAbilityPreventedEvent(1,1,actor,target,new MoveId("pret:MOVE_SWAGGER"),ability)];
            var display=SourceTrainerPresentationModel.Describe(events,0,s=>s.ToString(),m=>m.Value,(_,_)=>"party", abilityName:Names.AbilityName, typeName:Names.TypeName,
                battleText:BattleText);
            Assert.IsNotNull(display);Assert.IsFalse(display.Unsupported);Assert.IsFalse(display.Silent);
            var text=display.Texts.Single();Assert.AreEqual(symbol,text.Symbol);
            Assert.AreEqual(symbol,SourceTrainerPresentationModel.AbilityPreventionSymbol(ability));
            var fields=text.Values.ToDictionary(x=>x.Key,x=>x.Value);
            Assert.AreEqual(target.ToString(),fields["B_DEF_NAME_WITH_PREFIX"]);
            Assert.AreEqual(target.ToString(),fields["B_SCR_ACTIVE_NAME_WITH_PREFIX"]);
            Assert.AreEqual(Names.AbilityName(ability),fields["B_SCR_ACTIVE_ABILITY"]);
            Assert.AreEqual(actor.ToString(),fields["B_ATK_NAME_WITH_PREFIX"]);
            Assert.AreEqual(ability=="ABILITY_HYPER_CUTTER"?"ATTACK":ability=="ABILITY_KEEN_EYE"?"accuracy":"",fields["B_BUFF1"]);
        }
    }
    [TestMethod][DataRow("ABILITY_VOLT_ABSORB")][DataRow("ABILITY_WATER_ABSORB")]
    public void AbsorbPreventionRemainsSilentUntilItsMatchingRecovery(string ability)
    {
        var move=new MoveId("pret:MOVE_THUNDERBOLT");
        WildBattleEvent[] events=[new WildBattleAbilityPreventedEvent(1,1,WildBattleSide.Player,WildBattleSide.Opponent,move,ability),new WildBattleRecoveredEvent(2,1,WildBattleSide.Opponent,move,5,10,5)];
        var before=SourceTrainerPresentationModel.Describe(events,0,s=>s.ToString(),m=>m.Value,(_,_)=>"party", abilityName:Names.AbilityName, typeName:Names.TypeName);
        Assert.IsTrue(before!.Silent);
        var recovered=SourceTrainerPresentationModel.Describe(events,1,s=>s.ToString(),m=>m.Value,(_,_)=>"party", abilityName:Names.AbilityName, typeName:Names.TypeName)!.Texts.Single();
        Assert.AreEqual("sText_PkmnRestoredHPUsing",recovered.Symbol);
        Assert.AreEqual(Names.AbilityName(ability),recovered.Values.Single(x=>x.Key=="B_DEF_ABILITY").Value);
    }

    [TestMethod]
    [DataRow("TYPE_FIGHTING", "FIGHT")]
    [DataRow("TYPE_ELECTRIC", "ELECTR")]
    [DataRow("TYPE_PSYCHIC", "PSYCHC")]
    [DataRow("TYPE_MYSTERY", "???")]
    public void ChangedTypeMessagesUseImportedDisplayName(string type, string expected)
    {
        WildBattleEvent[] events = [new WildBattleCalledOverlayChangedEvent(1, 1, WildBattleSide.Player,
            new("pret:MOVE_CONVERSION"), null, new() { Type1 = type, Type2 = type })];
        var display = SourceTrainerPresentationModel.Describe(events, 0, s => s.ToString(), m => m.Value,
            (_, _) => "party", abilityName: Names.AbilityName, typeName: Names.TypeName)!;
        Assert.AreEqual("sText_PkmnChangedType", display.Texts.Single().Symbol);
        Assert.AreEqual(expected, display.Texts.Single().Values.Single(v => v.Key == "B_BUFF1").Value);
    }

    [TestMethod]
    [DataRow("ABILITY_NONE", "-------")]
    [DataRow("ABILITY_COMPOUND_EYES", "COMPOUNDEYES")]
    [DataRow("ABILITY_LIGHTNING_ROD", "LIGHTNINGROD")]
    public void CopiedAbilityMessagesUseImportedDisplayName(string ability, string expected)
    {
        WildBattleEvent[] events = [new WildBattleCalledOverlayChangedEvent(1, 1, WildBattleSide.Player,
            new("pret:MOVE_ROLE_PLAY"), null, new() { Ability = ability })];
        var display = SourceTrainerPresentationModel.Describe(events, 0, s => s.ToString(), m => m.Value,
            (_, _) => "party", abilityName: Names.AbilityName, typeName: Names.TypeName)!;
        Assert.AreEqual(expected, display.Texts.Single().Values.Single(v => v.Key == "B_DEF_ABILITY").Value);
    }

    [TestMethod]
    public void InjectedNamesAreLiteralAndDoNotChangeEventIdentities()
    {
        WildBattleEvent[] events = [new WildBattleAbilityActivatedEvent(1, 1, WildBattleSide.Player, "ABILITY_COLOR_CHANGE"),
            new WildBattleCalledOverlayChangedEvent(2, 1, WildBattleSide.Player, new("pret:MOVE_TACKLE"), null,
                new() { Type1 = "TYPE_FIRE", Type2 = "TYPE_FIRE" })];
        var before = System.Text.Json.JsonSerializer.Serialize(events);
        string Ability(string id) { Assert.AreEqual("ABILITY_COLOR_CHANGE", id); return "ability {literal}"; }
        string Type(string id) { Assert.AreEqual("TYPE_FIRE", id); return "type {literal}"; }
        var display = SourceTrainerPresentationModel.Describe(events, 0, s => s.ToString(), m => m.Value,
            (_, _) => "party", abilityName: Ability, typeName: Type)!;
        var values = display.Texts.Single().Values.ToDictionary(v => v.Key, v => v.Value);
        Assert.AreEqual("ability {literal}", values["B_DEF_ABILITY"]);
        Assert.AreEqual("type {literal}", values["B_BUFF1"]);
        Assert.AreEqual(before, System.Text.Json.JsonSerializer.Serialize(events));
        Assert.IsTrue(SourceTrainerPresentationModel.Describe(events, 1, s => s.ToString(), m => m.Value,
            (_, _) => "party", abilityName: Ability, typeName: Type)!.Silent);
    }

    [TestMethod]
    [DataRow(WildBattleStat.Attack, "sText_Attack2", "ATTACK")]
    [DataRow(WildBattleStat.Defense, "sText_Defense2", "DEFENSE")]
    [DataRow(WildBattleStat.Speed, "sText_Speed", "SPEED")]
    [DataRow(WildBattleStat.SpecialAttack, "sText_SpAtk2", "SP. ATK")]
    [DataRow(WildBattleStat.SpecialDefense, "sText_SpDef2", "SP. DEF")]
    [DataRow(WildBattleStat.Accuracy, "sText_Accuracy", "accuracy")]
    [DataRow(WildBattleStat.Evasion, "sText_Evasiveness", "evasiveness")]
    public void BattleStatLabelsUseTheirSourceSymbols(WildBattleStat stat, string symbol, string english)
    {
        var source = BattleText.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Assert.AreEqual(english, SourceTrainerPresentationModel.StatName(source, stat));
        source[symbol] = "CHANGED";
        Assert.AreEqual("CHANGED", SourceTrainerPresentationModel.StatName(source, stat));
    }

    [TestMethod]
    [DataRow(true, false, "rose!", "increased!")]
    [DataRow(true, true, "sharply rose!", "intensely increased!")]
    [DataRow(false, false, "fell!", "decreased!")]
    [DataRow(false, true, "harshly fell!", "severely decreased!")]
    public void BattleStatPhrasesUseSourceOrderAndBindNamesOnce(bool raised, bool sharply, string english, string changed)
    {
        var source = BattleText.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Assert.AreEqual(english, SourceTrainerPresentationModel.StatChangeText(source, raised, sharply));
        source["gBattleText_Rose"] = "increased!"; source["sText_StatSharply"] = "intensely ";
        source["sText_StatFell"] = "decreased!"; source["sText_StatHarshly"] = "severely ";
        string phrase = SourceTrainerPresentationModel.StatChangeText(source, raised, sharply);
        Assert.AreEqual(changed, phrase);
        const string name = @"{B_BUFF1}\p";
        string symbol = raised ? "sText_AttackersStatRose" : "sText_DefendersStatFell";
        var values = new Dictionary<string,string> { ["B_ATK_NAME_WITH_PREFIX"] = name,
            ["B_DEF_NAME_WITH_PREFIX"] = name, ["B_BUFF1"] = "POWER", ["B_BUFF2"] = phrase };
        Assert.AreEqual(name + "'s POWER\n" + changed, SourceBattleText.Pages(source, symbol, values).Single());
    }

    [TestMethod]
    [DataRow("ABILITY_HYPER_CUTTER", "sText_Attack2")]
    [DataRow("ABILITY_KEEN_EYE", "sText_Accuracy")]
    public void AbilityPreventionConsumesTheSameStatLookup(string ability, string symbol)
    {
        WildBattleEvent[] events = [new WildBattleAbilityPreventedEvent(1, 1, WildBattleSide.Player,
            WildBattleSide.Opponent, new("pret:MOVE_SWAGGER"), ability)];
        var source = BattleText.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        source[symbol] = "CHANGED";
        var text = SourceTrainerPresentationModel.Describe(events, 0, s => s.ToString(), m => m.Value,
            (_, _) => "party", abilityName: Names.AbilityName, typeName: Names.TypeName,
            battleText: source)!.Texts.Single();
        Assert.AreEqual("CHANGED", text.Values.Single(value => value.Key == "B_BUFF1").Value);
        Assert.ThrowsExactly<InvalidDataException>(() => SourceTrainerPresentationModel.AbilityPreventionStat(ability, null));
    }

    [TestMethod]
    [DataRow(true, 1, "sText_AllyPkmnPrefix")]
    [DataRow(false, 1, "sText_FoePkmnPrefix2")]
    [DataRow(true, 2, "sText_AllyPkmnPrefix2")]
    [DataRow(false, 2, "sText_FoePkmnPrefix3")]
    [DataRow(true, 3, "sText_AllyPkmnPrefix3")]
    [DataRow(false, 3, "sText_FoePkmnPrefix4")]
    public void SidePrefixKindsRetainTheirDistinctSourceSymbols(bool player, int prefix, string symbol)
    {
        var source = BattleText.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Assert.AreEqual(player ? "Ally" : "Foe", SourceTrainerPresentationModel.SidePrefix(source, player, prefix));
        source[symbol] = "CHANGED";
        Assert.AreEqual("CHANGED", SourceTrainerPresentationModel.SidePrefix(source, player, prefix));
    }
}
