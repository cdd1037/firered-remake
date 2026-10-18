/* Authored bounded source fixture harness: platform boundaries and observations. */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <limits.h>
typedef uint8_t u8;
typedef int16_t s16;
typedef uint16_t u16;
typedef void (*TaskFunc)(u8);
#define TRUE 1
#define FALSE 0
#define NUM_TASKS 16
#define MAX_BATTLERS_COUNT 4
#define HEAD_SENTINEL 0xFE
#define TAIL_SENTINEL 0xFF
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define BATTLER_COORD_ATTR_HEIGHT 0
#define TASK_ID 5
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Task { s16 data[16]; TaskFunc func; u8 isActive, prev, next; };
struct Sprite {
    s16 data[8];
    s16 x, y, x2, y2;
    u8 inUse, subpriority;
    SpriteCallback callback;
    int instance, isDot;
};
struct SpriteTemplate { int tileTag, paletteTag; const void *oam, *anims, *images, *affineAnims; SpriteCallback callback; };
struct UCoords8 { u8 x, y; };
struct Task gTasks[NUM_TASKS];
struct Sprite gSprites[MAX_SPRITES];
u8 gBattleAnimAttacker, gBattleAnimTarget, gAnimVisualTaskCount;
static int gOamData_AffineOff_ObjNormal_8x8;
static const int gDummySpriteAnimTable[1], gDummySpriteAffineAnimTable[1];
static void AnimTask_GlareEyeDots_Step(u8 taskId);
static void GetGlareEyeDotCoords(s16,s16,s16,s16,u8,u8,s16*,s16*);
static void AnimGlareEyeDot(struct Sprite *);
static void ObservedGlareEyeDot(struct Sprite *);
static u8 CreateSpriteAt(u8,const struct SpriteTemplate*,s16,s16,u8);
static u8 CreateSprite(const struct SpriteTemplate*,s16,s16,u8);
static void DestroySprite(struct Sprite *);
static void DestroyTask(u8);
void DestroyAnimVisualTask(u8);
static void AnimateSprite(struct Sprite *);
static u8 GetBattlerSide(u8);
static s16 GetBattlerSpriteCoord(u8,u8);
static s16 GetBattlerSpriteCoordAttr(u8,u8);
static u8 FindFirstActiveTask();
static void BuildOamBuffer(void);
static void RunTextPrinters(void);
static void UpdatePaletteFade(void);
/* The unrelated abort branch is held false; these names only satisfy typing. */
#define B_BUTTON 2
#define BATTLE_TYPE_POKEDUDE 0x200000
#define JOY_HELD(button) 0
#define B_OUTCOME_DREW 3
#define PALETTES_ALL 0xFFFFFFFFu
#define RGB_BLACK 0
static int gBattleTypeFlags, gSpecialVar_Result, gBattleOutcome;
static void ResetPaletteFadeControl(void) {}
static void BeginNormalPaletteFade(unsigned a,int b,int c,int d,int e) {}
static void CB2_QuitPokedudeBattle(void) {}
static void SetMainCallback2(void (*fn)(void)) {}
/* Every observation is bounded by MAX_SPRITES / 26 source allocations. */
static int frameNumber, phase, nextInstance, allocationAttempt, forceFailAttempt;
static int destroyedTask, peakChildren, occupiedSlots, layoutId, lastDestroyedTimer;
static int reads[16][4], readCount;
static int callbacks[64][7], callbackCount;
static int allocations[2][8], allocationCount;
static int oamChildren[64][10], oamCount;
static s16 controlledCoords[4][2];
static const s16 controlledHeights[4] = { 63, 50, 41, 28 };
static const s16 controlledPictureOffsets[4] = { 3, -5, 7, -9 };
static void fail(const char *s) { fprintf(stderr,"harness assertion: %s\n",s); exit(2); }
static void check(int value,const char *s) { if (!value) fail(s); }
static int liveDotCount(void) { int n=0; for(int i=0;i<MAX_SPRITES;i++) n += gSprites[i].inUse && gSprites[i].isDot; return n; }
static void DummySpriteCallback(struct Sprite *sprite) {}
static void readLog(int kind,int battler,int selector,int value) {
    check(readCount < 16,"read capacity");
    int *r=reads[readCount++];r[0]=kind;r[1]=battler;r[2]=selector;r[3]=value;
}
static u8 GetBattlerSide(u8 battler) { u8 value=battler&1; readLog(0,battler,0,value); return value; }
static s16 GetBattlerSpriteCoord(u8 battler,u8 coord) {
    check(coord==BATTLER_COORD_X_2 || coord==BATTLER_COORD_Y_PIC_OFFSET,"coordinate selector");
    s16 value=controlledCoords[battler][coord==BATTLER_COORD_Y_PIC_OFFSET];
    readLog(1,battler,coord,value); return value;
}
static s16 GetBattlerSpriteCoordAttr(u8 battler,u8 attr) {
    check(attr==BATTLER_COORD_ATTR_HEIGHT,"attribute selector");
    s16 value=controlledHeights[battler]; readLog(2,battler,attr,value); return value;
}
