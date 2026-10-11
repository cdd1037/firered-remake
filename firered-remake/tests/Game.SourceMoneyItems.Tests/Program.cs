using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if(args.Length!=3)return 2;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var c=proof.Catalog;
if(SourceHeldItemCatalog.For(c) is null)SourceHeldItemCatalog.Bind(proof,SourceBattleItemCatalogLoader.LoadAuthenticated(args[1]));
int groups=0,checks=0,failures=0,turns=0,oracle=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Test(string name,Action work){groups++;try{work();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value);
WildBattleCombatant Mon(string item="ITEM_NONE",string species="MAGIKARP",string move="SPLASH",int level=30,uint pid=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 MoveId id=new("pret:MOVE_"+move);Check(c.Species[p.Species].LevelUpMoves.Any(m=>m.Move==id&&m.Level<=level),"natural move");
 return new(p with{Moves=[new(id,c.Moves[id].Pp,c.Moves[id].Pp)]},HeldItem:item);
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
WildBattleState Good(WildBattleResult r){Check(r.Succeeded,r.Fault??"single accepted");Check(WildBattleRules.Validate(c,r.State)==null,"single valid "+WildBattleRules.Validate(c,r.State));return r.State!;}
WildBattleState Start(WildBattleCombatant p,WildBattleCombatant? o=null,params WildBattleCombatant[] reserves)=>Good(WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[p,..reserves],0,o??Mon(),new(31)));
WildBattleState Turn(WildBattleState s){turns++;return Good(WildBattleRules.ResolveMove(c,s,0));}
SourceBattleFrame Frame(WildBattleCombatant[] bodies,WildBattleCombatant[]? reserves=null)=>new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,
 SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),
 Parties=[new(BattleSide.Player,new[]{Member(bodies[0]),Member(bodies[2])}.Concat((reserves??[]).Select(Member)).ToImmutableArray()),new(BattleSide.Opponent,[Member(bodies[1]),Member(bodies[3])])],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,bodies[i])).ToImmutableArray()};
