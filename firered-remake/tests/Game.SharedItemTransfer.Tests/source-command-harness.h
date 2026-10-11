/* Authored host scaffolding only. Upstream command bodies are extracted separately.
 * This does not emulate damage, accuracy, scripts, DMA, animation or save loading. */
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;
typedef uint16_t u16;
typedef int32_t s32;
struct BattlePokemon { u16 item, hp, moves[4]; u8 ability; uint32_t status2; };
struct Pokemon { u16 item; };
static struct BattlePokemon gBattleMons[4];
static struct Pokemon gPlayerParty[6], gEnemyParty[6];
static struct { u16 changedItems[4], usedHeldItems[4], choicedMove[4]; } storage, *gBattleStruct = &storage;
static struct { u8 knockedOffMons[2]; } gWishFutureKnock;
static struct { u8 moveendState; } gBattleScripting;
static u8 gBattlerAttacker, gBattlerTarget, gEffectBattler, gActiveBattler, gBattlersCount;
static u8 gBattlerPartyIndexes[4], gBattleCommunication[8], gBattleBufferA[4][8];
static u8 gLastUsedAbility, gMoveResultFlags, gBattleTextBuff1[8], gBattleTextBuff2[8];
static u16 gLastUsedItem, gTrainerBattleOpponent_A, gChosenMove;
static uint32_t gBattleTypeFlags, gHitMarker;
static const uint32_t gBitTable[] = {1, 2, 4, 8, 16, 32};
static u8 command[16], failure[16], BattleScript_StickyHoldActivates[1], BattleScript_ItemSteal[1], BattleScript_KnockedOff[1];
static u8 *gBattlescriptCurrInstr;
static int controllerWrites;
#define BUFFER_A 0
#define T1_READ_PTR(p) ((void)(p), failure)
#define PREPARE_ITEM_BUFFER(buffer, item) (void)(buffer); (void)(item);
#define INCREMENT_RETURN { gBattlescriptCurrInstr++; return; }
static u8 GetBattlerSide(u8 battler) { return battler & 1; }
static u8 GetBattlerForBattleScript(u8 battler) { return battler; }
static void MarkBattlerForControllerExec(u8 battler) { (void)battler; }
static void BattleScriptPush(const u8 *cursor) { (void)cursor; }
static void RecordAbilityBattle(u8 battler, u8 ability) { (void)battler; (void)ability; }
static void SetMonData(struct Pokemon *mon, int field, const void *data)
{ assert(field == MON_DATA_HELD_ITEM); memcpy(&mon->item, data, sizeof(mon->item)); }
static void SourcePlayerController(int request, int monId);
static void SourceOpponentController(int request, int monId);
static void BtlController_EmitSetMonData(int buffer, int request, int mask, size_t size, const void *data)
{
    assert(buffer == BUFFER_A && mask == 0 && size == sizeof(u16));
    memcpy(&gBattleBufferA[gActiveBattler][3], data, size);
    if (GetBattlerSide(gActiveBattler)) SourceOpponentController(request, gBattlerPartyIndexes[gActiveBattler]);
    else SourcePlayerController(request, gBattlerPartyIndexes[gActiveBattler]);
    controllerWrites++;
}
static void SourceEffect(int effect);
static void Cmd_tryswapitems(void);
static void Cmd_tryrecycleitem(void);
static void Cmd_removeitem(void);
static void SourceMoveEnd(void);
static void SourceSwitchMask(void);
static struct Pokemon *Party(u8 battler)
{ return GetBattlerSide(battler) ? gEnemyParty : gPlayerParty; }
static void Reset(u16 attackerItem, u16 targetItem)
{
    memset(gBattleMons, 0, sizeof(gBattleMons));
    memset(gPlayerParty, 0, sizeof(gPlayerParty));
    memset(gEnemyParty, 0, sizeof(gEnemyParty));
    memset(&storage, 0, sizeof(storage));
    memset(&gWishFutureKnock, 0, sizeof(gWishFutureKnock));
    memset(gBattlerPartyIndexes, 0, sizeof(gBattlerPartyIndexes));
    memset(gBattleCommunication, 0, sizeof(gBattleCommunication));
    gBattlerAttacker = 0; gBattlerTarget = 1; gBattlersCount = 2;
    gActiveBattler = 0; gBattlerPartyIndexes[0] = 2; gBattlerPartyIndexes[1] = 4;
    gBattleMons[0].item = gPlayerParty[2].item = attackerItem;
    gBattleMons[1].item = gEnemyParty[4].item = targetItem;
    gBattleMons[0].hp = gBattleMons[1].hp = 40;
    gBattleMons[0].moves[0] = gBattleMons[1].moves[0] = gChosenMove = MOVE_THIEF;
    gHitMarker = HITMARKER_OBEYS; gMoveResultFlags = 0;
    gBattleTypeFlags = BATTLE_TYPE_TRAINER; gTrainerBattleOpponent_A = 1;
    gBattlescriptCurrInstr = command; command[1] = 0;
    gLastUsedAbility = 0; gLastUsedItem = 0; controllerWrites = 0;
}
static void Reload(u8 battler, u8 partyIndex)
{
    gActiveBattler = battler; gBattlerPartyIndexes[battler] = partyIndex;
    gBattleMons[battler].item = Party(battler)[partyIndex].item;
    /* Source switch mask is exact; unrelated switch state is outside this probe. */
    SourceSwitchMask();
}
static void Emit(const char *name, int auxiliary)
{
    int path = -1;
    if (gBattlescriptCurrInstr == BattleScript_StickyHoldActivates) path = 100;
    else if (gBattlescriptCurrInstr == BattleScript_ItemSteal) path = 101;
    else if (gBattlescriptCurrInstr == BattleScript_KnockedOff) path = 102;
    else if (gBattlescriptCurrInstr == failure) path = 103;
    else for (int i = 0; i < 16; i++) if (gBattlescriptCurrInstr == command + i) path = i;
    u8 a = gBattlerAttacker, t = gBattlerTarget;
    printf("{\"name\":\"%s\",\"values\":[%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%d,%d]}\n", name,
        gBattleMons[a].item, gBattleMons[t].item,
        Party(a)[gBattlerPartyIndexes[a]].item, Party(t)[gBattlerPartyIndexes[t]].item,
        storage.changedItems[a], storage.usedHeldItems[a],
        gWishFutureKnock.knockedOffMons[0], gWishFutureKnock.knockedOffMons[1],
        storage.choicedMove[a], storage.choicedMove[t], controllerWrites, path, auxiliary);
}
int main(void)
{
    Reset(ITEM_NONE, ITEM_CHOICE_BAND); storage.choicedMove[1] = MOVE_THIEF;
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-trainer-before-moveend", 0);
    SourceMoveEnd(); Emit("thief-trainer-after-moveend", 0);
    Reset(ITEM_NONE, ITEM_ORAN_BERRY); gBattleTypeFlags = 0; gBattleMons[1].hp = 0;
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); SourceMoveEnd(); Emit("thief-wild-fainted-target", 0);
    Reset(ITEM_ORAN_BERRY, ITEM_NONE); gBattlerAttacker = 1; gBattlerTarget = 0;
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-opponent-trainer-blocked", 0);
    gBattleTypeFlags = 0; gBattlescriptCurrInstr = command;
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-opponent-wild-blocked", 0);
    Reset(ITEM_ORAN_BERRY, ITEM_CHOICE_BAND); gBattleMons[1].ability = ABILITY_STICKY_HOLD;
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-sticky-before-occupied-check", gLastUsedAbility);
    Reset(ITEM_NONE, ITEM_ORAN_BERRY); gWishFutureKnock.knockedOffMons[0] = 4;
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-knocked-attacker-blocked", 0);
    Reset(ITEM_NONE, ITEM_ENIGMA_BERRY);
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-enigma-blocked", 0);
    Reset(ITEM_NONE, ITEM_ORANGE_MAIL);
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-mail-blocked", 0);
    Reset(ITEM_ORAN_BERRY, ITEM_CHOICE_BAND);
    storage.choicedMove[0] = storage.choicedMove[1] = MOVE_THIEF;
    Cmd_tryswapitems(); Emit("trick-both-before-moveend", gBattleCommunication[MULTISTRING_CHOOSER]);
    SourceMoveEnd(); Emit("trick-both-after-moveend", gBattleCommunication[MULTISTRING_CHOOSER]);
    Reset(ITEM_CHOICE_BAND, ITEM_NONE); Cmd_tryswapitems(); SourceMoveEnd();
    Emit("trick-given-only", gBattleCommunication[MULTISTRING_CHOOSER]);
    Reset(ITEM_NONE, ITEM_ORAN_BERRY); gWishFutureKnock.knockedOffMons[1] = 16;
    Cmd_tryswapitems(); Emit("trick-knocked-target-blocked", 0);
    Reset(ITEM_ORAN_BERRY, ITEM_NONE); gBattlerAttacker = 1; gBattlerTarget = 0;
    Cmd_tryswapitems(); Emit("trick-opponent-blocked", 0);
    Reset(ITEM_NONE, ITEM_CHOICE_BAND); storage.choicedMove[1] = MOVE_THIEF;
    SourceEffect(MOVE_EFFECT_KNOCK_OFF); Reload(1, 4);
    Emit("knockoff-party-item-retained-on-return", 0);
    Reset(ITEM_NONE, ITEM_ORAN_BERRY); gBattleMons[1].hp = 0;
    SourceEffect(MOVE_EFFECT_KNOCK_OFF); Emit("knockoff-fainted-target-skipped", 0);
    Reset(ITEM_NONE, ITEM_ORAN_BERRY); gBattleMons[1].status2 = STATUS2_SUBSTITUTE;
    SourceEffect(MOVE_EFFECT_STEAL_ITEM); Emit("thief-substitute-skipped", 0);
    Reset(ITEM_ORAN_BERRY, ITEM_NONE); Cmd_removeitem();
    Reload(0, 3); gBattlescriptCurrInstr = command; Cmd_tryrecycleitem();
    Emit("recycle-consumed-slot-item-after-switch", gPlayerParty[2].item);
    Reset(ITEM_CHOICE_BAND, ITEM_NONE); storage.usedHeldItems[0] = ITEM_ORAN_BERRY;
    Cmd_tryrecycleitem(); Emit("recycle-occupied-preserves-used-slot", 0);
    Reset(ITEM_NONE, ITEM_NONE); storage.usedHeldItems[0] = ITEM_ORAN_BERRY;
    gWishFutureKnock.knockedOffMons[0] = 4; Cmd_tryrecycleitem();
    Emit("recycle-ignores-knocked-party-bit", 0);
    Reload(0, 2); Emit("recycle-restored-item-suppressed-on-return", 0);
    return 0;
}
