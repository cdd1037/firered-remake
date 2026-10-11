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
 return new(p with{Moves=moves.Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()});
}
var snapshots=new List<object>();
foreach(var pair in new[]{("FOCUS_PUNCH","DEFENSE_CURL"),("FOCUS_PUNCH","TACKLE"),("FOCUS_PUNCH","EMBER"),("SNATCH","DEFENSE_CURL"),("MAGIC_COAT","TAIL_WHIP"),("SNATCH","RECOVER")}){
 var actor=Mon("SNORLAX",50,pair.Item1,"DEFENSE_CURL");var target=Mon("SNORLAX",100,pair.Item2);
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[actor],0,target,new(31));if(!start.Succeeded)throw new Exception("start:"+start.Fault);
 var first=WildBattleRules.ResolveMove(catalog,start.State!,0);if(!first.Succeeded)throw new Exception("first:"+first.Fault);
 var second=WildBattleRules.ResolveMove(catalog,first.State!,1);if(!second.Succeeded)throw new Exception("second:"+second.Fault);
 var data=Json(new{first,second});snapshots.Add(new{move=pair.Item1,target=pair.Item2,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant(),bytes=Encoding.UTF8.GetByteCount(data),draws=first.Trace.Length+second.Trace.Length});
}
if(args.Contains("--capture")){
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("HasPreparedMoveRecipe",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unmodified fixed baseline");
 File.WriteAllText(args[1],Json(new{baseline="516c492a2c78862e1100983edba22d867b618b5f",snapshots})+"\n");return 0;
}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string label,Action body){groups++;try{body();Console.WriteLine("PASS "+label);results.Add(new{label,passed=true});}catch(Exception ex){failures++;Console.WriteLine("FAIL "+label+" "+ex);results.Add(new{label,passed=false,error=ex.ToString()});}}
Test("exact original C commands retain four-position arming and Pressure behavior",()=>{
 using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-command-proof.json")));
 Check(source.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit&&source.RootElement.GetProperty("checks").GetInt32()==20&&source.RootElement.GetProperty("passed").GetBoolean(),"nonempty exact C proof");
});
Test("immutable singles complete state event RNG goldens",()=>{
 Check(Environment.Version.Major==10,"hosted runtime10");
 using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-baseline.json")));
 var old=baseline.RootElement.GetProperty("snapshots").EnumerateArray().ToArray();Check(old.Length==6&&old.Length==snapshots.Count,"nonempty suite");
 for(int i=0;i<old.Length;i++)Check(JsonSerializer.SerializeToElement(snapshots[i]).GetProperty("sha256").GetString()==old[i].GetProperty("sha256").GetString(),"full state/event/RNG parity "+Json(snapshots[i]));
});
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null,uint seed=31){
 var m=supplied??[Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("SNORLAX",90,"DEFENSE_CURL"),Mon("SNORLAX",60,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL")];
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
}
SourceBattleFrame Put(SourceBattleFrame f,int i,WildBattleCombatant body){var c=f.Slots[i];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(i,c with{Body=body}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(body))})};}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? positions=null,int[]? targets=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int pos=positions?[c.Slot.Value]??0;var move=catalog.Moves[c.Body!.Pokemon.Moves[pos].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,new BattlerSlot(targets?[c.Slot.Value]??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1)));}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"success");Check(SourceBattleIdentityRules.Validate(r.State)is null,"valid result");return r;}
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Turn(SourceBattleFrame f,int[]? positions=null,int[]? targets=null){
 var r=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,positions,targets)));var events=r.Events.ToList();var trace=r.Trace.ToList();
 while(r.State.FourTurn is not null){Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"expected bounded phase");var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var next=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var replay=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);Check(Json(next)==Json(replay),"serialized continuation replay");r=Ok(next);events.AddRange(r.Events);trace.AddRange(r.Trace);}
 Check(r.CompletedCleanTurn,"clean four-action turn");return(r.State,events,trace);
}
Test("Focus Punch numeric setup, physical/special losses, Substitute and normal continuation",()=>{
 var f=Frame([Mon("SNORLAX",70,"FOCUS_PUNCH","DEFENSE_CURL"),Mon("SNORLAX",80,"TACKLE","DEFENSE_CURL"),Mon("SNORLAX",90,"FOCUS_PUNCH","DEFENSE_CURL"),Mon("CHARMANDER",20,"EMBER","DEFENSE_CURL")]);
 var r=Turn(f,null,[1,0,1,2]);var setup=r.Events.OfType<SourceFocusPunchPreparedEvent>().ToArray();
 Check(setup.Select(e=>e.Actor.Value).SequenceEqual(new[]{0,2})&&setup.Select(e=>e.PartyIndex).SequenceEqual(new[]{0,1}),"setup numeric battler/actual party identity");
 Check(r.Events.OfType<SourceFocusPunchLostEvent>().Count()==2,"both retained physical/special damage cancel");
 Check(!r.Events.OfType<SourceAttackStringEvent>().Any(e=>e.CurrentMove==Id("FOCUS_PUNCH")),"lost focus has no attack string");
 Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==19&&r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==19,"lost focus spends PP once");
 var normal=Turn(r.State,[1,1,1,1]);Check(normal.Events.OfType<SourceFocusPunchPreparedEvent>().Count()==0&&normal.State.Turn==f.Turn+2,"normal next turn resets preparation and damage");
 var sub=Frame([Mon("SNORLAX",50,"FOCUS_PUNCH") with{CalledVolatile=new(){SubstituteHp=100}},Mon("SNORLAX",80,"TACKLE"),Mon("SNORLAX",60,"DEFENSE_CURL"),Mon("SNORLAX",70,"DEFENSE_CURL")]);
 var sr=Turn(sub);Check(!sr.Events.OfType<SourceFocusPunchLostEvent>().Any()&&sr.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Id("FOCUS_PUNCH")),"substitute hit never interrupts focus");
});
Test("Focus Punch preturn sleep and raw Truant gates",()=>{
 foreach(var body in new[]{Mon("SNORLAX",50,"FOCUS_PUNCH") with{Status=3},Mon("SNORLAX",50,"FOCUS_PUNCH") with{TruantLoafing=true}}){var f=Put(Frame(),0,body);var b=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)));Check(!b.Events.OfType<SourceFocusPunchPreparedEvent>().Any(),"sleep/raw Truant suppresses setup");}
});
Test("three Snatch interceptors consume in retained queue order with original PP owner",()=>{
 var f=Frame([Mon("SNORLAX",70,"SNATCH"),Mon("SNORLAX",90,"SNATCH"),Mon("SNORLAX",80,"SNATCH"),Mon("SNORLAX",60,"DEFENSE_CURL")]);var r=Turn(f);
 var ready=r.Events.OfType<SourceSnatchReadyEvent>().ToArray();var stolen=r.Events.OfType<SourceMoveSnatchedEvent>().ToArray();Check(ready.Length==3&&stolen.Length==3,"three actual interceptors");Check(stolen.Select(e=>e.Snatcher).SequenceEqual(ready.Select(e=>e.Actor)),"queue order preserved");
 var leaf=r.Events.OfType<SourcePpCommandEvent>().Where(e=>e.CurrentMove==Id("DEFENSE_CURL")).ToArray();Check(leaf.Length==4&&leaf.All(e=>e.PpOwner.Value==3)&&leaf.Sum(e=>e.Before-e.After)==1,"all passes preserve original PP owner and deduct once");
 Check(r.State.Slots[ready[^1].Actor.Value].Body!.DefenseStage==7&&r.State.Slots[3].Body!.DefenseStage==6,"effect reaches final snatcher only");Check(r.State.Slots.All(c=>c.Body!.CalledVolatile?.SnatchOrderIndex is null),"flags consumed and cleanup complete");
});
Test("Snatch Pressure learned slot and exhausted PP are source-correct",()=>{
 foreach(int pp in new[]{1,10}){
 var f=Frame([Mon("SNORLAX",70,"SNATCH"),Mon("DUSCLOPS",80,"DEFENSE_CURL"),Mon("SNORLAX",60,"DEFENSE_CURL"),Mon("SNORLAX",50,"DEFENSE_CURL")]);var m=f.Slots[0].Body!;f=Put(f,0,m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.SetItem(0,m.Pokemon.Moves[0] with{CurrentPp=pp})}});
 var r=Turn(f);var extra=r.Events.OfType<SourceSnatchPressurePpEvent>().Single();Check(extra.Slot==0&&extra.Before==pp-1&&extra.After==Math.Max(0,pp-2)&&extra.PressuringActor.Value==1,"Pressure uses actual learned exhausted/nonexhausted slot");Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==Math.Max(0,pp-2),"ordinary and interception PP costs");
 }
 Check(WildBattleSourcePreparedMoves.SnatchPressureSlot("ABILITY_PRESSURE",new[]{new PokemonMoveSlot(Id("SNATCH"),0,10),new PokemonMoveSlot(Id("SNATCH"),10,10)})==0,"source primitive chooses first duplicate even exhausted");
});
Test("Snatch uses four retained positions even when a faster action is cancelled",()=>{
 var f=Frame([Mon("SNORLAX",70,"SNATCH"),Mon("SNORLAX",90,"SNATCH"),Mon("SNORLAX",80,"SNATCH"),Mon("SNORLAX",60,"SNATCH")]);var all=Turn(f);Check(all.Events.OfType<SourceSnatchReadyEvent>().Count()==3&&all.Events.OfType<SourceMoveFailedEvent>().Count(e=>e.Reason=="snatch-last-action")==1,"fourth source queue position fails");
 f=Put(f,3,f.Slots[3].Body! with{Pokemon=f.Slots[3].Body!.Pokemon with{CurrentHp=0}}) with{SourceAbsentMask=8};var partial=Turn(f);Check(partial.Events.OfType<SourceSnatchReadyEvent>().Any(e=>e.ActionOrderIndex==2),"last living actor can arm at index two");Check(!partial.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="snatch-last-action"),"cancelled slot still counted");
});
Test("Magic Coat four positions, last failure, reflection and Pressure",()=>{
 var f=Frame([Mon("SNORLAX",70,"MAGIC_COAT"),Mon("SNORLAX",90,"MAGIC_COAT"),Mon("SNORLAX",80,"MAGIC_COAT"),Mon("SNORLAX",60,"MAGIC_COAT")]);var r=Turn(f);
 Check(r.Events.OfType<SourceMoveFailedEvent>().Count(e=>e.Reason=="magic-coat-last-action")==1,"only retained last action fails");Check(r.Events.OfType<SourceMtMoonVolatileEvent>().Count(e=>e.Volatile=="magic-coat"&&e.After==1)==3,"indices zero one two arm");Check(r.State.Slots.All(c=>c.Body!.CalledVolatile?.MagicCoat!=true),"endturn clears every flag");
 f=Frame([Mon("SNORLAX",70,"MAGIC_COAT"),Mon("DUSCLOPS",80,"TOXIC"),Mon("SNORLAX",60,"DEFENSE_CURL"),Mon("SNORLAX",50,"DEFENSE_CURL")]);r=Turn(f,null,[0,0,2,3]);var reflect=r.Events.OfType<SourceMagicCoatEvent>().Single();Check(reflect.OriginalActor.Value==1&&reflect.Reflector.Value==0,"actual reflector identity");var pressure=r.Events.OfType<SourceMagicCoatPressurePpEvent>().Single();Check(pressure.Before-pressure.After==1&&pressure.Slot==0,"extra Magic Coat Pressure PP");Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==13&&r.State.Slots[1].Body!.Pokemon.Moves[0].CurrentPp==9,"correct ordinary and extra costs");
});
Test("Magic Coat reflection honors source Follow Me and source spread continuation",()=>{
 var f=Frame([Mon("SNORLAX",70,"MAGIC_COAT"),Mon("SNORLAX",90,"TOXIC"),Mon("SNORLAX",60,"DEFENSE_CURL"),Mon("SNORLAX",80,"FOLLOW_ME")]);var r=Turn(f,null,[0,0,2,3]);Check(r.Events.OfType<SourceMagicCoatEvent>().Single().Reflector.Value==0,"reflection happened");Check(r.Events.OfType<SourcePpCommandEvent>().Any(e=>e.CurrentMove==Id("TOXIC")&&e.Actor.Value==0&&e.Target.Value==3&&e.PpOwner.Value==1),"reflected effect follows opposite-side Follow Me");
 f=Frame([Mon("SNORLAX",70,"MAGIC_COAT"),Mon("SNORLAX",90,"TAIL_WHIP"),Mon("SNORLAX",60,"DEFENSE_CURL"),Mon("SNORLAX",50,"DEFENSE_CURL")]);r=Turn(f,null,[0,0,2,3]);Check(r.Events.OfType<SourcePpCommandEvent>().Any(e=>e.CurrentMove==Id("TAIL_WHIP")&&e.Actor.Value==0&&e.Target.Value==3),"spread next target follows reflected target partner");
});
Test("called spread reflection retains fourth learned slot on a one-move reflector",()=>{
 uint inv=1,seed;unchecked{for(int i=0;i<5;i++)inv*=2-0x41C64E6D*inv;seed=((44u<<16)-0x6073)*inv;}
 var f=Frame([Mon("SNORLAX",70,"MAGIC_COAT"),Mon("SNORLAX",90,"DEFENSE_CURL","TACKLE","MINIMIZE","METRONOME"),Mon("SNORLAX",60,"DEFENSE_CURL"),Mon("SNORLAX",50,"DEFENSE_CURL")],seed);
 var r=Turn(f,[0,3,0,0]);Check(r.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation.CurrentMove==Id("GROWL"),"authentic Metronome selected Growl");
 Check(r.Events.OfType<SourceMagicCoatEvent>().Single().Reflector.Value==0,"called leaf reflected once");
 Check(r.State.Slots[1].Body!.Pokemon.Moves[3].CurrentPp==9&&r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==14,"original PP owner preserved through copied short moveset");
 Check(r.State.Slots[1].Body!.AttackStage==5&&r.State.Slots[3].Body!.AttackStage==5&&r.State.Slots[2].Body!.AttackStage==6,"reflected spread visits correct pair once");
});
Test("two Magic Coats terminate reflection chain and selected ally keeps exact slots",()=>{
 var f=Frame([Mon("SNORLAX",80,"MAGIC_COAT"),Mon("SNORLAX",90,"TOXIC"),Mon("SNORLAX",70,"MAGIC_COAT"),Mon("SNORLAX",60,"DEFENSE_CURL")]);
 var b=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,null,[0,0,2,3])));var r=Ok(SourceFourActionTurns.Advance(catalog,b.State,b.Continuation!.Value));r=Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
 var actor=r.State.Slots[1].Body!;var marked=Put(r.State,1,actor with{CalledVolatile=new(){MagicCoat=true}});r=Ok(SourceFourActionTurns.Advance(catalog,marked,r.Continuation!.Value));Check(r.Events.OfType<SourceMagicCoatEvent>().Count()==2,"each flag consumed at most once");Check(r.Events.OfType<SourcePpCommandEvent>().Where(e=>e.CurrentMove==Id("TOXIC")).All(e=>e.PpOwner.Value==1),"reflection chain retains original PP owner");
});
Test("reflected Soundproof retains obedience and original PP while stopping that target",()=>{
 var f=Frame([Mon("SNORLAX",70,"MAGIC_COAT"),Mon("EXPLOUD",60,"DEFENSE_CURL","TACKLE","MINIMIZE","GROWL"),Mon("SNORLAX",50,"DEFENSE_CURL"),Mon("SNORLAX",40,"DEFENSE_CURL")]);
 var r=Turn(f,[0,3,0,0],[0,0,2,3]);Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Actor.Value==0&&e.Target.Value==1&&e.Ability=="ABILITY_SOUNDPROOF"),"reflected target Soundproof");
 Check(r.State.Slots[0].Registers.History?.LastResultingMove==Id("GROWL"),"interceptor retains previously established obedience");
 Check(r.Events.OfType<SourcePpCommandEvent>().Where(e=>e.CurrentMove==Id("GROWL")).All(e=>e.PpOwner.Value==1&&e.PpSlot==3)&&r.State.Slots[1].Body!.Pokemon.Moves[3].CurrentPp==39,"original slot three stays owned across one-slot interceptor and second target");
});
Test("Magic Coat precedes the original Lightning Rod message",()=>{
 var f=Frame([Mon("RHYDON",70,"MAGIC_COAT"),Mon("SNORLAX",60,"THUNDER_WAVE"),Mon("SNORLAX",50,"DEFENSE_CURL"),Mon("SNORLAX",40,"DEFENSE_CURL")]);
 var r=Turn(f,null,[0,2,2,3]);Check(r.Events.OfType<SourceMagicCoatEvent>().Single().Reflector.Value==0,"Lightning Rod selected the actual reflector");Check(!r.Events.OfType<SourceAbilityActivatedEvent>().Any(e=>e.Ability=="ABILITY_LIGHTNING_ROD"),"reflection reenters before original target redirect message");Check(r.State.Slots[1].Body!.Status==WildBattleStatus.Paralysis,"reflected status reaches original actor");
});
Test("canonical singles Magic Coat Pressure correction is explicit",()=>{
 var start=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("SNORLAX",80,"MAGIC_COAT")],0,Mon("DUSCLOPS",70,"TOXIC"),new(31));Check(start.Succeeded,"start");var r=WildBattleRules.ResolveMove(catalog,start.State!,0);Check(r.Succeeded,r.Fault??"turn");var pp=r.Events.OfType<WildBattleMagicCoatPressurePpEvent>().Single();Check(pp.Before-pp.After==1&&r.State!.Player.Pokemon.Moves[0].CurrentPp==13,"source extra PP fixes previously omitted command");
});
Test("family definitions and actual order boundaries reject atomically",()=>{
 foreach(string name in new[]{"FOCUS_PUNCH","SNATCH","MAGIC_COAT"})Check(!SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(name)] with{Priority=99}),"forged recipe tuple "+name);
 var f=Frame([Mon("SNORLAX",70,"SNATCH"),Mon("SNORLAX",90,"DEFENSE_CURL"),Mon("SNORLAX",80,"DEFENSE_CURL"),Mon("SNORLAX",60,"DEFENSE_CURL")]);var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(0)));Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"standalone invented queue index denied");
 var invalid=Put(f,0,f.Slots[0].Body! with{CalledVolatile=new(){SnatchOrderIndex=3}});var denied=SourceFourActionTurns.BeginActions(catalog,invalid,invalid.Revision,Choices(invalid));Check(!denied.Succeeded&&ReferenceEquals(denied.State,invalid)&&denied.Events.IsEmpty&&denied.Trace.IsEmpty,"invalid retained arming index denied");
});
File.WriteAllText(args[1],Json(new{groups,checks,failures,results})+"\n");
return failures==0?0:1;
