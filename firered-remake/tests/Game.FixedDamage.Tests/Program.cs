using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
if(args.Length<2)throw new ArgumentException("CONTENT_ROOT OUTPUT [--capture]");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
string Json<T>(T x)=>JsonSerializer.Serialize(x);
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 foreach(string move in moves)if(!catalog.Species[p.Species].LevelUpMoves.Any(m=>m.Move==Id(move)&&m.Level<=level))throw new Exception("unlearned "+species+"/"+move);
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var moves=new[]{("SCYTHER","FALSE_SWIPE"),("CHARMANDER","DRAGON_RAGE"),("MAGNEMITE","SONIC_BOOM"),("MACHOP","SEISMIC_TOSS"),("GASTLY","NIGHT_SHADE"),("MISDREAVUS","PSYWAVE"),("RATTATA","SUPER_FANG"),("RATTATA","ENDEAVOR")};
var snapshots=new List<object>();var runs=new List<WildBattleResult>();
foreach(var (species,move) in moves)foreach(uint seed in new uint[]{0,1,2,5,17,31,69,127})foreach(int scenario in Enumerable.Range(0,6)){
 var actor=Mon(species,80,move);var target=Mon(scenario==2?"GASTLY":scenario==3?"PIKACHU":"MAGIKARP",100,scenario==2?"NIGHT_SHADE":scenario==3?"GROWL":"SPLASH");
 if(scenario==1)target=Hp(target,1);if(scenario==4)actor=Hp(actor,1);if(scenario==5)target=target with{HeldItem="ITEM_FOCUS_BAND"};
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(seed));if(!start.Succeeded)throw new Exception("start "+species+":"+start.Fault);
 var result=WildBattleRules.ResolveMove(catalog,start.State!,0);if(!result.Succeeded)throw new Exception("result "+species+":"+result.Fault);
 runs.Add(result); string serialized=Json(result);snapshots.Add(new{species,move,seed,scenario,bytes=Encoding.UTF8.GetByteCount(serialized),sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant(),rng=result.State!.Rng,draws=result.Trace.Length});

}
if(args.Contains("--capture-3c78")){
 var family=typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasFixedDamageRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 if(family is not null)throw new Exception("Baseline capture rejects candidate assemblies; use unmodified 3c78f0a source.");
 File.WriteAllText(args[1],Json(new{baseCommit="3c78f0a71871c39143b04650e3bccd4564bb81bc",snapshots}));return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
int unchanged=0,corrected=0;
Test("immutable 3c78 singles goldens retain ordinary results and preserve known Super Fang red evidence",()=>{
 Check(Environment.Version.Major==10,"actual hosted runtime 10");
 using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-3c78f0a.json")));
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==snapshots.Count&&old.Length==384,"nonempty immutable suite");
 for(int i=0;i<old.Length;i++){
  var current=JsonSerializer.SerializeToElement(snapshots[i]);string move=current.GetProperty("move").GetString()!;
  bool same=current.GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString();
  if(move!="SUPER_FANG"||!runs[i].Trace.Any(t=>t.Purpose=="wild-battle.player.secondary-effect")) {Check(same,"unchanged entire state/event/RNG result "+current);unchanged++;}
  else {Check(!same,"original defective Super Fang golden remains red");Check(runs[i].Trace.Count(t=>t.Purpose=="wild-battle.player.secondary-effect")==1,"source secondary command runs exactly once");corrected++;}
 }
 Check(unchanged==347&&corrected==37,"only 37 source-proven Super Fang cases differ");
});
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant actor,WildBattleCombatant target,uint seed=31,int actorSlot=2,int targetSlot=3){
 var mons=Enumerable.Range(0,4).Select(_=>Mon("JIGGLYPUFF",80,"DEFENSE_CURL")).ToArray();mons[actorSlot]=actor;mons[targetSlot]=target;
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),
  Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var cell=f.Slots[slot];var member=cell.Member!.Value;var party=f.Party(member.Side);return f with{Slots=f.Slots.SetItem(slot,cell with{Body=m}),Parties=f.Parties.SetItem((int)member.Side,party with{Members=party.Members.SetItem(member.Index,Member(m))})};}