SourceFourActionResult Good4(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four accepted");Check(SourceBattleIdentityRules.Validate(r.State)==null,"four identity");return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good4(SourceFourEntryAbilities.InitializeEntry(c,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f)=>f.Slots.Where(s=>f.CanAct(s.Slot)).Select(s=>new SourceActionSelection(s.Occupant!.Value,SourceActionKind.Move,0,c.Moves[s.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?s.Slot:s.Slot.Opposite)).ToImmutableArray();
SourceBattleFrame Full4(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null){var r=Good4(SourceFourActionTurns.BeginActions(c,f,f.Revision,choices??Choices(f)));for(int i=0;r.State.FourTurn!=null&&i<16;i++)r=Good4(SourceFourActionTurns.Advance(c,r.State,r.Continuation!.Value));Check(r.CompletedCleanTurn,"complete four turn");turns++;return r.State;}
Test("source singles initial entry latches either side, consumes nothing and has no extra event or RNG",()=>{
 var baseline=Start(Mon());
 foreach(bool enemy in new[]{false,true}){var s=Start(Mon(enemy?"ITEM_NONE":"ITEM_AMULET_COIN"),Mon(enemy?"ITEM_AMULET_COIN":"ITEM_NONE"));
 Check(s.SourceMoneyMultiplier==2,"either side latches");Check((enemy?s.Opponent:s.Player).HeldItem=="ITEM_AMULET_COIN","not consumed");
 Check(s.Rng==baseline.Rng&&s.EventSequence==baseline.EventSequence,"silent deterministic entry");Check(Turn(s).SourceMoneyMultiplier==2,"turn preserves latch");}
 Check(baseline.SourceMoneyMultiplier is null,"new battle source default one");
});
Test("singles reserve entry, departure, faint and real replacement retain one battle latch",()=>{
 var s=Start(Mon(),Mon(),Mon("ITEM_AMULET_COIN",pid:1),Mon(pid:2));Check(s.SourceMoneyMultiplier is null,"unentered reserve does not count");
 s=Good(WildBattleRules.SwitchSourceWildPlayer(c,s,1));turns++;Check(s.SourceMoneyMultiplier==2,"real reserve entry latches");
 s=Good(WildBattleRules.SwitchSourceWildPlayer(c,s,2));turns++;Check(s.SourceMoneyMultiplier==2&&s.Player.HeldItem=="ITEM_NONE","departure does not reset");
 var faint=Turn(Start(Hp(Mon("ITEM_AMULET_COIN",level:5),1),Mon(species:"SNORLAX",move:"TACKLE",level:60),Mon(pid:3)));
 Check(faint.Player.Pokemon.CurrentHp==0&&faint.AwaitingReplacement&&faint.SourceMoneyMultiplier==2,"faint keeps latch");
 faint=Good(WildBattleRules.ReplaceSourceWildPlayer(c,faint,1));Check(faint.SourceMoneyMultiplier==2&&faint.Player.HeldItem=="ITEM_NONE","faint replacement keeps latch");
 var reset=Start(Mon());Check(reset.SourceMoneyMultiplier is null,"battle reset only");
});
Test("four-position initial and voluntary entries share latch, including opposing right slot and repeated holders",()=>{
 foreach(int holder in Enumerable.Range(0,4)){var bodies=Enumerable.Range(0,4).Select(i=>Mon(i==holder?"ITEM_AMULET_COIN":"ITEM_NONE",pid:(uint)i)).ToArray();
 var f=Entry(Frame(bodies));Check(f.Field.MoneyMultiplier==2,"four slot entry "+holder);f=Full4(f);Check(f.Field.MoneyMultiplier==2,"four full turn "+holder);}
 var all=Entry(Frame([Mon("ITEM_AMULET_COIN"),Mon("ITEM_AMULET_COIN"),Mon("ITEM_AMULET_COIN"),Mon("ITEM_AMULET_COIN")]));Check(all.Field.MoneyMultiplier==2,"never stacks");
 var reserve=Entry(Frame([Mon(),Mon(),Mon(),Mon()],[Mon("ITEM_AMULET_COIN",pid:1),Mon(pid:2)]));Check(reserve.Field.MoneyMultiplier is null,"unentered double reserve");
 var choices=Choices(reserve).SetItem(2,new(reserve.Slots[2].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)));
 reserve=Full4(reserve,choices);Check(reserve.Field.MoneyMultiplier==2&&reserve.Slots[2].Body!.HeldItem=="ITEM_AMULET_COIN","actual right switch");
 choices=Choices(reserve).SetItem(2,new(reserve.Slots[2].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,3)));
 reserve=Full4(reserve,choices);Check(reserve.Field.MoneyMultiplier==2&&reserve.Slots[2].Body!.HeldItem=="ITEM_NONE","right departure retains");
});
Test("actual Pay Day terminal turn stores raw coins and settlement doubles each source money command once",()=>{
 var s=Turn(Start(Mon("ITEM_AMULET_COIN","MEOWTH","PAY_DAY",25),Hp(Mon(level:5),1)));
 Check(s.Outcome==WildBattleOutcome.Won&&s.PayDayMoney==125&&s.SourceMoneyMultiplier==2,"win raw125");
 var prize=SourceBattleMoneyRules.SettleTrainerPrize(1000,528,s.Outcome,s.SourceMoneyMultiplier);
 Check(prize.Amount==1056&&prize.Awarded==1056&&prize.Money==2056&&prize.DisplayAmount==1056,"double prize factor already included in528");
 var payday=SourceBattleMoneyRules.SettlePayDay(prize.Money,s.PayDayMoney,s.Outcome,s.SourceMoneyMultiplier);
 Check(payday.Amount==250&&payday.Money==2306&&s.PayDayMoney==125,"raw coins never multiplied in frame");
 var four=Entry(Frame([Mon("ITEM_AMULET_COIN","MEOWTH","PAY_DAY",25),Mon(species:"SNORLAX",move:"DEFENSE_CURL",level:80),Mon(),Mon()]));
 four=Full4(four);Check(four.Field.PayDayMoney==125&&four.Field.MoneyMultiplier==2,"real four PayDay raw125");
});
Test("adapter JSON continuation and omitted defaults preserve the canonical latch without historical admission",()=>{
 var s=Turn(Start(Mon("ITEM_AMULET_COIN")));var imported=SinglesBattleAdapter.Import(c,s);Check(imported.Succeeded,imported.Fault??"import");
 Check(imported.Frame!.Field.MoneyMultiplier==2,"frame authority");var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(imported.Frame))!;
 var exported=SinglesBattleAdapter.Export(copy);Check(exported.Succeeded&&Json(exported.State)==Json(s),"lossless source boundary");
 Check(Json(WildBattleRules.ResolveMove(c,s,0))==Json(WildBattleRules.ResolveMove(c,exported.State!,0)),"exact following turn/event/RNG");turns+=2;
 var ordinary=Start(Mon());Check(!Json(ordinary).Contains("MoneyMultiplier")&&!Json(SinglesBattleAdapter.Import(c,ordinary).Frame).Contains("MoneyMultiplier"),"default bytes omitted");
 foreach(int bad in new[]{-1,0,1,3}){Check(WildBattleRules.Validate(c,s with{SourceMoneyMultiplier=bad})=="source_money_multiplier","single invalid multiplier");Check(SourceBattleIdentityRules.Validate(copy with{Field=copy.Field with{MoneyMultiplier=bad}})=="source_money_multiplier","frame invalid multiplier");}
 Check(SourceBattleIdentityRules.Validate(copy with{SourceMechanicsRulesVersion=null})=="source_money_multiplier","historical frame rejects source state");
});
Test("source C reward vectors cover exact wallet cap, u16 display wrap, nonwin and multiplier arithmetic",()=>{
 using var doc=JsonDocument.Parse(File.ReadAllText(args[2]));Check(doc.RootElement.GetProperty("sourceCommit").GetString()==WildBattleRules.SourceCommit,"pinned source");
 foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray()){
 int wallet=row.GetProperty("wallet").GetInt32(),raw=row.GetProperty("raw").GetInt32(),mult=row.GetProperty("multiplier").GetInt32();
 var r=row.GetProperty("kind").GetString()=="payday"?SourceBattleMoneyRules.SettlePayDay(wallet,raw,WildBattleOutcome.Won,mult==1?null:mult):SourceBattleMoneyRules.SettleTrainerPrize(wallet,raw,WildBattleOutcome.Won,mult==1?null:mult);
 Check(r.Money==row.GetProperty("money").GetInt32()&&r.Amount==row.GetProperty("amount").GetInt32()&&r.DisplayAmount==row.GetProperty("display").GetInt32(),"original C vector");oracle++;}
 foreach(var outcome in new[]{WildBattleOutcome.Lost,WildBattleOutcome.Draw,WildBattleOutcome.Escaped,WildBattleOutcome.Caught}){Check(SourceBattleMoneyRules.SettleTrainerPrize(999990,528,outcome,2)==new SourceBattleMoneySettlement(999990,0,0,0),"nonwin prize");Check(SourceBattleMoneyRules.SettlePayDay(999990,65535,outcome,2)==new SourceBattleMoneySettlement(999990,0,0,0),"nonwin payday");}
 var cap=SourceBattleMoneyRules.SettlePayDay(999990,65535,WildBattleOutcome.Won,2);Check(cap==new SourceBattleMoneySettlement(999999,9,131070,65534),"u32 grant/u16 source text/capped actual");
 var zeroText=SourceBattleMoneyRules.SettlePayDay(0,32768,WildBattleOutcome.Won,2);Check(zeroText.Amount==65536&&zeroText.DisplayAmount==0,"nonzero grant must still show zero source buffer");
});
Check(groups>0&&turns>=12&&oracle>=20,"nonempty actual-turn and source oracle coverage");
Console.WriteLine(Json(new{passed=failures==0,groups,checks,failures,turns,oracle,runtime=Environment.Version.ToString()}));return failures==0?0:1;
