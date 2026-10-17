using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Application.Campaign;

[TestClass]
public sealed class ScriptInventoryExecutorTests : WildBattleFixture
{
    // Direct shared-executor tests use actual compiled instructions and controlled
    // interpreter cursors, independently of any map or powder vendor context.
    private (CampaignState State, StoryEffect Effect) Pending(CampaignState state, string programId, string opcode, string item, int quantity)
    {
        var program=content.FindProgram(programId)!;
        int index=Enumerable.Range(0,program.Instructions.Length).Single(i=>program.Instructions[i].Opcode==opcode);
        var instruction=program.Instructions[index];
        Assert.IsTrue(content.TryConstant(item,out int id));
        var variables=state.Script.Variables.SetItem("VAR_0x8000",id).SetItem("VAR_0x8001",quantity);
        state=state with{Script=state.Script with{Stack=[new(programId,index+1)],Variables=variables,
            Locked=true,Completed=false,Wait=new(StoryWaitKind.NativeEffect,"event:123"),EffectSequence=123}};
        var args=instruction.Args.Select(a=>a.StartsWith("VAR_",StringComparison.Ordinal)?variables[a].ToString(System.Globalization.CultureInfo.InvariantCulture):a).ToImmutableArray();
        return (state,new(123,opcode,args,instruction.Source));
    }
    private CampaignState Apply(CampaignState state,StoryEffect effect)
    {
        object?[] args=[state,effect,null];
        Assert.AreEqual(true,typeof(CampaignSession).GetMethod("TryApplyScriptInventoryEffect",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(validator,args));
        return (CampaignState)args[2]!;
    }
    [TestMethod]
    [DataRow("ITEM_POTION")]
    [DataRow("ITEM_POWDER_JAR")]
    [DataRow("ITEM_TM34")]
    public void SameSourceExecutorChecksAndAddsOrdinaryKeyAndMachinePockets(string symbol)
    {
        var state=FieldFixture();var item=content.Inventory.Items[new("pret:"+symbol)];
        state=state with{Inventory=state.Inventory! with{
            BagItems=state.Inventory.BagItems.RemoveAll(s=>s.Item==item.Id),
            KeyItems=state.Inventory.KeyItems!.Value.RemoveAll(s=>s.Item==item.Id),
            Machines=state.Inventory.Machines!.Value.RemoveAll(s=>s.Item==item.Id)}};
        var pending=Pending(state,"Std_FindItem","checkitemspace",symbol,1);
        var check=Apply(pending.State,pending.Effect);Assert.AreEqual(1,check.Script.Variables["VAR_RESULT"]);
        Assert.AreSame(state.Inventory,check.Inventory);
        pending=Pending(state,"Std_ObtainItem","additem",symbol,1);
        var added=Apply(pending.State,pending.Effect);
        var pocket=item.Pocket switch{"POCKET_KEY_ITEMS"=>added.Inventory!.KeyItems!.Value,"POCKET_TM_CASE"=>added.Inventory!.Machines!.Value,_=>added.Inventory!.BagItems};
        Assert.AreEqual(1,OpeningInventoryRules.Quantity(pocket,item.Id));Assert.AreEqual(1,added.Script.Variables["VAR_RESULT"]);
    }
    [TestMethod]
    [DataRow("ITEM_POKE_BALL","POKé BALLS")]
    [DataRow("ITEM_CHERI_BERRY","CHERI BERRIES")]
    [DataRow("ITEM_POTION","POTION")]
    public void PluralBuffersFollowSourceItemRules(string symbol,string expected)
    {
        var pending=Pending(FieldFixture(),"EventScript_ObtainItemMessage","bufferitemnameplural",symbol,2);
        Assert.AreEqual(expected,Apply(pending.State,pending.Effect).TextBuffers["STR_VAR_2"]);
    }
    [TestMethod]
    public void UnadmittedItemCannotBorrowBattleCatalogDefinitionForText()
    {
        var pending=Pending(FieldFixture(),"EventScript_ObtainItemMessage","bufferitemnameplural","ITEM_ENIGMA_BERRY",2);
        var error=Assert.ThrowsExactly<TargetInvocationException>(()=>Apply(pending.State,pending.Effect));
        Assert.AreEqual("script_inventory_item_not_supported",error.InnerException!.Message);
    }
    [TestMethod]
    public void RegisteredFieldServiceUsesSameInventoryAndTextSemantics()
    {
        var service=content.FieldItemServices.First(s=>s.ProgramIds.Contains("Std_ObtainItem")&&s.ProgramIds.Contains("EventScript_ObtainItemMessage"));
        var state=FieldFixture();
        state=state with{World=state.World with{Player=state.World.Player with{MapId=service.Map}},FieldItemService=new(service.Id)};
        foreach(var (program,opcode) in new[]{("Std_ObtainItem","additem"),("EventScript_ObtainItemMessage","bufferitemnameplural")})
        {
            var pending=Pending(state,program,opcode,service.ItemSymbols[0],1);
            var shared=Apply(pending.State,pending.Effect);
            object?[] args=[pending.State,pending.Effect,null];
            Assert.AreEqual(true,typeof(CampaignSession).GetMethod("TryApplyFieldItemServiceEffect",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(validator,args));
            Assert.AreEqual(CanonicalJson.Hash(shared),CanonicalJson.Hash((CampaignState)args[2]!));
        }
    }
    [TestMethod]
    public void StaleCursorAndAlteredOperandsCannotCommitInventory()
    {
        var pending=Pending(FieldFixture(),"Std_ObtainItem","additem","ITEM_POTION",1);
        var error=Assert.ThrowsExactly<TargetInvocationException>(()=>Apply(pending.State with{Script=pending.State.Script with{Wait=new(StoryWaitKind.NativeEffect,"event:122")}},pending.Effect));
        Assert.AreEqual("script_inventory_effect_context",error.InnerException!.Message);
        error=Assert.ThrowsExactly<TargetInvocationException>(()=>Apply(pending.State,pending.Effect with{Args=pending.Effect.Args.SetItem(1,"2")}));
        Assert.AreEqual("script_inventory_effect_context",error.InnerException!.Message);
    }
}
