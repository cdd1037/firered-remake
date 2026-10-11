using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

if (args.Length != 3) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
// The private, authenticated C15 pack can be read from its independent source-only rebuild root.
if (!File.Exists(Path.Combine(args[0], SourceBattleItemCatalogLoader.RelativePath)))
    SourceHeldItemCatalog.Bind(proof, SourceBattleItemCatalogLoader.LoadAuthenticated(args[1]));
int groups=0, checks=0, failures=0, turns=0;
void Check(bool value,string label) { checks++; if(!value) throw new Exception(label); }
void Test(string name,Action body) { groups++;try {body();Console.WriteLine("PASS "+name);} catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);} }
string Json<T>(T value)=>JsonSerializer.Serialize(value);
WildBattleCombatant Mon(string item="ITEM_NONE",string species="MAGIKARP",string move="SPLASH",uint pid=0,int level=30)
{
    var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
    var id=new MoveId("pret:MOVE_"+move);Check(catalog.Species[p.Species].LevelUpMoves.Any(m=>m.Move==id&&m.Level<=level),"natural move "+species+" "+move);
    return new(p with {Moves=[new(id,catalog.Moves[id].Pp,catalog.Moves[id].Pp)]},HeldItem:item);
}
WildBattleCombatant Hp(WildBattleCombatant m,int hp)=>m with {Pokemon=m.Pokemon with {CurrentHp=hp}};
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(WildBattleTransformationRules.PersistentPokemon(m),m.HeldItem,WildBattleTransformationRules.PersistentStatus(m));
WildBattleState Good(WildBattleResult r){Check(r.Succeeded,r.Fault??"accepted");Check(WildBattleRules.Validate(catalog,r.State)==null,"valid singles "+WildBattleRules.Validate(catalog,r.State));return r.State!;}
WildBattleState Start(WildBattleCombatant p,WildBattleCombatant? o=null,uint seed=31,params WildBattleCombatant[] reserves)=>Good(WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[p,..reserves],0,o??Mon(),new(seed)));
WildBattleResult Turn(WildBattleState s,int slot=0){var r=WildBattleRules.ResolveMove(catalog,s,slot);Good(r);turns++;return r;}
SourceBattleFrame Frame(WildBattleCombatant[]? bodies=null,WildBattleCombatant[]? reserves=null)
{
    var m=bodies??[Mon(),Mon(),Mon(),Mon()];return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,
        SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),
        Parties=[new(BattleSide.Player,new[]{Member(m[0]),Member(m[2])}.Concat((reserves??[]).Select(Member)).ToImmutableArray()),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],
        Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
}
SourceFourActionResult Good4(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"accepted four");Check(SourceBattleIdentityRules.Validate(r.State)==null,"identity four");return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good4(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f)=>f.Slots.Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target=="MOVE_TARGET_USER"?c.Slot:c.Slot.Opposite)).ToImmutableArray();
SourceFourActionResult Full4(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null)
{
    var r=Good4(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices??Choices(f)));var ev=r.Events.ToBuilder();var tr=r.Trace.ToBuilder();
    while(r.State.FourTurn is not null&&r.Continuation is {} token){r=Good4(SourceFourActionTurns.Advance(catalog,r.State,token));ev.AddRange(r.Events);tr.AddRange(r.Trace);if(r.State.PendingSwitches.Length>0)break;}
    turns++;return r with {Events=ev.ToImmutable(),Trace=tr.ToImmutable()};
}
var domain=typeof(SourceBattleFrame).Assembly;
var frameItems=domain.GetType("Game.Domain.Battle.SourceFrameBerryItems")!;
var phaseType=domain.GetType("Game.Domain.Battle.SourceBerryPhase")!;
SourceFourActionResult Apply(SourceBattleFrame f,int phase,int? slot=null)=>
    (SourceFourActionResult)frameItems.GetMethod("ApplyCanonical",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[f,Enum.ToObject(phaseType,phase),slot is {} v ? new BattlerSlot(v):(BattlerSlot?)null,catalog])!;
