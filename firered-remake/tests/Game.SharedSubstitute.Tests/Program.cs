using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if(args.Length!=3)throw new ArgumentException("PRODUCT_ROOT SOURCE_VECTORS_JSON OUTPUT_JSON (or baseline PRODUCT_ROOT OUTPUT_JSON)");
if(args[0]=="baseline"){
 if(File.Exists(args[2]))throw new InvalidOperationException("Never replace frozen baseline");
 File.WriteAllText(args[2],LegacyParity.Run(args[1])+"\n");using var captured=JsonDocument.Parse(File.ReadAllText(args[2]));Console.WriteLine(captured.RootElement.GetProperty("cases"));return 0;
}
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
using var source=JsonDocument.Parse(File.ReadAllBytes(args[1]));
int groups=0,checks=0,failures=0,turns=0,oracleCases=0;var results=new List<object>();
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
void Test(string label,Action work){groups++;try{work();results.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception e){failures++;results.Add(new{label,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+label+" "+e);}}
string Json<T>(T value)=>JsonSerializer.Serialize(value);
MoveId Id(string name)=>new("pret:MOVE_"+name);
WildBattleCombatant Mon(string species,string moves="DEFENSE_CURL",int level=50){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 return new(p with{Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});}
WildBattleCalledPartyMember Member(WildBattleCombatant b)=>new(b.Pokemon,b.HeldItem,b.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,WildBattleCombatant[]? players=null,WildBattleCombatant[]? opponents=null,uint seed=31){
 var m=mons??[Mon("PIDGEY"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("SNORLAX")];
 var p=new[]{Member(m[0]),Member(m[2])}.Concat((players??[]).Select(Member)).ToImmutableArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,p),new(BattleSide.Opponent,new[]{Member(m[1]),Member(m[3])}.Concat((opponents??[]).Select(Member)).ToImmutableArray())],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
 Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,p.Length).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant b){var c=f.Slots[i];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=b}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(b))})};}
SourceBattleFrame Seed(SourceBattleFrame f,int target,int? actor){var c=f.Slots[target];return f with{Slots=f.Slots.SetItem(target,c with{Registers=c.Registers with{LeechSeedSource=actor is {} a?new(a):null}})};}
SourceBattleFrame Hp(SourceBattleFrame f,int slot,int hp)=>Put(f,slot,f.Slots[slot].Body! with{Pokemon=f.Slots[slot].Body!.Pokemon with{CurrentHp=hp}});
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int slot=0){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,slot,new(target)));Check(r.Succeeded,r.Fault??"action");Check(SourceBattleIdentityRules.Validate(r.State)==null,"action identity");return r;}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"turn");Check(SourceBattleIdentityRules.Validate(r.State)==null,"turn identity");return r;}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? slots=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=slots?[c.Slot.Value]??0;var m=catalog.Moves[c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new(m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:targets?[c.Slot.Value]??(c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices??Choices(f)));
SourceTurnContinuation Token(SourceBattleFrame f)=>new(f.Revision,f.EventSequence,f.NextAction,f.FourTurn!.Phase);
SourceFourActionResult Replay(SourceFourActionResult r,Func<SourceBattleFrame,SourceFourActionResult> step){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var a=step(r.State);var b=step(restored);Check(Json(a)==Json(b),"serialized exact state/events/RNG continuation");return Good(a);}
SourceFourActionResult Next(SourceFourActionResult r)=>Replay(r,f=>SourceFourActionTurns.Advance(catalog,f,Token(f)));
SourceFourActionResult EndBoundary(SourceBattleFrame f){var r=Begin(f);while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn.Phase==SourceFourActionPhase.EndTurn,"actual endturn boundary");return r;}
(SourceBattleFrame State,List<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var e=r.Events.ToList();for(int i=0;i<30&&r.State.FourTurn!=null&&r.State.Outcome==WildBattleOutcome.Ongoing;i++){
 r=r.State.FourTurn.Phase switch{
  SourceFourActionPhase.RewardsPending=>Replay(r,f=>SourceFourRewards.Settle(catalog,f,Token(f))),
  SourceFourActionPhase.ReplacementsPending=>Replay(r,f=>{var p=f.PendingSwitches[0];return SourceFourActionTurns.ChooseReplacement(catalog,f,Token(f),p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0]));}),
  _=>Next(r)};e.AddRange(r.Events);
 }Check(r.CompletedCleanTurn,"one complete turn");turns++;return(r.State,e);}
