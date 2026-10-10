using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 4) throw new ArgumentException("CANONICAL_ROOT MOVE_ROOT PINNED_SOURCE VECTOR_JSON");
var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0], args[1]);
var catalog = mechanics.Catalog;
string source = Path.GetFullPath(args[2]);
string tm = File.ReadAllText(Path.Combine(source, "src/data/pokemon/tmhm_learnsets.h"));
string tutor = File.ReadAllText(Path.Combine(source, "src/data/pokemon/tutor_learnsets.h"));
int groups = 0, checks = 0, failures = 0, turns = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
void Test(string name, Action action) { groups++; try { action(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " " + e); } }
MoveId Move(string name) => new("pret:MOVE_" + name);
WildBattleCombatant Mon(string species, string[] moves, int level = 50, uint pid = 0)
{
    var p = SourcePokemonFactory.Create(mechanics, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    foreach (var name in moves)
    {
        bool learned = catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == Move(name) && m.Level <= level);
        string tmBlock = Regex.Match(tm, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
        string tutorBlock = Regex.Match(tutor, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
        Check(learned || Regex.IsMatch(tmBlock, @"TM\d\d_" + name + @"\)") || tutorBlock.Contains("TUTOR(MOVE_" + name + ")"), "authentic retained level/TM/tutor move " + species + "/" + name);
    }
    return new(p with { Moves = moves.Select(name => new PokemonMoveSlot(Move(name), catalog.Moves[Move(name)].Pp, catalog.Moves[Move(name)].Pp)).ToImmutableArray() });
}
WildBattleState Require(WildBattleResult r, string label)
{
    Check(r.Succeeded, label + ": " + r.Fault);
    Check(WildBattleRules.Validate(catalog, r.State) is null, label + " output valid: " + WildBattleRules.Validate(catalog, r.State));
    return r.State!;
}
WildBattleState Start(WildBattleCombatant player, WildBattleCombatant opponent, uint seed = 0, params WildBattleCombatant[] reserves) =>
    Require(WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [player, .. reserves], 0, opponent, new(seed)), "entry");
WildBattleResult Turn(WildBattleState s, int slot = 0)
{
    var r = WildBattleRules.ResolveMove(catalog, s, slot);
    var next = Require(r, "full turn"); turns++;
    Check(next.Outcome != WildBattleOutcome.Ongoing || next.AwaitingReplacement || next.CalledParty?.PendingSwitch is not null || next.Turn == s.Turn + 1, "full turn owns readiness");
    return r;
}
WildBattleCombatant Get(WildBattleState s, WildBattleSide side) => side == WildBattleSide.Player ? s.Player : s.Opponent;
WildBattleState Sync(WildBattleState s) => s with { CalledParty = s.CalledParty! with {
    Player = s.CalledParty.Player.SetItem(s.CalledParty.PlayerActiveIndex, s.CalledParty.Player[s.CalledParty.PlayerActiveIndex] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(s.Player), Status = WildBattleTransformationRules.PersistentStatus(s.Player) }),
    Opponent = s.CalledParty.Opponent.SetItem(0, s.CalledParty.Opponent[0] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(s.Opponent), Status = WildBattleTransformationRules.PersistentStatus(s.Opponent) }) } };
WildBattleState Restore(WildBattleState s)
{
    string json = CanonicalJson.Text(s); var result = JsonSerializer.Deserialize<WildBattleState>(json)!;
    Check(CanonicalJson.Text(result) == json && WildBattleRules.Validate(catalog, result) is null, "canonical JSON round trip and validation");
    return result;
}
void Atomic(WildBattleState s, string label)
{
    string before = CanonicalJson.Hash(s); var r = WildBattleRules.ResolveMove(catalog, s, 0);
    Check(!r.Succeeded && ReferenceEquals(r.State, s) && r.Events.IsEmpty && r.Trace.IsEmpty && CanonicalJson.Hash(s) == before, label + " rollback");
}
WildBattleResult Action(WildBattleState s, WildBattleSide side = WildBattleSide.Player, int slot = 0)
{
    var r = WildBattleRules.ResolveSourceMoveAction(catalog, s, new(side, slot)).Battle;
    Require(r, "shared source action"); return r;
}

