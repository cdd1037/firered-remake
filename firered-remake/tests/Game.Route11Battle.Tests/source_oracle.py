#!/usr/bin/env python3
"""Independent source-derived branch/RNG vectors; does not import product code.
Recovered from the 2026-10-09 worker context. Source SHA values are freshly verified.
The prior explicitly approved Pewter secondary correction (< rather than <=) remains.
"""
import hashlib,json,pathlib,sys,subprocess
HERE=pathlib.Path(__file__).resolve().parent
SOURCE=pathlib.Path(sys.argv[1]).resolve()
commit=subprocess.check_output(['git','-C',str(SOURCE),'rev-parse','HEAD'],text=True).strip()
assert commit=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
FILES=['src/random.c','src/battle_main.c','src/battle_util.c','src/battle_script_commands.c','src/pokemon.c','src/wild_encounter.c','src/data/battle_moves.h','data/battle_scripts_1.s','src/data/pokemon/pokedex_entries.h']
ANCHORS={
 'src/battle_main.c':['u8 IsRunningFromBattleImpossible(void)','gBattleMons[i].ability == ABILITY_ARENA_TRAP','gBattleMons[gActiveBattler].ability != ABILITY_LEVITATE','ABILITY_SWIFT_SWIM'],
 'src/battle_util.c':['u8 GetImprisonedMovesCount','case CANCELLER_IMPRISONED:','case CANCELLER_FROZEN:','case ABILITY_WATER_ABSORB:','gBattleMoves[move].power != 0'],
 'src/battle_script_commands.c':['static void Cmd_tryimprison(void)','static void Cmd_tryexplosion(void)','case MOVE_EFFECT_TRI_ATTACK:','Random() % 3 + 3','gWishFutureKnock.weatherDuration = 5;'],
 'data/battle_scripts_1.s':['BattleScript_EffectWillOWisp::','jumpiftype BS_TARGET, TYPE_FIRE, BattleScript_NotAffected','BattleScript_EffectRainDance::','BattleScript_EffectImprison::']}
for f,anchors in ANCHORS.items():assert all(a in (SOURCE/f).read_text() for a in anchors),f
part=(SOURCE/'src/wild_encounter.c').read_text().split('static u8 GetAbilityEncounterRateModType(void)')[1].split('\n}')[0]
assert 'ABILITY_STENCH' in part and 'ABILITY_ILLUMINATE' in part and 'ABILITY_ARENA_TRAP' not in part
class Rng:
 def __init__(self,seed):self.seed=seed;self.trace=[]
 def draw(self,purpose,bound=65536):
  self.seed=(self.seed*0x41c64e6d+0x6073)&0xffffffff;raw=self.seed>>16
  self.trace.append(dict(purpose=purpose,raw=raw,result=raw%bound));return raw
 def choose(self):
  while self.draw('wild-battle.opponent.choose-slot',4)&3:pass
 def end(self):self.draw('wild-battle.turn-end.random-turn-number')
def vector(seed,kind):
 r=Rng(seed);r.choose();status=0;hit=True;binding=0;confusion=0
 if kind=='wisp':hit=r.draw('wild-battle.player.accuracy',100)%100+1<=75;status=16 if hit else 0
 elif kind in ('tri','mud'):
  r.draw('wild-battle.player.accuracy',100);r.draw('wild-battle.player.critical',16);r.draw('wild-battle.player.damage',16)
  sec=r.draw('wild-battle.player.secondary-effect',100)%100
  if kind=='tri' and sec<20:status=[16,32,64][r.draw('wild-battle.player.tri-attack-status',3)%3]
 elif kind=='sand':
  hit=r.draw('wild-battle.player.accuracy',100)%100+1<=70
  if hit:
   r.draw('wild-battle.player.critical',16);r.draw('wild-battle.player.damage',16);r.draw('wild-battle.player.secondary-effect',100)
   binding=(r.draw('wild-battle.player.bind-duration',4)&3)+2
 elif kind=='confuse':
  r.draw('wild-battle.player.accuracy',100);confusion=(r.draw('wild-battle.player.confusion-duration',4)&3)+2
 elif kind=='absorb':r.draw('wild-battle.player.accuracy',100)
 elif kind=='frozen':
  hit=r.draw('wild-battle.player.freeze-thaw',5)%5==0;status=0 if hit else 32
 r.end();return dict(seed=seed,kind=kind,hit=hit,status=status,binding=binding,confusion=confusion,seedAfter=r.seed,trace=r.trace)
vectors=[]
for outcome in (False,True):vectors.append(next(vector(s,'wisp') for s in range(500) if vector(s,'wisp')['hit']==outcome))
for outcome in (0,16,32,64):vectors.append(next(vector(s,'tri') for s in range(500) if vector(s,'tri')['status']==outcome))
for kind in ('mud','absorb','damp','imprison','rain'):vectors.append(vector(0,kind))
vectors += [vector(0,'sand'),vector(2,'sand'),vector(0,'confuse')]
for thaw in (False,True):vectors.append(next(vector(s,'frozen') for s in range(500) if vector(s,'frozen')['hit']==thaw))
record=dict(sourceCommit=commit,secondaryChanceContract='Inherited Pewter correction: roll < chance',inputs=[dict(path=f,sha256=hashlib.sha256((SOURCE/f).read_bytes()).hexdigest()) for f in FILES],anchors=ANCHORS,fieldArenaTrapEffect=False,vectors=vectors)
encoded=json.dumps(record,indent=2)+'\n';out=HERE/'source-vectors.json'
if '--check' in sys.argv:assert out.read_text()==encoded,'source oracle changed'
else:out.write_text(encoded)
print(json.dumps(dict(sourceFiles=len(FILES),vectors=len(vectors),fieldArenaTrapEffect=False)))
