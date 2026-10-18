/* Authored allocation boundary: source CreateSprite selects the free slot. */
static u8 CreateSpriteAt(u8 slot,const struct SpriteTemplate *template,s16 x,s16 y,u8 subpriority) {
    if (allocationAttempt == forceFailAttempt) return MAX_SPRITES;
    struct Sprite *sprite=&gSprites[slot];
    memset(sprite,0,sizeof(*sprite));
    sprite->inUse=TRUE;sprite->subpriority=subpriority;sprite->callback=template->callback;
    sprite->x=x;sprite->y=y;sprite->instance=++nextInstance;sprite->isDot=1;
    return slot;
}
static u8 CreateSprite(const struct SpriteTemplate *template,s16 x,s16 y,u8 subpriority) {
    check(phase==3,"allocation must occur during RunTasks");
    check(allocationCount<2,"allocation capacity");
    allocationAttempt++;
    u8 slot=Source_CreateSprite(template,x,y,subpriority);
    int *r=allocations[allocationCount];
    r[0]=allocationAttempt;r[1]=gTasks[TASK_ID].data[2];r[2]=allocationCount;
    r[3]=x;r[4]=y;r[5]=subpriority;r[6]=slot;r[7]=slot==MAX_SPRITES?0:gSprites[slot].instance;
    allocationCount++;
    return slot;
}
static void DestroySprite(struct Sprite *sprite) {
    check(phase==1 && sprite->isDot && sprite->inUse,"raw sprite destruction phase");
    lastDestroyedTimer=sprite->data[0];
    memset(sprite,0,sizeof(*sprite));
}
static void DestroyTask(u8 id) {
    check(phase==3 && id==TASK_ID,"parent destruction phase");
    check(gTasks[id].data[10]==0,"parent destroyed only after all children");
    destroyedTask=1;gTasks[id].isActive=FALSE;
}
static void ObservedGlareEyeDot(struct Sprite *sprite) {
    check(phase==1,"child callback sprite phase");
    check(callbackCount<64,"callback capacity");
    int *r=callbacks[callbackCount++];
    r[0]=(int)(sprite-gSprites);r[1]=sprite->instance;r[2]=sprite->data[0];r[4]=gTasks[TASK_ID].data[10];
    u8 visualBefore=gAnimVisualTaskCount;
    AnimGlareEyeDot(sprite);
    check(visualBefore==gAnimVisualTaskCount,"raw child leaves global animation count unchanged");
    r[3]=sprite->inUse?sprite->data[0]:lastDestroyedTimer;r[5]=gTasks[TASK_ID].data[10];r[6]=!sprite->inUse;
}
static void AnimateSprite(struct Sprite *sprite) {}
static void spriteRow(struct Sprite *s,int slot,int *r) {
    r[0]=slot;r[1]=s->instance;r[2]=s->x;r[3]=s->y;r[4]=s->x2;r[5]=s->y2;
    r[6]=s->data[0];r[7]=s->data[1];r[8]=s->data[2];r[9]=s->subpriority;
}
static void BuildOamBuffer(void) {
    phase=2;
    for(int i=0;i<MAX_SPRITES;i++) if(gSprites[i].inUse && gSprites[i].isDot) spriteRow(&gSprites[i],i,oamChildren[oamCount++]);
}
static void RunTextPrinters(void) {}
static void UpdatePaletteFade(void) { phase=3; }
static void printInts(const int *rows,int count,int width) {
    putchar('[');
    for(int i=0;i<count;i++) {
        if(i) putchar(',');
        putchar('[');
        for(int j=0;j<width;j++){if(j)putchar(',');printf("%d",rows[i*width+j]);}
        putchar(']');
    }
    putchar(']');
}
static void printFrame(void) {
    int children[64][10], count=0;
    for(int i=0;i<MAX_SPRITES;i++) if(gSprites[i].inUse && gSprites[i].isDot) spriteRow(&gSprites[i],i,children[count++]);
    check(count==gTasks[TASK_ID].data[10],"live child and source parent counter agree");
    if(count>peakChildren)peakChildren=count;
    printf("{\"frame\":%d,\"taskAlive\":%s,\"visualCount\":%d,\"taskData\":[",frameNumber,gTasks[TASK_ID].isActive?"true":"false",gAnimVisualTaskCount);
    for(int i=0;i<16;i++){if(i)putchar(',');printf("%d",gTasks[TASK_ID].data[i]);}
    printf("],\"callbacks\":");printInts(&callbacks[0][0],callbackCount,7);
    printf(",\"oamChildren\":");printInts(&oamChildren[0][0],oamCount,10);
    printf(",\"allocations\":");printInts(&allocations[0][0],allocationCount,8);
    printf(",\"children\":");printInts(&children[0][0],count,10);
    printf(",\"taskDestroyedInTaskPhase\":%s}",destroyedTask?"true":"false");
}
static void runScenario(const char *name,int layout,int attacker,int target,int occupied,int failAttempt,int seedContest,int forceContest) {
    memset(gTasks,0,sizeof(gTasks));memset(gSprites,0,sizeof(gSprites));
    nextInstance=0;allocationAttempt=0;forceFailAttempt=failAttempt;peakChildren=0;readCount=0;
    frameNumber=0;phase=0;destroyedTask=0;callbackCount=0;allocationCount=0;oamCount=0;
    occupiedSlots=occupied;layoutId=layout;
    for(int i=0;i<4;i++) {
        controlledCoords[i][0]=sBattlerCoords[layout][i].x;
        controlledCoords[i][1]=sBattlerCoords[layout][i].y+controlledPictureOffsets[i];
    }
    for(int i=0;i<occupied;i++){gSprites[i].inUse=1;gSprites[i].callback=DummySpriteCallback;}
    gBattleAnimAttacker=attacker;gBattleAnimTarget=target;gAnimVisualTaskCount=0;
    struct Task *task=&gTasks[TASK_ID];task->isActive=1;task->prev=HEAD_SENTINEL;task->next=TAIL_SENTINEL;
    task->func=AnimTask_GlareEyeDots;task->data[7]=seedContest;
    /* Cmd_createvisualtask invokes init immediately and increments count once. */
    task->func(TASK_ID);gAnimVisualTaskCount++;
    int contestAfterInit=task->data[7];
    if(forceContest)task->data[7]=1;
    printf("{\"name\":\"%s\",\"layout\":\"%s\",\"attacker\":%d,\"target\":%d,\"taskId\":%d,\"occupiedSlots\":%d,\"failAllocationAttempt\":%d,\"contestBeforeInit\":%d,\"contestAfterInit\":%d,\"forceContestBranch\":%s,\"battlerInputs\":[",name,layout?"double":"single",attacker,target,TASK_ID,occupied,failAttempt,seedContest,contestAfterInit,forceContest?"true":"false");
    for(int i=0;i<4;i++) {
        if(i)putchar(',');
        printf("{\"battler\":%d,\"position\":%d,\"side\":%d,\"sourceBaseX\":%d,\"sourceBaseY\":%d,\"pictureYOffset\":%d,\"x2Read\":%d,\"yPicRead\":%d,\"heightRead\":%d}",i,i,i&1,sBattlerCoords[layout][i].x,sBattlerCoords[layout][i].y,controlledPictureOffsets[i],controlledCoords[i][0],controlledCoords[i][1],controlledHeights[i]);
    }
    printf("],\"initReads\":");printInts(&reads[0][0],readCount,4);printf(",\"frames\":[");printFrame();
    while(task->isActive) {
        check(frameNumber<128,"task completion bound");
        frameNumber++;phase=1;callbackCount=0;allocationCount=0;oamCount=0;destroyedTask=0;
        BattleMainCB2();putchar(',');printFrame();
    }
    check(gAnimVisualTaskCount==0 && liveDotCount()==0,"complete parent and children");
    printf("],\"summary\":{\"lastFrame\":%d,\"allocationAttempts\":%d,\"successfulChildren\":%d,\"peakChildren\":%d}}",frameNumber,allocationAttempt,nextInstance,peakChildren);
}
static void printCoordinateRows(void) {
    const s16 endpoints[][4] = {
        {87,68,176,35},{164,23,72,83},{10,10,0,-1},{0,0,-1,-2},
        {-20,-30,17,19},{17,19,-20,-30},{-32768,-32768,32767,32767},
        {32767,32767,-32768,-32768},{0,0,1,1},{1,1,0,0},
        {-1,-1,-1,-1},{72,80,72,80},{200,40,32,80}
    };
    const u8 maxima[]={0,1,2,3,12,255};
    int first=1;
    printf("\"coordinates\":[");
    for(unsigned e=0;e<sizeof(endpoints)/sizeof(endpoints[0]);e++)
      for(unsigned m=0;m<sizeof(maxima)/sizeof(maxima[0]);m++)
        for(int p=0;p<=255;p++) {
            /* Every pair index for max12 and both edge max values; bounded. */
            if(maxima[m]!=0 && maxima[m]!=1 && maxima[m]!=12 && maxima[m]!=255 && p>maxima[m]+1)continue;
            s16 x=12345,y=-12345;
            GetGlareEyeDotCoords(endpoints[e][0],endpoints[e][1],endpoints[e][2],endpoints[e][3],maxima[m],(u8)p,&x,&y);
            if(!first)putchar(',');
        first=0;
            printf("{\"startX\":%d,\"startY\":%d,\"endX\":%d,\"endY\":%d,\"pairMax\":%d,\"pairNum\":%d,\"x\":%d,\"y\":%d}",endpoints[e][0],endpoints[e][1],endpoints[e][2],endpoints[e][3],maxima[m],p,x,y);
        }
    putchar(']');
}
int main(void) {
    check(sizeof(s16)==2 && sizeof(int)==4 && (-1>>1)==-1,"source integer model");
    printf("{\"scenarios\":[");int first=1;char name[80];
    for(int layout=0;layout<2;layout++)for(int attacker=0;attacker<4;attacker++)for(int target=0;target<4;target++) {
        if(!first)putchar(',');
        first=0;
        snprintf(name,sizeof(name),"%s_a%d_t%d",layout?"double":"single",attacker,target);
        runScenario(name,layout,attacker,target,0,0,0,0);
    }
    printf(",");runScenario("full_saturation",0,0,1,64,0,0,0);
    printf(",");runScenario("one_free_slot_partial_pairs",1,3,0,63,0,0,0);
    printf(",");runScenario("first_sibling_allocation_failure",0,0,1,0,1,0,0);
    printf(",");runScenario("contest_seed_is_reset_false",0,0,1,0,0,1,0);
    printf(",");runScenario("unreachable_forced_contest_diagonal",0,0,1,0,0,0,1);
    printf("],");printCoordinateRows();printf("}\n");
    return 0;
}
