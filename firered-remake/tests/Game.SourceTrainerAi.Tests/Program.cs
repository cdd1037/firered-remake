using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass project root.");
string root = Path.GetFullPath(args[0]); int assertions=0, groups=0, failures=0;
void Check(bool condition,string message) {assertions++;if(!condition)throw new Exception(message);}
void Test(string title,Action test) {groups++;try{test();Console.WriteLine("PASS "+title);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+title+" "+ex);}}
MoveId Id(string name)=>new("pret:MOVE_"+name);
var original=PretRocketHideoutPokemon.Load(root).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(root,PretCeladonApproachPokemon.Load(root).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,root,root).Catalog,root).Catalog,root).Catalog;
var fuchsia=PretFuchsiaRoutePokemon.Load(old,root);
var catalog=PretMetronomeMoveCatalog.Load(root).Extend(fuchsia.Catalog);
var gymData=PretFuchsiaGymTrainers.Load(root);
var gym=new FuchsiaGymTrainerCatalog(gymData.SourceCommit,gymData.Profile,false,gymData.Trainers,gymData.Battles);
var routes=fuchsia.Trainers;
PokemonInstance Mon(string species,int level=40,uint pid=0)=>PokemonFactory.Create(catalog,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(WildBattleTransformationRules.PersistentPokemon(m),m.HeldItem,WildBattleTransformationRules.PersistentStatus(m),KnockedOffItem:m.KnockedOffItem);
ForestTrainerCombatState Sync(ForestTrainerCombatState s)
{
    var party=s.OpponentParty.SetItem(s.OpponentIndex,s.Battle.Opponent).Select(m=>m.Pokemon.CurrentHp==0 ? m with{FirstTurnCounter=0}:m).ToImmutableArray();
    var p=s.Battle.CalledParty!;
    p=p with{Player=p.Player.SetItem(p.PlayerActiveIndex,Member(s.Battle.Player)),Opponent=party.Select(Member).ToImmutableArray(),OpponentActiveIndex=s.OpponentIndex};
    int completed=s.ExperienceCompletedMask;
    for(int i=0;i<party.Length;i++)if(i!=s.OpponentIndex && party[i].Pokemon.CurrentHp==0)completed|=1<<i;
    return s with{OpponentParty=party,SsAnnePlayerParty=[s.Battle.Player],ExperienceCompletedMask=completed,Battle=s.Battle with{CalledParty=p}};
}
ForestTrainerCombatState Gym(string name="LEADER_KOGA",int index=0,string target="RATTATA",uint seed=1)
{
    var id=new TrainerId("pret:TRAINER_"+name);
    var party=FuchsiaGymTrainerPartyFactory.Create(gym,catalog,id,new(0)).Party.Select(m=>new WildBattleCombatant(m.Pokemon){FirstTurnCounter=1}).ToImmutableArray();
    var player=new WildBattleCombatant(Mon(target)){FirstTurnCounter=1};
    var battle=new WildBattleState(player,party[index],0,0,0,new(seed),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Building,
        CalledField=new(),CalledParty=new([Member(player)],party.Select(Member).ToImmutableArray(),0,index,WildBattleCalledBattleKind.Trainer)};
    return new(id,battle,party,index,StaleCurrentMove:ForestTrainerCombatRules.NoMove,RulesVersion:ForestTrainerCombatRules.KogaRulesVersion){SourceMoveRulesVersion=ForestTrainerCombatRules.SourceTrainerContractVersion,SsAnnePlayerParty=[player]};
}
ForestTrainerCombatState Route(string name)
{
    var id=new TrainerId("pret:TRAINER_"+name);
    var party=FuchsiaRouteTrainerPartyFactory.Create(routes,catalog,id,new(0)).Party.Select(m=>new WildBattleCombatant(m.Pokemon){FirstTurnCounter=1}).ToImmutableArray();
    int index=Enumerable.Range(0,party.Length).First(i=>party[i].Pokemon.Moves.Any(m=>m.Move==Id("METRONOME")));
    var player=new WildBattleCombatant(Mon("RATTATA")){FirstTurnCounter=1};
    var battle=new WildBattleState(player,party[index],0,0,0,new(1),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Grass,
        CalledField=new(),CalledParty=new([Member(player)],party.Select(Member).ToImmutableArray(),0,index,WildBattleCalledBattleKind.Trainer)};
    return new(id,battle,party,index,StaleCurrentMove:ForestTrainerCombatRules.NoMove,RulesVersion:ForestTrainerCombatRules.FuchsiaRulesVersion){SourceMoveRulesVersion=ForestTrainerCombatRules.SourceTrainerContractVersion,SsAnnePlayerParty=[player]};
}
CeruleanTrainerAiDecision Choose(ForestTrainerCombatState s)
{var r=SourceTrainerAi.Choose(catalog,s);Check(r.Succeeded,r.Diagnostic?.Code??"choose failed");return r;}
Test("official runtime, seven exact gym teams and both natural Metronome singles",()=>{
    Check(Environment.Version.Major==10 && gym.Trainers.Count==7,"official runtime and source roster");
    foreach(var id in FuchsiaGymTrainerPartyFactory.SupportedTrainers)
    {
        var state=Gym(id.Value[13..]);
        for(int i=0;i<state.OpponentParty.Length;i++){
            var b=Sync(state with{OpponentIndex=i,Battle=state.Battle with{Opponent=state.OpponentParty[i]}});
            var decision=Choose(b);Check(decision.Scores.Length==4 && decision.Trace.Length>=5,"scored source slots");
        }
    }
    foreach(var name in new[]{"BEAUTY_SHEILA","PICNICKER_CELIA"}){
        var s=Route(name);int slot=Enumerable.Range(0,s.Battle.Opponent.Pokemon.Moves.Length).First(i=>s.Battle.Opponent.Pokemon.Moves[i].Move==Id("METRONOME"));
        var d=Choose(s);Check(d.Scores[slot]==100,"Metronome keeps authentic no-dispatch score");
        Check(d.Trace.Count(t=>t.Purpose.Contains("damage-percent"))==4,"four original setup draws");
    }
});
Test("Toxic sees converted live type, existing poison and source ability knowledge",()=>{
    var s=Gym();var clean=Choose(s);Check(clean.Scores[3]==100,"healthy normal target Toxic score");
    var poison=Sync(s with{Battle=s.Battle with{Player=s.Battle.Player with{CalledOverlay=new(){Type1="TYPE_POISON",Type2="TYPE_POISON"}}}});
    Check(Choose(poison).Scores[3]==90,"Conversion blocks Toxic without replacing species");
    Check(poison.Battle.Player.Pokemon.Species==s.Battle.Player.Pokemon.Species,"persistent species unchanged");
    var status=Sync(s with{Battle=s.Battle with{Player=s.Battle.Player with{Status=WildBattleSourceFieldEffects.ToxicPoison|0x400}}});
    Check(Choose(status).Scores[3]==90,"existing escalating toxic counted as status");
});
Test("Future Sight duplicate slot and Role Play source no-dispatch behavior",()=>{
    var s=Gym("JUGGLER_NATE",1);var d=Choose(s);Check(d.Scores[2]==100 && d.Scores[3]==100,"new learned source effects");
    s=s with{Battle=s.Battle with{CalledDelayed=new(){PlayerFutureAttack=new(2,Id("FUTURE_SIGHT"),WildBattleSide.Opponent,35)}}};
    Check(Choose(s).Scores[2]==90,"future-attack slot already pending");
});
Test("Koga Minimize, Acid Armor and Selfdestruct retain source viability and faint rules",()=>{
    var muk=Gym(index:1,seed:17);var d=Choose(muk);
    Check(d.Scores[0]>=100 && d.Scores[2]>=100,"source defensive encouragement");
    var capped=Sync(muk with{Battle=muk.Battle with{Opponent=muk.Battle.Opponent with{DefenseStage=12,EvasionStage=12}}});
    var c=Choose(capped);Check(c.Scores[0]<100 && c.Scores[2]<100,"capped defensive stats penalized");
    var koffing=Gym();koffing=Sync(koffing with{Battle=koffing.Battle with{Player=koffing.Battle.Player with{Pokemon=koffing.Battle.Player.Pokemon with{CurrentHp=1}}}});
    var ko=Choose(koffing);Check(ko.PredictedDamage[0]>=1 && ko.Scores[0]<=100,"self-KO gets no TRY_TO_FAINT +4");
    Check(ko.Scores[1]>=104,"ordinary KO gets TRY_TO_FAINT +4");
});
Test("nine pinned independent Muk scoring vectors, semantic RNG and original duplicate tie list",()=>{
    using var vectors=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tests/Game.SourceTrainerAi.Tests/source-vectors.json")));
    foreach(var v in vectors.RootElement.GetProperty("vectors").EnumerateArray()){
        var s=Gym(index:1,seed:v.GetProperty("seed").GetUInt32()) with{KnownPlayerAbility="ABILITY_RUN_AWAY"};
        if(v.GetProperty("capped").GetBoolean()) s=s with{Battle=s.Battle with{Opponent=s.Battle.Opponent with{EvasionStage=12,DefenseStage=12}}};
        if(v.GetProperty("toxic").GetBoolean()) s=s with{Battle=s.Battle with{Player=s.Battle.Player with{Status=WildBattleSourceFieldEffects.ToxicPoison|0x300}}};
        s=Sync(s);var d=Choose(s);
        Check(d.PredictedDamage[1]<s.Battle.Player.Pokemon.CurrentHp,"oracle excludes KO branch by actual damage");
        Check(d.Scores.SequenceEqual(v.GetProperty("scores").EnumerateArray().Select(x=>x.GetInt32())),"source scores seed="+v.GetProperty("seed"));
        Check(d.SimulatedDamagePercent.SequenceEqual(v.GetProperty("simulated").EnumerateArray().Select(x=>x.GetInt32())),"four setup values");
        Check(d.MoveSlot==v.GetProperty("chosen").GetInt32(),"original duplicate best-slot tie choice");
        Check(d.Trace.Select(t=>t.Result).SequenceEqual(v.GetProperty("draws").EnumerateArray().Select(x=>x.GetProperty("result").GetInt32())),"complete semantic RNG path");
    }
});
Test("Koga item original compact slots, strict quarter threshold, reserve gates and purity",()=>{
    var s=Gym();int max=s.Battle.Opponent.Pokemon.Stats.MaxHp;
    ForestTrainerCombatState At(int hp)=>Sync(s with{Battle=s.Battle with{Opponent=s.Battle.Opponent with{Pokemon=s.Battle.Opponent.Pokemon with{CurrentHp=hp}}}});
    var border=SourceTrainerAi.DecideItem(catalog,At(max/4));Check(border.Succeeded && border.ItemSlot is null,"quarter equality must not heal");
    var low=At(max/4-1);var first=SourceTrainerAi.DecideItem(catalog,low);
    Check(first.Succeeded && first.ItemSlot==0 && first.Item=="ITEM_HYPER_POTION" && first.HpAfter==max,"first hyper potion");
    Check(low.CeruleanItemsUsedMask==0 && low.Battle.Opponent.Pokemon.CurrentHp==max/4-1,"selection consumes nothing");
    low=low with{CeruleanItemsUsedMask=1};Check(SourceTrainerAi.DecideItem(catalog,low).ItemSlot is null,"slot1 withheld while four live");
    var faint=low.OpponentParty[3] with{Pokemon=low.OpponentParty[3].Pokemon with{CurrentHp=0}};
    low=Sync(low with{OpponentParty=low.OpponentParty.SetItem(3,faint)});
    var second=SourceTrainerAi.DecideItem(catalog,low);Check(second.ItemSlot==1,"slot1 allowed with three live: "+second.Diagnostic?.Code);
    var cure=Gym();cure=Sync(cure with{CeruleanItemsUsedMask=3,Battle=cure.Battle with{Opponent=cure.Battle.Opponent with{Status=WildBattleSourceFieldEffects.ToxicPoison|0x600,ConfusionTurnsRemaining=3}}});
    Check(SourceTrainerAi.DecideItem(catalog,cure).ItemSlot is null,"Full Heal withheld with four live");
    foreach(int i in new[]{2,3})cure=cure with{OpponentParty=cure.OpponentParty.SetItem(i,cure.OpponentParty[i] with{Pokemon=cure.OpponentParty[i].Pokemon with{CurrentHp=0}})};
    cure=Sync(cure);var full=SourceTrainerAi.DecideItem(catalog,cure);
    Check(full.ItemSlot==2 && full.StatusAfter==0 && full.ConfusionTurnsAfter==0,"Full Heal cures toxic counter and confusion with two live");
    Check(cure.CeruleanItemsUsedMask==3,"Full Heal selection pure");
});
Test("source switch uses live trapping types and zero-timer Perish Song before RNG",()=>{
    var s=Gym();s=Sync(s with{Battle=s.Battle with{Opponent=s.Battle.Opponent with{CalledVolatile=new(){PerishSongTimer=0}}}});
    var choose=SourceTrainerAi.DecideSwitch(catalog,s);Check(choose.Succeeded && choose.PartyIndex is not null && choose.Trace.IsEmpty,"perish forces source replacement without random draws");
    var magnet=Gym(target:"MAGNEMITE");magnet=Sync(magnet with{Battle=magnet.Battle with{Opponent=magnet.Battle.Opponent with{CalledVolatile=new(){PerishSongTimer=0},CalledOverlay=new(){Type1="TYPE_STEEL",Type2="TYPE_STEEL"}}}});
    var trapped=SourceTrainerAi.DecideSwitch(catalog,magnet);Check(trapped.Succeeded && trapped.PartyIndex is null && trapped.Trace.IsEmpty,"converted Steel trapping evaluated before Perish escape");
});
Test("source Natural Cure overlay reaches original switch branch and persistent ability stays intact",()=>{
    var s=Gym();var natural=Enumerable.Range(0,4).Select(i=>Mon("STARYU",pid:(uint)i)).First(m=>m.AbilityId=="ABILITY_NATURAL_CURE");
    var p=new WildBattleCombatant(natural){FirstTurnCounter=1};
    s=Sync(s with{Battle=s.Battle with{Player=p,Opponent=s.Battle.Opponent with{Status=3,CalledOverlay=new(){Ability="ABILITY_NATURAL_CURE"}}}});
    var d=SourceTrainerAi.DecideSwitch(catalog,s);Check(d.Succeeded,d.Diagnostic?.Code??"switch");
    Check(d.Trace.Any(t=>t.Purpose.Contains("natural-cure")),"source Natural Cure branch consumes real semantic RNG");
    Check(s.Battle.Opponent.Pokemon.AbilityId=="ABILITY_LEVITATE","persistent trainer ability retained");
});
Test("Metronome Transform retains real roster identity and scores the live copied moves",()=>{
    var s=Route("BEAUTY_SHEILA");var p=new WildBattleCombatant(Mon("SNORLAX")){FirstTurnCounter=1};
    s=Sync(s with{Battle=s.Battle with{Player=p}});
    int slot=Enumerable.Range(0,s.Battle.Opponent.Pokemon.Moves.Length).First(i=>s.Battle.Opponent.Pokemon.Moves[i].Move==Id("METRONOME"));
    var transformed=WildBattleRules.ResolveCalledMoveAction(catalog,s.Battle,new(WildBattleSide.Opponent,slot,Id("TRANSFORM")));
    Check(transformed.Succeeded,transformed.Fault??"transform");
    s=Sync(s with{Battle=transformed.State!});var d=Choose(s);
    Check(s.Battle.Opponent.Pokemon.SourceSpeciesId==143 && WildBattleTransformationRules.PersistentPokemon(s.Battle.Opponent).SourceSpeciesId==35,"actual transformed view and original Clefairy roster");
    for(int i=0;i<s.Battle.Opponent.Pokemon.Moves.Length;i++)if(catalog.Moves[s.Battle.Opponent.Pokemon.Moves[i].Move].Effect is "EFFECT_SNORE" or "EFFECT_SLEEP_TALK")
        Check(d.Scores[i]==92,"original asleep-only move scoring after Transform");
});
Test("replacement authenticates EXP-first boundary and permits only its internal completed-bit transient",()=>{
    var s=Gym();var faint=new WildBattleCombatant(s.Battle.Opponent.Pokemon with{CurrentHp=0});
    s=Sync(s with{Phase=ForestTrainerCombatPhase.AwaitingExperience,Battle=s.Battle with{Opponent=faint}});
    Check(ForestTrainerCombatRules.Validate(catalog,s) is null,"public EXP-first state valid before award");
    var before=SourceTrainerAi.ChooseReplacement(catalog,s);Check(before.Succeeded && before.PartyIndex is >0,before.Diagnostic?.Code??"replacement");
    var transient=s with{ExperienceCompletedMask=1};
    Check(ForestTrainerCombatRules.Validate(catalog,transient) is not null,"transient is not a publishable trainer state");
    var after=SourceTrainerAi.ChooseReplacement(catalog,transient);
    Check(after.Succeeded && after.PartyIndex==before.PartyIndex,"same source suitability after EXP bit set");
    Check(transient.Battle.Opponent.Pokemon.CurrentHp==0,"replacement never fabricates active HP");
    Check(!SourceTrainerAi.ChooseReplacement(catalog,Gym()).Succeeded,"ordinary ready phase is not replacement");
});
Test("bad opt-in, roster, item mask and history fail without RNG or partial scores",()=>{
    var s=Gym();foreach(var bad in new[]{s with{SourceMoveRulesVersion=null},s with{RulesVersion=ForestTrainerCombatRules.FuchsiaRulesVersion},s with{CeruleanItemsUsedMask=8},s with{KnownPlayerAbility="ABILITY_FAKE"}}){
        var d=SourceTrainerAi.Choose(catalog,bad);Check(!d.Succeeded && d.Rng==s.Battle.Rng && d.Trace.IsEmpty && d.Scores.IsEmpty,"atomic source rejection");
    }
    var forged=s.Battle.Opponent with{Pokemon=s.Battle.Opponent.Pokemon with{Personality=s.Battle.Opponent.Pokemon.Personality+2}};
    Check(!SourceTrainerAi.Choose(catalog,Sync(s with{Battle=s.Battle with{Opponent=forged}})).Succeeded,"forged roster rejected");
});
Console.WriteLine(JsonSerializer.Serialize(new{groups,assertions,failures,runtime=Environment.Version.ToString(),source=SourceTrainerAi.SourceCommit}));
if(groups==0||assertions==0||failures!=0)Environment.Exit(1);
