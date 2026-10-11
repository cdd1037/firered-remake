using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

if(args.Length!=3)return 2;
string root=args[0],source=args[1],output=args[2];
string Json<T>(T o)=>JsonSerializer.Serialize(o);
string Hash<T>(T o)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(o))));
MoveId Id(string name)=>new("pret:MOVE_"+name);
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;
string tm=File.ReadAllText(Path.Combine(source,"src/data/pokemon/tmhm_learnsets.h"));
string tutor=File.ReadAllText(Path.Combine(source,"src/data/pokemon/tutor_learnsets.h"));
int groups=0,checks=0,failures=0,vectors=0,turns=0;var results=new List<object>();
void Check(bool ok,string reason){checks++;if(!ok)throw new InvalidDataException(reason);}
void Test(string name,Action run){groups++;try{run();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+" "+e);}}
WildBattleCombatant Mon(string species,string[] moves,uint pid=200,int level=50){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 var block=Regex.Match(tm,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
 var tb=Regex.Match(tutor,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
 foreach(string move in moves)Check(catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(move)&&x.Level<=level)||Regex.IsMatch(block,@"[TH]M\d\d_"+move+@"\)")||tb.Contains("TUTOR(MOVE_"+move+")"),"authentic source move "+species+"/"+move);
 return new(p with{Moves=moves.Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});
}
WildBattleState Pair(WildBattleCombatant actor,WildBattleCombatant holder,uint seed=31)=>new(actor,holder,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
WildBattleResult Single(WildBattleState s,int slot=0){var r=WildBattleRules.ResolveSourceMoveAction(catalog,s,new(WildBattleSide.Player,slot)).Battle;Check(r.Succeeded,"single accepted "+r.Fault);return r;}
var baseline=new List<object>();
foreach(var fixture in new[]{("NIDORAN_F",0u),("CARVANHA",0u),("MAGMAR",0u),("PARAS",0u),("PIKACHU",0u),("JIGGLYPUFF",0u)})foreach(uint seed in new uint[]{0,1,31,38}){
 var s=Pair(Mon("SQUIRTLE",["TACKLE"],level:30),Mon(fixture.Item1,[fixture.Item1 switch{"CARVANHA" or "MAGMAR"=>"LEER","PARAS"=>"GROWTH","JIGGLYPUFF"=>"DEFENSE_CURL",_=>"GROWL"}],fixture.Item2,100),seed);
 var r=Single(s);baseline.Add(new{species=fixture.Item1,seed,hash=Hash(r)});
}
if(output=="--baseline"){Console.WriteLine(Json(baseline));return 0;}
if(output=="--probe"){
 foreach(var species in new[]{"NIDORAN_F","MAGMAR","PARAS","PIKACHU"}){
  string hm=species switch{"MAGMAR"=>"LEER","PARAS"=>"GROWTH",_=>"GROWL"};var s=Pair(Mon("CROBAT",["BITE"],level:100),Mon(species,[hm],0,100));
  s=s with{Opponent=s.Opponent with{CalledVolatile=new(){SubstituteHp=1}},SourceOpponentHitReceipt=new(65535,7)};
  var r=Single(s);Console.WriteLine(Json(new{species,trace=r.Trace,events=r.Events,state=r.State}));
 }
 return 0;
}
Test("immutable canonical ordinary singles state event RNG hashes",()=>Check(Json(baseline)==File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-2c3e33e6.json")).Trim(),"24 baseline hashes"));
WildBattleCalledPartyMember Member(WildBattleCombatant b)=>new(b.Pokemon,b.HeldItem,b.Status);
SourceBattleFrame Frame(uint seed=0){WildBattleCombatant[] b=[Mon("RATICATE",["TACKLE"]),Mon("CLEFAIRY",["CALM_MIND"],0),Mon("RATICATE",["TACKLE"],202),Mon("JIGGLYPUFF",["LIGHT_SCREEN"],0)];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(b[0]),Member(b[2])]),new(BattleSide.Opponent,[Member(b[1]),Member(b[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,b[i])).ToImmutableArray()};}
SourceBattleFrame Rebind(SourceBattleFrame f,int i,WildBattleCombatant b){var c=f.Slots[i];var key=c.Member!.Value;var p=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=b}),Parties=f.Parties.SetItem((int)key.Side,p with{Members=p.Members.SetItem(key.Index,Member(b))})};}
SourceBattleFrame Hp(SourceBattleFrame f,int i,int hp)=>Rebind(f,i,f.Slots[i].Body! with{Pokemon=f.Slots[i].Body!.Pokemon with{CurrentHp=hp}});
SourceBattleFrame Receipt(SourceBattleFrame f,int i,SourceHitDamageReceipt r)=>f with{Slots=f.Slots.SetItem(i,f.Slots[i] with{Position=f.Slots[i].Position with{HitItemReceipt=r==new SourceHitDamageReceipt()?null:r}})};
SourceKernelResult Act(SourceBattleFrame f,int a,int t){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[a].Occupant!.Value,f.Revision,0,new(t)));Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid resulting state");return r;}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four turn accepted");Check(SourceBattleIdentityRules.Validate(r.State)is null,"four identity");return r;}
SourceFourActionResult Next(SourceFourActionResult r)=>Good(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
SourceFourActionResult Begin(SourceBattleFrame f)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot:new(c.Slot.Value switch{0=>3,2=>1,1=>2,_=>0}))).ToImmutableArray()));
SourceBattleFrame Rewards(SourceBattleFrame f)=>f with{Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=f.Party(BattleSide.Player).Members.Select((_,i)=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
void PartySync(SourceBattleFrame f){foreach(var c in f.Slots){var k=c.Member!.Value;var m=f.Party(k.Side).Members[k.Index];Check(m.Pokemon.CurrentHp==c.Body!.Pokemon.CurrentHp&&m.Status==SourcePartyStatusRules.FromBattle(c.Body.Status),"actual party member HP/status "+c.Slot);}}
var asm=typeof(SourceBattleKernel).Assembly;var ct=asm.GetType("Game.Domain.Battle.FrameRecipeContext")!;var shared=ct.BaseType!;const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
object Context(SourceBattleFrame f,int a,int t)=>Activator.CreateInstance(ct,flags,null,[catalog,f,new SourceResolvedMoveAction(f.Slots[a].Occupant!.Value,f.Revision,0,new(t)),null],null)!;
SourceBattleFrame ContextState(object context)=>(SourceBattleFrame)ct.GetProperty("Frame",flags)!.GetValue(context)!;
IEnumerable<SourceBattleEvent> ContextEvents(object context)=>(IEnumerable<SourceBattleEvent>)ct.GetField("Events",flags)!.GetValue(context)!;
IEnumerable<RandomTrace> ContextTrace(object context)=>(IEnumerable<RandomTrace>)ct.GetField("Trace",flags)!.GetValue(context)!;
var symbols=new[]{"abilities","pokemon"}.SelectMany(h=>Regex.Matches(File.ReadAllText(Path.Combine(source,"include/constants/"+h+".h")),@"(?m)^#define (ABILITY_\w+|TYPE_\w+)\s+(\d+)\s*(?://[^\n]*)?$" ).Select(m=>(Name:m.Groups[1].Value,Value:int.Parse(m.Groups[2].Value)))).ToArray();
string Symbol(string prefix,int value)=>symbols.First(s=>s.Name.StartsWith(prefix)&&s.Value==value).Name;
int sourceOnly=0;
Test("exact original C contact source guard, status, slot and RNG vectors",()=>{
 foreach(var line in File.ReadLines(Path.Combine(output,"source-vectors.jsonl"))){var row=JsonDocument.Parse(line).RootElement;var input=row.GetProperty("input");var expected=row.GetProperty("expected");string label=row.GetProperty("name").GetString()!;int I(string name)=>input.GetProperty(name).GetInt32();
  // Uproar and the confusion-self-hit callback are not representable four-slot phases.
  // Their unchanged original C vectors remain independently run, not counted as C# matches.
  if(I("uproarMask")!=0||I("confusionSelfDamage")!=0){sourceOnly++;continue;}
  int a=I("attacker"),t=I("target");var f=Frame((uint)I("seed"));
  for(int i=0;i<4;i++){var b=f.Slots[i].Body!;f=Rebind(f,i,b with{Pokemon=b.Pokemon with{CurrentHp=70+i,Stats=b.Pokemon.Stats with{MaxHp=100+i}},CalledOverlay=new(){Ability="ABILITY_NONE",Type1="TYPE_NORMAL",Type2="TYPE_NORMAL"}});}
  var actor=f.Slots[a].Body!;f=Rebind(f,a,actor with{Pokemon=actor.Pokemon with{CurrentHp=I("attackerHp"),Stats=actor.Pokemon.Stats with{MaxHp=I("attackerMaxHp")}},Status=(uint)I("attackerStatus"),CalledVolatile=(I("attackerStatus2")&0x1000000)!=0?new(){SubstituteHp=7}:null,CalledOverlay=new(){Ability=Symbol("ABILITY_",I("attackerAbility")),Type1=Symbol("TYPE_",I("type1")),Type2=Symbol("TYPE_",I("type2"))}});
  var holder=f.Slots[t].Body!;f=Rebind(f,t,holder with{Pokemon=holder.Pokemon with{CurrentHp=I("targetHp")},CalledOverlay=new(){Ability=Symbol("ABILITY_",I("defenderAbility")),Type1="TYPE_NORMAL",Type2="TYPE_NORMAL"}});
  f=Receipt(f,t,new(0,I("physicalDmg"),I("specialDmg")));if(I("safeguard")!=0)f=f with{Sides=f.Sides.SetItem(a&1,f.Sides[a&1] with{SafeguardTurnsRemaining=5})};
  if((I("attackerStatus2")&0x1000)!=0)f=f with{Slots=f.Slots.SetItem(a,f.Slots[a] with{Registers=f.Slots[a].Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Rollout,Id("ROLLOUT"),0,Id("TACKLE"),new(t),3,0,null,false),0,null)}})};
  var ctx=Context(f,a,t);ct.GetField("commandNoEffect",flags)!.SetValue(ctx,(I("resultFlags")&41)!=0);
  var move=catalog.Moves[Id(I("moveFlags")==0?"GROWL":"TACKLE")];for(int n=0;n<I("repeatCount");n++)shared.GetMethod("ContactAbility",flags)!.Invoke(ctx,[catalog,new BattlerSlot(a),new BattlerSlot(t),move,"contact-oracle",I("initialDamage")]);
  var actual=ContextState(ctx);var trace=ContextTrace(ctx).ToArray();Check(actual.Rng.Seed==expected.GetProperty("rng").GetUInt32()&&actual.Rng.Cursor==(ulong)expected.GetProperty("drawCount").GetInt32(),label+" RNG state/cursor");Check(trace.Select(x=>(int)x.Raw).SequenceEqual(expected.GetProperty("consumedDraws").EnumerateArray().Select(x=>x.GetInt32())),label+" ordered raw words");
  for(int i=0;i<4;i++){var e=expected.GetProperty("slots")[i];Check(actual.Slots[i].Body!.Pokemon.CurrentHp==e.GetProperty("hp").GetInt32()&&actual.Slots[i].Body!.Status==e.GetProperty("status").GetUInt32(),label+" exact slot "+i);}
  Check(ContextEvents(ctx).OfType<SourceStatusChangedEvent>().Count()==expected.GetProperty("statusWrites").GetInt32(),label+" status writes");
  if(I("defenderAbility")==24){Check(ContextEvents(ctx).OfType<SourceAbilityDamageEvent>().Count()==expected.GetProperty("effect").GetInt32(),label+" passive damage count");Check(actual.Slots[a].Position.HitItemReceipt is null&&actual.Slots[t].Position.HitItemReceipt==f.Slots[t].Position.HitItemReceipt,label+" no passive SpecialStatus receipt");}
  if((I("attackerStatus2")&0x1000)!=0)Check(actual.Slots[a].Registers.Turn?.Lock is null&&expected.GetProperty("cancelCalls").GetInt32()>0,label+" shared cancellation clears the supported Rollout lock; source Bide/Thrash bits remain original-only");
  PartySync(actual);vectors++;
 }
 Check(vectors==165&&sourceOnly==6,"171 original vectors; 165 applicable frame vectors and six source-only closed phases");
});
Test("established singles retained receipt plus Substitute boundary remains explicit",()=>{
 foreach(string species in new[]{"NIDORAN_F","MAGMAR","PARAS","PIKACHU"}){
  string move=species switch{"MAGMAR"=>"LEER","PARAS"=>"GROWTH",_=>"GROWL"};var s=Pair(Mon("CROBAT",["BITE"],level:100),Mon(species,[move],0,100));s=s with{Opponent=s.Opponent with{CalledVolatile=new(){SubstituteHp=1}},SourceOpponentHitReceipt=new(65535,7)};var r=Single(s);
  Check(r.Events.OfType<WildBattleSubstituteDamagedEvent>().Count()==1&&!r.Trace.Any(x=>x.Purpose.Contains("poison-point")||x.Purpose.Contains("flame-body")||x.Purpose.Contains("effect-spore")||x.Purpose.Contains(".static")),"injected combination keeps baseline singles contract "+species);
 }
});
Test("contact source absence and holder KO guards use actual holder identity",()=>{
 foreach(var spec in new[]{("CARVANHA","BITE"),("MAGMAR","EMBER"),("NIDORAN_F","SCRATCH"),("PARAS","SCRATCH")}){
  var f=Rebind(Frame(0),3,Mon(spec.Item1,[spec.Item2],0));f=Receipt(Hp(f,3,0),3,new(7,7));f=f with{SourceAbsentMask=8};var ctx=Context(f,2,3);shared.GetMethod("ContactAbility",flags)!.Invoke(ctx,[catalog,new BattlerSlot(2),new BattlerSlot(3),catalog.Moves[Id("TACKLE")],"absent-holder",0]);var after=ContextState(ctx);
  Check(spec.Item1=="CARVANHA"?ContextEvents(ctx).OfType<SourceAbilityDamageEvent>().Single().Holder==new BattlerSlot(3):ContextTrace(ctx).Any(),"zero HP and absent holder do not suppress source branch "+spec.Item1);Check(after.Slots[0].Body==f.Slots[0].Body&&after.Slots[1].Body==f.Slots[1].Body,"other positions untouched");
 }
});
Test("real four-action turns use both partners and synchronize exact party statuses",()=>{
 var roughHolders=new HashSet<int>();
 foreach(var spec in new[]{("NIDORAN_F","SCRATCH","poison-point"),("CARVANHA","BITE","rough-skin"),("MAGMAR","EMBER","flame-body"),("PARAS","SCRATCH","effect-spore")})foreach(int side in new[]{0,1})foreach(uint seed in new uint[]{0,31,61}){
  var f=Frame(seed);for(int i=0;i<4;i++)f=Rebind(f,i,(i&1)==side?Mon(spec.Item1,[spec.Item2],(uint)(i*2),100):Mon("RATICATE",["TACKLE"],(uint)(200+i*2),100));var r=Begin(f);var events=r.Events;int boundaries=0;
  while(r.State.FourTurn is not null){Check(++boundaries<=6,"bounded four actions and residual");r=Next(r);events=events.AddRange(r.Events);PartySync(r.State);}
  Check(r.CompletedCleanTurn&&r.State.Turn==f.Turn+1,"actual clean Ready");Check(events.OfType<SourceMoveUsedEvent>().Select(e=>e.Actor.Value).Distinct().Count()==4,"all four genuine actors act");
  var contact=events.OfType<SourceAbilityDamageEvent>().ToArray();if(spec.Item1=="CARVANHA"){int landed=events.OfType<SourceHpChangedEvent>().Count(e=>e.Target.Side==(BattleSide)side&&e.Actor.Side!=(BattleSide)side&&e.HpAfter<e.HpBefore);Check(contact.Length==landed&&contact.All(e=>e.Holder.Side==(BattleSide)side&&e.Target.Side!=(BattleSide)side),"one actual Rough Skin holder callback for every landed cross-partner hit");foreach(var e in contact)roughHolders.Add(e.Holder.Value);}
  Check(r.State.Slots.All(c=>c.Position.HitItemReceipt is null),"ActionFinished clears receipts");var json=Json(r.State);Check(Json(JsonSerializer.Deserialize<SourceBattleFrame>(json))==json,"exact transient frame round trip");turns++;
 }
 Check(roughHolders.SetEquals(new[]{0,1,2,3}),"all four actual holder positions reached across bounded turns");
});
Test("per-hit Rough Skin and Effect Spore stop source multihit without a final duplicate",()=>{
 foreach(string species in new[]{"CARVANHA","MAGMAR","NIDORAN_F","PARAS"}){
  var f=Rebind(Rebind(Frame(31),2,Mon("SPEAROW",["FURY_ATTACK"],200,30)),3,Mon(species,[species switch{"CARVANHA"=>"BITE","MAGMAR"=>"EMBER","PARAS"=>"SCRATCH",_=>"SCRATCH"}],0,100));
  var r=Act(f,2,3);int hits=r.Events.OfType<SourceHpChangedEvent>().Count(e=>e.Actor==new BattlerSlot(2)&&e.Target==new BattlerSlot(3));Check(hits>=2,"real source multihit "+species);if(species=="CARVANHA")Check(r.Events.OfType<SourceAbilityDamageEvent>().Count()==hits,"exactly one passive callback per hit");else Check(r.Trace.Count(x=>x.Purpose.EndsWith(species=="MAGMAR"?"flame-body":species=="PARAS"?"effect-spore":"poison-point"))==hits,"one chance draw per hit, no final repeat");PartySync(r.State);
 }
 var ko=Hp(Rebind(Rebind(Frame(0),2,Mon("SPEAROW",["FURY_ATTACK"],200,30)),3,Mon("CARVANHA",["BITE"],0,100)),2,1);var killed=Act(ko,2,3);Check(killed.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==1&&killed.Events.OfType<SourceFaintedEvent>().Single().Side==new BattlerSlot(2),"passive actor KO stops remaining hits");
 var sleep=Rebind(Rebind(Frame(0),2,Mon("SPEAROW",["FURY_ATTACK"],200,30)),3,Mon("PARAS",["SCRATCH"],0,100));var slept=Act(sleep,2,3);Check(slept.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Side==new BattlerSlot(2)&&WildBattleStatus.IsSleeping(e.After)),"source seed causes Effect Spore sleep");var multi=slept.Events.OfType<SourceMultiHitEvent>().Single();Check(multi.ActualHits<multi.PlannedHits,"new sleep stops loop early");PartySync(slept.State);
});
Test("Rough Skin double KO preserves reward pause and defers both actual replacements",()=>{
 var f=Frame(31);f=Rebind(f,0,Mon("CLEFAIRY",["CALM_MIND"],0));f=Rebind(f,2,Mon("RATICATE",["TACKLE"],200,100));f=Rebind(f,1,Mon("CARVANHA",["BITE"],0,1));f=Hp(Hp(f,2,1),1,1);
 var p=f.Party(BattleSide.Player);var o=f.Party(BattleSide.Opponent);f=f with{Parties=[p with{Members=p.Members.Add(Member(Mon("RATICATE",["TACKLE"],200,100)))},o with{Members=o.Members.Add(Member(Mon("CARVANHA",["BITE"],0,1)))}]};f=Rewards(f);var oldP=f.Slots[2].Occupant;var oldO=f.Slots[1].Occupant;
 var r=Begin(f);var all=r.Events;int pauses=0,replacements=0,boundaries=0;while(r.State.FourTurn is not null){Check(++boundaries<=14,"bounded KO continuation");if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){var pending=r.State.PendingSwitches[0];Check(r.State.NextAction==4,"replacements only after original four actions");r=Good(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,pending.Slot,new(pending.Slot.Side,2)));replacements++;}else{bool rewards=r.State.FourTurn.Phase==SourceFourActionPhase.RewardsPending;int next=r.State.NextAction;r=Next(r);if(rewards){pauses++;Check(r.State.NextAction==next&&r.Trace.IsEmpty&&!r.Events.OfType<SourceMoveUsedEvent>().Any(),"rewards never consume an extra action/draw");}}all=all.AddRange(r.Events);PartySync(r.State);}
 Check(pauses==1&&replacements==2&&r.CompletedCleanTurn,"one reward and two completed replacement continuations");Check(r.State.Slots[2].Occupant!=oldP&&r.State.Slots[1].Occupant!=oldO,"same-species replacements have fresh actual occupants");Check(all.OfType<SourceFaintedEvent>().Select(x=>x.Side.Value).SequenceEqual(new[]{1,2}),"holder then passive attacker faint order");Check(!all.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor==new BattlerSlot(1)),"defeated holder queued action cancelled");Check(r.State.Rewards!.CompletedOpponentMask==1,"exactly defeated opposing party identity rewarded");turns++;
});
Test("new contact statuses affect later real actions and old Static remains once per hit",()=>{
 var f=Rebind(Rebind(Frame(119),2,Mon("RATICATE",["TACKLE"],200,100)),3,Mon("PARAS",["SCRATCH"],0,100));var a=Act(f,2,3);Check(WildBattleStatus.IsSleeping(a.State.Slots[2].Body!.Status),"Effect Spore applies sleep to actual partner");var b=Act(a.State,2,1);Check(b.Events.OfType<SourceActionBlockedEvent>().Any(e=>e.Side==new BattlerSlot(2))&&!b.Events.OfType<SourcePpCommandEvent>().Any(),"later partner action asleep with no PP");PartySync(b.State);
 var rollout=Rebind(Rebind(Frame(119),2,Mon("JIGGLYPUFF",["ROLLOUT"],200,30)),3,Mon("PARAS",["SCRATCH"],0,100));var rolled=Act(rollout,2,3);Check(WildBattleStatus.IsSleeping(rolled.State.Slots[2].Body!.Status)&&rolled.State.Slots[2].Registers.Turn?.Lock is null&&rolled.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Reason=="sleep"),"real contact sleep clears a just-created Rollout lock");PartySync(rolled.State);
 var staticFrame=Rebind(Frame(0),3,Mon("PIKACHU",["THUNDER_SHOCK"],0,100));var s=Act(staticFrame,2,3);Check(s.Trace.Count(x=>x.Purpose.EndsWith(".static"))==1,"unchanged Static one roll; dispatcher does not duplicate");
 var confused=Rebind(Frame(0),2,Frame(0).Slots[2].Body! with{ConfusionTurnsRemaining=2});confused=Rebind(confused,3,Mon("CARVANHA",["BITE"],0,100));var c=Act(confused,2,3);Check(c.Events.OfType<SourceConfusionSelfHitEvent>().Any()&&!c.Events.OfType<SourceAbilityDamageEvent>().Any(),"confusion self-damage never invokes contact");
});
Test("Color Change, fire thaw and foreign permanent identities remain closed atomically",()=>{
 var f=Rebind(Frame(),3,Mon("KECLEON",["SCRATCH"],0));var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision,0,new(3)));Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"Color Change remains closed");
 var fire=Rebind(Frame(),2,Mon("MAGMAR",["EMBER"],0));fire=Rebind(fire,3,fire.Slots[3].Body! with{Status=WildBattleStatus.Freeze});var fr=SourceBattleKernel.ResolveAction(catalog,fire,new(fire.Slots[2].Occupant!.Value,fire.Revision,0,new(3)));Check(fr.Fault=="source_kernel_fire_thaw_move_end_not_migrated"&&ReferenceEquals(fr.State,fire)&&fr.Trace.IsEmpty,"fire-thaw remains closed");
 var bad=Frame();bad=Rebind(bad,3,bad.Slots[3].Body! with{Pokemon=bad.Slots[3].Body!.Pokemon with{AbilityId="ABILITY_ROUGH_SKIN"}});var br=SourceBattleKernel.ResolveAction(catalog,bad,new(bad.Slots[2].Occupant!.Value,bad.Revision,0,new(3)));Check(!br.Succeeded&&ReferenceEquals(br.State,bad)&&br.Trace.IsEmpty,"forged permanent ability rejected");
});

Test("nonempty meaningful proof",()=>Check(groups>0&&checks>0&&vectors==165&&turns>=25,"nonempty proof"));var report=new{groups,checks,failures,vectors,turns,runtime=Environment.Version.ToString(),canonicalHashes=baseline.Count,sourceOnly,results};File.WriteAllText(Path.Combine(output,"report.json"),Json(report));Console.WriteLine(Json(report));return failures==0?0:1;
