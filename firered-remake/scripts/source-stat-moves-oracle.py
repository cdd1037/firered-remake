"""Grouped original callback harness, called by the existing source oracle runner."""
import importlib.util,json,hashlib,re,subprocess,struct

def run(root,reference,out):
    spec=importlib.util.spec_from_file_location('stat_moves',root/'scripts/import-source-stat-moves.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod);raw=mod.authenticated(reference)
    functions={
      'src/task.c':['ResetTasks','CreateTask','InsertTask','DestroyTask','RunTasks','FindFirstActiveTask'],
      'src/battle_anim.c':['RunAnimScriptCommand','Cmd_createvisualtask','Cmd_createsprite','Cmd_waitforvisualfinish','Cmd_end','Cmd_call','Cmd_return','Cmd_playsewithpan','Cmd_loopsewithpan','Cmd_setalpha','Cmd_blendoff','Task_LoopAndPlaySE','BattleAnimAdjustPanning','DestroyAnimVisualTask','DestroyAnimSprite'],
      'src/battle_anim_effects_2.c':['AnimTask_Withdraw','AnimTask_Withdraw_Step','AnimTask_Minimize','AnimTask_Minimize_Step1','CreateMinimizeSprite','ClonedMinizeSprite_Step'],
      'src/battle_anim_dark.c':['AnimTask_MetallicShine','AnimTask_MetallicShine_Step'],
      'src/battle_anim_normal.c':['AnimComplexPaletteBlend','AnimComplexPaletteBlend_Step1','AnimComplexPaletteBlend_Step2','AnimTask_BlendColorCycle','BlendColorCycle','AnimTask_BlendColorCycleLoop','UnpackSelectedBattlePalettes'],
      'src/battle_anim_mon_movement.c':['AnimTask_ScaleMonAndRestore','AnimTask_ScaleMonAndRestore_Step'],
      'src/battle_anim_mons.c':['CreateInvisibleSpriteCopy','SetGreyscaleOrOriginalPalette','GetBattlePalettesMask','SetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped','PrepareBattlerSpriteForRotScale','ResetSpriteRotScale','SetBattlerSpriteYOffsetFromRotation','SetBattlerSpriteYOffsetFromYScale','CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet'],
      'src/palette.c':['BeginNormalPaletteFade','UpdatePaletteFade','UpdateNormalPaletteFade','IsSoftwarePaletteFadeFinishing','BlendPalettes'],
      'src/sprite.c':['UpdateOamCoords','BuildSpritePriorities','SortSprites'],
      'src/blend_palette.c':['BlendPalette'],
    }
    bodies=[];identities=[]
    for path,names in functions.items():
        source=raw[path].decode()
        for name in names:
            m=re.search(r'^((?:static )?\w+ '+name+r'\([^;]*?\))\n\{',source,re.M)
            if not m:raise ValueError(name)
            pos=m.end();depth=1
            while depth:
                if source[pos]=='{':depth+=1
                if source[pos]=='}':depth-=1
                pos+=1
            body=source[m.start():pos];bodies.append((m[1],body));identities.append(dict(path=path,name=name,line=source.count('\n',0,m.start())+1,sha256=hashlib.sha256(body.encode()).hexdigest()))
    palette=re.search(r'struct PaletteFadeControl\n\{.*?\n\};',raw['include/palette.h'].decode(),re.S)[0]
    songs={k:int(v) for k,v in re.findall(r'^#define (SE_M_TAKE_DOWN|SE_M_HARDEN|SE_SHINY|SE_M_HEADBUTT|SE_M_MINIMIZE)\s+(\d+)',raw['include/constants/songs.h'].decode(),re.M)}
    manifestBytes,material=mod.build(reference);manifest=json.loads(manifestBytes);pointers={'AnimTask_MetallicShine':1,'AnimTask_BlendColorCycle':2,'AnimTask_ScaleMonAndRestore':3,'gComplexPaletteBlendSpriteTemplate':4,'GrowthEffect':5,'AnimTask_Withdraw':6,'AnimTask_Minimize':7}
    values=dict(ANIM_ATTACKER=0,F_PAL_ATTACKER=2,F_PAL_BG=1,RGB_WHITE=32767,RGB_WHITEALPHA=65535,SOUND_PAN_ATTACKER=-64,**songs)
    names=['createvisualtask','createsprite','waitforvisualfinish','end','call','return','playsewithpan','loopsewithpan','setalpha','blendoff']
    def number(s):return values[s] if s in values else int(s)
    def h(v):return [v&255,(v>>8)&255]
    def bytecode(commands):
        result=[]
        for c in commands:
            op=c['op'];a=c['args'];result.append(names.index(op))
            if op in ['createvisualtask','createsprite']:
                n=2 if op=='createvisualtask' else 3;result += [pointers[a[0]],0,0,0,number(a[n-1]),len(a)-n]
                for s in a[n:]:result+=h(number(s))
            elif op=='setalpha':result += [number(s) for s in a]
            elif op=='call':result += [pointers[a[0]],0,0,0]
            elif op=='playsewithpan':result += h(number(a[0]))+[number(a[1])&255]
            elif op=='loopsewithpan':result += h(number(a[0]))+[number(s)&255 for s in a[1:]]
        return '{'+','.join(map(str,result))+'}'
    scripts='\n'.join('static const u8 script'+str(s['moveId'])+'[]='+bytecode(s['commands'])+';' for s in manifest['scripts'])
    scripts+='\nstatic const u8 growthEffect[]='+bytecode(manifest['subroutines']['GrowthEffect'])+';'
    bios=(root/'tests/SourceHitAnimation.Oracle/bios-affine-q14.json').read_bytes()
    if hashlib.sha256(bios).hexdigest()!='c796b6172553a983e62e30da8753d82033b123ab8a77875fae86ddeaaffb093e':raise ValueError('BIOS affine reference differs')
    biosTable=json.loads(bios)['sineQ14']
    generated='\n'.join([raw['include/gba/io_reg.h'].decode(),(root/'tests/SourceHitAnimation.Oracle/stat-moves-host.h').read_text().replace('BIOS_SINE_VALUES','{'+','.join(map(str,biosTable))+'}').replace('METAL_PALETTE_VALUES','{'+','.join(map(str,struct.unpack('<16H',material[-32:])))+'}'),palette,'static struct PaletteFadeControl gPaletteFade;','\n'.join(sig+';' for sig,b in bodies),'\n'.join(b for sig,b in bodies),scripts,(root/'tests/SourceHitAnimation.Oracle/stat-moves-driver.c').read_text()])
    (out/'oracle.c').write_text(generated)
    p=subprocess.run(['cc','-std=c99','-O0','-Wall','-Wextra','-Werror','-Wno-unused-parameter','-Wno-unused-variable','-Wno-unused-function',str(out/'oracle.c'),'-o',str(out/'oracle')],capture_output=True,text=True);(out/'compile.log').write_text(p.stdout+p.stderr)
    if p.returncode:raise SystemExit(p.stderr)
    p=subprocess.run([str(out/'oracle'),str(out/'matrix.csv')],capture_output=True,text=True,timeout=15);(out/'source-c.csv').write_text(p.stdout)
    if p.returncode:raise SystemExit('Stat-moves oracle failed '+str(p.returncode)+': '+p.stderr)
    rows=len(p.stdout.splitlines())-1
    if rows<=0:raise ValueError('Empty oracle')
    (out/'provenance.json').write_text(json.dumps(dict(commit=mod.COMMIT,sourceLockSha256=mod.LOCK_SHA,functions=identities,rows=rows,vectors=50,roots=manifest['scripts'],subroutines=manifest['subroutines'],originalRuntimeCompared=False,platformShims=['GPU registers and resource identity','64-slot sprite allocation/callbacks and pre-task OAM mode/window snapshot','integer BIOS ObjAffineSet shim checked against pinned Q14 table; not a BIOS-runtime comparison','no sound tail; VBlank palette-transfer completion'],hostSha256=hashlib.sha256(generated.encode()).hexdigest()),indent=2)+'\n')
    print('PASS stat-move original C:',len(bodies),'functions,',rows,'frames, 50 vectors')
