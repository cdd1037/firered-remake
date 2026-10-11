using System.Collections.Immutable;
using System.Reflection;
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
string Hash<T>(T x)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x))));
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,int level,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0u)),new(0)).Pokemon;
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with{Pokemon=m.Pokemon with{CurrentHp=hp}};
var original=PretRocketHideoutPokemon.Load(args[0]).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(args[0],PretCeladonApproachPokemon.Load(args[0]).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,args[0],args[0]).Catalog,args[0]).Catalog,args[0]).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(old,args[0]).Catalog);
var names=new[]{"PAY_DAY","RAPID_SPIN","TELEPORT","UPROAR"};
var snapshots=new List<object>();
foreach(bool canonical in new[]{false,true})foreach(string move in names)foreach(int scenario in Enumerable.Range(0,8))foreach(bool reverse in new[]{false,true}){
 var actor=Mon("CLEFAIRY",50,"METRONOME");var target=Mon(scenario==4?"GASTLY":"SNORLAX",100,"DEFENSE_CURL");
 var side=reverse?WildBattleSide.Opponent:WildBattleSide.Player;
 if(scenario==1)target=target with{CalledVolatile=new(){SubstituteHp=1}};
 if(scenario==2)target=target with{Protected=true};
 if(scenario==3)actor=actor with{AccuracyStage=0};
 if(scenario==5)target=Hp(target,1);
 if(scenario==6)actor=actor with{BindTurnsRemaining=3,BindingMove=Id("BIND"),LeechSeedSource=reverse?WildBattleSide.Player:WildBattleSide.Opponent};
 if(scenario==7)target=target with{Status=3,Nightmare=true};
 var state=new WildBattleState(reverse?target:actor,reverse?actor:target,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,PayDayMoney=65500,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null};
 var result=WildBattleRules.ResolveCalledMoveAction(canonical?catalog:legacy,state,new(side,0,Id(move)));
 if(!result.Succeeded)throw new Exception($"baseline {canonical}/{move}/{scenario}/{reverse}: "+result.Fault);
 snapshots.Add(new{canonical,move,scenario,reverse,sha256=Hash(result),result});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse to overwrite immutable baseline");
 File.WriteAllText(args[1],Json(new{baseCommit="bfc976e21084ba2ad0b7a2f9da0652ccdab6c7e3",snapshots})+"\n");Console.WriteLine($"CAPTURE COMPLETE {snapshots.Count}");return 0;
}
File.WriteAllText(args[1]+".singles-after.json",Json(snapshots)+"\n");
int groups=0,checks=0,failures=0;var reports=new List<object>();
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Test(string name,Action body){groups++;try{body();reports.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;reports.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+" "+e);}}
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied = null, uint seed = 31) {
    var m = supplied ?? [Mon("PIDGEY", 30, "MINIMIZE"), Mon("SNORLAX", 100, "DEFENSE_CURL"), Mon("SPEAROW", 20, "DEFENSE_CURL"), Mon("RATTATA", 20, "MINIMIZE")];
    return new() { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn, SourceMechanicsRulesVersion = SourceBattleKernel.Version,
        BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(seed),
        Parties = [new(BattleSide.Player, [Member(m[0]), Member(m[2])]), new(BattleSide.Opponent, [Member(m[1]), Member(m[3])])],
        Slots = Enumerable.Range(0,4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, m[i])).ToImmutableArray() };
}
SourceBattleFrame Put(SourceBattleFrame f, int i, WildBattleCombatant m) { var c = f.Slots[i]; var key = c.Member!.Value; var p = f.Party(key.Side); return f with { Slots = f.Slots.SetItem(i,c with { Body=m }), Parties=f.Parties.SetItem((int)key.Side,p with { Members=p.Members.SetItem(key.Index,Member(m)) }) }; }
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f, int[]? positions = null, int[]? targets = null) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c => {
    int pos = positions?[c.Slot.Value] ?? c.Registers.Turn?.Lock?.PpSlot ?? 0;
    var move = catalog.Moves[c.Registers.Turn?.Lock?.Move ?? c.Body!.Pokemon.Moves[pos].Move];
    var target = c.Registers.Turn?.Lock?.Target ?? new BattlerSlot(targets?[c.Slot.Value] ?? (move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1));
    return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,target);
}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r) { Check(r.Succeeded,r.Fault ?? "accepted"); Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output"); return r; }
SourceFourActionResult Begin(SourceBattleFrame f,int[]? p=null,int[]? t=null) => Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,p,t)));
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Turn(SourceBattleFrame f,int[]? p=null,int[]? t=null) {
    var r=Begin(f,p,t); var events=r.Events.ToList(); var trace=r.Trace.ToList();
    while(r.State.FourTurn is not null) { Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"no unexpected external boundary");
        var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!; var actual=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value); var replay=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);
        Check(Json(actual)==Json(replay),"exact pause JSON state/events/RNG replay"); r=Ok(actual); events.AddRange(r.Events);trace.AddRange(r.Trace);
    } Check(r.CompletedCleanTurn,"clean full turn");  return(r.State,events,trace);
}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int pos=0) { var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,pos,new(target))); Check(r.Succeeded,r.Fault??"action"); return r; }
void Rejected(SourceBattleFrame f, SourceFourActionResult r,string why) => Check(!r.Succeeded && ReferenceEquals(f,r.State) && r.Trace.IsEmpty && r.Events.IsEmpty,why+" atomic: "+r.Fault);



