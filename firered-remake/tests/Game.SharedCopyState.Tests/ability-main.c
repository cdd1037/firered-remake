int main(void)
{
    const u8 abilities[] = {ABILITY_NONE, ABILITY_LIMBER, ABILITY_IMMUNITY,
        ABILITY_WONDER_GUARD, ABILITY_TRACE, ABILITY_FORECAST, ABILITY_INTIMIDATE,
        ABILITY_DROUGHT, ABILITY_CLOUD_NINE};
    unsigned comma = 0;
    printf("{\"exchange\":[");
    for (unsigned swap = 0; swap < 2; swap++)
    for (unsigned actor = 0; actor < 4; actor++)
    for (unsigned a = 0; a < sizeof abilities; a++)
    for (unsigned b = 0; b < sizeof abilities; b++)
    {
        memset(gBattleMons, 0, sizeof gBattleMons);
        gBattlerAttacker = actor; gBattlerTarget = actor ^ 1;
        gBattleMons[actor].ability = abilities[a];
        gBattleMons[actor ^ 1].ability = abilities[b];
        gBattlescriptCurrInstr = script; gLastUsedAbility = 255; gMoveResultFlags = 0;
        if (swap) Cmd_tryswapabilities(); else Cmd_trycopyability();
        printf("%s{\"swap\":%u,\"actor\":%u,\"own\":%u,\"target\":%u,\"success\":%s,\"afterOwn\":%u,\"afterTarget\":%u}",
            comma++ ? "," : "", swap, actor, abilities[a], abilities[b],
            gBattlescriptCurrInstr == failedScript ? "false" : "true",
            gBattleMons[actor].ability, gBattleMons[actor ^ 1].ability);
    }
    printf("]}\n");
    return 0;
}
