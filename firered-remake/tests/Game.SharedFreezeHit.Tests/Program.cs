#pragma warning disable CS8321
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Reflection;
using Game.Domain.Randomness;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
if(args.Length!=3)return 2;
string root=args[0],source=args[1],output=args[2];
string J<T>(T x)=>JsonSerializer.Serialize(x);
string Hash<T>(T x)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(J(x))));
MoveId Id(string x)=>new("pret:MOVE_"+x);
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;
string tm=File.ReadAllText(Path.Combine(source,"src/data/pokemon/tmhm_learnsets.h"));
int groups=0,checks=0,failures=0,turns=0;
void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
void Test(string name,Action run){groups++;try{run();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
WildBattleCombatant Mon(string species="DROWZEE",string moves="MEDITATE",int level=70,uint pid=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 var block=Regex.Match(tm,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
 foreach(string move in moves.Split(','))Check(catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(move)&&x.Level<=level)||Regex.IsMatch(block,@"[TH]M\d\d_"+move+@"\)"),"authentic natural/TM move "+species+"/"+move);
 return new(p with{Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});
}
var fixtures=new[]{("JYNX","ICE_PUNCH"),("LAPRAS","ICE_BEAM"),("LAPRAS","BLIZZARD"),("JYNX","POWDER_SNOW")};
WildBattleState Pair(WildBattleCombatant a,WildBattleCombatant b,uint seed=31)=>new(a,b,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
var baseline=new List<object>();var full=new List<object>();
foreach(var (species,move) in fixtures)foreach(int variant in Enumerable.Range(0,10))foreach(uint seed in Enumerable.Range(0,24).Select(x=>(uint)x).Concat(new uint[]{60,61,62,63,64,65,66,67})){
 var a=Mon(species,move,35);var b=Mon("SNORLAX","AMNESIA",80);
 if(variant==1)b=b with{Protected=true};
 if(variant==2)b=b with{Status=WildBattleStatus.Poison};
 if(variant==3)b=Mon("LAPRAS","MIST",80);
 if(variant==4)b=Mon("CATERPIE","STRING_SHOT",80);
 if(variant==5)b=b with{HeldItem="ITEM_ASPEAR_BERRY"};
 if(variant==6)b=b with{CalledVolatile=new(){SubstituteHp=1}};
 if(variant==7)b=b with{Pokemon=b.Pokemon with{CurrentHp=1}};
 var state=Pair(a,b,seed);
 if(variant==8)state=state with{CalledField=new(){SunTurnsRemaining=5}};
 if(variant==9)state=state with{CalledField=new(){OpponentSafeguardTurnsRemaining=5}};
 var result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));
 Check(result.Succeeded,"baseline admitted "+move+"/"+variant+" "+result.Fault);
 baseline.Add(new{move,variant,seed,hash=Hash(result)});full.Add(new{move,variant,seed,result});
}
if(output=="--baseline"){
 Check(!File.Exists("tests/Game.SharedFreezeHit.Tests/singles-baseline-88543cb9.json"),"baseline capture refuses overwrite");
 File.WriteAllText("tests/Game.SharedFreezeHit.Tests/singles-baseline-88543cb9.json",J(JsonSerializer.SerializeToElement(baseline).EnumerateArray().Where(x=>x.GetProperty("seed").GetUInt32()<24).ToArray()));
 File.WriteAllText("evidence/freeze-singles-before-full.json",J(full));
 Console.WriteLine(J(new{baseline=baseline.Count,checks}));return 0;
}
var zeroBoundary=new List<object>();
foreach(var (species,move) in fixtures)foreach(string item in new[]{"endure","focus-band"})foreach(uint seed in Enumerable.Range(0,24).Select(x=>(uint)x).Concat(new uint[]{60,61,62,63,64,65,66,67})){
 var a=Mon(species,move,35);var b=Mon("SNORLAX","AMNESIA",80);b=b with{Pokemon=b.Pokemon with{CurrentHp=1},Enduring=item=="endure",HeldItem=item=="focus-band"?"ITEM_FOCUS_BAND":"ITEM_NONE"};
 var result=WildBattleRules.ResolveSourceMoveAction(catalog,Pair(a,b,seed),new(WildBattleSide.Player,0));Check(result.Succeeded,"zero boundary admitted "+result.Fault);
 zeroBoundary.Add(new{move,item,seed,hash=Hash(result),result});
}
if(output=="--capture-zero-boundary"){
 string path="tests/Game.SharedFreezeHit.Tests/singles-zero-boundary-88543cb9.json";Check(!File.Exists(path),"do not replace boundary baseline");File.WriteAllText(path,J(JsonSerializer.SerializeToElement(zeroBoundary).EnumerateArray().Where(x=>x.GetProperty("seed").GetUInt32()<24).ToArray()));Console.WriteLine(J(new{zeroBoundary=zeroBoundary.Count}));return 0;
}
if(output=="--capture-freeze-positive"){
 var extra=JsonSerializer.SerializeToElement(baseline).EnumerateArray().Where(x=>x.GetProperty("seed").GetUInt32()>=60).ToArray();
 var zero=JsonSerializer.SerializeToElement(zeroBoundary).EnumerateArray().Where(x=>x.GetProperty("seed").GetUInt32()>=60).ToArray();
 string a="tests/Game.SharedFreezeHit.Tests/singles-positive-baseline-88543cb9.json",b="tests/Game.SharedFreezeHit.Tests/singles-zero-positive-baseline-88543cb9.json";
 Check(!File.Exists(a)&&!File.Exists(b),"supplementary baseline capture refuses overwrite");File.WriteAllText(a,J(extra));File.WriteAllText(b,J(zero));
 File.WriteAllText("evidence/freeze-singles-before-full-positive.json",J(full));Console.WriteLine(J(new{positiveBaseline=extra.Length,positiveZero=zero.Length}));return 0;
}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? bodies=null,uint seed=31){var m=bodies??[Mon(),Mon(),Mon(),Mon()];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};}
SourceBattleFrame Body(SourceBattleFrame f,int slot,WildBattleCombatant b){var c=f.Slots[slot];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=b}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(b))})};}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four action accepted");Check(SourceBattleIdentityRules.Validate(r.State)==null,"four identity");return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? targets=null)=>f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot.Value:targets?[c.Slot.Value]??(c.Slot.Value^1)))).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame f,int[]? targets=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,targets)));
SourceFourActionResult Next(SourceFourActionResult r)=>Good(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
(SourceFourActionResult Result,ImmutableArray<SourceBattleEvent> Events,ImmutableArray<RandomTrace> Trace) Finish(SourceFourActionResult r){var e=r.Events.ToBuilder();var t=r.Trace.ToBuilder();for(int i=0;i<24&&r.State.FourTurn!=null;i++){r=Next(r);e.AddRange(r.Events);t.AddRange(r.Trace);}Check(r.CompletedCleanTurn,"actual complete four-action turn");turns++;return(r,e.ToImmutable(),t.ToImmutable());}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)));Check(r.Succeeded,r.Fault??"kernel accepted");return r;}
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+why+" "+r.Fault);
Test("1280 frozen complete canonical singles results",()=>{
 var old=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-88543cb9.json"))).RootElement;
 var current=JsonSerializer.SerializeToElement(baseline);var first=current.EnumerateArray().Where(x=>x.GetProperty("seed").GetUInt32()<24).ToArray();
 Check(old.GetArrayLength()==960&&J(old)==J(first),"exact state, invocation, ordered events and trace hashes");
 var extra=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-positive-baseline-88543cb9.json"))).RootElement;
 var positive=current.EnumerateArray().Where(x=>x.GetProperty("seed").GetUInt32()>=60).ToArray();Check(extra.GetArrayLength()==320&&J(extra)==J(positive),"successful infliction baseline also immutable");
 File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"freeze-singles-after-full.json"),J(full));
});
using var sourceVectors=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
var vectors=sourceVectors.RootElement.GetProperty("vectors").EnumerateArray().ToDictionary(v=>(v.GetProperty("mode").GetInt32(),v.GetProperty("serene").GetInt32(),v.GetProperty("roll").GetInt32()));
Test("zero-loss source correction is limited to canonical Endure and Focus Band freeze",()=>{
 var original=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-zero-boundary-88543cb9.json"))).RootElement;
 var positive=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-zero-positive-baseline-88543cb9.json"))).RootElement;
 string Key(JsonElement x)=>x.GetProperty("move").GetString()+"/"+x.GetProperty("item").GetString()+"/"+x.GetProperty("seed").GetUInt32();
 var old=original.EnumerateArray().Concat(positive.EnumerateArray()).ToDictionary(Key);
 var current=JsonSerializer.SerializeToElement(zeroBoundary);int changed=0,survivalMessages=0;
 Check(original.GetArrayLength()==192&&positive.GetArrayLength()==64&&old.Count==256,"frozen boundary count");
 foreach(var row in current.EnumerateArray()){
  var prior=old[Key(row)];if(prior.GetProperty("hash").GetString()==row.GetProperty("hash").GetString())continue;
  var before=prior.GetProperty("result");var after=row.GetProperty("result");
  var normalized=JsonNode.Parse(after.GetRawText())!;var events=normalized["Battle"]!["Events"]!.AsArray();int removed=0;
  var survival=events.OfType<JsonObject>().Where(e=>e["kind"]!.GetValue<string>()=="held-item-survival").ToArray();
  // An independently integrated source-resultmessage fix can add this one typed
  // Focus Band event. Verify its exact applicability/order and account separately.
  if(survival.Length!=0){
   Check(survival.Length==1&&row.GetProperty("item").GetString()=="focus-band","only one documented Focus Band message");var message=survival[0];
   Check(message["Target"]!.GetValue<int>()==1&&message["Item"]!.GetValue<string>()=="ITEM_FOCUS_BAND","survival holder and item");
   var hp=events.OfType<JsonObject>().Single(e=>e["kind"]!.GetValue<string>()=="hp-changed");var adjustment=events.OfType<JsonObject>().Single(e=>e["kind"]!.GetValue<string>()=="source-damage-adjusted");
   Check(hp["HpAfter"]!.GetValue<int>()==1&&hp["Sequence"]!.GetValue<long>()<message["Sequence"]!.GetValue<long>()&&adjustment["HungOn"]!.GetValue<bool>()&&!adjustment["Endured"]!.GetValue<bool>(),"successful survival after HP only");
   events.Remove(message);removed++;survivalMessages++;
  }
  if(after.GetProperty("State").GetProperty("Opponent").GetProperty("Status").GetUInt32()!=before.GetProperty("State").GetProperty("Opponent").GetProperty("Status").GetUInt32()){
   changed++;Check(before.GetProperty("State").GetProperty("Opponent").GetProperty("Status").GetUInt32()==0&&after.GetProperty("State").GetProperty("Opponent").GetProperty("Status").GetUInt32()==WildBattleStatus.Freeze,"only unadjusted-effective freeze correction");
   Check(after.GetProperty("State").GetProperty("Opponent").GetProperty("Pokemon").GetProperty("CurrentHp").GetInt32()==1,"Endure/Focus Band preserve 1 HP");
   var status=events.OfType<JsonObject>().Single(e=>e["kind"]!.GetValue<string>()=="status-changed");Check(status["Side"]!.GetValue<int>()==1&&status["Before"]!.GetValue<uint>()==0&&status["After"]!.GetValue<uint>()==32&&status["Reason"]!.GetValue<string>()=="move","exact inserted freeze command");
   events.Remove(status);removed++;normalized["State"]!["Opponent"]!["Status"]=0;normalized["Battle"]!["State"]!["Opponent"]!["Status"]=0;
  }
  for(int i=0;i<events.Count;i++)events[i]!["Sequence"]=i+1;
  normalized["State"]!["EventSequence"]=normalized["State"]!["EventSequence"]!.GetValue<long>()-removed;
  normalized["Battle"]!["State"]!["EventSequence"]=normalized["Battle"]!["State"]!["EventSequence"]!.GetValue<long>()-removed;
  Check(removed>0&&JsonNode.DeepEquals(normalized,JsonNode.Parse(before.GetRawText())),"entire result unchanged beyond documented commands "+Key(row));
 }
 Check(changed==33,"exact source-backed zero-loss corrections");
 File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!,"zero-boundary-after.json"),J(new{changed,survivalMessages,rows=zeroBoundary}));
});
Test("four-slot Endure zero-loss still applies freeze and critical message",()=>{
 int frozen=0,critical=0;
 for(uint seed=0;seed<128;seed++){
  var f=Entry(Frame([Mon(),Mon(),Mon("LAPRAS","ICE_BEAM",35),Mon("SNORLAX","AMNESIA",100)],seed));var b=f.Slots[3].Body!;f=Body(f,3,b with{Enduring=true,Pokemon=b.Pokemon with{CurrentHp=1}});var r=Act(f,2,3);
  var damage=r.Events.OfType<SourceHpChangedEvent>().Single();Check(damage.HpAfter==1&&damage.Damage==0,"actual zero-loss hit");
  var roll=r.Trace.Single(x=>x.Purpose.EndsWith("secondary-effect"));uint expected=vectors[(0,0,roll.Raw%100)].GetProperty("status").GetUInt32();Check(r.State.Slots[3].Body!.Status==expected,"source effect after zero-loss adjustment");
  if(expected==WildBattleStatus.Freeze)frozen++;if(damage.Critical){critical++;Check(r.Events.OfType<SourceCriticalHitEvent>().Count()==1,"critical message survives zero-loss adjustment");}
 }
 Check(frozen>5&&critical>0,"nonempty zero-loss effect and critical coverage");
});
Test("all four authenticated natural moves run complete four-action turns",()=>{
 foreach(var (species,move) in fixtures){var f=Entry(Frame([Mon("DROWZEE","MEDITATE",30),Mon("SNORLAX","AMNESIA",80),Mon(species,move,35),Mon("SNORLAX","AMNESIA",80)]));var (r,e,t)=Finish(Begin(f));
  Check(e.OfType<SourceMoveUsedEvent>().Select(x=>x.Actor.Value).Concat(e.OfType<SourceActionBlockedEvent>().Select(x=>x.Side.Value)).Order().SequenceEqual(new[]{0,1,2,3}),"four real actors "+move);
  bool both=catalog.Moves[Id(move)].Target=="MOVE_TARGET_BOTH";var pp=e.OfType<SourcePpCommandEvent>().Where(x=>x.Actor.Value==2).ToArray();
  Check(pp.Length==(both?2:1)&&r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(move)].Pp-1,"actual PP owner "+move);
  var hitTargets=e.Where(x=>x is SourceHpChangedEvent { Actor.Value: 2 } or SourceMoveMissedEvent { Actor.Value: 2 }).Select(x=>x is SourceHpChangedEvent hp?hp.Target.Value:((SourceMoveMissedEvent)x).Target.Value);
  Check(hitTargets.SequenceEqual(both?new[]{3,1}:new[]{3}),"actual per-target hit or miss script "+move);
  Check(t.Count(x=>x.Purpose=="source-battle.slot-2.secondary-effect")==e.OfType<SourceHpChangedEvent>().Count(x=>x.Actor.Value==2),"only hit passes draw secondary RNG "+move);
 }
});
Test("source chance and immunity vectors through actual four-slot damage actions",()=>{
 Check(vectors.Count==2400&&sourceVectors.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"nonempty authenticated C vectors");int compared=0,applied=0;
 foreach(int mode in Enumerable.Range(0,8))foreach(bool serene in new[]{false})for(uint seed=0;seed<96;seed++){
  var target=mode switch{1=>Mon("LAPRAS","MIST",100),2=>Mon("SLUGMA","AMNESIA",100),3=>Mon("CATERPIE","STRING_SHOT",100),7=>Mon("RATTATA","TAIL_WHIP",100),_=>Mon("SNORLAX","AMNESIA",100)};
  if(mode==7)target=target with{Status=WildBattleStatus.Poison};
  var actor=serene?Mon("BLISSEY","ICE_BEAM",35,1):Mon("LAPRAS","ICE_BEAM",35);
  var f=Entry(Frame([mode==6?Mon("PSYDUCK","TAIL_WHIP",70,1):Mon(),Mon(),actor,target],seed));
  if(mode==4)f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{SafeguardTurnsRemaining=5})};
  if(mode is 5 or 6)f=f with{Field=f.Field with{SunTurnsRemaining=5}};
  var r=Act(f,2,3);var secondary=r.Trace.Single(x=>x.Purpose.EndsWith("secondary-effect"));var expected=vectors[(mode,serene?1:0,secondary.Raw%100)];
  Check(r.State.Slots[3].Body!.Status==expected.GetProperty("status").GetUInt32(),"C effect vector "+mode+"/"+serene+"/"+seed);
  Check(r.State.Party(BattleSide.Opponent).Members[1].Status==r.State.Slots[3].Body!.Status,"actual party target status sync");
  Check(r.Trace.Take(4).Select(x=>x.Purpose.Split('.').Last()).SequenceEqual(new[]{"accuracy","critical","damage","secondary-effect"}),"ordered hit/secondary command RNG");
  Check(r.Events.OfType<SourcePpCommandEvent>().Single().RngCursor==f.Rng.Cursor+1,"accuracy before PP");
  if(r.State.Slots[3].Body!.Status==WildBattleStatus.Freeze)applied++;compared++;
 }
 Check(compared==768&&applied>10,"nonempty effect and immunity coverage: "+applied);
});
Test("BOTH targets preserve independent status rolls and two Pressure holder PP",()=>{
 int asymmetric=0;
 for(uint seed=0;seed<96;seed++){
  var f=Entry(Frame([Mon(),Mon("LUGIA","RECOVER",100),Mon("JYNX","POWDER_SNOW",35),Mon("LUGIA","RECOVER",100)],seed));var r=Act(f,2,3);
  var rolls=r.Trace.Where(x=>x.Purpose.EndsWith("secondary-effect")).ToArray();Check(rolls.Length==2,"two independent per-target rolls");
  for(int pass=0;pass<2;pass++){int target=pass==0?3:1;uint expected=vectors[(0,0,rolls[pass].Raw%100)].GetProperty("status").GetUInt32();Check(r.State.Slots[target].Body!.Status==expected,"per-pass status "+target);}
  var pp=r.Events.OfType<SourcePpCommandEvent>().ToArray();Check(pp.Length==2&&pp[0].CommandCost==3&&pp[0].Before-pp[0].After==3&&pp[1].Before==pp[1].After,"first pass charges two Pressure holders only");
  Check(r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==22,"one three-PP debit");
  if(r.State.Slots[1].Body!.Status!=r.State.Slots[3].Body!.Status)asymmetric++;
 }
 Check(asymmetric>5,"independent outcomes proven");
});
Test("selected ally freezes only its actual member and Aspear cures per target",()=>{
 int frozen=0,cured=0;
 for(uint seed=0;seed<96;seed++){
  var f=Entry(Frame([Mon("SNORLAX","AMNESIA",100),Mon(),Mon("LAPRAS","ICE_BEAM",35),Mon("SNORLAX","AMNESIA",100)],seed));var r=Act(f,2,0);
  Check(r.State.Slots[3].Body!.Status==0&&r.State.Party(BattleSide.Opponent).Members[1].Status==0,"equal species opponent untouched");
  if(r.State.Slots[0].Body!.Status==WildBattleStatus.Freeze){frozen++;Check(r.State.Party(BattleSide.Player).Members[0].Status==WildBattleStatus.Freeze,"selected ally sync");}
  f=Body(f,0,f.Slots[0].Body! with{HeldItem="ITEM_ASPEAR_BERRY"});r=Act(f,2,0);
  if(r.Events.OfType<SourceStatusChangedEvent>().Any(x=>x.After==WildBattleStatus.Freeze)){cured++;Check(r.State.Slots[0].Body!.Status==0&&r.State.Slots[0].Body!.HeldItem=="ITEM_NONE"&&r.State.Party(BattleSide.Player).Members[0].HeldItem=="ITEM_NONE","Aspear active and party cure");}
 }
 Check(frozen==cured&&frozen>5,"same infliction rolls, move-end cures");
});
Test("freeze cancels an actual retained charge before publishing status",()=>{
 int found=0;
 for(uint seed=0;seed<96;seed++){
  var f=Entry(Frame([Mon(),Mon(),Mon("LAPRAS","ICE_BEAM",35),Mon("SNORLAX","SOLAR_BEAM",100)],seed));var target=f.Slots[3];
  var locked=new SourceTurnLock(WildBattleCalledLockKind.Charge,Id("SOLAR_BEAM"),0,Id("SOLAR_BEAM"),new(2),1,0,null,false);
  f=f with{Slots=f.Slots.SetItem(3,target with{Registers=target.Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,locked,0,null)}})};
  var r=Act(f,2,3);if(r.State.Slots[3].Body!.Status!=WildBattleStatus.Freeze)continue;found++;
  Check(r.State.Slots[3].Registers.Turn is null,"charge cancelled");
  Check(r.Events.OfType<SourceMtMoonVolatileEvent>().Single(x=>x.Reason=="freeze").Sequence<r.Events.OfType<SourceStatusChangedEvent>().Single(x=>x.After==WildBattleStatus.Freeze).Sequence,"cancel before status");
 }
 Check(found>5,"nonempty freeze charge cancellation");
});
Test("real KO turn preserves second BOTH pass and rejects stale waits",()=>{
 var f=Entry(Frame([Mon("DROWZEE","MEDITATE",100),Mon("SNORLAX","AMNESIA",100),Mon("JYNX","POWDER_SNOW",100),Mon("SNORLAX","AMNESIA",100)],31));f=Body(f,3,f.Slots[3].Body! with{Pokemon=f.Slots[3].Body!.Pokemon with{CurrentHp=1}});
 var r=Begin(f);var events=r.Events.ToBuilder();var traces=r.Trace.ToBuilder();bool sawReward=false;
 for(int step=0;step<30&&r.State.FourTurn is not null;step++){
  var old=r.Continuation!.Value;var oldState=r.State;r=Next(r);events.AddRange(r.Events);traces.AddRange(r.Trace);
  sawReward|=r.State.FourTurn?.Phase==SourceFourActionPhase.RewardsPending;
  Atomic(r.State,SourceFourActionTurns.Advance(catalog,r.State,old),"old continuation cannot replay action/reward");
 }
 Check(r.CompletedCleanTurn&&sawReward,"real turn reaches and leaves reward wait");turns++;
 var hp=events.OfType<SourceHpChangedEvent>().Where(x=>x.Actor.Value==2).ToArray();Check(hp.Select(x=>x.Target.Value).SequenceEqual(new[]{3,1})&&hp[0].HpAfter==0&&hp[1].HpAfter>0,"KO first pass and live second pass");
 Check(!events.OfType<SourceStatusChangedEvent>().Any(x=>x.Side.Value==3&&x.After==WildBattleStatus.Freeze),"no status infliction on fainted target");
 Check(!events.OfType<SourceMoveUsedEvent>().Any(x=>x.Actor.Value==3),"departed queued action cancelled");
 Check(r.State.Party(BattleSide.Opponent).Members[1].Pokemon.CurrentHp==0&&r.State.Party(BattleSide.Opponent).Members[1].Status==0,"permanent KO sync");
 Check(traces.Count(x=>x.Purpose=="source-battle.slot-2.secondary-effect")==2,"secondary RNG remains on lethal first pass");
});
Test("actual frozen actors retain PP until thaw and later actors continue",()=>{
 int blocked=0,thawed=0;
 for(uint seed=0;seed<32&&(blocked==0||thawed==0);seed++){
  var f=Entry(Frame([Mon("DROWZEE","MEDITATE",30),Mon("SNORLAX","AMNESIA",100),Mon("JYNX","POWDER_SNOW",35),Mon("SNORLAX","AMNESIA",100)],seed));f=Body(f,2,f.Slots[2].Body! with{Status=WildBattleStatus.Freeze});
  var (r,e,t)=Finish(Begin(f));var freeze=t.Single(x=>x.Purpose=="source-battle.slot-2.freeze-thaw");bool prevented=freeze.Raw%5!=0;
  Check(e.OfType<SourceActionBlockedEvent>().Any(x=>x.Side.Value==2)==prevented,"freeze draw owns actual cancellation");
  Check(r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==(prevented?25:24),"frozen actor PP command boundary");
  Check(e.OfType<SourceMoveUsedEvent>().Count(x=>x.Actor.Value!=2)==3,"other queued actors execute");
  if(prevented)blocked++;else thawed++;
 }
 Check(blocked>0&&thawed>0,"both real-turn freeze outcomes");
});
Test("Ice Punch contact draws follow freeze chance and target status",()=>{
 int frozen=0,paralyzed=0;
 for(uint seed=0;seed<96;seed++){
  var f=Entry(Frame([Mon(),Mon(),Mon("JYNX","ICE_PUNCH",35),Mon("PIKACHU","AGILITY",100)],seed));var r=Act(f,2,3);
  int secondary=Array.FindIndex(r.Trace.ToArray(),x=>x.Purpose.EndsWith("secondary-effect")),contact=Array.FindIndex(r.Trace.ToArray(),x=>x.Purpose.EndsWith(".static"));
  Check(secondary==3&&contact==4,"source secondary precedes Static contact RNG");
  if(r.State.Slots[3].Body!.Status==WildBattleStatus.Freeze)frozen++;
  if(r.State.Slots[2].Body!.Status==WildBattleStatus.Paralysis){paralyzed++;Check(r.State.Party(BattleSide.Player).Members[1].Status==WildBattleStatus.Paralysis,"contact actor party sync");}
 }
 Check(frozen>5&&paralyzed>10,"nonempty freeze/contact coverage");
});
Test("shared thaw and Tri Attack admission retains atomic stale rejection",()=>{
 foreach(var fixture in new[]{("DODRIO","TRI_ATTACK"),("GROWLITHE","FLAME_WHEEL"),("HO_OH","SACRED_FIRE")}){
  var f=Entry(Frame([Mon(),Mon(),Mon(fixture.Item1,fixture.Item2,100),Mon()]));var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision,0,new(3)));
  Check(r.Succeeded,"shared effect admitted "+fixture.Item2);
  var stale=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[2].Occupant!.Value,f.Revision-1,0,new(3)));
  Check(!stale.Succeeded&&ReferenceEquals(stale.State,f)&&stale.Trace.IsEmpty&&stale.Events.IsEmpty,"stale shared effect remains atomic "+fixture.Item2);
 }
 var fire=Entry(Frame([Mon(),Mon(),Mon("CHARMANDER","EMBER"),Mon()]));fire=Body(fire,3,fire.Slots[3].Body! with{Status=WildBattleStatus.Freeze});var denied=SourceBattleKernel.ResolveAction(catalog,fire,new(fire.Slots[2].Occupant!.Value,fire.Revision,0,new(3)));
 Check(denied.Succeeded&&denied.Events.OfType<SourceStatusChangedEvent>().Any(x=>x.Side.Value==3&&x.Reason=="fire-hit-thaw"),"shared Fire thaw replaces the former global gate");
});
var report=new{passed=failures==0&&groups>0,groups,checks,failures,turns,singlesBaseline=baseline.Count,sourceVectors=vectors.Count,runtime=Environment.Version.ToString()};File.WriteAllText(output,J(report));Console.WriteLine(J(report));return failures==0&&groups>0?0:1;