Test("Sitrus fixed thirty; half-HP floor and source phase gates",()=>{
    foreach(int phase in Enumerable.Range(0,4))foreach(int hp in new[]{0,39,40,41}){
        var m=Mon("ITEM_SITRUS_BERRY");m=m with{Pokemon=m.Pokemon with{Stats=m.Pokemon.Stats with{MaxHp=81},CurrentHp=hp}};
        var r=Apply(Frame([m,Mon(),Mon(),Mon()]),phase,0);bool heals=phase==0&&hp>0&&hp<=40;
        Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==hp+(heals?30:0),"fixed amount and phase");Check(r.Trace.IsEmpty,"no RNG");
    }
    var small=Mon("ITEM_SITRUS_BERRY",level:1);small=Hp(small,1);var result=Turn(Start(small));
    Check(result.State!.Player.Pokemon.CurrentHp==small.Pokemon.Stats.MaxHp,"cap full small HP");
});
Test("Lum simultaneously cures status, confusion, Nightmare at move-end",()=>{
    var m=Mon("ITEM_LUM_BERRY") with{Status=3,ConfusionTurnsRemaining=4,Nightmare=true};var r=Apply(Frame([m,Mon(),Mon(),Mon()]),2,0);
    Check(r.State.Slots[0].Body is{Status:0,ConfusionTurnsRemaining:0,Nightmare:false,HeldItem:"ITEM_NONE"},"all cured");
    Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Count()==1&&r.Trace.IsEmpty,"single item, no RNG");
    var f=Entry(Frame([Mon("ITEM_LUM_BERRY") with{Status=WildBattleStatus.Poison,ConfusionTurnsRemaining=4},Mon(),Mon(),Mon()]));var whole=Full4(f);
    Check(whole.State.Slots[0].Body is{Status:0,ConfusionTurnsRemaining:0,HeldItem:"ITEM_NONE"},"actual four move-end");
});
Test("Leppa scans permanent PP slots and includes PP Ups",()=>{
    var m=Mon("ITEM_LEPPA_BERRY","DITTO","TRANSFORM");var slot=m.Pokemon.Moves[0] with{CurrentPp=0,MaximumPp=16,PpUps=3};m=m with{Pokemon=m.Pokemon with{Moves=[slot]}};
    var r=Apply(Frame([m,Mon(),Mon(),Mon()]),0,0);var e=r.Events.OfType<SourceHeldItemConsumedEvent>().Single();
    Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==10&&e.PpRecovery==new SourceItemPpRecovery(slot.Move,0,0,10),"Leppa fixed ten metadata");
    var ditto=Mon("ITEM_LEPPA_BERRY","DITTO","TRANSFORM");ditto=ditto with{Pokemon=ditto.Pokemon with{Moves=[ditto.Pokemon.Moves[0] with{CurrentPp=1,MaximumPp=16,PpUps=3}]}};
    var whole=Turn(Start(ditto));var after=whole.State!.Player;
    Check(after.Transform is not null&&after.Pokemon.Moves[0].Move.Value=="pret:MOVE_SPLASH"&&after.Pokemon.Moves[0].CurrentPp==5,"copied PP stays five");
    Check(WildBattleTransformationRules.PersistentPokemon(after).Moves[0].CurrentPp==10,"party Transform PP restored");
    Check(whole.Events.OfType<WildBattleHeldItemConsumedEvent>().Single().PpRecovery!.Move.Value=="pret:MOVE_TRANSFORM","actual move named");
});
Test("flavor relation covers all natures, existing confusion and Own Tempo RNG guards",()=>{
    string[] items=["FIGY","WIKI","MAGO","AGUAV","IAPAPA"];int[] disliked=[0,3,2,4,1];
    for(uint pid=0;pid<25;pid++)for(int i=0;i<5;i++){
        var m=Mon("ITEM_"+items[i]+"_BERRY",pid:pid);m=Hp(m,m.Pokemon.Stats.MaxHp/2);var r=Apply(Frame([m,Mon(),Mon(),Mon()]),0,0);
        bool hates=pid/5!=pid%5&&pid%5==disliked[i];Check(r.Trace.Length==(hates?1:0),"nature draw "+pid+" "+i);
        Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==m.Pokemon.CurrentHp+Math.Max(1,m.Pokemon.Stats.MaxHp/8),"Gen III eighth");
        Check((r.State.Slots[0].Body!.ConfusionTurnsRemaining>0)==hates,"nature confusion");
    }
    foreach(bool tempo in new[]{false,true}){var m=Mon("ITEM_FIGY_BERRY",pid:5) with{ConfusionTurnsRemaining=tempo?0:3};if(tempo)m=m with{Pokemon=m.Pokemon with{AbilityId="ABILITY_OWN_TEMPO"}};m=Hp(m,10);
        var r=Apply(Frame([m,Mon(),Mon(),Mon()]),0,0);Check(r.Trace.IsEmpty&&r.State.Slots[0].Body!.HeldItem=="ITEM_NONE","guarded draw and still consumes");}
    var whole=Turn(Start(Hp(Mon("ITEM_WIKI_BERRY",pid:3),10)));Check(whole.Trace.Any(t=>t.Purpose.EndsWith("flavor-confusion")),"actual flavor turn draw");
});
Test("pinch stat, critical and random rejection retain caps and exact draw stream",()=>{
    string[] items=["LIECHI","GANLON","SALAC","PETAYA","APICOT"];
    for(int i=0;i<5;i++){var m=Hp(Mon("ITEM_"+items[i]+"_BERRY"),1);var r=Apply(Frame([m,Mon(),Mon(),Mon()]),0,0);Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().StatChanges!.Value.Single().After==7,"one stage");}
    var capped=Hp(Mon("ITEM_STARF_BERRY"),1) with{AttackStage=12,DefenseStage=12,SpeedStage=12,SpecialAttackStageDelta=6,SpecialDefenseStageDelta=6};
    Check(Apply(Frame([capped,Mon(),Mon(),Mon()]),0,0).Trace.IsEmpty,"all capped no random");
    capped=capped with{SpecialDefenseStageDelta=5};var f=Frame([capped,Mon(),Mon(),Mon()]);var r2=Apply(f,0,0);
    var rng=f.Rng;var expected=new List<int>();do {var d=Gen3Rng.Next(rng,"expected",5);rng=d.State;expected.Add(d.Value%5);}while(expected[^1]!=4);
    Check(r2.Trace.Select(t=>t.Result).SequenceEqual(expected)&&r2.State.Rng==rng&&expected.Count>1,"actual rejected rolls retained");
    Check(r2.State.Slots[0].Body!.SpecialDefenseStageDelta==6,"two-stage rise caps at 12");
    var critical=Hp(Mon("ITEM_LANSAT_BERRY"),1);var cr=Apply(Frame([critical,Mon(),Mon(),Mon()]),0,0);
    Check(cr.State.Slots[0].Body!.FocusEnergy&&cr.Events.OfType<SourceHeldItemConsumedEvent>().Single().FocusEnergyRaised==true,"focus energy bit");
    Check(!Apply(Frame([critical with{FocusEnergy=true},Mon(),Mon(),Mon()]),0,0).Events.Any(),"no duplicate critical consume");
    var whole=Full4(Entry(Frame([Hp(Mon("ITEM_STARF_BERRY"),1),Mon(),Mon(),Mon()])));Check(whole.Events.OfType<SourceHeldItemConsumedEvent>().Any(),"actual random berry endturn");
});
Test("White and Mental Herb preserve positives and unrelated move restrictions",()=>{
    var white=Mon("ITEM_WHITE_HERB") with{AttackStage=3,DefenseStage=9,SpeedStage=5,AccuracyStage=4,EvasionStage=8,SpecialAttackStageDelta=-2,SpecialDefenseStageDelta=1};
    foreach(int phase in Enumerable.Range(0,4)){var r=Apply(Frame([white,Mon(),Mon(),Mon()]),phase,0);var a=r.State.Slots[0].Body!;Check(a.AttackStage==6&&a.DefenseStage==9&&a.SpeedStage==6&&a.AccuracyStage==6&&a.EvasionStage==8&&a.SpecialAttackStageDelta==0&&a.SpecialDefenseStageDelta==1,"only negatives reset");}
    var entrant=Mon("ITEM_WHITE_HERB");var intimidator=Mon(species:"ARBOK",move:"GLARE");var f=Entry(Frame([entrant,intimidator,Mon(),Mon()]));Check(f.Slots[0].Body is{AttackStage:6,HeldItem:"ITEM_NONE"},"real initial intimidate then White Herb");
    var mental=Mon("ITEM_MENTAL_HERB") with{DisableTurnsRemaining=3,DisabledMove=new("pret:MOVE_SPLASH")};var mf=Frame([mental,Mon(),Mon(),Mon()]);mf=mf with{Slots=mf.Slots.SetItem(0,mf.Slots[0] with{Registers=mf.Slots[0].Registers with{InfatuationSource=new(1)}})};
    var mr=Apply(mf,2,0);Check(mr.State.Slots[0].Registers.InfatuationSource==null&&mr.State.Slots[0].Body!.DisableTurnsRemaining==3,"Gen III attract only");
});
Test("Leftovers heals without consumption and new metadata survives event adapters",()=>{
    var m=Hp(Mon("ITEM_LEFTOVERS"),10);var r=Turn(Start(m));Check(r.State!.Player.HeldItem=="ITEM_LEFTOVERS"&&r.State.Player.Pokemon.CurrentHp==10+Math.Max(1,m.Pokemon.Stats.MaxHp/16),"actual leftovers");
    Check(r.Events.OfType<WildBattleHeldItemConsumedEvent>().Count()==0&&r.State.CalledParty!.PlayerUsedHeldItem==null,"not consumed nor recyclable");
    var e=new SourceHeldItemConsumedEvent(1,2,new(0),"ITEM_LEPPA_BERRY",20,20,0){PpRecovery=new(new("pret:MOVE_TRANSFORM"),0,0,10)};
    Check(Json(SourceBattleEventAdapter.Import(SourceBattleEventAdapter.Export(e)))==Json<SourceBattleEvent>(e),"full event adapter roundtrip");
    var old=new SourceHeldItemConsumedEvent(1,2,new(0),"ITEM_ORAN_BERRY",1,11,10);Check(!Json(old).Contains("PpRecovery"),"old null bytes absent");
});
Test("independently compiled original C item phases, PP and Starf rejection vectors",()=>{
    using var doc=JsonDocument.Parse(File.ReadAllText(args[2]));var vectors=doc.RootElement.GetProperty("vectors");Check(vectors.GetArrayLength()>500,"nonempty source suite");
    foreach(var v in vectors.EnumerateArray()){
        int N(string k)=>v.GetProperty(k).GetInt32();var x=v.GetProperty("expected").EnumerateArray().Select(a=>a.GetInt64()).ToArray();
        var m=Mon(v.GetProperty("item").GetString()!,pid:(uint)N("pid"));var stage=v.GetProperty("stages").EnumerateArray().Select(a=>a.GetInt32()).ToArray();
        var ppMove=new MoveId("pret:MOVE_HYPER_BEAM");int max=N("ppBase")+N("ppBase")*20*N("bonus")/100;
        m=m with{Status=(uint)N("status"),ConfusionTurnsRemaining=N("confusion")&7,FocusEnergy=(N("confusion")&(1<<20))!=0,
            AttackStage=stage[0],DefenseStage=stage[1],SpeedStage=stage[2],SpecialAttackStageDelta=stage[3]-6,SpecialDefenseStageDelta=stage[4]-6,
            Pokemon=m.Pokemon with{CurrentHp=N("hp"),Stats=m.Pokemon.Stats with{MaxHp=N("maxHp")},Moves=[new(ppMove,N("ppNow"),max,N("bonus"))]}};
        if(N("transformed")!=0){var original=m.Pokemon;var copied=Mon().Pokemon;m=m with{Pokemon=m.Pokemon with{Moves=[new(new("pret:MOVE_SPLASH"),5,40)]},Transform=new(original,copied,[N("bonus"),0,0,0])};}
        var f=Frame([m,Mon(),Mon(),Mon()]) with{Rng=new((uint)N("seed"))};
        if((N("confusion")&0xF0000)!=0)f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Registers=f.Slots[0].Registers with{InfatuationSource=new(1)}})};
        var r=Apply(f,N("phase"),0);var a=r.State.Slots[0].Body!;string label=v.GetProperty("item").GetString()+" phase "+N("phase")+" hp "+N("hp");
        Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Any()==(x[0]!=0&&m.HeldItem!="ITEM_LEFTOVERS"),label+" consume");
        Check(a.Pokemon.CurrentHp==m.Pokemon.CurrentHp+x[3],label+" HP");Check(a.Status==(uint)x[1],label+" status");
        Check(a.ConfusionTurnsRemaining==((int)x[2]&7),label+" confusion");Check(a.FocusEnergy==((x[2]&(1<<20))!=0),label+" critical bit");
        Check(WildBattleTransformationRules.PersistentPokemon(a).Moves[0].CurrentPp==x[6],label+" party PP");Check(a.Pokemon.Moves[0].CurrentPp==x[7],label+" battle PP");
        Check(r.Trace.Length==x[8]&&r.State.Rng.Seed==(uint)x[9],label+" exact RNG");
        int[] stages=[a.AttackStage,a.DefenseStage,a.SpeedStage,a.SpecialAttackStageDelta+6,a.SpecialDefenseStageDelta+6];
        for(int i=0;i<5;i++)Check(stages[i]==(x[4]==i+1?Math.Min(12,(int)x[11+i]+(int)x[5]):x[11+i]),label+" stat "+i);
    }
});
Test("Baton Pass applies incoming White Herb at the source switch-in boundary",()=>{
    var passer=Mon(species:"MR_MIME",move:"BATON_PASS",level:50);var f=Entry(Frame([passer,Mon(),Mon(),Mon()],[Mon("ITEM_WHITE_HERB")]));
    f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Body=f.Slots[0].Body! with{AttackStage=3,DefenseStage=9}})};
    var r=Good4(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)));
    while(r.State.PendingSwitches.IsEmpty&&r.Continuation is{} c)r=Good4(SourceFourActionTurns.Advance(catalog,r.State,c));
    Check(r.State.PendingSwitches.Length==1,"real Baton pause");
    r=Good4(SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));
    Check(r.State.Slots[0].Body is{AttackStage:6,DefenseStage:9,HeldItem:"ITEM_NONE"},"passed negatives reset, positive retained");
    Check(r.State.Party(BattleSide.Player).Members[2].HeldItem=="ITEM_NONE"&&r.State.Slots[0].Position.UsedHeldItem=="ITEM_WHITE_HERB","actual incoming party consumed");
    Check(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Sequence>r.Events.OfType<SourceCalledPartySwitchedEvent>().Single().Sequence,"after source entry");
});
Test("consumption, Recycle, and Knock Off keep permanent and position identity distinct",()=>{
    var mime=Mon("ITEM_SITRUS_BERRY","MR_MIME","RECYCLE",level:50);mime=Hp(mime,mime.Pokemon.Stats.MaxHp/2);
    var first=Turn(Start(mime,Mon(),31,Mon()));Check(first.State!.CalledParty!.PlayerUsedHeldItem=="ITEM_SITRUS_BERRY","actual consumption receipt");
    var recycled=Turn(first.State);Check(recycled.State!.Player.HeldItem=="ITEM_SITRUS_BERRY"&&recycled.State.CalledParty!.PlayerUsedHeldItem==null,"Recycle returns item and clears receipt");
    Check(WildBattleSourcePartyEffects.PersistentHeldItem(recycled.State.CalledParty!.Player[0])=="ITEM_SITRUS_BERRY","Recycle writes permanent party item");
    var away=Good(WildBattleRules.SwitchSourceWildPlayer(catalog,recycled.State,1));var back=Good(WildBattleRules.SwitchSourceWildPlayer(catalog,away,0));
    Check(back.Player.HeldItem=="ITEM_SITRUS_BERRY","actual switch preserves recycled item");
    var knocked=Turn(Start(Mon("ITEM_SITRUS_BERRY"),Mon(species:"FARFETCHD",move:"KNOCK_OFF",level:21)));
    Check(knocked.State!.Player is{HeldItem:"ITEM_NONE",KnockedOffItem:"ITEM_SITRUS_BERRY"},"Knock Off disables battle item");
    Check(WildBattleSourcePartyEffects.PersistentHeldItem(knocked.State.CalledParty!.Player[0])=="ITEM_SITRUS_BERRY"&&knocked.State.CalledParty.PlayerUsedHeldItem==null,"Knock Off neither consumes permanent nor gives Recycle receipt");
});
Test("original C type and species stat modifiers include u16 assignment and wrong species",()=>{
    var type=domain.GetType("Game.Domain.Battle.SourceHeldItemModifiers")!;
    int Call(string name,params object[] values)=>(int)type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,values)!;
    using var doc=JsonDocument.Parse(File.ReadAllText(args[2]));var vectors=doc.RootElement.GetProperty("statVectors");Check(vectors.GetArrayLength()>1000,"nonempty original damage vectors");
    foreach(var v in vectors.EnumerateArray()){
        int basis=v.GetProperty("basis").GetInt32(),t=v.GetProperty("type").GetInt32();var m=Mon(v.GetProperty("item").GetString()!);m=m with{Pokemon=m.Pokemon with{Species=new("pret:SPECIES_"+v.GetProperty("species").GetString()),SourceSpeciesId=v.GetProperty("speciesId").GetInt32()}};
        var expected=v.GetProperty("expected").EnumerateArray().Select(e=>e.GetInt32()).ToArray();
        int boosted=Call("TypeAttack",catalog,m,(ElementType)t,basis);
        int attack=t<9?boosted:basis,spAttack=t<9?basis:boosted;
        spAttack=Call("SpecialAttack",catalog,m,spAttack);int spDefense=Call("SpecialDefense",catalog,m,basis);
        Check(attack==expected[0]&&basis==expected[1]&&spAttack==expected[2]&&spDefense==expected[3],"original stat vector "+v);
    }
    var pikachu=Mon("ITEM_LIGHT_BALL","PIKACHU","THUNDER_SHOCK");var target=Mon(level:70);
    var held=Turn(Start(pikachu,target));var plain=Turn(Start(pikachu with{HeldItem="ITEM_NONE"},target));
    Check(held.Events.OfType<WildBattleHpChangedEvent>().Single(e=>e.Actor==WildBattleSide.Player).Damage>plain.Events.OfType<WildBattleHpChangedEvent>().Single(e=>e.Actor==WildBattleSide.Player).Damage,"actual special Light Ball hit");
    var tackle=Mon("ITEM_LIGHT_BALL","PIKACHU","QUICK_ATTACK");var physical=Turn(Start(tackle,target));var normal=Turn(Start(tackle with{HeldItem="ITEM_NONE"},target));
    Check(physical.Events.OfType<WildBattleHpChangedEvent>().Single(e=>e.Actor==WildBattleSide.Player).Damage==normal.Events.OfType<WildBattleHpChangedEvent>().Single(e=>e.Actor==WildBattleSide.Player).Damage,"Gen III Light Ball does not boost physical");
});
Test("evasion holders affect actual generic accuracy and proof does not widen old profiles",()=>{
    var type=domain.GetType("Game.Domain.Battle.SourceHeldItemModifiers")!;int Accuracy(string item,int n)=>(int)type.GetMethod("Accuracy",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[catalog,Mon(item),n])!;
    Check(Accuracy("ITEM_BRIGHT_POWDER",85)==76&&Accuracy("ITEM_LAX_INCENSE",85)==80,"separate integer evasion truncation");
    var attacker=Mon(species:"MAGIKARP",move:"TACKLE");bool difference=false;
    for(uint seed=0;seed<80&&!difference;seed++){
        var a=Turn(Start(attacker,Mon(),seed));var b=Turn(Start(attacker,Mon("ITEM_BRIGHT_POWDER"),seed));
        difference=!a.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player)&&b.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player);
    }
    Check(difference,"actual retained accuracy draw crosses powder threshold");
    Check(SourceHeldItemCatalog.For(catalog) is not null&&SourceHeldItemCatalog.For(catalog with{}) is null,"proof exact root identity");
    var enigma=WildBattleRules.StartSourceWild(proof,BattleRulesProfile.CanonicalSourceSingles,[Mon("ITEM_ENIGMA_BERRY")],0,Mon(),new(31));Check(!enigma.Succeeded,"dynamic Enigma remains gated");
});
Test("mid-turn Intimidate checks White Herb only at source slot zero before next move",()=>{
    var f=Entry(Frame([Mon("ITEM_WHITE_HERB"),Mon(),Mon("ITEM_WHITE_HERB"),Mon()]));var foe=f.Party(BattleSide.Opponent);
    f=f with{Parties=f.Parties.SetItem(1,foe with{Members=foe.Members.Add(Member(Mon(species:"ARBOK",move:"GLARE")))})};
    var choices=Choices(f).SetItem(1,new(f.Slots[1].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Opponent,2)));
    var r=Good4(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));r=Good4(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
    Check(r.State.Slots[0].Body is{HeldItem:"ITEM_NONE",AttackStage:6},"zero checked immediately");
    Check(r.State.Slots[2].Body is{HeldItem:"ITEM_WHITE_HERB",AttackStage:5},"slot two waits source move-end");
    r=Good4(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));Check(r.State.Slots[2].Body is{HeldItem:"ITEM_NONE",AttackStage:6},"next real move-end cures slot two");
});
Test("Lum confusion-only writes party status and Leppa preserves a mimicked live slot",()=>{
    var m=Mon("ITEM_LUM_BERRY") with{ConfusionTurnsRemaining=3,SourcePartyStatusOverride=2};
    var r=Apply(Frame([m,Mon(),Mon(),Mon()]),0,0);Check(r.State.Party(BattleSide.Player).Members[0].Status==0,"Lum status controller writes even when battle status is zero");
    var leppa=Mon("ITEM_LEPPA_BERRY");var original=leppa.Pokemon.Moves[0] with{CurrentPp=0};var copied=new PokemonMoveSlot(new("pret:MOVE_TACKLE"),5,35);
    leppa=leppa with{Pokemon=leppa.Pokemon with{Moves=[copied]},CopyMoves=new([new(0,original)])};
    var pp=Apply(Frame([leppa,Mon(),Mon(),Mon()]),0,0);Check(pp.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==5&&WildBattleTransformationRules.PersistentPokemon(pp.State.Slots[0].Body!).Moves[0].CurrentPp==10,"Mimic battle PP untouched, first permanent slot restored");
});
Test("critical held items reach ordinary singles and four-slot damage paths",()=>{
    foreach(var row in new[]{("ITEM_SCOPE_LENS","MAGIKARP","TACKLE",8),("ITEM_LUCKY_PUNCH","CHANSEY","POUND",4),("ITEM_LUCKY_PUNCH","MAGIKARP","TACKLE",16),("ITEM_STICK","FARFETCHD","SLASH",3)}){
        var holder=Mon(row.Item1,row.Item2,row.Item3,level:50);var r=Turn(Start(holder,Mon(level:70)));
        Check(r.Trace.Any(t=>t.Purpose.EndsWith(".critical")&&t.Bound==row.Item4),"exact ordinary critical stage "+row.Item1+" "+row.Item2);
        var four=Full4(Entry(Frame([holder,Mon(level:70),Mon(),Mon()])));Check(four.Trace.Any(t=>t.Purpose.EndsWith(".critical")&&t.Bound==row.Item4),"exact shared critical stage");
    }
});
SmokeBallChecks.Run(proof,args[0],Check,Test);
var result=new{groups,checks,failures,turns,runtime=Environment.Version.ToString()};Console.WriteLine(Json(result));Console.WriteLine("HELD_ITEM_COMPLETION_END");return failures==0&&groups>=7&&checks>100?0:1;