Test("frozen canonical and historical complete singles",()=>{
 using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before.json")));
 var rows=doc.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(rows.Length==128&&snapshots.Count==128,"nonempty original corpus");
 int same=0,changed=0;
 for(int i=0;i<rows.Length;i++){
  var now=JsonSerializer.SerializeToElement(snapshots[i]);
  bool correction=rows[i].GetProperty("canonical").GetBoolean()&&rows[i].GetProperty("move").GetString()=="TELEPORT"&&rows[i].GetProperty("scenario").GetInt32()==6&&rows[i].GetProperty("reverse").GetBoolean();
  if(!correction){Check(rows[i].GetProperty("sha256").GetString()==now.GetProperty("sha256").GetString(),"exact result "+i+" "+rows[i].GetProperty("move"));same++;continue;}
  var old=System.Text.Json.Nodes.JsonNode.Parse(rows[i].GetProperty("result").GetRawText())!;var current=System.Text.Json.Nodes.JsonNode.Parse(now.GetProperty("result").GetRawText())!;
  Check(current["State"]!["Outcome"]!.GetValue<int>()==0&&current["Battle"]!["Events"]![5]!["Reason"]!.GetValue<string>()=="cannot-escape","source binding applies to opponent Teleport");
  foreach(var state in new[]{"State","Battle"}){
   var n=state=="State"?current["State"]!:current["Battle"]!["State"]!;var o=state=="State"?old["State"]!:old["Battle"]!["State"]!;
   n["Outcome"]=o["Outcome"]!.DeepClone();n["Opponent"]!["LastLandedMove"]=o["Opponent"]!["LastLandedMove"]!.DeepClone();n["Opponent"]!["SourceMoveHistory"]!["LastHitByType"]=o["Opponent"]!["SourceMoveHistory"]!["LastHitByType"]!.DeepClone();
  }
  current["Battle"]!["Events"]![5]=old["Battle"]!["Events"]![5]!.DeepClone();Check(Hash(current)==rows[i].GetProperty("sha256").GetString(),"only escape outcome, failure event and derived move history differ");changed++;
 }
 Check(same==127&&changed==1,"all64 historical exact,63 canonical exact,one source-proven escape correction");
});
SourceBattleFrame Basis(string name,int actor=0,uint seed=31) {
 var mons=Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"SPLASH","DEFENSE_CURL") with{SpeedStage=i}).ToArray();
 mons[actor]=Mon("CLEFAIRY",50,name,"SPLASH") with{SpeedStage=12};return Frame(mons,seed);
}
SourceBattleFrame Locked(SourceBattleFrame f,int actor,int remaining=3) {
 var cell=f.Slots[actor];return f with{Slots=f.Slots.SetItem(actor,cell with{Registers=cell.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,new(WildBattleCalledLockKind.Uproar,Id("UPROAR"),0,cell.Body!.Pokemon.Moves[0].Move,new(actor^1),remaining,0,null,false),0,null)}})};
}
Test("all four families execute each actual slot and complete real four-action turns",()=>{
 foreach(string name in names)for(int actor=0;actor<4;actor++){
  var f=Basis(name,actor);var r=Turn(f);Check(r.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==actor&&e.Move==Id(name)),"actual family actor "+actor+name);
  Check(r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(name)].Pp-1,"exact first PP");
  if(name=="TELEPORT")Check(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Actor.Value==actor&&e.Reason=="trainer-battle")&&r.State.Outcome==WildBattleOutcome.Ongoing,"trainer cannot escape");
  if(name=="PAY_DAY")Check(r.State.Field.PayDayMoney==(actor%2==0?250:0),"player money owner");
  if(name=="UPROAR")Check(r.State.Slots[actor].Registers.Turn?.Lock is{TurnsRemaining:>=1 and <=4},"timer progressed by existing residual owner");
 }
});
Test("Pay Day cap immunity substitute and KO effect boundary",()=>{
 for(int actor=0;actor<4;actor++){
  var f=Basis("PAY_DAY",actor);f=f with{Field=f.Field with{PayDayMoney=65500}};
  var r=Act(f,actor,actor^1);Check(r.State.Field.PayDayMoney==(actor%2==0?65535:65500),"unsigned16 saturation and side");
  Check(r.Events.OfType<SourcePayDayEvent>().Count()==1,"coin effect exists both sides");
  var sub=Act(Put(f,actor^1,f.Slots[actor^1].Body! with{CalledVolatile=new(){SubstituteHp=1}}),actor,actor^1);
  Check(!sub.Events.OfType<SourcePayDayEvent>().Any(),"initial substitute blocks Pay Day after breaking");
  var ghost=Act(Put(f,actor^1,Mon("GASTLY",100,"SPLASH")),actor,actor^1);Check(!ghost.Events.OfType<SourcePayDayEvent>().Any(),"immune target no money");
  var ko=Act(Put(f,actor^1,Hp(f.Slots[actor^1].Body!,1)),actor,actor^1);Check(ko.Events.OfType<SourcePayDayEvent>().Count()==1&&ko.State.Slots[actor^1].Body!.Pokemon.CurrentHp==0,"source PAYDAY exception on fainted effect target");
 }
 var invalid=Basis("PAY_DAY") with{Field=new(){PayDayMoney=65536}};Check(SourceBattleIdentityRules.Validate(invalid)=="source_frame_field","accumulator rejects out-of-source range");
});
Test("Rapid Spin clears binding seed and own side spikes in source order",()=>{
 for(int actor=0;actor<4;actor++)foreach(string mode in new[]{"normal","substitute","ghost","protected"}){
  var f=Basis("RAPID_SPIN",actor);f=Put(f,actor,f.Slots[actor].Body! with{BindTurnsRemaining=3,BindingMove=Id("BIND")});var c=f.Slots[actor];
  f=f with{Slots=f.Slots.SetItem(actor,c with{Registers=c.Registers with{WrappedBy=new(actor^1),LeechSeedSource=new(actor^1)}}),Sides=f.Sides.SetItem(0,f.Sides[0] with{Spikes=3}).SetItem(1,f.Sides[1] with{Spikes=2})};
  if(mode=="substitute")f=Put(f,actor^1,f.Slots[actor^1].Body! with{CalledVolatile=new(){SubstituteHp=1}});
  if(mode=="ghost")f=Put(f,actor^1,Mon("GASTLY",100,"SPLASH"));
  if(mode=="protected")f=Put(f,actor^1,f.Slots[actor^1].Body! with{Protected=true});
  var r=Act(f,actor,actor^1);bool clear=mode is "normal" or "substitute";
  Check((r.State.Slots[actor].Body!.BindTurnsRemaining==0)==clear,"binding "+mode);
  Check((r.State.Slots[actor].Registers.LeechSeedSource is null)==clear&&(r.State.Sides[actor%2].Spikes==0)==clear,"seed and spikes "+mode);
  Check(r.State.Sides[(actor%2)^1].Spikes==f.Sides[(actor%2)^1].Spikes,"other side unchanged");
  if(clear){var events=r.Events.Where(e=>e is SourceBindingChangedEvent or SourceLeechSeedChangedEvent or SourceCalledSpikesChangedEvent).ToArray();Check(events.Length==3&&events[0] is SourceBindingChangedEvent&&events[1] is SourceLeechSeedChangedEvent&&events[2] is SourceCalledSpikesChangedEvent,"ordered one command chain");Check(!r.Trace.Any(t=>t.Purpose.EndsWith("secondary-effect")),"CERTAIN effective hit omits RNG");}
 }
});
Test("Uproar forced continuation PP wake and Soundproof guards",()=>{
 var f=Basis("UPROAR");var initial=Act(f,0,1);Check(initial.State.Slots[0].Registers.Turn?.Lock?.Kind==WildBattleCalledLockKind.Uproar,"start lock");
 var continued=Act(initial.State,0,1);Check(!continued.Events.OfType<SourcePpCommandEvent>().Any()&&continued.Trace.Length==4,"hit continuation consumes no PP or duration draw");
 var miss=Act(Put(initial.State with{Rng=new(31)},0,initial.State.Slots[0].Body! with{AccuracyStage=0}),0,1);
 Check(miss.Events.OfType<SourceMoveMissedEvent>().Any()&&miss.Events.OfType<SourcePpCommandEvent>().Count()==1,"miss continuation executes ppreduce");
 var sound=Put(initial.State,1,Mon("VOLTORB",100,"SPLASH"));Check(WildBattleEffective.Ability(sound.Slots[1].Body!)=="ABILITY_SOUNDPROOF","soundproof fixture");
 var immune=Act(sound,0,1);Check(!immune.Events.OfType<SourcePpCommandEvent>().Any(e=>e.Before!=e.After),"Soundproof forced continuation no PP cost");
 var asleep=Put(Locked(Basis("UPROAR"),0),2,Mon("SNORLAX",100,"SPLASH") with{Status=3,Nightmare=true});
 var wake=Act(asleep,2,2);Check(wake.State.Slots[2].Body!.Status==0&&!wake.State.Slots[2].Body!.Nightmare&&wake.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Reason=="uproar-wake"),"partner action wakes and clears Nightmare before sleep check");
 var quiet=Put(asleep,2,Mon("VOLTORB",100,"SPLASH") with{Status=3,Nightmare=true});var sleeping=Act(quiet,2,2);Check(WildBattleStatus.IsSleeping(sleeping.State.Slots[2].Body!.Status),"Soundproof remains asleep");
});
Test("Uproar full quartet duration forced selection and exact restored continuation",()=>{
 var f=Frame(Enumerable.Range(0,4).Select(i=>Mon("SNORLAX",100,"UPROAR","SPLASH") with{DefenseStage=12,SpecialDefenseStageDelta=6}).ToArray());
 var first=Turn(f);Check(first.Trace.Count(t=>t.Purpose.Contains("target.learned.random-flank"))==4,"four actual random target selections");
 var second=Turn(first.State);Check(!second.Events.OfType<SourcePpCommandEvent>().Any(),"all four forced hits skip PP");
 var state=second.State;for(int i=0;i<4&&state.Slots.Any(c=>c.Registers.Turn?.Lock is not null);i++)state=Turn(state,state.Slots.Select(c=>c.Registers.Turn?.Lock is null?1:0).ToArray()).State;
 Check(state.Slots.All(c=>c.Registers.Turn?.Lock is null),"all source durations expire without confusion");
 var locked=Locked(Basis("UPROAR"),0);Rejected(locked,SourceFourActionTurns.BeginActions(catalog,locked,locked.Revision,Choices(locked,[1,0,0,0])),"cannot replace forced move slot");
});
Test("Uproar primary Rest Yawn and residual sleep protection",()=>{
 foreach(string move in new[]{"HYPNOSIS","REST","YAWN"}){
  var f=Locked(Basis("UPROAR"),0);int actor=move=="REST"?2:1,target=2;
  f=Put(f,actor,Mon("SNORLAX",100,move) with{AccuracyStage=12});if(move=="REST")f=Put(f,actor,Hp(f.Slots[actor].Body!,100));
  var r=Act(f,actor,target);Check(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="uproar"),move+" blocked by partner uproar");
 }
 var yawn=Locked(Basis("UPROAR"),0,3);yawn=Put(yawn,2,yawn.Slots[2].Body! with{YawnTurnsRemaining=1});var ended=Turn(yawn);
 Check(ended.State.Slots[2].Body!.YawnTurnsRemaining==0&&ended.State.Slots[2].Body!.Status==0,"Yawn expiry full-field guard");
 var paralyzed=Locked(Basis("UPROAR",seed:0),0);paralyzed=Put(paralyzed,0,paralyzed.Slots[0].Body! with{Status=WildBattleStatus.Paralysis});var blocked=Act(paralyzed,0,1);
 Check(blocked.State.Slots[0].Registers.Turn is null&&!blocked.Events.OfType<SourcePpCommandEvent>().Any(),"source paralysis script cancels Uproar immediately");
});
Test("Teleports always fail in trainer queue and unsupported wild doubles are atomic",()=>{
 for(int actor=0;actor<4;actor++){
  var f=Basis("TELEPORT",actor);var r=Act(f,actor,actor);Check(r.Trace.IsEmpty&&r.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="trainer-battle","trainer before legality / no random");
 }
 var wild=Basis("TELEPORT") with{BattleKind=WildBattleCalledBattleKind.Wild};Rejected(wild,SourceFourActionTurns.BeginActions(catalog,wild,wild.Revision,Choices(wild)),"four owner has no wild encounter contract");
});
Test("Uproar KO cleanup and real replacement never transfer lock or extra action",()=>{
 var f=Locked(Basis("UPROAR"),0);f=Put(f,0,Hp(f.Slots[0].Body! with{SpeedStage=0},1));f=Put(f,1,Mon("SNORLAX",100,"HYPER_BEAM","SPLASH") with{SpeedStage=12,AccuracyStage=12});
 var party=f.Party(BattleSide.Player);f=f with{Parties=f.Parties.SetItem(0,party with{Members=party.Members.Add(Member(Mon("VOLTORB",60,"SPLASH")))})};
 var r=Begin(f,null,[1,0,2,3]);bool replaced=false;var events=new List<SourceBattleEvent>();
 for(int guard=0;r.State.FourTurn is not null&&guard<50;guard++){
  if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){Check(r.State.Slots[0].Registers.Turn is null,"faint cleared Uproar");r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));replaced=true;}
  else{Check(r.State.FourTurn.Phase!=SourceFourActionPhase.RewardsPending,"no player KO reward");r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));}
  events.AddRange(r.Events);
 }
 Check(replaced&&r.CompletedCleanTurn&&r.State.Slots[0].EntrySerial==2&&r.State.Slots[0].Registers.Turn is null,"real clean entry boundary");
 Check(!events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==0),"cancelled original actor never acts as replacement");
 Check(r.State.Slots[1].Registers.Turn?.Lock?.Kind==WildBattleCalledLockKind.Recharge,"other slot recharge preserved");
});
Test("exact original C commands agree with managed field results",()=>{
 using var proof=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));var rows=proof.RootElement.GetProperty("vectors");Check(rows.GetArrayLength()==78&&proof.RootElement.GetProperty("complete").GetBoolean(),"nonempty C corpus");int compared=0;
 foreach(var row in rows.EnumerateArray()){
  string kind=row.GetProperty("kind").GetString()!;
  if(kind=="payday"){
   int actor=row.GetProperty("side").GetInt32();var f=Basis("PAY_DAY",actor);f=f with{Field=new(){PayDayMoney=row.GetProperty("before").GetInt32()}};Check(Act(f,actor,actor^1).State.Field.PayDayMoney==row.GetProperty("after").GetInt32(),"C Payday saturation");compared++;
  }
  if(kind=="uproar"){
   int source=row.GetProperty("source").GetInt32(),target=row.GetProperty("target").GetInt32();bool sound=row.GetProperty("soundproof").GetInt32()!=0;
   var f=Locked(Basis("UPROAR",source),source);var m=Mon(sound?"VOLTORB":"SNORLAX",100,target==source?"UPROAR":"SPLASH") with{Status=3};f=Put(f,target,m);var r=Act(f,target,target==source?source^1:target);
   Check((r.State.Slots[target].Body!.Status==0)==(row.GetProperty("blocked").GetInt32()!=0),"C wake prevention source/target "+source+target);compared++;
  }
 }
 Check(compared==38,"six Payday and32 Uproar vectors exercised; trap/rapidspin C commands authenticated separately");
});

File.WriteAllText(args[1],Json(new{runtime=Environment.Version.ToString(),groups,checks,failures,recipeAdmission=catalog.Moves.Values.Count(SourceBattleKernel.SupportsRecipe),unadmitted=catalog.Moves.Values.Where(m=>!SourceBattleKernel.SupportsRecipe(m)).Select(m=>m.Id.Value).Order().ToArray(),reports})+"\n");Console.WriteLine("FIELD RECIPES COMPLETE");return failures==0?0:1;