const WildBattleSide P = WildBattleSide.Player, O = WildBattleSide.Opponent;
WildBattleResult Entry(WildBattleCombatant p, WildBattleCombatant o, uint seed=0, params WildBattleCombatant[] reserves)
{
    var r = WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [p,.. reserves], 0, o, new(seed));
    Require(r,"entry"); return r;
}
void Trace(WildBattleState before, WildBattleResult r)
{
    uint seed=before.Rng.Seed;
    foreach(var draw in r.Trace) { seed=unchecked(seed*1103515245+24691); Check(draw.Raw==(ushort)(seed>>16),"raw LCG "+draw.Purpose); }
    Check(r.State!.Rng.Seed==seed && r.State.Rng.Cursor==before.Rng.Cursor+(ulong)r.Trace.Length,"terminal RNG");
}
WildBattleResult End(WildBattleState s)
{
    var r=WildBattleRules.ResolveSourceEndTurnPhase(catalog,s).Battle;
    Check(r.Succeeded,"end-turn stage "+r.Fault); Trace(s,r); return r;
}
WildBattleState Roundtrip(WildBattleResult r)
{
    var s=r.State!; var imported=SinglesBattleAdapter.Import(catalog,s);Check(imported.Succeeded,"frame import "+imported.Fault);
    var frame=JsonSerializer.Deserialize<SourceBattleFrame>(CanonicalJson.Text(imported.Frame))!;
    Check(SourceBattleIdentityRules.Validate(frame) is null,"frame valid");
    var exported=SinglesBattleAdapter.Export(frame);Check(exported.Succeeded,"frame export "+exported.Fault);
    Check(CanonicalJson.Text(s)==CanonicalJson.Text(exported.State),"sole weather authority lossless state");
    Check(frame.Field.PermanentWeather==s.PermanentWeather,"field weather retained");
    foreach(var e in r.Events) {
        var canonical=SourceBattleEventAdapter.Import(e);
        Check(SourceBattleEventHooks.ValidateRouting(frame,canonical) is null,"typed canonical routing");
        var saved=JsonSerializer.Deserialize<SourceBattleEvent>(JsonSerializer.Serialize<SourceBattleEvent>(canonical))!;
        Check(CanonicalJson.Text<WildBattleEvent>(e)==CanonicalJson.Text<WildBattleEvent>(SourceBattleEventAdapter.Export(saved)),"typed event roundtrip "+e.GetType().Name);
    }
    return exported.State!;
}
Test("real factory donors, permanent identity and bounded campaign admission",()=>{
    Check(Environment.Version.Major==10 && catalog.Species.Count==386,"official runtime/authenticated mechanics");
    foreach(var (name,pid,ability) in new[]{("KYOGRE",0u,"ABILITY_DRIZZLE"),("GROUDON",0u,"ABILITY_DROUGHT"),("TYRANITAR",0u,"ABILITY_SAND_STREAM"),
        ("LOTAD",1u,"ABILITY_RAIN_DISH"),("LOMBRE",1u,"ABILITY_RAIN_DISH"),("LUDICOLO",1u,"ABILITY_RAIN_DISH"),("PSYDUCK",1u,"ABILITY_CLOUD_NINE"),("RAYQUAZA",0u,"ABILITY_AIR_LOCK")}) {
        var mon=Mon(name,["PROTECT"],pid:pid);Check(mon.Pokemon.AbilityId==ability,"real donor "+name);
        Check(SourcePokemonValidator.ValidatePermanent(mechanics,mon.Pokemon) is null,"permanent source validation "+name);
        if(name!="PSYDUCK")Check(!CampaignAdmissionPolicy.CurrentFuchsia.Admits(mon.Pokemon.Species),"no campaign expansion "+name);
    }
});
Test("original C entry and Rain Dish vectors match shared recipes",()=>{
    using var d=JsonDocument.Parse(File.ReadAllBytes(args[3]));Check(d.RootElement.GetProperty("sourceCommit").GetString()==catalog.SourceCommit,"source commit");
    int vectors=0;
    foreach(var row in d.RootElement.GetProperty("entryRows").EnumerateArray()) {
        string ability=row.GetProperty("ability").GetString()!; int bits=row.GetProperty("before").GetInt32();
        SourceFieldRegisters f=bits switch {1=>new(){RainTurnsRemaining=4},5=>new(){PermanentWeather=new(SourcePermanentWeatherKind.Rain)},8=>new(){SandstormTurnsRemaining=4},24=>new(){PermanentWeather=new(SourcePermanentWeatherKind.Sandstorm)},32=>new(){SunTurnsRemaining=4},96=>new(){PermanentWeather=new(SourcePermanentWeatherKind.Sun)},128=>new(){HailTurnsRemaining=4},_=>new()};
        var next=SourceWeatherRules.ApplyEntry(f,ability);int actual=next.PermanentWeather?.Kind switch{SourcePermanentWeatherKind.Rain=>5,SourcePermanentWeatherKind.Sandstorm=>24,SourcePermanentWeatherKind.Sun=>96,_=>bits};
        Check(actual==row.GetProperty("after").GetInt32(),"source exact weather bits");Check((!ReferenceEquals(f,next)?1:0)==row.GetProperty("effect").GetInt32(),"source callback count");vectors++;
    }
    foreach(var row in d.RootElement.GetProperty("rainRows").EnumerateArray()) {
        int N(string n)=>row.GetProperty(n).GetInt32();
        int actual=SourceWeatherRules.RainDishRecovery("ABILITY_RAIN_DISH",N("hp"),N("maximum"),N("rain")!=0,N("suppressed")!=0);
        Check(actual==Math.Min(N("maximum")-N("hp"),N("recovery")),"source exact Rain Dish quantum/cap");vectors++;
    }
    Check(vectors==96,"all 96 extracted source vectors");
});
foreach(var (name,move,kind) in new[]{("KYOGRE","CALM_MIND",SourcePermanentWeatherKind.Rain),("GROUDON","BULK_UP",SourcePermanentWeatherKind.Sun),("TYRANITAR","LEER",SourcePermanentWeatherKind.Sandstorm)})
foreach(var side in new[]{P,O})Test(name+" creates permanent weather and persists across seven complete turns on "+side,()=>{
    var mon=Mon(name,[move]);var foe=Mon("MAGIKARP",["SPLASH"],100);
    var entry=side==P?Entry(mon,foe):Entry(foe,mon);var s=Roundtrip(entry);
    Check(s.PermanentWeather?.Kind==kind && s.RainTurnsRemaining==0 && s.SandstormTurnsRemaining==0 && (s.CalledField?.SunTurnsRemaining??0)==0,"distinct permanent representation");
    Check(entry.Events.OfType<WildBattlePermanentWeatherChangedEvent>().Single().Side==side,"actual owner entry callback");
    for(int i=0;i<7;i++){var r=Turn(s);Trace(s,r);s=Roundtrip(r);Check(s.PermanentWeather?.Kind==kind,"no countdown/expiry");
        Check(!r.Events.OfType<WildBattleMtMoonVolatileEvent>().Any(e=>e.Volatile is "rain" or "sun" or "sandstorm"),"no fictional duration events");}
});
Test("initial entry order is speed then source tie draw, slowest setter wins",()=>{
    foreach(bool reverse in new[]{false,true})foreach(uint seed in new uint[]{0,1,2,3}) {
        var k=Mon("KYOGRE",["CALM_MIND"],30);var g=Mon("GROUDON",["BULK_UP"],60);
        var r=reverse?Entry(g,k,seed):Entry(k,g,seed);var order=r.Events.OfType<WildBattleOrderEvent>().Single(e=>e.Phase==WildBattleOrderPhase.Entry);
        var weather=r.Events.OfType<WildBattlePermanentWeatherChangedEvent>().ToArray();
        Check(weather.Length==2 && weather[0].Side==order.First && weather[1].Side==order.Second,"callbacks use original sorted entry order");
        Check(r.State!.PermanentWeather?.Kind==SourcePermanentWeatherKind.Rain,"slower Kyogre wins");
        Check(r.Trace.Length==5,"four AI setup and one ready draw; no weather RNG");
    }
    for(uint seed=0;seed<6;seed++) {
        var r=Entry(Mon("KYOGRE",["CALM_MIND"]),Mon("GROUDON",["BULK_UP"]),seed);
        Check(r.State!.Player.Pokemon.Stats.Speed==r.State.Opponent.Pokemon.Stats.Speed,"real equal-speed donors");
        var order=r.Events.OfType<WildBattleOrderEvent>().Single(e=>e.Phase==WildBattleOrderPhase.Entry);
        Check(r.Events.OfType<WildBattlePermanentWeatherChangedEvent>().Last().Side==order.Second,"tie result reused");
        Check(r.Trace.Count(t=>t.Purpose=="wild-battle.entry.speed-tie")==1,"one source entry tie draw");
    }
});
Test("temporary weather replacement, same weather failure, and expiry",()=>{
    var s=Start(Mon("ALAKAZAM",["RAIN_DANCE","SUNNY_DAY","CALM_MIND"]),Mon("KYOGRE",["CALM_MIND"]));
    var same=Action(s);Check(same.State!.PermanentWeather==s.PermanentWeather && same.Events.OfType<WildBattleMoveFailedEvent>().Any(e=>e.Reason=="already-rain"),"Rain Dance fails under permanent rain");
    var changed=Action(s,O); // Calm Mind leaves weather untouched.
    var sun=Action(changed.State!,P,1);s=sun.State!;
    Check(s.PermanentWeather is null && s.CalledField?.SunTurnsRemaining==5 && !SourceWeatherRules.HasRain(s),"move replaces permanent with five-turn sun");
    for(int i=0;i<5;i++){var r=Turn(s,2);s=r.State!;Check(s.PermanentWeather is null,"sun never made permanent");}
    Check(!SourceWeatherRules.HasSun(s),"temporary sun expires normally");
    s=Start(Mon("MAGIKARP",["SPLASH"]),Mon("MAGIKARP",["SPLASH"]),0,Mon("KYOGRE",["CALM_MIND"]));
    s=s with{RainTurnsRemaining=3};var switched=WildBattleRules.SwitchSourceWildPlayer(catalog,s,1);Require(switched,"replace temporary rain by Drizzle");
    Check(switched.State!.PermanentWeather?.Kind==SourcePermanentWeatherKind.Rain && switched.State.RainTurnsRemaining==0,"entry upgrades same temporary weather");
});
Test("weather setter switching and fainting leave weather until another setter replaces it",()=>{
    var s=Start(Mon("KYOGRE",["CALM_MIND"]),Mon("MAGIKARP",["SPLASH"]),0,Mon("GROUDON",["BULK_UP"]),Mon("MAGIKARP",["SPLASH"]));
    s=Require(WildBattleRules.SwitchSourceWildPlayer(catalog,s,2),"leave setter");Check(SourceWeatherRules.HasRain(s),"rain survives source leaving");
    s=Require(WildBattleRules.SwitchSourceWildPlayer(catalog,s,1),"enter Groudon");Check(s.PermanentWeather?.Kind==SourcePermanentWeatherKind.Sun,"new setter replaces weather");
    s=Sync(s with{Player=s.Player with{Pokemon=s.Player.Pokemon with{CurrentHp=1},Status=WildBattleStatus.Poison}});
    var dead=Turn(s);Check(dead.State!.AwaitingReplacement && dead.State.Player.Pokemon.CurrentHp==0 && dead.State.PermanentWeather?.Kind==SourcePermanentWeatherKind.Sun,"source faint leaves weather at paused replacement");
    var replacement=WildBattleRules.ReplaceSourceWildPlayer(catalog,Restore(dead.State),0);s=Require(replacement,"post residual replacement");
    Check(s.PermanentWeather?.Kind==SourcePermanentWeatherKind.Rain,"replacement setter runs on actual callback");
});
Test("Cloud Nine and Air Lock suppress effects without clearing permanent weather",()=>{
    foreach(var (species,pid) in new[]{("PSYDUCK",1u),("RAYQUAZA",0u)}) {
        var s=Start(Mon("KYOGRE",["CALM_MIND"]),Mon("LOTAD",["GROWL"],pid:1),0,Mon(species,["PROTECT"],pid:pid));
        s=Sync(s with{Opponent=s.Opponent with{Pokemon=s.Opponent.Pokemon with{CurrentHp=s.Opponent.Pokemon.Stats.MaxHp-40}}});
        var switched=WildBattleRules.SwitchSourceWildPlayer(catalog,s,1);s=Require(switched,"suppressor enters");
        Check(s.PermanentWeather?.Kind==SourcePermanentWeatherKind.Rain && !switched.Events.OfType<WildBattleAbilityRecoveredEvent>().Any(),"suppression preserves weather and blocks Rain Dish");
        var frame=SinglesBattleAdapter.Import(catalog,s).Frame!;Check(SourceWeatherRules.IsSuppressed(frame),"whole frame suppression");
        var leaving=WildBattleRules.SwitchSourceWildPlayer(catalog,s,0);s=Require(leaving,"suppressor leaves");
        Check(s.PermanentWeather?.Kind==SourcePermanentWeatherKind.Rain && leaving.Events.OfType<WildBattleAbilityRecoveredEvent>().Any(),"weather effects resume immediately");
        Check(!leaving.Events.OfType<WildBattlePermanentWeatherChangedEvent>().Any(),"same permanent Drizzle does not fire again");
    }
});
Test("suppression queries preserve distinct source HP and absent semantics",()=>{
    var s=Start(Mon("PSYDUCK",["PROTECT"],pid:1),Mon("MAGIKARP",["SPLASH"]));
    var frame=SinglesBattleAdapter.Import(catalog,s).Frame!;var cell=frame.Slots[0];
    frame=frame with{Slots=frame.Slots.SetItem(0,cell with{Body=cell.Body! with{Pokemon=cell.Body.Pokemon with{CurrentHp=0}}})};
    Check(!SourceWeatherRules.IsSuppressed(frame) && SourceWeatherRules.IsSuppressed(frame,true),"CHECK_ON_FIELD living; FIELD_SPORT retained fainted");
    frame=frame with{Slots=frame.Slots.SetItem(0,cell),SourceAbsentMask=(byte)(frame.SourceAbsentMask|1)};
    Check(SourceWeatherRules.IsSuppressed(frame) && SourceWeatherRules.IsSuppressed(frame,true),"source queries do not filter absent flags");
});
foreach(string species in new[]{"LOTAD","LOMBRE","LUDICOLO"})foreach(var side in new[]{P,O})
Test(species+" Rain Dish restores sixteenth before poison on "+side,()=>{
    var dish=Mon(species,["PROTECT"],pid:1);dish=dish with{Pokemon=dish.Pokemon with{CurrentHp=dish.Pokemon.Stats.MaxHp-40},Status=WildBattleStatus.Poison};
    var setter=Mon("KYOGRE",["CALM_MIND"]);var s=side==P?Start(dish,setter):Start(setter,dish);
    int before=Get(s,side).Pokemon.CurrentHp;var r=Turn(s);var heal=r.Events.OfType<WildBattleAbilityRecoveredEvent>().Single(e=>e.Side==side);
    var poison=r.Events.OfType<WildBattleStatusDamageEvent>().Single(e=>e.Side==side);
    int quantum=Math.Max(1,dish.Pokemon.Stats.MaxHp/16);Check(heal.Recovered==quantum && heal.HpBefore==before,"source integer sixteenth");
    Check(heal.Sequence<poison.Sequence && poison.HpBefore==heal.HpAfter,"ability before status residual");
    Check(r.Events.OfType<WildBattleAbilityActivatedEvent>().Count(e=>e.Ability=="ABILITY_RAIN_DISH")==1,"single ability stage dispatch");
    Check(!r.Trace.Any(t=>t.Purpose.Contains("rain-dish")),"no ability RNG");Roundtrip(r);
});
Test("Rain Dish full HP, minimum one, cap, no rain, expired rain, and dead holder",()=>{
    var tiny=Mon("LOTAD",["PROTECT"],1,1);Check(tiny.Pokemon.Stats.MaxHp<16,"real level one max HP");
    var s=Start(tiny with{Pokemon=tiny.Pokemon with{CurrentHp=tiny.Pokemon.Stats.MaxHp-1}},Mon("KYOGRE",["CALM_MIND"]));
    var r=End(s);Check(r.Events.OfType<WildBattleAbilityRecoveredEvent>().Single().Recovered==1,"minimum and cap to max HP");
    s=Sync(s with{Player=s.Player with{Pokemon=s.Player.Pokemon with{CurrentHp=s.Player.Pokemon.Stats.MaxHp}}});Check(!End(s).Events.OfType<WildBattleAbilityRecoveredEvent>().Any(),"full HP no callback");
    s=Sync(s with{PermanentWeather=null,Player=s.Player with{Pokemon=s.Player.Pokemon with{CurrentHp=1}}});Check(!End(s).Events.OfType<WildBattleAbilityRecoveredEvent>().Any(),"no rain no callback");
    Check(!End(s with{RainTurnsRemaining=1}).Events.OfType<WildBattleAbilityRecoveredEvent>().Any(),"rain expires in field phase before ability");
    Check(SourceWeatherRules.RainDishRecovery("ABILITY_RAIN_DISH",0,15,true,false)==0,"dead holder no callback");
});
Test("real Role Play and Skill Swap acquire and lose Rain Dish at current end-turn stage",()=>{
    var s=Start(Mon("ALAKAZAM",["SKILL_SWAP","RAIN_DANCE"]),Mon("LOTAD",["GROWL"],pid:1));
    s=Action(s,P,1).State!;s=Sync(s with{Player=s.Player with{Pokemon=s.Player.Pokemon with{CurrentHp=s.Player.Pokemon.Stats.MaxHp-30}},Opponent=s.Opponent with{Pokemon=s.Opponent.Pokemon with{CurrentHp=s.Opponent.Pokemon.Stats.MaxHp-30}}});
    var swap=Action(s);s=swap.State!;Check(WildBattleEffective.Ability(s.Player)=="ABILITY_RAIN_DISH" && WildBattleEffective.Ability(s.Opponent)!="ABILITY_RAIN_DISH","real skill swap overlay");
    var end=End(s);Check(end.Events.OfType<WildBattleAbilityRecoveredEvent>().Single().Side==P,"only current holder heals");
    s=Start(Mon("KADABRA",["ROLE_PLAY","RAIN_DANCE"]),Mon("LOTAD",["GROWL"],pid:1));s=Action(s,P,1).State!;s=Action(s).State!;
    Check(WildBattleEffective.Ability(s.Player)=="ABILITY_RAIN_DISH","real role play");
});
Test("acquiring or losing entry weather abilities does not trigger another entry",()=>{
    var s=Start(Mon("ALAKAZAM",["SKILL_SWAP","SUNNY_DAY"]),Mon("KYOGRE",["CALM_MIND"]));s=Action(s,P,1).State!;
    var swap=Action(s);s=swap.State!;
    Check(WildBattleEffective.Ability(s.Player)=="ABILITY_DRIZZLE" && s.PermanentWeather is null && s.CalledField?.SunTurnsRemaining==5,"source ability copy does not call ON_SWITCHIN");
    Check(!swap.Events.OfType<WildBattlePermanentWeatherChangedEvent>().Any(),"no spurious entry callback");
});
Test("source ability phase combines Rain Dish, Speed Boost, Truant, and Shed Skin once",()=>{
    foreach(var (species,move) in new[]{("NINJASK","HARDEN"),("SLAKOTH","AMNESIA"),("KAKUNA","HARDEN")}) {
        var s=Start(Mon("LOTAD",["RAIN_DANCE"],pid:1),Mon(species,[move]));
        s=Sync(s with{Player=s.Player with{Pokemon=s.Player.Pokemon with{CurrentHp=s.Player.Pokemon.Stats.MaxHp-30}},Opponent=species=="KAKUNA"?s.Opponent with{Status=WildBattleStatus.Paralysis}:s.Opponent});
        var r=Turn(s);Check(r.Events.OfType<WildBattleAbilityRecoveredEvent>().Count()==1,"one Rain Dish callback with "+species);
        if(species=="NINJASK")Check(r.State!.Opponent.SpeedStage==7,"one Speed Boost");
        if(species=="SLAKOTH")Check(r.State!.Opponent.TruantLoafing,"one Truant toggle");
        if(species=="KAKUNA")Check(r.Trace.Count(t=>t.Purpose.EndsWith("shed-skin"))==1,"one Shed Skin draw");
    }
});
Test("Spikes KO precedes incoming weather, living entry precedes sandstorm residual",()=>{
    var incoming=Mon("TYRANITAR",["LEER"]);var dying=incoming with{Pokemon=incoming.Pokemon with{CurrentHp=1}};
    var s=Start(Mon("KYOGRE",["CALM_MIND"]),Mon("MAGIKARP",["SPLASH"]),0,dying,Mon("GROUDON",["BULK_UP"]));
    s=s with{CalledParty=s.CalledParty! with{PlayerSpikes=3}};
    var r=WildBattleRules.SwitchSourceWildPlayer(catalog,s,1);Require(r,"Spikes incoming KO");
    Check(r.State!.Player.Pokemon.CurrentHp==0 && r.State.PermanentWeather?.Kind==SourcePermanentWeatherKind.Rain,"fainted incoming Sand Stream never activates");
    Check(!r.Events.OfType<WildBattlePermanentWeatherChangedEvent>().Any(),"no callback before Spikes");
    s=Start(Mon("KYOGRE",["CALM_MIND"]),Mon("MAGIKARP",["SPLASH"]),0,incoming);
    r=WildBattleRules.SwitchSourceWildPlayer(catalog,s,1);Require(r,"living switch");
    Check(r.Events.OfType<WildBattlePermanentWeatherChangedEvent>().Single().Sequence<r.Events.OfType<WildBattleWeatherDamageEvent>().First().Sequence,"entry before residual weather damage");
});
Test("roundtrip whole-turn resume and failure atomicity, old profiles reject new state",()=>{
    var s=Start(Mon("LOTAD",["GROWL"],pid:1),Mon("KYOGRE",["CALM_MIND"]));
    s=Sync(s with{Player=s.Player with{Pokemon=s.Player.Pokemon with{CurrentHp=s.Player.Pokemon.Stats.MaxHp-20}}});
    var imported=SinglesBattleAdapter.Import(catalog,s).Frame!;var restored=SinglesBattleAdapter.Export(JsonSerializer.Deserialize<SourceBattleFrame>(CanonicalJson.Text(imported))!).State!;
    Check(CanonicalJson.Text(Turn(s))==CanonicalJson.Text(Turn(restored)),"canonical next full transition exact");
    Atomic(s with{PermanentWeather=s.PermanentWeather! with{RulesVersion="bad"}},"weather version");
    Atomic(s with{RainTurnsRemaining=5},"competing temporary/permanent weather");
    Atomic(s with{PermanentWeather=new((SourcePermanentWeatherKind)99)},"unknown permanent kind");
    Atomic(s with{SourceMechanicsRulesVersion=null},"old profile");
    Atomic(s with{Revision=long.MaxValue},"postexecution revision overflow");
    Atomic(s with{EventSequence=long.MaxValue-1},"postexecution event overflow");
    Check(SourceBattleIdentityRules.Validate(imported with{SourceMechanicsRulesVersion=null})=="source_weather_profile","canonical schema rejects legacy weather");
    Check(!CanonicalJson.Text(s with{PermanentWeather=null}).Contains("PermanentWeather"),"null register absent from historical JSON");
});
Test("permanent and temporary weather give identical shared damage, accuracy and recovery recipes",()=>{
    foreach(var (species,move,kind) in new[]{("KYOGRE","WATER_PULSE",SourcePermanentWeatherKind.Rain),("GROUDON","FLAMETHROWER",SourcePermanentWeatherKind.Sun),
        ("PIKACHU","THUNDER",SourcePermanentWeatherKind.Rain),("VENUSAUR","SOLAR_BEAM",SourcePermanentWeatherKind.Sun),("CLEFAIRY","MOONLIGHT",SourcePermanentWeatherKind.Sun),
        ("CLEFAIRY","MOONLIGHT",SourcePermanentWeatherKind.Rain),("KYOGRE","WATER_PULSE",SourcePermanentWeatherKind.Sandstorm)}) {
        var s=Start(Mon(species,[move]),Mon("SNORLAX",["AMNESIA"],100));
        s=Sync(s with{PermanentWeather=new(kind),RainTurnsRemaining=0,SandstormTurnsRemaining=0,CalledField=null,
            Player=s.Player with{Pokemon=s.Player.Pokemon with{CurrentHp=s.Player.Pokemon.Stats.MaxHp-40}}});
        var temporary=s with{PermanentWeather=null,RainTurnsRemaining=kind==SourcePermanentWeatherKind.Rain?4:0,SandstormTurnsRemaining=kind==SourcePermanentWeatherKind.Sandstorm?4:0,
            CalledField=kind==SourcePermanentWeatherKind.Sun?new(){SunTurnsRemaining=4}:null};
        var p=Action(s);var t=Action(temporary);
        Check(CanonicalJson.Text(p.Events)==CanonicalJson.Text(t.Events)&&CanonicalJson.Text(p.Trace)==CanonicalJson.Text(t.Trace),"weather source action event/RNG parity "+move+"/"+kind);
        Check(CanonicalJson.Text(p.State!.Player)==CanonicalJson.Text(t.State!.Player)&&CanonicalJson.Text(p.State.Opponent)==CanonicalJson.Text(t.State.Opponent),"weather source action combatants "+move);
    }
});
Test("weather speed abilities use permanent field and immediately respect suppression",()=>{
    foreach(var (name,move,kind,pid) in new[]{("LOTAD","GROWL",SourcePermanentWeatherKind.Rain,0u),("ODDISH","SWEET_SCENT",SourcePermanentWeatherKind.Sun,0u)}) {
        var s=Start(Mon(name,[move],60,pid),Mon("PSYDUCK",["TAIL_WHIP"],45,1));
        s=s with{PermanentWeather=new(kind)};
        int own=s.Player.Pokemon.Stats.Speed,other=s.Opponent.Pokemon.Stats.Speed;
        Check(own<other && own*2>other,"real donor speeds cross only with weather "+name);
        var suppressed=Turn(s);Check(suppressed.Events.OfType<WildBattleOrderEvent>().First(e=>e.Phase==WildBattleOrderPhase.Actions).First==O,"live Cloud Nine suppresses weather speed");
        var clear=Start(Mon(name,[move],60,pid),Mon("PSYDUCK",["TAIL_WHIP"],45,0));clear=clear with{PermanentWeather=new(kind)};
        var boosted=Turn(clear);Check(boosted.Events.OfType<WildBattleOrderEvent>().First(e=>e.Phase==WildBattleOrderPhase.Actions).First==P,"permanent weather doubles source speed "+name);
    }
});
Test("sandstorm survives setter entry under suppression and resumes on suppressor switch",()=>{
    var s=Start(Mon("PSYDUCK",["TAIL_WHIP"],pid:1),Mon("TYRANITAR",["LEER"]),0,Mon("MAGIKARP",["SPLASH"]));
    Check(s.PermanentWeather?.Kind==SourcePermanentWeatherKind.Sandstorm,"Sand Stream sets despite active suppressor");
    var protectedTurn=Turn(s);Check(!protectedTurn.Events.OfType<WildBattleWeatherDamageEvent>().Any(),"live suppression blocks sand damage");
    s=Require(WildBattleRules.SwitchSourceWildPlayer(catalog,protectedTurn.State!,1),"leave suppressor");
    Check(s.PermanentWeather?.Kind==SourcePermanentWeatherKind.Sandstorm,"sand still present after suppressor leaves");
    Check(s.Player.Pokemon.CurrentHp<s.Player.Pokemon.Stats.MaxHp,"sand damage resumes on switch turn");
});
Console.WriteLine(JsonSerializer.Serialize(new{passed=failures==0,groups,checks,turns,failures,runtime=Environment.Version.ToString(),sourceCommit=catalog.SourceCommit}));
return failures==0 && groups>0 && checks>0 && turns>0?0:1;
