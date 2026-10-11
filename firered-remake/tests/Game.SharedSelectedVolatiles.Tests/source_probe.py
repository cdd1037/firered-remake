#!/usr/bin/env python3
"""Run unchanged pinned C command bodies with explicit boundary stubs."""
import hashlib,json,pathlib,re,subprocess,sys,tempfile
root=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);here=pathlib.Path(__file__).parent
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
source_files=['src/battle_script_commands.c','src/battle_util.c','data/battle_scripts_1.s','include/constants/moves.h','include/constants/abilities.h']
source_hashes={}
for name in source_files:
 b=(root/name).read_bytes();assert b==subprocess.check_output(['git','show','037335f4c725d7c9aecdac87066f2002b4bd7e14:'+name],cwd=root),name
 source_hashes[name]=hashlib.sha256(b).hexdigest()
source=(root/'src/battle_script_commands.c').read_text();parts=[];names=['Cmd_tryinfatuating','Cmd_settaunt','Cmd_settorment','Cmd_tryspiteppreduce']
for name in names:
 m=re.search(r'^static void '+name+r'\(void\)\n\{',source,re.M);assert m,name
 end=m.end();depth=1
 while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
 parts.append(source[m.start():end])
with tempfile.TemporaryDirectory(prefix='firered-selected-source-') as td:
 p=pathlib.Path(td);(p/'probe.c').write_text((here/'source-prefix.h').read_text()+'\n'.join(parts)+(here/'source-main.c').read_text())
 subprocess.run(['cc','-std=c99','-I'+str(root/'include'),str(p/'probe.c'),'-o',str(p/'probe')],check=True)
 result=json.loads(subprocess.check_output([str(p/'probe')],text=True))
assert [r['success'] for r in result['attract']]==[True,False,False,False,False,True]
assert [r['after'] for r in result['taunt']]==[2,1,2]
assert [r['success'] for r in result['torment']]==[True,False]
assert [r['success'] for r in result['spite']]==[False,False,True,True,False,False,False,True,True,False]
assert [r['updates'] for r in result['spite']]==[0,0,1,1,0,0,0,0,0,0]
assert [r['cancels'] for r in result['spite']]==[0,0,1,1,0,0,0,0,0,0]
result['source_files']=source_hashes
result['source_sha']='037335f4c725d7c9aecdac87066f2002b4bd7e14';result['functions']={n:hashlib.sha256(p.encode()).hexdigest() for n,p in zip(names,parts)}
out.write_text(json.dumps(result,indent=2)+'\n');print('PASS 21 unchanged source command boundaries')
