int main(void) {
  for (int actor=0; actor<4; actor++) for (int target=0; target<4; target++)
  for (int physical=0; physical<4; physical++) for (int special=0; special<4; special++) {
    gBattlerAttacker=actor; gBattlerTarget=target; gBattleScripting.dmgMultiplier=1;
    gProtectStructs[actor]=(struct Protect){7,physical,5,special};
    gBattlescriptCurrInstr=stream; Cmd_doubledamagedealtifdamaged();
    if (gBattleScripting.dmgMultiplier != (target==physical || target==special ? 2 : 1) || gBattlescriptCurrInstr!=stream+1) return 1;
    gProtectStructs[actor]=(struct Protect){0,target,0,target};
    gBattleScripting.dmgMultiplier=1; Cmd_doubledamagedealtifdamaged();
    if (gBattleScripting.dmgMultiplier!=1) return 2;
  }
  for (int actor=0; actor<4; actor++) for (int target=0; target<4; target++) for(int screens=0;screens<4;screens++) {
    gBattlerAttacker=actor;gBattlerTarget=target;
    gSideTimers[0]=(struct Timer){screens&1?4:0,screens&2?3:0}; gSideTimers[1]=gSideTimers[0];
    gSideStatuses[0]=gSideStatuses[1]=screens;gBattlescriptCurrInstr=stream;
    Cmd_removelightscreenreflect();
    int foe=(actor&1)^1,own=actor&1;
    if(gSideTimers[foe].reflectTimer || gSideTimers[foe].lightscreenTimer || gSideStatuses[foe])return 3;
    if(gSideStatuses[own]!=screens || gSideTimers[own].reflectTimer!=(screens&1?4:0))return 4;
    if(gBattleScripting.animTurn!=(screens?1:0) || gBattlescriptCurrInstr!=stream+1)return 5;
  }
  puts("{\"passed\":true,\"revengeVectors\":512,\"brickBreakVectors\":64}");return 0;
}
