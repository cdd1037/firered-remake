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
var legacy=PretRoute3Pokemon.LoadContent(File.ReadAllText(Path.Combine(root,"content/generated/route1-pokemon/catalog.json")),File.ReadAllText(Path.Combine(root,"content/generated/old-man-tutorial/catalog.json")),File.ReadAllText(Path.Combine(root,"content/generated/forest-pokemon/catalog.json")),File.ReadAllText(Path.Combine(root,"content/generated/pewter-pokemon/catalog.json")),File.ReadAllText(Path.Combine(root,"content/generated/route22-pokemon/catalog.json")),File.ReadAllText(Path.Combine(root,"content/generated/route3-pokemon/catalog.json"))).Catalog;
PokemonInstance Old(string species,string name,uint pid)=>PokemonFactory.Create(legacy,new(new("pret:SPECIES_"+species),20,1,IvPolicy.Fixed(0),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon with{Moves=[new(Id(name),legacy.Moves[Id(name)].Pp,legacy.Moves[Id(name)].Pp)]};
var hashes=new List<object>();
foreach(uint seed in new uint[]{0,1,17,31,38})foreach(int variant in Enumerable.Range(0,6)) {
 var p=new WildBattleCombatant(Old("RATICATE",variant==2?"ROLLOUT":"TACKLE",200));var o=new WildBattleCombatant(Old("JIGGLYPUFF","DEFENSE_CURL",variant==4?200u:0));
 if(variant is 1 or 2)p=p with{InfatuationSource=WildBattleSide.Opponent};if(variant==2)p=p with{RolloutMoveSlot=0,RolloutTurnsRemaining=3};if(variant==3)o=o with{Pokemon=o.Pokemon with{CurrentHp=1}};if(variant==5)p=p with{Status=WildBattleStatus.Paralysis};
 var s=new WildBattleState(p,o,2,4,10,new(seed),123,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.Route3RulesVersion);
 var r=WildBattleRules.ResolveMove(legacy,s,0);if(!r.Succeeded)throw new Exception("legacy "+r.Fault);hashes.Add(new{seed,variant,hash=Hash(r)});
}
if(output=="--legacy"){Console.WriteLine(Json(hashes));return 0;}
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(root);var catalog=proof.Catalog;
string tm=File.ReadAllText(Path.Combine(source,"src/data/pokemon/tmhm_learnsets.h"));
int groups=0,checks=0,failures=0,vectors=0,turns=0;var results=new List<object>();
void Check(bool ok,string reason){checks++;if(!ok)throw new InvalidDataException(reason);}
void Test(string name,Action run){groups++;try{run();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+" "+e);}}
WildBattleCombatant Mon(string species,string move="TACKLE",uint pid=200,int level=50){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 var block=Regex.Match(tm,@"\[SPECIES_"+species+@"\].*?(?=\[SPECIES_|\z)",RegexOptions.Singleline).Value;
 Check(catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(move)&&x.Level<=level)||Regex.IsMatch(block,@"[TH]M\d\d_"+move+@"\)"),"authentic source move "+species+"/"+move);
 return new(p with{Moves=[new(Id(move),catalog.Moves[Id(move)].Pp,catalog.Moves[Id(move)].Pp)]});
}
WildBattleCalledPartyMember Member(WildBattleCombatant b)=>new(b.Pokemon,b.HeldItem,b.Status);
SourceBattleFrame Frame(uint seed=0){WildBattleCombatant[] b=[Mon("RATICATE"),Mon("CLEFAIRY","CALM_MIND",0),Mon("RATICATE",pid:202),Mon("JIGGLYPUFF","LIGHT_SCREEN",0)];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(b[0]),Member(b[2])]),new(BattleSide.Opponent,[Member(b[1]),Member(b[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,b[i])).ToImmutableArray()};}
SourceBattleFrame Rebind(SourceBattleFrame f,int i,WildBattleCombatant b){var c=f.Slots[i];var key=c.Member!.Value;var p=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=b}),Parties=f.Parties.SetItem((int)key.Side,p with{Members=p.Members.SetItem(key.Index,Member(b))})};}
SourceBattleFrame Hp(SourceBattleFrame f,int i,int hp)=>Rebind(f,i,f.Slots[i].Body! with{Pokemon=f.Slots[i].Body!.Pokemon with{CurrentHp=hp}});
SourceBattleFrame Love(SourceBattleFrame f,int i,int? sourceSlot)=>f with{Slots=f.Slots.SetItem(i,f.Slots[i] with{Registers=f.Slots[i].Registers with{InfatuationSource=sourceSlot is{}s?new(s):null}})};
SourceBattleFrame Receipt(SourceBattleFrame f,int i,SourceHitDamageReceipt r)=>f with{Slots=f.Slots.SetItem(i,f.Slots[i] with{Position=f.Slots[i].Position with{HitItemReceipt=r==new SourceHitDamageReceipt()?null:r}})};
SourceKernelResult Act(SourceBattleFrame f,int a,int t){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[a].Occupant!.Value,f.Revision,0,new(t)));Check(r.Succeeded,r.Fault??"accepted");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid resulting state");return r;}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"four turn accepted");Check(SourceBattleIdentityRules.Validate(r.State)is null,"four identity");return r;}
SourceFourActionResult Next(SourceFourActionResult r)=>Good(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
SourceFourActionResult Begin(SourceBattleFrame f)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(c.Slot.Value switch{0=>3,2=>1,1=>1,_=>3}))).ToImmutableArray()));
SourceBattleFrame Rewards(SourceBattleFrame f)=>f with{Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=[new(new(BattleSide.Player,0),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected),new(new(BattleSide.Player,1),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)]}};
var asm=typeof(SourceBattleKernel).Assembly;var ct=asm.GetType("Game.Domain.Battle.FrameRecipeContext")!;var shared=ct.BaseType!;const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
object Context(SourceBattleFrame f,int a,int t)=>Activator.CreateInstance(ct,flags,null,[catalog,f,new SourceResolvedMoveAction(f.Slots[a].Occupant!.Value,f.Revision,0,new(t)),null],null)!;
SourceBattleFrame ContextState(object context)=>(SourceBattleFrame)ct.GetProperty("Frame",flags)!.GetValue(context)!;
long[] Oracle(params object[] inputs){var pi=new ProcessStartInfo(Path.GetFullPath(Path.Combine(output,"source-contact"))){RedirectStandardOutput=true};foreach(var v in inputs)pi.ArgumentList.Add(v.ToString()!);using var p=Process.Start(pi)!;string line=p.StandardOutput.ReadToEnd();p.WaitForExit();Check(p.ExitCode==0,"original C execution");return line.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToArray();}
Test("exact legacy singles state event and RNG bytes",()=>{var expected=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"legacy-baseline-b8bc00c.json")).Trim();Check(Json(hashes)==expected,"30 original legacy whole-turn hashes unchanged");});
Test("original C Cute Charm guard and retained-receipt differential",()=>{
 foreach(int a in new[]{0,2})foreach(int t in new[]{1,3})foreach(uint seed in new uint[]{0,1,38})foreach(int test in Enumerable.Range(0,12)){
  var f=Frame(seed);if(test==5)f=Rebind(f,a,Mon("SLOWPOKE","HEADBUTT"));if(test==6)f=Rebind(f,a,Mon("RATICATE",pid:0));if(test==8)f=Rebind(f,a,Mon("MAGNEMITE"));if(test==3)f=Rebind(f,a,Mon("RATICATE","TAIL_WHIP"));if(test==1)f=Hp(f,a,0);if(test==4)f=Hp(f,t,0);if(test==7)f=Love(f,a,t);
  var receipt=test==2?new SourceHitDamageReceipt():test==10?new(7):test==9?new(65535,0,7):new SourceHitDamageReceipt(65535,7);
  f=Receipt(f,t,receipt);var context=Context(f,a,t);if(test==0)ct.GetField("commandNoEffect",flags)!.SetValue(context,true);
  var move=catalog.Moves[f.Slots[a].Body!.Pokemon.Moves[0].Move];var attacker=f.Slots[a].Body!;var holder=f.Slots[t].Body!;
  var original=Oracle(test==7?2:0,seed,a,t,test==0?1:0,attacker.Pokemon.CurrentHp,holder.Pokemon.CurrentHp,move.Flags.Contains("FLAG_MAKES_CONTACT")?1:0,receipt.Physical,receipt.Special,attacker.Pokemon.AbilityId=="ABILITY_OBLIVIOUS"?1:0,(int)attacker.Pokemon.Gender,(int)holder.Pokemon.Gender);
  shared.GetMethod("ContactCuteCharm",flags)!.Invoke(context,[new BattlerSlot(a),new BattlerSlot(t),move,"contact-oracle",0]);var after=ContextState(context);var events=(IEnumerable<SourceBattleEvent>)ct.GetField("Events",flags)!.GetValue(context)!;
  Check(after.Slots[a].Registers.InfatuationSource==(original[2]!=0?new BattlerSlot(t):null),"C slot source");Check(after.Rng.Cursor==(ulong)original[1]&&after.Rng.Seed==unchecked((uint)original[3]),"C exact draw count/state");Check(events.OfType<SourceAbilityActivatedEvent>().Count()==original[0],"C proc count");Check(after.Slots[a].Body!.InfatuationSource is null,"no legacy body pointer");vectors++;
 }
});
Test("original C in-love action decision and slot identity",()=>{
 foreach(int a in new[]{0,2})foreach(int t in new[]{1,3})foreach(uint seed in new uint[]{0,1,38}){
  var f=Love(Frame(seed),a,t);var context=Context(f,a,t);var original=Oracle(3,seed,a,t,0,1,1,1,1,0,0,0,254);
  var blocked=(bool)shared.GetMethod("InfatuationPreventsMove",flags)!.Invoke(context,[new BattlerSlot(a),Id("TACKLE"),"love-oracle"])!;var after=ContextState(context);
  Check(blocked==(original[4]!=0)&&after.Rng.Cursor==(ulong)original[1]&&after.Rng.Seed==unchecked((uint)original[3]),"C canceller decision/draw");Check(original[5]==t,"C exact source position");vectors++;
 }
});
Test("genuine four-action turns retain distinct Cute Charm holders and JSON slot pointers",()=>{
 bool saw1=false,saw3=false,sawBlock=false;
 foreach(uint seed in new uint[]{0,1,17,31,38,66,88,103}){
  var r=Begin(Frame(seed));var events=r.Events;while(r.State.FourTurn?.Phase==SourceFourActionPhase.Actions){r=Next(r);events=events.AddRange(r.Events);}Check(r.State.FourTurn?.Phase==SourceFourActionPhase.EndTurn,"all four actions complete");
  saw1|=events.OfType<SourceInfatuationChangedEvent>().Any(e=>e.After==new BattlerSlot(1)&&e.Side==new BattlerSlot(2));saw3|=events.OfType<SourceInfatuationChangedEvent>().Any(e=>e.After==new BattlerSlot(3)&&e.Side==new BattlerSlot(0));
  Check(r.State.Slots.All(c=>c.Body!.InfatuationSource is null&&c.Position.HitItemReceipt is null),"slot authority and ActionFinished receipt clear");var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;Check(Json(restored)==Json(r.State),"transient frame round trip");r=Next(r);Check(r.CompletedCleanTurn,"clean Ready");turns++;
  var second=Begin(r.State);while(second.State.FourTurn?.Phase==SourceFourActionPhase.Actions){second=Next(second);sawBlock|=second.Events.OfType<SourceInfatuationBlockedEvent>().Any();}second=Next(second);Check(second.CompletedCleanTurn,"second clean Ready");turns++;
 }Check(saw1&&saw3&&sawBlock,"both actual holders applied and later action cancelled");
});
Test("Cute Charm holder KO consumes no charm draw and clears all departed source links",()=>{
 var f=Love(Love(Hp(Frame(31),3,1),2,3),1,3);var r=Act(f,0,3);Check(r.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side==new BattlerSlot(3)),"real holder faint");Check(!r.Trace.Any(t=>t.Purpose.Contains("cute-charm")),"holder HP pre-roll guard");Check(r.State.Slots.All(c=>c.Registers.InfatuationSource!=new BattlerSlot(3)),"all source references clear, including ally");
 foreach(var kind in Enum.GetValues<SourceDepartureKind>()){var state=SourceBattleIdentityRules.ClearDepartureReferences(Love(Love(Frame(),0,3),2,1),new(3),kind);Check(state.Slots[0].Registers.InfatuationSource is null&&state.Slots[2].Registers.InfatuationSource==new BattlerSlot(1),"exact departure identity "+kind);}
});
Test("full paralysis precedes love, and sleep prevents the love RNG draw",()=>{
 foreach(var status in new uint[]{WildBattleStatus.Paralysis,3}){
  var f=Love(Frame(0),0,3);f=Rebind(f,0,f.Slots[0].Body! with{Status=status});var r=Act(f,0,3);Check(r.Events.OfType<SourceActionBlockedEvent>().Any(),"earlier canceller blocks");Check(!r.Trace.Any(t=>t.Purpose.Contains("infatuation")),"no later love draw");Check(!r.Events.OfType<SourcePpCommandEvent>().Any(),"no PP before cancellation");
 }
});
Test("Future Sight retained physical receipt survives Ready and clears on next action",()=>{
 var f=Frame(0);f=f with{Slots=f.Slots.SetItem(3,f.Slots[3] with{Position=new(){FutureAttack=new(1,Id("FUTURE_SIGHT"),new(0),9)}})};var r=Begin(f);while(r.State.FourTurn is not null)r=Next(r);Check(r.State.Slots[3].Position.HitItemReceipt is{Damage:65535,Physical:>0,Special:0},"retained future physical receipt");
 var next=Act(r.State,2,1);Check(next.State.Slots.All(c=>c.Position.HitItemReceipt is null),"next complete action clears all positions");turns++;
});
Test("holder KO reward pauses preserve original queue and clear love before a later action",()=>{
 var f=Love(Rewards(Hp(Hp(Frame(31),1,1),3,1)),2,3);var r=Begin(f);var all=r.Events;int boundaries=0,pauses=0;
 while(r.State.Outcome==WildBattleOutcome.Ongoing){
  int index=r.State.NextAction;bool rewards=r.State.FourTurn?.Phase==SourceFourActionPhase.RewardsPending;
  r=Next(r);all=all.AddRange(r.Events);Check(++boundaries<=12,"bounded original queue and two reward pauses");
  if(rewards){pauses++;Check(r.State.NextAction==index&&!r.Events.OfType<SourceMoveUsedEvent>().Any()&&r.Trace.IsEmpty,"reward callback never adds an actor or draw");}
  Check(r.State.Queue.Length==4,"contact never expands queue");
 }
 Check(pauses==2&&r.State.Outcome==WildBattleOutcome.Won,"both actual KO rewards settled");Check(!all.OfType<SourceInfatuationChangedEvent>().Any(e=>e.Reason=="cute-charm"),"dead holders never infatuate");
 Check(all.OfType<SourceMoveUsedEvent>().Select(e=>e.Actor.Value).SequenceEqual(new[]{0,2}),"fainted holders never use queued moves");Check(r.State.Slots[2].Registers.InfatuationSource is null,"departed source cleared later actor love");turns++;
});
Test("same-species replacement gets a new occupant without inheriting its departed love links",()=>{
 var f=Love(Rewards(Hp(Frame(31),3,1)),2,3);var party=f.Party(BattleSide.Opponent);f=f with{Parties=f.Parties.SetItem(1,party with{Members=party.Members.Add(Member(Mon("JIGGLYPUFF","LIGHT_SCREEN",0)))})};
 var old=f.Slots[3].Occupant;var r=Begin(f);bool replaced=false;int boundaries=0;
 while(r.State.FourTurn is not null){
  Check(++boundaries<=16,"bounded actual replacement turn");
  if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){r=Good(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(3),new(BattleSide.Opponent,2)));replaced=true;Check(r.State.Slots[3].Occupant!=old&&r.State.Slots.All(c=>c.Registers.InfatuationSource!=new BattlerSlot(3)),"new serial and no rebound love");}
  else r=Next(r);
 }
 Check(replaced&&r.CompletedCleanTurn,"actual replacement then Ready");turns++;
});
Test("nonempty meaningful proof",()=>Check(groups>0&&vectors>0&&turns>0,"nonempty proof"));var report=new{groups,checks,failures,vectors,turns,runtime=Environment.Version.ToString(),legacyWholeTurnHashes=hashes.Count,scope="source-only transient frame; no Application/full354 claim",results};File.WriteAllText(Path.Combine(output,"report.json"),Json(report));Console.WriteLine(Json(report));return failures==0?0:1;
