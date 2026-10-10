#!/usr/bin/env python3
"""Context-reconstructed source oracle. No pret code is executed.
--vectors-only reconstructs deterministic expected vectors without source hashes.
A recovered original source-oracle.json hash is NOT available. --check therefore
requires a separately reviewed fixture with sources and refuses vector-only data.
"""
import hashlib,json,pathlib,re,sys
HERE=pathlib.Path(__file__).resolve().parent
ROOT=HERE.parents[1]
PATHS=['src/battle_util.c','src/battle_script_commands.c','data/battle_scripts_1.s','src/data/battle_moves.h',
       'src/data/pokemon/pokedex_entries.h','src/random.c','include/random.h','src/data/items.json','src/pokemon.c']
def vector(seed,effect):
    state=seed;trace=[]
    def draw(purpose,bound):
        nonlocal state
        before=state;state=(state*0x41C64E6D+0x6073)&0xffffffff;raw=state>>16
        trace.append({'purpose':purpose,'before':before,'after':state,'raw':raw,'bound':bound,'result':raw%bound})
        return raw
    while draw('wild-battle.opponent.choose-slot',4)&3:pass
    hit=draw('wild-battle.player.accuracy',100)%100+1 <= {'rollout':90,'double-kick':100,'disable':55}[effect]
    remaining=0
    if hit:
        if effect=='disable':remaining=(draw('wild-battle.player.disable-duration',4)&3)+2-1
        else:
            for _ in range(2 if effect=='double-kick' else 1):
                draw('wild-battle.player.critical',16);draw('wild-battle.player.damage',16)
                if effect=='double-kick':draw('wild-battle.player.poison-point',3)
            draw('wild-battle.player.secondary-effect',100)
            if effect=='rollout':remaining=4
    draw('wild-battle.turn-end.random-turn-number',65536)
    return {'effect':effect,'seed':seed,'hit':hit,'remaining':remaining,'trace':trace}
held=[]
for wanted in [0,44,45,94,95,99]:
    for initial in range(4096):
        state=initial
        for _ in range(5):state=(state*0x41C64E6D+0x6073)&0xffffffff
        if (state>>16)%100==wanted:
            held.append({'seed':initial,'roll':wanted,'item':'ITEM_ORAN_BERRY' if 45<=wanted<95 else 'ITEM_NONE'});break
    else:raise AssertionError('Missing held-item boundary seed')
output={'sourceCommit':'037335f4c725d7c9aecdac87066f2002b4bd7e14','sources':None,'moves':None,
 'traceVectors':[vector(seed,effect) for effect in ['rollout','double-kick','disable'] for seed in [0,1,2,5,7,11,17,23]],
 'heldItemVectors':held,'rolloutPower':[30,60,120,240,480],'defenseCurlRolloutPower':[60,120,240,480,960],
 'disableDuration':[2,3,4,5],'wrapCounter':[3,4,5,6]}
path=HERE/'source-oracle.json'
if '--vectors-only' in sys.argv:
    output['recoverySourceHashesMissing']=True;path.write_text(json.dumps(output,indent=2)+'\n')
    print('Reconstructed 24 RNG + 6 held-item vectors; source hashes intentionally missing.');sys.exit(0)
source=pathlib.Path(sys.argv[sys.argv.index('--source-root')+1]) if '--source-root' in sys.argv else pathlib.Path((ROOT/'.tools/reference-root').read_text().strip())
texts={p:(source/p).read_text() for p in PATHS}
assert '#define RAND_MULT 1103515245' in texts['include/random.h'] and 'RAND_MULT * (val) + 24691' in texts['include/random.h']
assert 'disableTimer = (Random() & 3) + 2' in texts['src/battle_script_commands.c']
assert 'rolloutTimer = 5' in texts['src/battle_script_commands.c'] and 'gDynamicBasePower *= 2' in texts['src/battle_script_commands.c']
assert 'cancelmultiturnmoves BS_ATTACKER' in texts['data/battle_scripts_1.s'].split('BattleScript_MoveUsedIsParalyzed::')[1].split('\n\n')[0]
assert 'gBattleMons[battlerId].hp <= gBattleMons[battlerId].maxHP / 2 && !moveTurn' in texts['src/battle_util.c']
output['sources']=[{'path':p,'sha256':hashlib.sha256((source/p).read_bytes()).hexdigest()} for p in PATHS]
output['moves']=[]
for symbol in ['POUND','DOUBLE_KICK','HORN_ATTACK','WRAP','SING','DISABLE','GLARE','SPLASH','ROLLOUT']:
    match=re.search(r'\[MOVE_'+symbol+r'\]\s*=\s*\{(.*?)\n    \}',texts['src/data/battle_moves.h'],re.S)
    output['moves'].append({'symbol':'MOVE_'+symbol,'line':texts['src/data/battle_moves.h'][:match.start()].count('\n')+1,
       'fields':dict(re.findall(r'\.(\w+)\s*=\s*([^,\n]+)',match.group(1)))})
if '--check' in sys.argv:
    fixture=json.loads(path.read_text());assert fixture.get('sources'), 'Recovery fixture lacks original source hashes: restore/review sources first.'
    assert fixture==output,'Source/fixture changed';print(json.dumps({'passed':True,'sourceFiles':9,'moves':9,'vectors':24}))
else:
    path.write_text(json.dumps(output,indent=2)+'\n');print('New source-hash fixture generated; review before treating it as verified.')
