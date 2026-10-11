import argparse,hashlib,json,pathlib,re,subprocess
p=argparse.ArgumentParser();p.add_argument('--source',required=True);p.add_argument('--output',required=True);a=p.parse_args()
s=pathlib.Path(a.source);o=pathlib.Path(a.output);o.mkdir(parents=True,exist_ok=True)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','-C',str(s),'rev-parse','HEAD'],text=True).strip()==pin
def pinned(relative):
 data=subprocess.check_output(['git','-C',str(s),'show',pin+':'+relative])
 assert (s/relative).read_bytes()==data, 'Pinned source differs: '+relative
 return data.decode()
c=pinned('src/battle_script_commands.c');scripts=pinned('data/battle_scripts_1.s')
def function(name):
 start=c.index('static void '+name+'(void)\n{');end=start;depth=0
 for i in range(c.index('{',start),len(c)):
  depth+= (c[i]=='{')-(c[i]=='}')
  if depth==0:return c[start:i+1]
parts=[function('Cmd_doubledamagedealtifdamaged'),function('Cmd_removelightscreenreflect')]
here=pathlib.Path(__file__).parent
(o/'extracted.c').write_text((here/'source-prefix.h').read_text()+'\n'+'\n'.join(parts)+'\n'+(here/'source-main.c').read_text())
subprocess.run(['cc','-std=c99','-Wall','-Wextra','-Werror','-fsanitize=undefined',str(o/'extracted.c'),'-o',str(o/'oracle')],check=True)
r=json.loads(subprocess.check_output([str(o/'oracle')],text=True))
facade=scripts[scripts.index('BattleScript_EffectFacade::'):scripts.index('BattleScript_EffectFocusPunch::')]
smell=scripts[scripts.index('BattleScript_EffectSmellingsalt::'):scripts.index('BattleScript_EffectFollowMe::')]
brick=scripts[scripts.index('BattleScript_EffectBrickBreak::'):scripts.index('BattleScript_EffectYawn::')]
assert 'STATUS1_POISON | STATUS1_BURN | STATUS1_PARALYSIS | STATUS1_TOXIC_POISON' in facade and 'setbyte sDMG_MULTIPLIER, 2' in facade
assert smell.index('STATUS2_SUBSTITUTE')<smell.index('setmoveeffect MOVE_EFFECT_REMOVE_PARALYSIS | MOVE_EFFECT_CERTAIN')<smell.index('STATUS1_PARALYSIS')<smell.index('setbyte sDMG_MULTIPLIER, 2')
commands=['attackcanceler','accuracycheck','attackstring','ppreduce','removelightscreenreflect','critcalc','damagecalc','typecalc','adjustnormaldamage','typecalc2','healthbarupdate','datahpupdate','critmessage','resultmessage','seteffectwithchance','tryfaintmon']
positions=[brick.index('\t'+cmd) for cmd in commands];assert positions==sorted(positions)
r.update(sourceCommit=pin,scriptChecks=3,dependencies={str(path.relative_to(s)):hashlib.sha256(path.read_bytes()).hexdigest() for path in [s/'src/battle_script_commands.c',s/'data/battle_scripts_1.s']})
(o/'result.json').write_text(json.dumps(r,indent=2)+'\n');print(json.dumps(r))