SourceKernelResult Action(SourceBattleFrame f,int actor=2,int target=3){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)));Check(r.Succeeded,r.Fault??"kernel");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid result identity");return r;}
Test("all eight authenticated moves use real actor and target slots without rewriting definitions",()=>{
 foreach(var (species,move) in moves)foreach(int actor in Enumerable.Range(0,4))foreach(int target in Enumerable.Range(0,4).Where(t=>t!=actor)){
  var user=Mon(species,80,move);if(move=="ENDEAVOR")user=Hp(user,20);
  var f=Frame(user,Mon("MAGIKARP",100,"SPLASH"),31,actor,target);var r=Action(f,actor,target);
  Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(move)]),"exact admitted definition");
  var hp=r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Id(move));Check(hp.Actor.Value==actor&&hp.Target.Value==target,"actual selected positions");
  Check(r.State.Slots.Where((_,i)=>i!=actor&&i!=target).Select(c=>Json(c.Body)).SequenceEqual(f.Slots.Where((_,i)=>i!=actor&&i!=target).Select(c=>Json(c.Body))),"uninvolved partners untouched");
  Check(r.Events.OfType<SourcePpCommandEvent>().Single().After==catalog.Moves[Id(move)].Pp-1,"only selected actual PP owner spends");
  if(move!="FALSE_SWIPE")Check(!r.Trace.Any(t=>t.Purpose.EndsWith(".damage")||t.Purpose.EndsWith(".critical")),"fixed recipes skip crit and variance");
  Check(r.Trace.Count(t=>t.Purpose.EndsWith(".secondary-effect"))==1,"HitFromAtkAnimation exact secondary draw");
 }
});
using var original=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
Test("original C Psywave rejection loop and level-one zero follow exact four-slot RNG",()=>{
 Check(original.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"source pin");int seen=0,zero=0,rejected=0;
 foreach(var v in original.RootElement.GetProperty("vectors").EnumerateArray().Where(v=>v.GetProperty("kind").GetString()=="math"&&v.GetProperty("op").GetInt32()==0)){
  int level=v.GetProperty("level").GetInt32();uint seed=v.GetProperty("seed").GetUInt32();
  // Inverse the first source LCG step: the ordinary accuracy draw is zero, and the
  // unchanged C Psywave command then receives exactly its recorded starting seed.
  uint before=unchecked((seed-24691u)*4005161829u);var f=Frame(Mon("MISDREAVUS",level,"PSYWAVE"),Mon("MAGIKARP",100,"SPLASH"),before);
  var r=Action(f);var hp=r.Events.OfType<SourceHpChangedEvent>().Single();int amount=v.GetProperty("damage").GetInt32();
  Check(hp.Damage==amount&&hp.HpBefore-hp.HpAfter==amount,"unchanged C Psywave amount");
  Check(r.Trace.Count(t=>t.Purpose.EndsWith(".psywave"))==v.GetProperty("draws").GetInt32(),"all rejection draws retained");
  uint end=unchecked(v.GetProperty("finalSeed").GetUInt32()*1103515245u+24691u);
  Check(r.State.Rng.Seed==end&&r.State.Rng.Cursor==(ulong)(v.GetProperty("draws").GetInt32()+2),"accuracy, Psywave loop, secondary exact RNG");
  Check(!r.Events.OfType<SourceEffectivenessEvent>().Any(),"Psywave has no resistance multiplier text");
  if(amount==0){zero++;Check(r.State.Slots[3].Body!.LastLandedMove==Id("PSYWAVE"),"zero damage remains an effective landed move");}
  if(v.GetProperty("draws").GetInt32()>1)rejected++;seen++;
 }
 Check(seen==320&&zero>0&&rejected>0,"complete nonempty original C corpus");
});
Test("original C level, half-HP and health-difference math survives all admitted damage contexts",()=>{
 int seen=0;
 foreach(var v in original.RootElement.GetProperty("vectors").EnumerateArray().Where(v=>v.GetProperty("kind").GetString()=="math"&&v.GetProperty("op").GetInt32()>0)){
  int op=v.GetProperty("op").GetInt32(),level=v.GetProperty("level").GetInt32(),hp=v.GetProperty("targetHp").GetInt32();
  if(op==1&&level<19||hp>181)continue;
  string move=op==1?"SEISMIC_TOSS":op==2?"SUPER_FANG":"ENDEAVOR";var user=Mon(op==1?"MACHOP":"RATTATA",op==1?level:80,move);
  if(op==3)user=Hp(user,level);
  var r=Action(Frame(user,Hp(Mon("MAGIKARP",100,"SPLASH"),hp),31));
  bool failed=v.GetProperty("failed").GetInt32()!=0;
  if(failed){Check(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="endeavor-hp-not-lower"),"source difference branch");Check(r.Trace.IsEmpty,"HP failure precedes accuracy");}
  else Check(r.Events.OfType<SourceHpChangedEvent>().Single().Damage==v.GetProperty("damage").GetInt32(),"unchanged C amount");
  seen++;
 }
 Check(seen>600,"nonempty math comparison");
});
Test("immunity, Foresight, Pressure, protection and Endure preserve per-script branches",()=>{
 foreach(var (species,move) in moves){
  bool ghostImmune=move is "FALSE_SWIPE" or "SONIC_BOOM" or "SEISMIC_TOSS" or "SUPER_FANG" or "ENDEAVOR";
  var user=Mon(species,80,move);if(move=="ENDEAVOR")user=Hp(user,1);
  var r=Action(Frame(user,Mon("DUSCLOPS",100,"NIGHT_SHADE")));Check(r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2,"Pressure cost");
  Check(r.Events.OfType<SourceHpChangedEvent>().Single().Damage==0==ghostImmune,"source immunity");
  Check(r.Trace.Count(t=>t.Purpose.EndsWith(".secondary-effect"))==1,"immunity retains secondary RNG");
  var protectedResult=Action(Frame(user,Mon("DUSCLOPS",100,"NIGHT_SHADE") with{Protected=true}));
  Check(protectedResult.Trace.IsEmpty&&protectedResult.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"),"Protect skips accuracy and damage draws");
  Check(protectedResult.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==2,"protected Pressure still costs PP");
  if(ghostImmune){var foresight=Action(Frame(user,Mon("DUSCLOPS",100,"NIGHT_SHADE") with{Identified=true}));Check(foresight.Events.OfType<SourceHpChangedEvent>().Single().Damage>0,"Foresight bypasses Normal/Fighting Ghost immunity");}
  if(move=="ENDEAVOR")continue;
  var endured=Action(Frame(user,Hp(Mon("MAGIKARP",100,"SPLASH"),1) with{Enduring=true}));
  Check(endured.State.Slots[3].Body!.Pokemon.CurrentHp==(move=="SUPER_FANG"?0:1),"Super Fang skips adjustsetdamage; other family respects Endure");
  Check(endured.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="endure"&&e.Reason=="endured")== (move!="SUPER_FANG"),"Endure result command distinction");
 }
 var falseSwipe=Action(Frame(Mon("SCYTHER",80,"FALSE_SWIPE"),Hp(Mon("MAGIKARP",100,"SPLASH"),1)));
 Check(falseSwipe.State.Slots[3].Body!.Pokemon.CurrentHp==1&&falseSwipe.Events.OfType<SourceHpChangedEvent>().Single().Damage==0,"False Swipe preserves one HP");
 Check(falseSwipe.State.Slots[3].Body!.LastLandedMove==Id("FALSE_SWIPE"),"False Swipe effective-zero history");
 var dark=Action(Frame(Mon("MISDREAVUS",80,"PSYWAVE"),Mon("HOUNDOUR",100,"HOWL")));
 Check(dark.Events.OfType<SourceHpChangedEvent>().Single().Damage==0&&dark.Trace.Any(t=>t.Purpose.EndsWith(".psywave")),"Psywave generates nominal damage before immune HP update");
});
ImmutableArray<SourceActionSelection> Picks(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot:c.Slot.Opposite)).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four-turn");return r;}
SourceFourActionResult Next(SourceFourActionResult r){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var next=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var copy=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);Check(Json(next)==Json(copy),"exact serialized continuation");return Ok(next);}
Test("real four-action fixed-damage KO yields rewards and surviving partners finish without replay",()=>{
 var f=Frame(Mon("CHARMANDER",80,"DRAGON_RAGE"),Hp(Mon("MAGIKARP",5,"SPLASH"),1));
 // The fainted opponent's Splash is a genuine learned move but not admitted in the
 // four-slot family; choose a naturally learned admitted target move instead.
 f=Put(f,3,Hp(Mon("JIGGLYPUFF",5,"DEFENSE_CURL"),1));
 var started=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Picks(f)));var r=started;var events=started.Events.ToList();bool rewardPause=false;int limit=0;
 while(r.State.FourTurn is not null&&r.State.FourTurn.Phase is not(SourceFourActionPhase.BattleEnded or SourceFourActionPhase.ReplacementsPending)){
  Check(++limit<30,"finite continuation");rewardPause|=r.State.FourTurn.Phase==SourceFourActionPhase.RewardsPending;
  if(r.State.Rewards?.Pending is{} pending){var m=pending.Moves[0];r=Ok(SourceFourRewards.Learn(catalog,r.State,r.Continuation!.Value,new(pending.Recipient,m.Level,m.Move,null)));}
  else r=Next(r);events.AddRange(r.Events);
 }
 Check(rewardPause&&events.OfType<SourceRewardAppliedEvent>().Any(),"KO pauses and pays through canonical owner");
 Check(events.OfType<SourceHpChangedEvent>().Count(e=>e.Move==Id("DRAGON_RAGE"))==1,"damage never replayed across reward yield");
 Check(events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==0)&&events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==1),"both remaining partners act");
 Check(!events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==3),"KO action is cancelled");
 Check(r.State.Rewards!.CompletedOpponentMask==2,"actual opponent party index rewarded once");
});
Test("contact acts on the selected flank and immunity never triggers contact",()=>{
 bool contact=false;for(uint seed=0;seed<32;seed++){
  var r=Action(Frame(Mon("MACHOP",80,"SEISMIC_TOSS"),Mon("PIKACHU",100,"GROWL"),seed));
  if(r.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Side.Value==2&&e.After==WildBattleStatus.Paralysis))contact=true;
  Check(r.State.Slots[0].Body!.Status==0,"contact cannot redirect to actor partner");
 }
 Check(contact,"actual Static source chance observed");
 var immune=Action(Frame(Mon("MACHOP",80,"SEISMIC_TOSS"),Mon("GASTLY",100,"NIGHT_SHADE")));
 Check(!immune.Trace.Any(t=>t.Purpose.EndsWith(".static")),"immunity skips contact chance");
});
Test("unsupported state and malformed actions fail atomically before PP, events and RNG",()=>{
 var f=Frame(Mon("RATTATA",80,"SUPER_FANG"),Mon("MAGIKARP",100,"SPLASH"));
 void Atomic(SourceBattleFrame input,SourceResolvedMoveAction action,string name){var r=SourceBattleKernel.ResolveAction(catalog,input,action);Check(!r.Succeeded&&ReferenceEquals(r.State,input)&&r.Events.IsEmpty&&r.Trace.IsEmpty,name+": "+r.Fault);}
 var valid=new SourceResolvedMoveAction(f.Slots[2].Occupant!.Value,f.Revision,0,new(3));
 Atomic(f,valid with{Revision=-1},"stale revision");Atomic(f,valid with{Actor=valid.Actor with{EntrySerial=9}},"stale occupant");Atomic(f,valid with{ChosenSlot=4},"bad move slot");
 var band=Put(f,3,f.Slots[3].Body! with{HeldItem="ITEM_FOCUS_BAND"});var admitted=SourceBattleKernel.ResolveAction(catalog,band,valid);Check(admitted.Succeeded,"previously migrated four-slot Focus Band remains admitted");
 var unsupported=Put(f,3,f.Slots[3].Body! with{HeldItem="ITEM_MASTER_BALL"});Atomic(unsupported,valid,"unsupported held-item category");
 var overflow=f with{Rng=new(31,ulong.MaxValue)};Atomic(overflow,valid,"RNG overflow");
 var badDefinition=catalog.Moves[Id("SUPER_FANG")] with{Accuracy=100};Check(!SourceBattleKernel.SupportsRecipe(badDefinition),"modified move definition rejected");
 var fixedFamily=typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasFixedDamageRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
 Check((bool)fixedFamily.Invoke(null,[catalog.Moves[Id("FISSURE")].Effect])!,"later OHKO shared fixed-damage dispatch remains admitted");
 foreach(string excluded in new[]{"COUNTER","BIDE","DOUBLE_EDGE","ERUPTION"})Check(!(bool)fixedFamily.Invoke(null,[catalog.Moves[Id(excluded)].Effect])!,"outside fixed-damage family "+excluded);
});
Test("original C adjustment and nominal-versus-actual receipt vectors match shared commands",()=>{
 var assembly=typeof(WildBattleRules).Assembly;
 var adjust=assembly.GetType("Game.Domain.Battle.SourceHeldDamageRules")!.GetMethod("Adjust",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
 var record=assembly.GetType("Game.Domain.Battle.SourceHitItemRules")!.GetMethod("Record",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;int seen=0;
 foreach(var v in original.RootElement.GetProperty("vectors").EnumerateArray().Where(v=>v.GetProperty("kind").GetString()=="adjust")){
  int I(string name)=>v.GetProperty(name).GetInt32();bool sub=I("substitute")!=0,immune=I("immune")!=0;
  var value=adjust.Invoke(null,[I("hp"),I("damage"),sub,I("endure")!=0,I("swipe")!=0,false])!;
  int amount=(int)value.GetType().GetProperty("Damage")!.GetValue(value)!;
  bool endured=(bool)value.GetType().GetProperty("Endured")!.GetValue(value)!;
  Check(amount==I("adjusted"),"unchanged adjustsetdamage body amount");Check((immune?8:0)+(endured?64:0)==I("flags"),"exact no-effect and Endure flags");
  int lost=immune?0:Math.Min(sub?10:I("hp"),amount);
  var receipt=(SourceHitDamageReceipt)record.Invoke(null,[new SourceHitDamageReceipt(),lost,sub,immune,true,false])!;
  Check(receipt.Damage==I("receipt")&&receipt.Physical==I("physical"),"unchanged datahpupdate nominal/actual receipt");seen++;
 }
 Check(seen==96,"complete original adjustment corpus");
});
File.WriteAllText(args[1],Json(new{groups,checks,failures,unchanged,corrected,results}));Console.WriteLine(Json(new{groups,checks,failures,unchanged,corrected}));return groups<8||checks<2000||failures>0?1:0;
