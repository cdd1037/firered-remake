/* Controlled command driver. JSON groups compress exhaustive integer inputs;
 * values are emitted by the source commands, not recomputed by Python/C#.
 */
static void Check(int condition) { if (!condition) { fputs("oracle invariant failed\n", stderr); exit(2); } }
static void Reset(u16 move)
{
    memset(gBattleMons, 0, sizeof(gBattleMons));
    memset(gProtectStructs, 0, sizeof(gProtectStructs));
    memset(gStatuses3, 0, sizeof(gStatuses3));
    gBattlerAttacker = 0; gBattlerTarget = 1; gCurrentMove = move;
    gDynamicBasePower = 0; gBattleMovePower = 0; gBattleMoveDamage = 0;
    gBattleTypeFlags = 0; gHitMarker = 0; gCritMultiplier = 1;
    gBattleScripting.dmgMultiplier = 1; gBattlescriptCurrInstr = script;
    randomCalls = 0; weightCalls = 0;
}
static void Friendship(void)
{
    const u16 moves[] = { MOVE_RETURN, MOVE_FRUSTRATION };
    for (unsigned m = 0; m < ARRAY_COUNT(moves); m++)
    {
        u16 raw[256], effective[256];
        for (unsigned friendship = 0; friendship < 256; friendship++)
        {
            Reset(moves[m]); gBattleMons[0].friendship = (u8)friendship;
            Cmd_friendshiptodamagecalculation(); raw[friendship] = gDynamicBasePower;
            Check(gBattlescriptCurrInstr == script + 1);
            Cmd_damagecalc(); effective[friendship] = gBattleMovePower;
            Check(gDynamicBasePower == raw[friendship] && gBattlescriptCurrInstr == script + 2);
            Check(randomCalls == 0);
        }
        printf("{\"kind\":\"friendship\",\"move\":%u,\"firstFriendship\":0,\"commandOffset\":1,\"damageOffset\":2,\"powers\":[", moves[m]);
        for (unsigned i = 0; i < 256; i++) printf("%s%u", i ? "," : "", raw[i]);
        printf("],\"effectivePowers\":[");
        for (unsigned i = 0; i < 256; i++) printf("%s%u", i ? "," : "", effective[i]);
        puts("]}");
    }
}
static void HpSweeps(void)
{
    const u16 maxima[] = { 1, 2, 3, 7, 47, 48, 49, 97, 100, 146, 149, 150, 151, 178, 226, 242, 255, 256, 322, 714, 1023, 32767 };
    const u16 moves[][2] = { { MOVE_FLAIL, MOVE_REVERSAL }, { MOVE_ERUPTION, MOVE_WATER_SPOUT } };
    for (unsigned group = 0; group < ARRAY_COUNT(moves); group++)
        for (unsigned i = 0; i < ARRAY_COUNT(maxima); i++)
        {
            unsigned maximum = maxima[i];
            printf("{\"kind\":\"hp\",\"moves\":[%u,%u],\"maxHp\":%u,\"firstHp\":0,\"commandOffset\":1,\"powers\":[", moves[group][0], moves[group][1], maximum);
            for (unsigned hp = 0; hp <= maximum; hp++)
            {
                u16 first = 0;
                for (unsigned member = 0; member < 2; member++)
                {
                    Reset(moves[group][member]); gBattleMons[0].hp = (u16)hp; gBattleMons[0].maxHP = (u16)maximum;
                    if (group == 0) Cmd_remaininghptopower(); else Cmd_scaledamagebyhealthratio();
                    Check(gBattlescriptCurrInstr == script + 1 && randomCalls == 0);
                    if (member == 0) first = gDynamicBasePower; else Check(gDynamicBasePower == first);
                }
                printf("%s%u", hp ? "," : "", first);
            }
            puts("]}");
        }
}
static void WeightSweep(void)
{
    unsigned first = 0; u16 previous = 0;
    printf("{\"kind\":\"weight\",\"move\":%u,\"units\":\"hectograms\",\"firstWeight\":0,\"lastWeight\":65535,\"commandOffset\":1,\"ranges\":[", MOVE_LOW_KICK);
    unsigned groups = 0;
    for (unsigned weight = 0; weight <= 65535; weight++)
    {
        Reset(MOVE_LOW_KICK); weightRegister = (u16)weight; gBattleMons[1].species = SPECIES_DITTO;
        Cmd_weightdamagecalculation();
        Check(gBattlescriptCurrInstr == script + 1 && weightCalls >= 1 && weightCalls <= 5 && randomCalls == 0);
        if (weight == 0) previous = gDynamicBasePower;
        if (previous != gDynamicBasePower)
        {
            printf("%s{\"from\":%u,\"through\":%u,\"power\":%u}", groups++ ? "," : "", first, weight - 1, previous);
            first = weight; previous = gDynamicBasePower;
        }
    }
    printf(",{\"from\":%u,\"through\":65535,\"power\":%u}]}\n", first, previous);
    Check(groups == 5);
}
static void CachedEruption(void)
{
    const u16 moves[] = { MOVE_ERUPTION, MOVE_WATER_SPOUT };
    const u16 cache[] = { 0, 1, 17, 149, 150, 65535 };
    for (unsigned m = 0; m < ARRAY_COUNT(moves); m++)
        for (unsigned c = 0; c < ARRAY_COUNT(cache); c++)
            for (unsigned calculate = 0; calculate < 2; calculate++)
            {
                Reset(moves[m]); gDynamicBasePower = cache[c];
                gBattleMons[0].hp = 99; gBattleMons[0].maxHP = 100;
                Cmd_scaledamagebyhealthratio(); u16 first = gDynamicBasePower;
                Check(gBattlescriptCurrInstr == script + 1);
                if (calculate) Cmd_damagecalc();
                u16 afterDamage = gDynamicBasePower;
                MoveValuesCleanUp(); u16 afterCleanup = gDynamicBasePower;
                gBattlerTarget = 3; gBattleMons[0].hp = 1;
                Cmd_scaledamagebyhealthratio(); u16 second = gDynamicBasePower;
                unsigned secondOffset = (unsigned)(gBattlescriptCurrInstr - script);
                SourceActionFinishDynamicReset(); u16 afterFinish = gDynamicBasePower;
                Cmd_scaledamagebyhealthratio(); u16 nextAction = gDynamicBasePower;
                Check(first == afterDamage && first == afterCleanup && first == second && afterFinish == 0);
                printf("{\"kind\":\"eruption-cache\",\"move\":%u,\"initialPower\":%u,\"firstHp\":99,\"secondHp\":1,\"maxHp\":100,\"damageCalc\":%s,\"firstPower\":%u,\"afterDamagePower\":%u,\"afterCleanupPower\":%u,\"secondPower\":%u,\"secondOffset\":%u,\"afterActionFinishPower\":%u,\"nextActionPower\":%u}\n", moves[m], cache[c], calculate ? "true" : "false", first, afterDamage, afterCleanup, second, secondOffset, afterFinish, nextAction);
            }
}
static void OrdinaryCritRandom(void)
{
    const u16 moves[] = { MOVE_FLAIL, MOVE_REVERSAL };
    for (unsigned m = 0; m < ARRAY_COUNT(moves); m++)
        for (unsigned raw = 0; raw < 32; raw++)
        {
            Reset(moves[m]); randomRegister = (u16)raw;
            Cmd_critcalc(); u8 crit = gCritMultiplier;
            Check(gBattlescriptCurrInstr == script + 1 && randomCalls == 1);
            gBattleMoveDamage = 101 * crit; ApplyRandomDmgMultiplier();
            Check(gBattlescriptCurrInstr == script + 1 && randomCalls == 2);
            printf("{\"kind\":\"flail-crit-random\",\"move\":%u,\"rawRandom\":%u,\"criticalMultiplier\":%u,\"damageBeforeCritical\":101,\"finalDamage\":%d,\"randomCalls\":%u,\"scriptOffset\":1}\n", moves[m], raw, crit, (int)gBattleMoveDamage, randomCalls);
        }
}
int main(void)
{
    Friendship(); HpSweeps(); WeightSweep(); CachedEruption(); OrdinaryCritRandom();
    return ferror(stdout) ? 2 : 0;
}
