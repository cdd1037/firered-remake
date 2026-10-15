"""Original-source explosion batch harness; called by the existing source oracle runner."""
import hashlib, importlib.util, json, re, subprocess


def run(root, reference, out):
    spec = importlib.util.spec_from_file_location('explosion_importer', root/'scripts/import-source-explosion-animations.py')
    mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
    raw = mod.authenticated(reference)
    functions = {
        'src/task.c': ['ResetTasks','CreateTask','InsertTask','DestroyTask','RunTasks','FindFirstActiveTask'],
        'src/battle_anim.c': ['AddSpriteIndex','WaitAnimFrameCount','RunAnimScriptCommand','Cmd_loadspritegfx','Cmd_createvisualtask','Cmd_createsprite','Cmd_delay','Cmd_waitforvisualfinish','Cmd_end','Cmd_call','Cmd_return','Cmd_playsewithpan','BattleAnimAdjustPanning','DestroyAnimVisualTask','DestroyAnimSprite'],
        'src/battle_anim_normal.c': ['AnimComplexPaletteBlend','AnimComplexPaletteBlend_Step1','AnimComplexPaletteBlend_Step2','UnpackSelectedBattlePalettes'],
        'src/battle_anim_utility_funcs.c': ['AnimTask_BlendBattleAnimPal','StartBlendAnimSpriteColor','AnimTask_BlendSpriteColor_Step2'],
        'src/battle_anim_mon_movement.c': ['AnimTask_ShakeMon2','AnimTask_ShakeMon2Step'],
        'src/battle_anim_mons.c': ['GetBattlePalettesMask','GetBattleMonSpritePalettesMask','AnimSpriteOnMonPos','InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','SetAnimSpriteInitialXOffset','DestroySpriteAndMatrix'],
        'src/palette.c': ['BeginNormalPaletteFade','UpdatePaletteFade','UpdateNormalPaletteFade','IsSoftwarePaletteFadeFinishing','BlendPalettes'],
        'src/blend_palette.c': ['BlendPalette'],
        'src/sprite.c': ['CreateSpriteAndAnimate','AnimateSprites','UpdateOamCoords','BuildSpritePriorities','SortSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','SetSpriteOamFlipBits'],
    }
    bodies = []; identities = []; constants = []
    def identity(path, name, source, match, body, kind):
        identities.append(dict(path=path, name=name, line=source.count('\n',0,match.start())+1, sha256=hashlib.sha256(body.encode()).hexdigest(), kind=kind))
    def extract(path, name, kind='function'):
        source=raw[path].decode(); match=re.search(r'^((?:static )?\w+ '+re.escape(name)+r'\([^;]*?\))\n\{',source,re.M)
        if not match: raise ValueError('Missing original function '+name)
        pos=match.end(); depth=1
        while depth:
            if source[pos]=='{': depth+=1
            if source[pos]=='}': depth-=1
            pos+=1
        body=source[match.start():pos]; identity(path,name,source,match,body,kind)
        if kind=='function': bodies.append((match[1],body))
    for path,names in functions.items():
        for name in names: extract(path,name)
    extract('src/battle_main.c','BattleMainCB2','schedulingReference')
    for path,names in {
        'src/sprite.c':['sDummyAnim','gDummySpriteAnimTable','sDummyAffineAnim','gDummySpriteAffineAnimTable'],
        'src/battle_anim_effects_2.c':['sExplosionAnimCmds','sExplosionAnimTable','gExplosionSpriteTemplate'],
        'src/battle_anim_normal.c':['gComplexPaletteBlendSpriteTemplate'],
    }.items():
        source=raw[path].decode()
        for name in names:
            match=re.search(r'^(?:static )?const [^\n;]*\b'+name+r'(?:\[\])?\s*=\s*\{.*?\};',source,re.M|re.S)
            if not match: raise ValueError('Missing original constant '+name)
            constants.append(match[0]);identity(path,name,source,match,match[0],'constant')
    palette=re.search(r'struct PaletteFadeControl\n\{.*?\n\};',raw['include/palette.h'].decode(),re.S)[0]
    values=dict(ANIM_ATTACKER=0,F_PAL_ATTACKER=2,F_PAL_BG=1,RGB_WHITE=32767,RGB_BLACK=0,RGB_RED=31,SOUND_PAN_ATTACKER=-64)
    for path,pattern in [('include/constants/songs.h','SE_M_(?:SELF_DESTRUCT|EXPLOSION)'),('include/constants/battle_anim.h','ANIM_TAG_EXPLOSION')]:
        values.update({k:int(v) for k,v in re.findall(r'^#define ('+pattern+r')\s+(\d+)',raw[path].decode(),re.M)})
    tagSource=raw['include/constants/battle_anim.h'].decode()
    values['ANIM_TAG_EXPLOSION']=int(re.search(r'^#define ANIM_SPRITES_START\s+(\d+)',tagSource,re.M)[1])+int(re.search(r'^#define ANIM_TAG_EXPLOSION\s+\(ANIM_SPRITES_START \+ (\d+)\)',tagSource,re.M)[1])
    manifestBytes, material = mod.build(reference); manifest=json.loads(manifestBytes)
    pointers={'AnimTask_BlendBattleAnimPal':1,'AnimTask_ShakeMon2':2,'gComplexPaletteBlendSpriteTemplate':3,'gExplosionSpriteTemplate':4,'SelfDestructExplode':5,'Explosion1':6}
    names=['loadspritegfx','createvisualtask','createsprite','delay','waitforvisualfinish','end','call','return','playsewithpan']
    def number(s):
        if s in values: return values[s]
        rgb=re.fullmatch(r'RGB\((\d+), (\d+), (\d+)\)',s)
        if rgb: return int(rgb[1]) | int(rgb[2])<<5 | int(rgb[3])<<10
        return int(s)
    def h(v): return [v&255,(v>>8)&255]
    def bytecode(commands):
        result=[]
        for c in commands:
            op=c['op'];a=c['args'];result.append(names.index(op))
            if op in ['createvisualtask','createsprite']:
                n=2 if op=='createvisualtask' else 3
                result += [pointers[a[0]],0,0,0,number(a[n-1]),len(a)-n]
                for s in a[n:]: result+=h(number(s))
            elif op=='loadspritegfx': result+=h(number(a[0]))
            elif op=='delay': result += [number(a[0])]
            elif op=='call': result += [pointers[a[0]],0,0,0]
            elif op=='playsewithpan': result += h(number(a[0]))+[number(a[1])&255]
        return '{'+','.join(map(str,result))+'}'
    scripts='\n'.join('static const u8 script'+str(s['moveId'])+'[]='+bytecode(s['commands'])+';' for s in manifest['scripts'])
    scripts+='\n'+'\n'.join('static const u8 '+name+'[]='+bytecode(cmds)+';' for name,cmds in manifest['subroutines'].items())
    generated='\n'.join([raw['include/gba/io_reg.h'].decode(),'#define ANIM_TAG_EXPLOSION '+str(values['ANIM_TAG_EXPLOSION']), (root/'tests/SourceHitAnimation.Oracle/explosion-host.h').read_text(),palette,'static struct PaletteFadeControl gPaletteFade;','\n'.join(sig+';' for sig,b in bodies),'\n'.join(constants),'\n'.join(b for sig,b in bodies),scripts,(root/'tests/SourceHitAnimation.Oracle/explosion-driver.c').read_text()])
    (out/'oracle.c').write_text(generated)
    compiled=subprocess.run(['cc','-std=c99','-O0','-Wall','-Wextra','-Werror','-Wno-unused-parameter','-Wno-unused-variable','-Wno-unused-function',str(out/'oracle.c'),'-o',str(out/'oracle')],capture_output=True,text=True)
    (out/'compile.log').write_text(compiled.stdout+compiled.stderr)
    if compiled.returncode: raise SystemExit(compiled.stderr)
    result=subprocess.run([str(out/'oracle')],capture_output=True,text=True,timeout=15)
    (out/'source-c.csv').write_text(result.stdout)
    if result.returncode: raise SystemExit('Explosion oracle failed '+str(result.returncode)+': '+result.stderr)
    rows=len(result.stdout.splitlines())-1
    if rows<=0: raise ValueError('Empty explosion source oracle')
    provenance=dict(commit=mod.COMMIT,revision=0,functions=[i for i in identities if i['kind']=='function'],constants=[i for i in identities if i['kind']=='constant'],schedulingReferences=[i for i in identities if i['kind']=='schedulingReference'],rows=rows,vectors=20,roots=manifest['scripts'],subroutines=manifest['subroutines'],originalRuntimeCompared=False,sourceFiles=[dict(path=p,bytes=len(b),sha256=hashlib.sha256(b).hexdigest()) for p,b in raw.items()],fixture='Singles attacker 0/1; doubles attacker 0..3, all visible and attacker partner absent. Target is attacker^1. Frame0 executes graphics load. RGB555 seed is (index*61+37)&32767.',scheduling='Original RunAnimScriptCommand; original AnimateSprites plus frame interpreter; original OAM coordinates/priorities/sort snapshot; original linked task scheduler; original UpdatePaletteFade.',fieldNotes='Post-task battler offsets and palette values; final draw offsets capture pre-task OAM snapshot. Explosion slot records contain 11 values each, max four; no-sprite sentinel -1. Loaded graphics are freed only by original Cmd_end.',platformShims=['GPU register array; fixed visible battler geometry/species; non-contest identity mapping','64-slot sprite allocation/destruction with authenticated original sprite/frame engine and source recipe; affine engine disabled because all admitted effects are affine-off','compressed sprite graphics/palette load/free identity only: effect palette is verified separately through RGB555 asset; fixture palettes remain deterministic','VBlank palette transfer completion; no asynchronous audio tail'],hostSha256=hashlib.sha256(generated.encode()).hexdigest())
    (out/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
    print('PASS explosion original C:',len(bodies),'functions,',rows,'frames, 20 vectors')
