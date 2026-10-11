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
 foreach(string move in moves.Split(','))Check(move=="BLAST_BURN"&&species=="CHARIZARD"||catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(move)&&x.Level<=level)||Regex.IsMatch(block,@"[TH]M\d\d_"+move+@"\)"),"authentic natural/TM move "+species+"/"+move);
 return new(p with{Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});
}
WildBattleState Pair(WildBattleCombatant a,WildBattleCombatant b,uint seed=31)=>new(a,b,0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
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


var rocket=PretRocketHideoutPokemon.Load(root).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(root,PretCeladonApproachPokemon.Load(root).Catalog,celadonApproachRules:true);
var tower=PretPokemonTowerPokemon.Load(rocket with{Moves=rocket.Moves.SetItems(supplement.Moves)},root,root).Catalog;
var legacyCatalog=PretMetronomeMoveCatalog.Load(root).Extend(PretFuchsiaRoutePokemon.Load(PretEeveePokemon.Load(PretSnorlaxPokemon.Load(tower,root).Catalog,root).Catalog,root).Catalog);
WildBattleCombatant LegacyMon(string species,string move,int level,uint pid=0){var p=PokemonFactory.Create(legacyCatalog,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;return new(p with{Moves=[new(Id(move),legacyCatalog.Moves[Id(move)].Pp,legacyCatalog.Moves[Id(move)].Pp)]});}
var fixtures=new[]{("BLAZIKEN","BLAZE_KICK"),("SEVIPER","POISON_TAIL"),("GOLBAT","POISON_FANG")};
var baseline=new List<object>();
foreach(bool canonical in new[]{true,false})foreach(var (species,move) in fixtures)foreach(int variant in Enumerable.Range(0,12))foreach(uint seed in new uint[]{0,1,4,13,31,60,61,63}){
 var a=Mon(species,move,50);var b=Mon("SNORLAX","AMNESIA",100,1);
 if(variant==1)b=b with{Protected=true};
 if(variant==2)b=b with{Status=WildBattleStatus.Poison};
 if(variant==3)b=Mon("CATERPIE","STRING_SHOT",100);
 if(variant==4)b=b with{CalledVolatile=new(){SubstituteHp=1}};
 if(variant==5)b=b with{Pokemon=b.Pokemon with{CurrentHp=1}};
 if(variant==6)b=b with{Pokemon=b.Pokemon with{CurrentHp=1},Enduring=true};
 if(variant==7)b=b with{Pokemon=b.Pokemon with{CurrentHp=1},HeldItem="ITEM_FOCUS_BAND"};
 if(variant==8)b=Mon("GENGAR","SPITE",100);
 if(variant==9)b=b with{HeldItem="ITEM_PECHA_BERRY"};
 if(variant==10)b=b with{Status=WildBattleStatus.Freeze};
 if(!canonical&&variant==7)b=b with{HeldItem="ITEM_NONE"};
 if(!canonical){a=LegacyMon("CLEFABLE","METRONOME",50);var lb=legacyCatalog.Species.ContainsKey(b.Pokemon.Species)?LegacyMon(b.Pokemon.Species.Value[13..],b.Pokemon.Moves[0].Move.Value[10..],100,b.Pokemon.Personality):LegacyMon("KOFFING","SMOG",100);b=b with{Pokemon=lb.Pokemon with{CurrentHp=b.Pokemon.CurrentHp==1?1:lb.Pokemon.Stats.MaxHp}};}
 var s=Pair(a,b,seed);if(!canonical)s=s with{SourceMechanicsRulesVersion=null};
 if(variant==11)s=s with{CalledField=new(){OpponentSafeguardTurnsRemaining=4}};
 var r=canonical?WildBattleRules.ResolveSourceMoveAction(catalog,s,new(WildBattleSide.Player,0)):WildBattleRules.ResolveCalledMoveAction(legacyCatalog,s,new(WildBattleSide.Player,0,Id(move)));Check(r.Succeeded,"baseline "+canonical+"/"+move+"/"+variant+" "+r.Fault);
 baseline.Add(new{canonical,move,variant,seed,hash=Hash(r),result=r});
}
string baselinePath="tests/Game.SharedCriticalStatus.Tests/singles-baseline-db8579a.json";
if(output=="--baseline"){
 Check(!File.Exists(baselinePath),"immutable baseline refuses overwrite");File.WriteAllText(baselinePath,J(baseline));
 using var doc=JsonDocument.Parse(File.ReadAllText(baselinePath));Check(doc.RootElement.GetArrayLength()==576,"baseline count");Console.WriteLine(J(new{baseline=baseline.Count,checks}));return 0;
}
int corrections=0,compared=0;
Test("immutable canonical and historical complete-result corpus",()=>{
 var old=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline-db8579a.json"))).RootElement;
 var current=JsonSerializer.SerializeToElement(baseline);Check(old.GetArrayLength()==576,"immutable corpus count");
 foreach(var (a,b) in old.EnumerateArray().Zip(current.EnumerateArray())){
  if(a.GetProperty("hash").GetString()==b.GetProperty("hash").GetString())continue;
  Check(a.GetProperty("canonical").GetBoolean()&&a.GetProperty("move").GetString()=="POISON_FANG"&&a.GetProperty("variant").GetInt32() is 6 or 7,"bounded zero-loss Toxic only");
  var before=JsonNode.Parse(a.GetProperty("result").GetRawText())!;var after=JsonNode.Parse(b.GetProperty("result").GetRawText())!;
  Check(after["State"]!["Opponent"]!["Status"]!.GetValue<uint>()==128&&before["State"]!["Opponent"]!["Status"]!.GetValue<uint>()==0,"only previously omitted Toxic");
  Check(after["State"]!["Opponent"]!["Pokemon"]!["CurrentHp"]!.GetValue<int>()==1,"one HP unchanged");
  var events=after["Battle"]!["Events"]!.AsArray();var effect=events.OfType<JsonObject>().Single(e=>e["kind"]!.GetValue<string>()=="status-changed");
  Check(effect["Side"]!.GetValue<int>()==1&&effect["After"]!.GetValue<uint>()==128&&effect["Reason"]!.GetValue<string>()=="move","exact Toxic command");events.Remove(effect);
  foreach(string prop in new[]{"State","Battle"}){var state=prop=="State"?after["State"]!:after["Battle"]!["State"]!;state["Opponent"]!["Status"]=0;state["EventSequence"]=state["EventSequence"]!.GetValue<long>()-1;state.AsObject().Remove("CalledField");}
  for(int i=0;i<events.Count;i++)events[i]!["Sequence"]=i+1;
  Check(JsonNode.DeepEquals(before,after),"entire result/RNG unchanged outside Toxic command");corrections++;
 }
 Check(corrections>0,"correction actually reached");
});
using var vectorsJson=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
var vectors=vectorsJson.RootElement.GetProperty("vectors").EnumerateArray().ToDictionary(v=>(v.GetProperty("effect").GetInt32(),v.GetProperty("mode").GetInt32(),v.GetProperty("serene").GetInt32(),v.GetProperty("roll").GetInt32()));
Test("natural donors execute all real actor slots with source critical/RNG/PP identity",()=>{
 int critical=0,applied=0;
 for(int effect=0;effect<3;effect++)for(int actor=0;actor<4;actor++)for(uint seed=0;seed<48;seed++){
  var (species,move)=fixtures[effect];int target=actor^1;
  var bodies=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","AMNESIA",100,1)).ToArray();bodies[actor]=Mon(species,move,50);
  var f=Entry(Frame(bodies,seed));var r=Act(f,actor,target);var hp=r.Events.OfType<SourceHpChangedEvent>().SingleOrDefault();
  var pp=r.Events.OfType<SourcePpCommandEvent>().Single();Check(pp.Actor.Value==actor&&pp.Target.Value==target&&pp.PpOwner.Value==actor&&pp.Before-pp.After==1&&pp.RngCursor==f.Rng.Cursor+1,"source PP owner/order");
  if(hp is null){Check(move=="BLAZE_KICK"&&r.Trace.Length==1,"only Blaze accuracy miss skips damage/status");continue;}
  Check(r.Trace.Select(x=>x.Purpose.Split('.').Last()).SequenceEqual(new[]{"accuracy","critical","damage","secondary-effect"}),"source draw order");
  int denominator=effect==2?16:8;Check(hp.Critical==(r.Trace[1].Raw%denominator==0),"effect critical stage");if(hp.Critical)critical++;
  uint expected=vectors[(effect,0,0,r.Trace[3].Raw%100)].GetProperty("status").GetUInt32();Check(r.State.Slots[target].Body!.Status==expected,"C secondary result");
  Check(r.State.Party((BattleSide)(target&1)).Members[target/2].Status==expected,"actual member write");
  Check(r.State.Slots.Where(c=>c.Slot.Value!=actor&&c.Slot.Value!=target).All(c=>c.Body!.Status==0&&c.Body.Pokemon.CurrentHp==c.Body.Pokemon.Stats.MaxHp),"unrelated positions unchanged");if(expected!=0)applied++;compared++;
 }
 Check(critical>0&&applied>0&&compared>500,"nonempty all-slot corpus");
});
Test("source secondary immunity and Substitute gates",()=>{
 for(int effect=0;effect<3;effect++)foreach(int mode in new[]{1,2,3,4,5,6,7,8,9,10})for(uint seed=0;seed<48;seed++){
  var (species,move)=fixtures[effect];var b=mode switch{1=>Mon("CATERPIE","STRING_SHOT",100),6=>Mon("GENGAR","SPITE",100),7=>Mon("STEELIX","SCREECH",100),8=>Mon("SNORLAX","AMNESIA",100,0),9=>Mon("CHARIZARD","GROWL",100),10=>Mon("SEAKING","AGILITY",100,1),_=>Mon("SNORLAX","AMNESIA",100,1)};
  if(mode==3)b=b with{Status=WildBattleStatus.Poison};if(mode==4)b=b with{Pokemon=b.Pokemon with{CurrentHp=1}};if(mode==5)b=b with{CalledVolatile=new(){SubstituteHp=1}};
  var f=Entry(Frame([Mon(),Mon(),Mon(species,move,50),b],seed));if(mode==2)f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{SafeguardTurnsRemaining=4})};
  var r=Act(f,2,3);var roll=r.Trace.SingleOrDefault(x=>x.Purpose.EndsWith("secondary-effect"));if(roll is null)continue;
  uint expected=vectors[(effect,mode,0,roll.Raw%100)].GetProperty("status").GetUInt32();if(mode==4)expected=0;
  Check(r.State.Slots[3].Body!.Status==expected,$"C secondary gate {effect}/{mode}/{seed}: {r.State.Slots[3].Body!.Status} expected {expected}");compared++;
 }
});
Test("zero-loss Endure and Focus Band retain secondary chance without extra RNG",()=>{
 int applied=0,bands=0,critical=0;
 foreach(var (species,move) in fixtures)foreach(bool band in new[]{false,true})for(uint seed=0;seed<128;seed++){
  var b=Mon("SNORLAX","AMNESIA",100,1);b=b with{Pokemon=b.Pokemon with{CurrentHp=1},Enduring=!band,HeldItem=band?"ITEM_FOCUS_BAND":"ITEM_NONE"};
  var f=Entry(Frame([Mon(),Mon(),Mon(species,move,50),b],seed));var r=Act(f,2,3);var hp=r.Events.OfType<SourceHpChangedEvent>().SingleOrDefault();if(hp is null||hp.HpAfter==0)continue;
  Check(hp.Damage==0&&hp.HpAfter==1,"actual zero regular HP damage");var roll=r.Trace.Single(x=>x.Purpose.EndsWith("secondary-effect"));int effect=Array.FindIndex(fixtures,x=>x.Item2==move);uint expected=vectors[(effect,0,0,roll.Raw%100)].GetProperty("status").GetUInt32();Check(r.State.Slots[3].Body!.Status==expected,"effective hit applies secondary after zero adjustment");
  if(expected!=0)applied++;if(band)bands++;if(hp.Critical){critical++;Check(r.Events.OfType<SourceCriticalHitEvent>().Count()==1,"critical message on zero-loss hit");}
 }
 Check(applied>5&&bands>5&&critical>0,"successful status/survival/critical boundaries");
});
Test("Flash Fire and shared thaw before contact and all-battler berries",()=>{
 int thaw=0,contact=0;
 for(uint seed=0;seed<96;seed++){
  var a=Mon("BLAZIKEN","BLAZE_KICK",50);var b=Mon("GROWLITHE","AGILITY",100,1);Check(b.Pokemon.AbilityId=="ABILITY_FLASH_FIRE","natural Flash Fire");
  var f=Entry(Frame([Mon(),Mon(),a,b],seed));var r=Act(f,2,3);
  if(r.Events.OfType<SourceAbilityPreventedEvent>().Any(x=>x.Ability=="ABILITY_FLASH_FIRE")){Check(r.Trace.Length==1&&r.State.Slots[3].Body!.FlashFireBoost,"absorb before critical and secondary");}
  f=Body(f,3,b with{Status=WildBattleStatus.Freeze});r=Act(f,2,3);
  if(r.Events.OfType<SourceHpChangedEvent>().Any()){thaw++;Check(r.State.Slots[3].Body!.Status==0&&!r.State.Slots[3].Body!.FlashFireBoost,"frozen Flash Fire takes damage and thaws");}
  b=Mon("MAGMAR","SMOKESCREEN",100) with{Status=WildBattleStatus.Freeze,HeldItem="ITEM_ASPEAR_BERRY"};
  a=a with{HeldItem="ITEM_RAWST_BERRY",CalledOverlay=new(){Type1="TYPE_NORMAL",Type2="TYPE_NORMAL"}};
  f=Entry(Frame([Mon(),Mon(),a with{CalledOverlay=null},b],seed));f=Body(f,2,a);r=Act(f,2,3);var t=r.Events.OfType<SourceStatusChangedEvent>().SingleOrDefault(x=>x.Reason=="fire-hit-thaw");
  if(t is null)continue;Check(r.State.Slots[3].Body!.HeldItem=="ITEM_ASPEAR_BERRY","thaw runs before item cure");
  var burn=r.Events.OfType<SourceStatusChangedEvent>().SingleOrDefault(x=>x.Reason=="flame-body");if(burn is null)continue;contact++;
  Check(t.Sequence<burn.Sequence&&r.State.Slots[2].Body!.Status==0&&r.State.Slots[2].Body!.HeldItem=="ITEM_NONE","thaw then contact burn then Rawst");
 }
 Check(thaw>0&&contact>0,"thaw/contact branches reached");
});
Test("Poison Fang no-effect suppresses Poison Point only after rolled toxic rejection",()=>{
 int skipped=0,drawn=0;
 for(uint seed=0;seed<96;seed++){
  var f=Entry(Frame([Mon(),Mon(),Mon("GOLBAT","POISON_FANG",50),Mon("NIDOKING","FOCUS_ENERGY",100)],seed));var r=Act(f,2,3);var roll=r.Trace.Single(x=>x.Purpose.EndsWith("secondary-effect"));
  bool toxic=roll.Raw%100<=30,poisonDraw=r.Trace.Any(x=>x.Purpose.EndsWith("poison-point"));
  Check(poisonDraw==!toxic,"source no-effect retained through contact");Check(r.Events.OfType<SourceEffectivenessEvent>().Any(x=>x.Effectiveness==WildBattleEffectiveness.NoEffect)==toxic,"toxic type rejection result");if(toxic)skipped++;else drawn++;
 }
 Check(skipped>0&&drawn>0,"both contact gates");
});
Test("Poison Fang retained no-effect also gates Static and Shell Bell",()=>{
 int rejected=0,ordinary=0;
 for(uint seed=0;seed<64;seed++){
  var a=Mon("GOLBAT","POISON_FANG",50);a=a with{Pokemon=a.Pokemon with{CurrentHp=a.Pokemon.Stats.MaxHp/2},HeldItem="ITEM_SHELL_BELL"};
  var b=Mon("PIKACHU","AGILITY",100);var f=Entry(Frame([Mon(),Mon(),a,b],seed));f=Body(f,3,b with{CalledOverlay=new(){Type1="TYPE_POISON",Type2="TYPE_POISON"}});
  var r=Act(f,2,3);bool noEffect=r.Trace.Single(x=>x.Purpose.EndsWith("secondary-effect")).Raw%100<=30;
  Check(r.Trace.Any(x=>x.Purpose.EndsWith("static"))==!noEffect,"original Static NO_EFFECT precheck");
  Check((r.State.Slots[2].Body!.Pokemon.CurrentHp>a.Pokemon.CurrentHp)==!noEffect,"Shell Bell sees retained Toxic result flag");if(noEffect)rejected++;else ordinary++;
 }
 Check(rejected>0&&ordinary>0,"both Toxic result branches");
});
Test("toxic Synchronize becomes ordinary poison and Pecha cures actual recipients",()=>{
 int synchronized=0,cured=0;
 for(uint seed=0;seed<96;seed++){
  var a=Mon("GOLBAT","POISON_FANG",50) with{CalledOverlay=new(){Type1="TYPE_NORMAL",Type2="TYPE_NORMAL"}};var b=Mon("UMBREON","SCREECH",100);
  var f=Entry(Frame([Mon(),Mon(),a with{CalledOverlay=null},b],seed));f=Body(f,2,a);var r=Act(f,2,3);if(r.State.Slots[3].Body!.Status!=128)continue;synchronized++;
  Check(r.State.Slots[2].Body!.Status==8&&r.State.Party(BattleSide.Player).Members[1].Status==8,"Synchronize ordinary poison on actual attacker");
  f=Body(f,2,a with{HeldItem="ITEM_PECHA_BERRY"});f=Body(f,3,b with{HeldItem="ITEM_PECHA_BERRY"});r=Act(f,2,3);cured++;
  Check(r.State.Slots[2].Body!.Status==0&&r.State.Slots[3].Body!.Status==0&&r.State.Party(BattleSide.Player).Members[1].HeldItem=="ITEM_NONE"&&r.State.Party(BattleSide.Opponent).Members[1].HeldItem=="ITEM_NONE","both actual synchronized berry cures");
 }
 Check(synchronized>5&&cured==synchronized,"synchronize corpus reached");
});
Test("complete turns retain Toxic live counter and permanent identity across JSON and switch",()=>{
 int toxic=0;
 foreach(var (species,move) in fixtures)for(uint seed=0;seed<12;seed++){
  var f=Entry(Frame([Mon("DROWZEE","MEDITATE",50),Mon("SNORLAX","AMNESIA",100,1),Mon(species,move,50),Mon("SNORLAX","AMNESIA",100,1)],seed));
  var begin=Begin(f);var restored=JsonSerializer.Deserialize<SourceBattleFrame>(J(begin.State))!;Check(J(Next(begin))==J(Good(SourceFourActionTurns.Advance(catalog,restored,begin.Continuation!.Value))),"exact serialized action continuation");
  var (r,e,t)=Finish(begin);Check(e.OfType<SourceMoveUsedEvent>().Select(x=>x.Actor.Value).Order().SequenceEqual(new[]{0,1,2,3}),"four genuine executed actors");
  if(move!="POISON_FANG"||r.State.Slots[3].Body!.Status!=0x180)continue;toxic++;
  Check(r.State.Party(BattleSide.Opponent).Members[1].Status==128,"live toxic counter never persists");
  var next=r.State;var reserve=Mon("SNORLAX","AMNESIA",100,1);var party=next.Party(BattleSide.Opponent);next=next with{Parties=next.Parties.SetItem(1,party with{Members=party.Members.Add(Member(reserve))})};
  var choices=Choices(next).SetItem(3,new(next.Slots[3].Occupant!.Value,SourceActionKind.Switch){Incoming=new(BattleSide.Opponent,2)});
  var sw=Good(SourceFourActionTurns.BeginActions(catalog,next,next.Revision,choices));var done=Finish(sw).Result;Check(done.State.Party(BattleSide.Opponent).Members[1].Status==128&&done.State.Slots[3].Member!.Value.Index==2,"outgoing Toxic stays with party identity");
 }
 Check(toxic>0,"live/persistent toxic turn reached");
});
Test("lethal donor hits defer replacement and resume the exact serialized four-turn token",()=>{
 foreach(var (species,move) in fixtures){
  var victim=Mon("DROWZEE","MEDITATE",50);victim=victim with{Pokemon=victim.Pokemon with{CurrentHp=1}};
  var f=Frame([victim,Mon(species,move,100),Mon(),Mon("SNORLAX","AMNESIA",100,1)],31);
  var reserve=Mon("SNORLAX","AMNESIA",100,1);f=f with{Parties=f.Parties.SetItem(0,f.Parties[0] with{Members=f.Parties[0].Members.Add(Member(reserve))}),Rewards=f.Rewards! with{Metadata=f.Rewards!.Metadata.Add(new(new(BattleSide.Player,2),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected))}};
  f=Entry(f);var r=Begin(f);for(int i=0;i<12&&r.State.FourTurn?.Phase!=SourceFourActionPhase.ReplacementsPending;i++)r=Next(r);
  Check(r.State.FourTurn?.Phase==SourceFourActionPhase.ReplacementsPending&&r.State.Slots[0].Body!.Pokemon.CurrentHp==0,"actual endturn KO replacement wait "+move);
  var token=r.Continuation!.Value;var copy=JsonSerializer.Deserialize<SourceBattleFrame>(J(r.State))!;
  var replaced=Good(SourceFourActionTurns.ChooseReplacement(catalog,r.State,token,new(0),new(BattleSide.Player,2)));
  var resumed=Good(SourceFourActionTurns.ChooseReplacement(catalog,copy,token,new(0),new(BattleSide.Player,2)));
  Check(J(replaced)==J(resumed)&&replaced.State.Slots[0].Member!.Value.Index==2,"exact replacement JSON/resident identity");
  Atomic(replaced.State,SourceFourActionTurns.ChooseReplacement(catalog,replaced.State,token,new(0),new(BattleSide.Player,2)),"stale replacement token");Finish(replaced);
 }
});
Test("selected allies and Pressure retain real target and party owner",()=>{
 int applied=0;
 foreach(var (species,move) in fixtures)for(uint seed=0;seed<24;seed++){
  var f=Entry(Frame([Mon("SNORLAX","AMNESIA",100,1),Mon("LUGIA","RECOVER",100),Mon(species,move,50),Mon("LUGIA","RECOVER",100)],seed));
  var r=Act(f,2,0);Check(r.State.Slots[1].Body!.Status==0&&r.State.Slots[3].Body!.Status==0,"unselected pressure foes untouched");
  if(r.State.Slots[0].Body!.Status!=0){applied++;Check(r.State.Party(BattleSide.Player).Members[0].Status==r.State.Slots[0].Body!.Status,"selected ally status identity");}
  r=Act(f,2,3);Check(r.Events.OfType<SourcePpCommandEvent>().Single().Before-r.Events.OfType<SourcePpCommandEvent>().Single().After==2,"only selected Pressure holder debited");
 }
 Check(applied>0,"ally effects reached");
});
Test("remaining naturally learned Poison Fang donors execute in every actual slot",()=>{
 foreach(string species in new[]{"ZUBAT","CROBAT","SEVIPER"})for(int actor=0;actor<4;actor++){
  var bodies=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","AMNESIA",100,1)).ToArray();bodies[actor]=Mon(species,"POISON_FANG",50);
  var f=Entry(Frame(bodies,31));var r=Act(f,actor,actor^1);Check(r.Events.OfType<SourceHpChangedEvent>().Single().Actor.Value==actor,"actual donor actor "+species);
 }
});
Test("protection preserves source PP timing and existing Focus Energy critical stages",()=>{
 foreach(var (species,move) in fixtures){
  var a=Mon(species,move,50) with{FocusEnergy=true};var b=Mon("SNORLAX","AMNESIA",100,1);
  var f=Entry(Frame([Mon(),Mon(),a,b],31));var blocked=Act(Body(f,3,b with{Protected=true}),2,3);
  Check(blocked.Trace.IsEmpty&&blocked.Events.OfType<SourcePpCommandEvent>().Single().RngCursor==f.Rng.Cursor,"protect precedes RNG but spends PP");
  for(uint seed=0;seed<24;seed++){var r=Act(f with{Rng=new(seed)},2,3);var hp=r.Events.OfType<SourceHpChangedEvent>().SingleOrDefault();if(hp is null)continue;int d=move=="POISON_FANG"?4:3;Check(hp.Critical==(r.Trace.Single(x=>x.Purpose.EndsWith("critical")).Raw%d==0),"existing focus/scope/crit effect hook");}
 }
});
Test("atomic admission does not expand Serene Grace or changed source definitions",()=>{
 var a=Mon("BLISSEY","POUND",50,1);Check(a.Pokemon.AbilityId=="ABILITY_SERENE_GRACE","natural excluded ability");var f=Frame([a,Mon(),Mon(),Mon()]);
 var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1)));Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"Serene Grace stays closed");
 foreach(var (_,move) in fixtures)Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(move)] with{SecondaryEffectChance=100}),"forged source remains closed");
});
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output,J(new{passed=failures==0,groups,checks,turns,baseline=baseline.Count,corrections,compared,recipeAdmission=catalog.Moves.Values.Count(SourceBattleKernel.SupportsRecipe),admittedMoves=catalog.Moves.Values.Where(SourceBattleKernel.SupportsRecipe).Select(m=>m.Id.Value).Order().ToArray()}));using(var parsed=JsonDocument.Parse(File.ReadAllText(output)))Check(parsed.RootElement.GetProperty("checks").GetInt32()>0,"nonempty report");Console.WriteLine(File.ReadAllText(output));return failures==0?0:1;
