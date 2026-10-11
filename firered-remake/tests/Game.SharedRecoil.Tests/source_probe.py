#!/usr/bin/env python3
"""Compile exact pinned source arithmetic snippets; no upstream scripts execute."""
import hashlib,json,pathlib,re,subprocess,sys
ref=pathlib.Path(sys.argv[1]); out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=False)
sha=subprocess.check_output(['git','-C',str(ref),'rev-parse','HEAD'],text=True).strip()
assert sha=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
src=(ref/'src/battle_script_commands.c').read_text()
def block(a,b):return src[src.index(a):src.index(b,src.index(a))]
quarter=block('gBattleMoveDamage = (gHpDealt) / 4;','BattleScriptPush(gBattlescriptCurrInstr + 1);')
third=block('gBattleMoveDamage = gHpDealt / 3;','BattleScriptPush(gBattlescriptCurrInstr + 1);')
crash=block('    case DMG_RECOIL_FROM_MISS:','    case DMG_DOUBLED:').replace('    case DMG_RECOIL_FROM_MISS:','').replace('        break;','')
code='''#include <stdio.h>
struct { int maxHP; } gBattleMons[4];
int gBattlerTarget,gBattleMoveDamage,gHpDealt;
int main(void) { int kind,input,hp; while(scanf("%d %d %d",&kind,&input,&hp)==3) {
 gHpDealt=input;gBattleMoveDamage=input;gBattlerTarget=3;gBattleMons[3].maxHP=hp;
 switch(kind) { case 0: {'''+quarter+'''} break;case 1: {'''+third+'''} break;case 2: {'''+crash+'''} break;}
 printf("%d\\n",gBattleMoveDamage); } }
'''
(out/'oracle.c').write_text(code)
inputs=[(k,d,h) for k in (0,1,2) for d in (0,1,2,3,4,7,12,39,100,65535) for h in (1,2,3,21,100,714)]
text=''.join('%d %d %d\n'%x for x in inputs);(out/'inputs.txt').write_text(text)
answers=[]
for name,flags in [('oracle-o2',['-O2']),('oracle-ubsan',['-O1','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
 subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',*flags,str(out/'oracle.c'),'-o',str(out/name)],check=True)
 answers.append([int(x) for x in subprocess.check_output([str(out/name)],input=text,text=True).splitlines()])
assert answers[0]==answers[1] and len(answers[0])==len(inputs)
rows=[dict(kind=k,damage=d,targetMaxHp=h,expected=a) for (k,d,h),a in zip(inputs,answers[0])]
(out/'vectors.json').write_text(json.dumps(rows,indent=2)+'\n')
sources={str(p):hashlib.sha256((ref/p).read_bytes()).hexdigest() for p in ('src/battle_script_commands.c','data/battle_scripts_1.s','src/data/battle_moves.h')}
(out/'source-report.json').write_text(json.dumps(dict(commit=sha,sources=sources,vectors=len(rows),optimizedAndUbsanAgree=True,scope='verbatim quarter/third recoil and DMG_RECOIL_FROM_MISS command scalars; full script branches separately asserted against pinned text'),indent=2)+'\n')
print(json.dumps(dict(vectors=len(rows),optimizedAndUbsanAgree=True)))
