/* Reconstructed 17-field stdin adapter, 2026-10-11. Each line is:
 * attack defense abilityA abilityD statusA statusD stageA stageD critical
 * power type effect itemA itemD reflect badgeAttack badgeDefense
 * Both offensive stats use attack; both defensive stats use defense. The
 * species registers are Cubone -> Ditto, both level 37 and 500/500 HP. This
 * executable returns base damage only; source_probe.py applies critical and
 * random damage factors outside the extracted function, with no STAB/typecalc.
 */
int main(void)
{
    unsigned a, d, abilityA, abilityD, statusA, statusD, stageA, stageD;
    unsigned critical, power, type, effect, itemA, itemD, reflect, badgeA, badgeD;
    int fields;
    while ((fields = scanf("%u %u %u %u %u %u %u %u %u %u %u %u %u %u %u %u %u",
        &a, &d, &abilityA, &abilityD, &statusA, &statusD, &stageA, &stageD,
        &critical, &power, &type, &effect, &itemA, &itemD, &reflect, &badgeA, &badgeD)) != EOF)
    {
        if (fields != 17 || a == 0 || a > 65535 || d < 2 || d > 65535 ||
            abilityA > 255 || abilityD > 255 || stageA > 12 || stageD > 12 ||
            critical > 1 || power == 0 || power > 255 || type > 17 || effect > 255 ||
            itemA >= ITEM_ENIGMA_BERRY || itemD >= ITEM_ENIGMA_BERRY ||
            reflect > 1 || badgeA > 1 || badgeD > 1)
        {
            fputs("unsupported or malformed controlled damage registers\n", stderr);
            return 2;
        }
        struct BattlePokemon attacker = { .species = SPECIES_CUBONE,
            .attack = (u16)a, .defense = 50, .spAttack = (u16)a, .spDefense = 50,
            .hp = 500, .maxHP = 500, .item = (u16)itemA, .level = 37,
            .ability = (u8)abilityA, .status1 = statusA };
        struct BattlePokemon defender = { .species = SPECIES_DITTO,
            .attack = 50, .defense = (u16)d, .spAttack = 50, .spDefense = (u16)d,
            .hp = 500, .maxHP = 500, .item = (u16)itemD, .level = 37,
            .ability = (u8)abilityD, .status1 = statusD };
        memset(attacker.statStages, DEFAULT_STAT_STAGE, sizeof(attacker.statStages));
        memset(defender.statStages, DEFAULT_STAT_STAGE, sizeof(defender.statStages));
        attacker.statStages[STAT_ATK] = attacker.statStages[STAT_SPATK] = (u8)stageA;
        defender.statStages[STAT_DEF] = defender.statStages[STAT_SPDEF] = (u8)stageD;
        gCurrentMove = 1;
        gBattleMoves[1] = (struct HarnessMove){ .effect = (u8)effect, .power = (u8)power,
            .type = (u8)type, .target = 0 };
        gCritMultiplier = critical ? 2 : 1;
        badgeAttack = (u8)badgeA;
        badgeDefense = (u8)badgeD;
        gBattleTypeFlags = gBattleWeather = gBattleMovePower = 0;
        memset(&resourceFlags, 0, sizeof(resourceFlags));
        printf("%d\n", (int)CalculateBaseDamage(&attacker, &defender, gCurrentMove,
            reflect ? SIDE_STATUS_REFLECT : 0, 0, 0, 0, 1));
    }
    return ferror(stdin) ? 2 : 0;
}