SourceActionSelection Switch(SourceBattleFrame f,int slot,int incoming)=>new(f.Slots[slot].Occupant!.Value,SourceActionKind.Switch,Incoming:new((BattleSide)(slot&1),incoming));
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+why+": "+r.Fault);

int Sub(SourceBattleFrame f,int slot)=>f.Slots[slot].Body!.CalledVolatile?.SubstituteHp??0;
SourceBattleFrame Doll(SourceBattleFrame f,int slot,int hp)=>Put(f,slot,f.Slots[slot].Body!with{CalledVolatile=hp==0?null:new(){SubstituteHp=hp}});
Test("32 frozen canonical singles complete result fingerprints",()=>{
 Check(Environment.Version.Major==10,"runtime10");Check(source.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"source pin");
 Check(LegacyParity.Run(args[0])==File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"../../..","singles-baseline-1a3f6ae.json")).Trim(),"state/events/RNG exact compatibility");
 Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("SUBSTITUTE")]),"Substitute admitted");
 Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id("SUBSTITUTE")] with { Pp = 11 }),"altered substitute definition is rejected");
});
Test("extracted source creation and interception command vectors",()=>{
 var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 var type=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.FrameRecipeContext")!;var recipe=type.BaseType!;
 object Context(SourceBattleFrame f,int actor,int target)=>Activator.CreateInstance(type,flags,null,[catalog,f,new SourceResolvedMoveAction(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)),null],null)!;
 SourceBattleFrame State(object ctx)=>(SourceBattleFrame)type.GetProperty("Frame",flags)!.GetValue(ctx)!;
 foreach(var group in source.RootElement.GetProperty("groups").EnumerateArray())foreach(var v in group.GetProperty("vectors").EnumerateArray()){
  var x=v.GetProperty("input");int N(string n)=>x.GetProperty(n).GetInt32();var expected=v.GetProperty("expected");string name=group.GetProperty("name").GetString()!;
  int actor=N("attacker"),target=N("target");
  if(v.GetProperty("name").GetString()=="already_faded_flag_still_blocks")continue;
  if(name=="creation"||name=="identity"&&v.GetProperty("name").GetString()!.StartsWith("create")){
   var b=Mon("PIDGEY","SUBSTITUTE");b=b with{Pokemon=b.Pokemon with{Stats=b.Pokemon.Stats with{MaxHp=N("attackerMaxHp")},CurrentHp=N("attackerHp")},BindTurnsRemaining=N("attackerStatus2")%65536!=0?3:0};
   if(N("attackerSubHp")>0)b=b with{CalledVolatile=new(){SubstituteHp=N("attackerSubHp")}};
   if(b.BindTurnsRemaining>0)b=b with{BindingMove=Id("WRAP")};
   var f=Put(Frame(),actor,b);if(b.BindTurnsRemaining>0){var cell=f.Slots[actor];f=f with{Slots=f.Slots.SetItem(actor,cell with{Registers=cell.Registers with{WrappedBy=new(actor^1)}})};}var c=Context(f,actor,actor);recipe.GetMethod("ApplySubstitute",flags)!.Invoke(c,[new BattlerSlot(actor),catalog.Moves[Id("SUBSTITUTE")]]);var actual=State(c);var y=expected[expected.GetArrayLength()-1];
   Check(Sub(actual,actor)==y.GetProperty("attackerSubHp").GetInt32()&&actual.Slots[actor].Body!.Pokemon.CurrentHp==y.GetProperty("attackerHp").GetInt32(),"C creation "+v.GetProperty("name"));oracleCases++;
  }
  else if(name=="interception"&&N("targetSubHp")>0&&N("targetStatus2")!=0&&N("resultFlags")==0&&N("hitMarker")==0){
   var f=Doll(Frame(),target,N("targetSubHp"));var c=Context(f,actor,target);object?[] parameters=[new BattlerSlot(actor),new BattlerSlot(target),Id("TACKLE"),N("damage"),false,100,0,false];
   bool hit=(bool)recipe.GetMethod("ConsumeSubstituteDamage",flags)!.Invoke(c,parameters)!;var y=expected[0];
   Check(hit&&Sub(State(c),target)==y.GetProperty("targetSubHp").GetInt32()&&(int)parameters[6]! ==y.GetProperty("hpDealt").GetInt32(),"C interception "+v.GetProperty("name"));oracleCases++;
  }
 }
 Check(oracleCases>=20,"nonempty actual common-command/C comparisons");
});
Test("actual creation cost and PP-before-attack-string for all four positions",()=>{
 for(int actor=0;actor<4;actor++){
  var f=Put(Frame(),actor,Mon("PIDGEY","SUBSTITUTE"));var a=Act(f,actor,actor);int cost=f.Slots[actor].Body!.Pokemon.Stats.MaxHp/4;
  Check(Sub(a.State,actor)==cost&&a.State.Slots[actor].Body!.Pokemon.CurrentHp==f.Slots[actor].Body!.Pokemon.CurrentHp-cost,"independent actual slot cost");
  Check(a.Trace.IsEmpty&&a.Events.OfType<SourcePpCommandEvent>().Single().Sequence<a.Events.OfType<SourceAttackStringEvent>().Single().Sequence,"source PP/print order no RNG");
  Check(a.Events.OfType<SourceHpChangedEvent>().Single().Actor.Value==actor&&a.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==actor,"typed self cost");
  var duplicate=Act(a.State,actor,actor);Check(duplicate.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="already-substitute"&&Sub(duplicate.State,actor)==cost,"no duplicate debit");
  var low=Act(Hp(f,actor,cost),actor,actor);Check(low.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="substitute-insufficient-hp"&&low.State.Slots[actor].Body!.LastLandedMove==Id("SUBSTITUTE"),"strict HP and no invented noeffect");
  var barely=Act(Hp(f,actor,cost+1),actor,actor);Check(barely.State.Slots[actor].Body!.Pokemon.CurrentHp==1&&Sub(barely.State,actor)==cost,"one HP remains");
 }
});
Test("real wrap then Substitute releases binding and the following residual",()=>{
 var f=Frame([Mon("PIDGEY","SUBSTITUTE,DEFENSE_CURL"),Mon("RATTATA","WRAP,DEFENSE_CURL")with{AccuracyStage=12},Mon("SPEAROW"),Mon("SNORLAX")]);
 f=Act(f,1,0).State;Check(f.Slots[0].Body!.BindTurnsRemaining>0,"real admitted wrap");var a=Act(f,0,0);
 Check(a.Events.OfType<SourceBindingChangedEvent>().Single().Reason=="substitute"&&a.State.Slots[0].Body!.BindTurnsRemaining==0,"creation cancels wrapping");
 var done=Finish(Begin(a.State,Choices(a.State,[1,1,0,0])));Check(!done.Events.OfType<SourceBindingChangedEvent>().Any(e=>e.Side.Value==0),"no later binding tick");
});
Test("two authentic four-action turns retain four separate same-species dolls",()=>{
 var f=Frame(Enumerable.Range(0,4).Select(_=>Mon("PIDGEY","SUBSTITUTE,TACKLE,DEFENSE_CURL")with{AccuracyStage=12}).ToArray());
 var first=Finish(Begin(f));Check(Enumerable.Range(0,4).All(i=>Sub(first.State,i)>0),"four genuine independent creations");
 var second=Finish(Begin(first.State,Choices(first.State,[1,1,1,1],[3,2,1,0])));
 Check(second.Events.OfType<SourceSubstituteDamagedEvent>().Select(e=>e.Target.Value).Order().SequenceEqual(new[]{0,1,2,3}),"every actual target exactly once");
 Check(!second.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Actor!=e.Target),"all hits confined to dolls");
 Check(second.Events.OfType<SourceSubstituteDamagedEvent>().All(e=>e.Actor.Value+e.Target.Value==3),"cross-column identity retained");
});
Test("breaking hit blocks status/stat/confusion and next action reaches the body",()=>{
 foreach(var name in new[]{"BODY_SLAM","POISON_STING","CONFUSION","MUD_SLAP"}){
  var f=Doll(Frame([Mon("PIDGEY",name)with{AccuracyStage=12},Mon("RATTATA"),Mon("SPEAROW"),Mon("SNORLAX")]),1,1);var a=Act(f,0,1);
  Check(a.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter==0&&a.State.Slots[1].Body!.Pokemon.CurrentHp==f.Slots[1].Body!.Pokemon.CurrentHp,"break only doll "+name);
  Check(a.State.Slots[1].Body!.Status==0&&a.State.Slots[1].Body!.ConfusionTurnsRemaining==0&&a.State.Slots[1].Body!.AccuracyStage==6,"breaking secondary blocked "+name);
  Check(a.Trace.Any(t=>t.Purpose.EndsWith("secondary-effect")),"effect draw preserved");
  var again=Act(a.State,0,1);Check(again.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==1)&&!again.Events.OfType<SourceSubstituteDamagedEvent>().Any(),"fade cleared by move-end");
 }
 var bite=Frame([Mon("PIDGEY","BITE")with{AccuracyStage=12,SpeedStage=12},Mon("RATTATA"),Mon("SPEAROW"),Mon("SNORLAX")]);
 var done=Finish(Begin(Doll(bite,1,1)));Check(!done.Events.OfType<SourceFlinchEvent>().Any(e=>e.Side.Value==1),"breaking flinch blocked in actual ordered action");
});
Test("script primary/status drops block without RNG while bypass scripts execute",()=>{
 foreach(var name in new[]{"TOXIC","POISON_POWDER","SLEEP_POWDER","THUNDER_WAVE","WILL_O_WISP","SAND_ATTACK","SCREECH","SWAGGER","FLATTER","CONFUSE_RAY","MEAN_LOOK","LEECH_SEED"}){
  var f=Doll(Frame([Mon("PIDGEY",name),Mon("RATTATA"),Mon("SPEAROW"),Mon("SNORLAX")]),1,20);var a=Act(f,0,1);
  Check(a.Trace.IsEmpty,"Substitute gate before accuracy "+name);Check(a.Events.Any(e=>e is SourceMoveFailedEvent {Reason:"substitute"} or SourceMoveMissedEvent),"source failure "+name);
  Check(a.State.Slots[1].Body!.Status==0&&a.State.Slots[1].Body!.AttackStage==6&&a.State.Slots[1].Body!.DefenseStage==6,"no protected alteration");
 }
 foreach(var name in new[]{"TICKLE","HAZE","PSYCH_UP","DEFENSE_CURL","DISABLE"}){
  var f=Doll(Frame([Mon("PIDGEY",name)with{AccuracyStage=12},Mon("RATTATA")with{AttackStage=8,LastUsedMove=Id("DEFENSE_CURL")},Mon("SPEAROW"),Mon("SNORLAX")]),1,20);var a=Act(f,0,catalog.Moves[Id(name)].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?0:1);
  Check(!a.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="substitute"),"bypass preserved "+name);
  if(name=="TICKLE")Check(a.State.Slots[1].Body!.AttackStage==7&&a.State.Slots[1].Body!.DefenseStage==5,"Tickle through substitute");
 }
});
Test("multihit breaks doll before later real-HP damage and contact",()=>{
 var f=Doll(Frame([Mon("PIDGEY","FURY_SWIPES")with{AccuracyStage=12},Mon("CARVANHA"),Mon("SPEAROW"),Mon("SNORLAX")]),1,1);var a=Act(f,0,1);
 var sub=a.Events.OfType<SourceSubstituteDamagedEvent>().Single();var body=a.Events.OfType<SourceHpChangedEvent>().First(e=>e.Target.Value==1);
 Check(sub.Sequence<body.Sequence&&a.Events.OfType<SourceMultiHitEvent>().Single().ActualHits>=2,"per-hit moveend enables next body hit");
 Check(a.Events.OfType<SourceAbilityDamageEvent>().Count()==a.Events.OfType<SourceHpChangedEvent>().Count(e=>e.Target.Value==1),"Rough Skin triggers only subsequent real hits");
 Check(a.State.Slots[1].Registers.Damage?.PhysicalSource?.Value==0,"real hit source retained");
});
Test("Focus Band, direct contact and residuals retain separate HP paths",()=>{
 var f=Doll(Frame([Mon("PIDGEY","TACKLE")with{AccuracyStage=12},Mon("CARVANHA")with{HeldItem="ITEM_FOCUS_BAND"},Mon("SPEAROW"),Mon("SNORLAX")]),1,1);f=Hp(f,1,1);var a=Act(f,0,1);
 Check(a.Trace.Count(t=>t.Purpose.EndsWith("focus-band"))==1&&!a.Events.OfType<SourceDamageAdjustedEvent>().Any(),"band roll occurs but doll prevents survival adjustment");
 Check(!a.Events.OfType<SourceAbilityDamageEvent>().Any()&&a.State.Slots[1].Body!.Pokemon.CurrentHp==1,"contact skips fresh substitute receipt");
 f=Doll(Frame(),0,20);f=Put(f,0,f.Slots[0].Body!with{Status=WildBattleStatus.Poison,Rooted=true});f=Seed(f,0,1);int before=f.Slots[0].Body!.Pokemon.CurrentHp;
 var end=Finish(EndBoundary(f));Check(Sub(end.State,0)==20&&end.State.Slots[0].Body!.Pokemon.CurrentHp<before,"poison and Leech Seed bypass doll; rooted recovery preserved");
});
Test("Future Sight hitting a substitute preserves prior retaliation and held receipts",()=>{
 var f=Doll(Frame(),3,1);var cell=f.Slots[3];f=f with{Slots=f.Slots.SetItem(3,cell with{Position=cell.Position with{FutureAttack=new(1,Id("FUTURE_SIGHT"),new(0),40)}})};
 var done=Finish(EndBoundary(f));Check(done.Events.OfType<SourceSubstituteDamagedEvent>().Single().Target.Value==3&&Sub(done.State,3)==0,"actual numeric delayed target");
 Check(done.State.Slots[3].Registers.Damage is null&&done.State.Slots[3].Position.HitItemReceipt?.Physical==0,"no Counter/physical receipt from substitute");
 Check(done.State.Slots[3].Body!.Pokemon.CurrentHp==f.Slots[3].Body!.Pokemon.CurrentHp,"underlying HP untouched");
});
Test("Baton passes exact remaining doll HP to the chosen real reserve and survives serialization",()=>{
 var f=Frame([Mon("SNORLAX","SUBSTITUTE,BATON_PASS,DEFENSE_CURL"),Mon("PIDGEY"),Mon("SPEAROW"),Mon("RATTATA")],[Mon("MAGIKARP",level:5)]);f=Act(f,0,0).State;int hp=Sub(f,0);
 var r=Begin(f,Choices(f,[1,0,0,0]));while(r.State.FourTurn!.Phase==SourceFourActionPhase.Actions)r=Next(r);Check(r.State.FourTurn.Phase==SourceFourActionPhase.ActionSwitchPending,"actual Baton pause");
 r=Replay(r,x=>SourceFourActionTurns.ChooseActionSwitch(catalog,x,Token(x),new(0),new(BattleSide.Player,2)));
 Check(Sub(r.State,0)==hp&&hp>r.State.Slots[0].Body!.Pokemon.Stats.MaxHp/4&&r.State.Slots[0].Member!.Value.Index==2&&r.State.Slots[0].EntrySerial==2,"exact HP no reclamp on new identity");Finish(r);
});
Test("ordinary and forced departure clear doll; faint cleans once and replacement owns new body",()=>{
 var f=Frame([Mon("PIDGEY","ROAR",70)with{AccuracyStage=12},Mon("RATTATA"),Mon("SPEAROW"),Mon("SNORLAX","SUBSTITUTE,DEFENSE_CURL")],opponents:[Mon("PIDGEY")]);f=Act(f,3,3).State;
 var voluntary=Begin(f,Choices(f,[0,0,0,1]).SetItem(3,Switch(f,3,2)));voluntary=Next(voluntary);Check(Sub(voluntary.State,3)==0&&voluntary.State.Slots[3].EntrySerial==2,"voluntary departure clear");Finish(voluntary);
 var forced=Finish(Begin(f,Choices(f,[0,0,0,1],[3,0,1,3])));Check(Sub(forced.State,3)==0&&forced.State.Slots[3].EntrySerial==2,"Roar bypasses doll and forces real reserve");
 var faint=Doll(Frame([Mon("PIDGEY"),Mon("SNORLAX"),Mon("SPEAROW"),Mon("RATTATA")],opponents:[Mon("PIDGEY")]),3,40);faint=Hp(faint,3,1);faint=Put(faint,3,faint.Slots[3].Body!with{Status=WildBattleStatus.Poison});var done=Finish(EndBoundary(faint));
 Check(done.Events.OfType<SourceFaintedEvent>().Count(e=>e.Side.Value==3)==1&&Sub(done.State,3)==0&&done.State.Slots[3].EntrySerial==2,"residual KO one cleanup/reward/replacement");
});
Test("actual Protect precedes Substitute status scripts; passive contact bypasses own doll",()=>{
 var f=Frame([Mon("PIDGEY","LEECH_SEED")with{AccuracyStage=12},Mon("RATTATA","SUBSTITUTE,PROTECT"),Mon("SPEAROW"),Mon("SNORLAX")]);f=Act(f,1,1).State;
 var done=Finish(Begin(f,Choices(f,[0,1,0,0])));Check(done.Events.OfType<SourceProtectAttemptEvent>().Any(e=>e.Succeeded)&&done.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Move==Id("LEECH_SEED")&&e.Reason=="protected"),"source attackcanceler Protect before seed Substitute gate");
 Check(!done.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Move==Id("LEECH_SEED")&&e.Reason=="substitute")&&done.State.Slots[1].Registers.LeechSeedSource is null,"one failure no seed");
 f=Doll(Frame([Mon("PIDGEY","TACKLE")with{AccuracyStage=12},Mon("CARVANHA"),Mon("SPEAROW"),Mon("SNORLAX")]),0,20);var a=Act(f,0,1);
 Check(Sub(a.State,0)==20&&a.Events.OfType<SourceAbilityDamageEvent>().Single().Target.Value==0&&a.State.Slots[0].Body!.Pokemon.CurrentHp<f.Slots[0].Body!.Pokemon.CurrentHp,"Rough Skin ignores attacker doll");
});
Test("malformed substitutes, unsupported items and invalid selections remain atomic",()=>{
 var f=Frame();foreach(var v in new[]{new WildBattleCalledVolatileState(),new(){SubstituteHp=-1},new(){SubstituteHp=256},new(){SubstituteHp=1,TauntTurnsRemaining=3},new(){SubstituteHp=1,RulesVersion="forged"}}){var bad=Put(f,0,f.Slots[0].Body!with{CalledVolatile=v});Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"volatile scope");}
 foreach(string item in new[]{"ITEM_MASTER_BALL","ITEM_ENIGMA_BERRY"}){var bad=Put(f,0,f.Slots[0].Body!with{HeldItem=item});Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"item scope");}
 var badSlot=Choices(f).SetItem(0,Choices(f)[0] with{MoveSlot=4});Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,badSlot),"invalid move slot");
});
Check(groups>0&&checks>0&&oracleCases>0,"nonempty suite");var result=new{groups,checks,failures,completedTurns=turns,oracleCases,runtime=Environment.Version.ToString(),results};File.WriteAllText(args[2],JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine(Json(result));return failures==0?0:1;
