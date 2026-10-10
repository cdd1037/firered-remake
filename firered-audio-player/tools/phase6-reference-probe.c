/* Developer-only phase6 fixed-song reference sampler, adapted from the
 * earlier private probe/phase4 validated sampler. Executes original m4aSongNumStart,
 * never part of the standalone C# player/runtime. Baseline ROM and pack are
 * read-only; only the in-memory bootstrap/resource overlay is installed.
 * Usage: reference-probe pack baseline-ROM-or-dash output.wav song-id frames
 * Exactly five phase5-selected song IDs and exactly480 capture frames admitted.
 * GUARD_ROM=0 disables read/branch hooks only for the explicit control run.
 */
#include <mgba/flags.h>
#include <mgba/core/core.h>
#include <mgba/core/blip_buf.h>
#include <mgba/core/config.h>
#include <mgba/core/log.h>
#include <stdint.h>
#include <errno.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <mgba/gba/core.h>
#include <mgba-util/vfs.h>
#include <time.h>
#include <mgba/internal/arm/arm.h>
#include <sys/resource.h>

#define RATE 48000
#define CAPACITY 8192
#define ROM_SIZE 0x1000000u
#define ROM_BASE 0x08000000u
#define SONG_TABLE 0x084a32ccu
#define SOUND_INFO 0x03005f50u
static struct mCore *core;
static int16_t samples[CAPACITY * 2];
static uint32_t video[240 * 160];

static void fail(const char *msg) { fprintf(stderr, "%s\n", msg); exit(2); }
static void log_checked(struct mLogger *logger, int category, enum mLogLevel level, const char *format, va_list args) {
    (void)logger; (void)category;
    if (strstr(format, "Illegal opcode") || (level & mLOG_FATAL)) {
        vfprintf(stderr, format, args); fputc('\n', stderr); fail("Emulated CPU fault");
    }
}
static struct mLogger checked_logger = {.log=log_checked, .filter=NULL};
static void put16(FILE *f, uint16_t v) { fputc(v & 255, f); fputc(v >> 8, f); }
static void put32(FILE *f, uint32_t v) { put16(f, v & 65535); put16(f, v >> 16); }
static void wav_header(FILE *f, uint32_t frames) {
    fseek(f, 0, SEEK_SET);
    fwrite("RIFF", 1, 4, f); put32(f, 36 + frames * 4); fwrite("WAVEfmt ", 1, 8, f);
    put32(f, 16); put16(f, 1); put16(f, 2); put32(f, RATE); put32(f, RATE * 4);
    put16(f, 4); put16(f, 16); fwrite("data", 1, 4, f); put32(f, frames * 4);
}

static int drain(FILE *output) {
    struct blip_t *left = core->getAudioChannel(core, 0);
    struct blip_t *right = core->getAudioChannel(core, 1);
    int n = blip_samples_avail(left);
    if (n != blip_samples_avail(right) || n > CAPACITY) fail("Audio channel/buffer mismatch");
    if (blip_read_samples(left, samples, n, 1) != n || blip_read_samples(right, samples + 1, n, 1) != n) fail("Short audio read");
    if (output && fwrite(samples, 4, n, output) != (size_t)n) fail("Audio output write failed");
    return n;
}

static void frames_without_capture(int count) {
    for (int i = 0; i < count; ++i) { core->runFrame(core); drain(NULL); }
}


static struct {uint32_t start,end;} ranges[32];
static uint32_t nranges;
static uint64_t checks;
static void check_address(uint32_t a,uint32_t n){
 if(a<0x08000000||a>=0x0e000000)return;
 a&=0x01ffffff;checks++;
 for(uint32_t i=0;i<nranges;i++)if(a>=ranges[i].start && a+n<=ranges[i].end)return;
 fprintf(stderr,"unpacked ROM read/branch offset=%08x bytes=%u\n",a,n);fail("resource closure violation");
}
static uint32_t (*old_load32)(struct ARMCore*,uint32_t,int*);
static uint32_t (*old_load16)(struct ARMCore*,uint32_t,int*);
static uint32_t (*old_load8)(struct ARMCore*,uint32_t,int*);
static void (*old_active)(struct ARMCore*,uint32_t);
static uint32_t guarded32(struct ARMCore*c,uint32_t a,int*t){check_address(a&~3u,4);return old_load32(c,a,t);}
static uint32_t guarded16(struct ARMCore*c,uint32_t a,int*t){check_address(a&~1u,2);return old_load16(c,a,t);}
static uint32_t guarded8(struct ARMCore*c,uint32_t a,int*t){check_address(a,1);return old_load8(c,a,t);}
static void guarded_active(struct ARMCore*c,uint32_t a){check_address(a&~1u,2);old_active(c,a);}
static void guards(void){struct ARMCore*c=core->cpu;old_load32=c->memory.load32;old_load16=c->memory.load16;old_load8=c->memory.load8;old_active=c->memory.setActiveRegion;c->memory.load32=guarded32;c->memory.load16=guarded16;c->memory.load8=guarded8;c->memory.setActiveRegion=guarded_active;}
static uint32_t u32(FILE*f){uint32_t x;if(fread(&x,4,1,f)!=1)fail("short pack");return x;}
static uint32_t argument(const char *text, uint32_t max, const char *message) {
 char *end; errno=0;
 if(!text[0] || text[0]=='-')fail(message);
 unsigned long value=strtoul(text,&end,0);
 if(errno || *end || value>max)fail(message);
 return (uint32_t)value;
}
static uint32_t read_le32(const uint8_t *p) {
 return p[0] | ((uint32_t)p[1]<<8) | ((uint32_t)p[2]<<16) | ((uint32_t)p[3]<<24);
}
static void packed_span(uint32_t address,uint32_t length) {
 if(address<ROM_BASE || address-ROM_BASE>=ROM_SIZE || length>ROM_SIZE-(address-ROM_BASE))fail("validated span outside fixed ROM");
 uint32_t off=address-ROM_BASE;
 for(uint32_t i=0;i<nranges;i++)if(off>=ranges[i].start && off<=ranges[i].end && length<=ranges[i].end-off)return;
 fail("validated span absent from packed resources");
}
struct FixedSong {uint32_t id,address,voices;uint8_t tracks,priority,player;};
static const struct FixedSong songs[]={
 {4,0x086b56f4,0x0848bd14,1,5,1},
 {241,0x086bc670,0x0848ed74,2,5,1},
 {291,0x086d1ddc,0x08495bfc,8,0,0},
 {314,0x086e8810,0x0849db28,9,0,0},
 {298,0x086da810,0x08498278,8,0,0}
};
static const struct FixedSong *song_by_id(uint32_t id) {
 for(size_t i=0;i<sizeof(songs)/sizeof(songs[0]);i++)if(songs[i].id==id)return &songs[i];
 fail("song ID is outside the five-song fixed whitelist");return NULL;
}
static void validate_song(const uint8_t *rom,const struct FixedSong *song) {
 uint32_t entry=SONG_TABLE+song->id*8;packed_span(entry,8);
 const uint8_t *e=rom+entry-ROM_BASE;
 if(read_le32(e)!=song->address || read_le32(e+4)!=(uint32_t)(song->player|(song->player<<16)))fail("unexpected fixed song table entry");
 packed_span(song->address,8+4*song->tracks);
 const uint8_t *h=rom+song->address-ROM_BASE;
 if(h[0]!=song->tracks || h[1] || h[2]!=song->priority || h[3]!=178 || read_le32(h+4)!=song->voices)fail("unexpected fixed song header");
 packed_span(song->voices,12);
 for(uint32_t i=0;i<song->tracks;i++)packed_span(read_le32(h+8+4*i),1);
}
static void proc_snapshot(long *rss,long *hwm) {
 *rss=-1;*hwm=-1;
 FILE *f=fopen("/proc/self/status","r");if(!f)return;
 char line[256];
 while(fgets(line,sizeof(line),f)){
  if(strncmp(line,"VmRSS:",6)==0)sscanf(line+6,"%ld",rss);
  if(strncmp(line,"VmHWM:",6)==0)sscanf(line+6,"%ld",hwm);
 }
 fclose(f);
}
int main(int argc,char**argv){
 if(argc!=6)fail("reference-probe pack baseline-ROM-or-dash output.wav song-id frames");
 uint32_t song_id=argument(argv[4],UINT32_MAX,"invalid song-id argument");
 const struct FixedSong *song=song_by_id(song_id);
 int count=(int)argument(argv[5],480,"capture frame count must be exactly480");
 if(count!=480)fail("capture frame count must be exactly480");
 const char *guard_text=getenv("GUARD_ROM");
 if(guard_text && strcmp(guard_text,"0") && strcmp(guard_text,"1"))fail("GUARD_ROM must be0 or1");
 int guarded=!guard_text || strcmp(guard_text,"0");
 uint8_t *rom=calloc(1,ROM_SIZE);if(!rom)fail("alloc");
 if(strcmp(argv[2],"-")){FILE*f=fopen(argv[2],"rb");if(!f)fail("baseline open");if(fread(rom,1,ROM_SIZE,f)!=ROM_SIZE || fgetc(f)!=EOF)fail("baseline must have exact fixed ROM size");fclose(f);}
 FILE*f=fopen(argv[1],"rb");if(!f)fail("pack open");char magic[8];
 if(fread(magic,1,8,f)!=8 || memcmp(magic,"AUDPK001",8))fail("pack magic");
 uint32_t n=u32(f);if(!n || n>32)fail("pack segment count");
 for(uint32_t i=0;i<n;i++){
  uint32_t off=u32(f),sz=u32(f);
  if(off>=ROM_SIZE || !sz || sz>ROM_SIZE-off)fail("pack range");
  for(uint32_t j=0;j<nranges;j++)if(off<ranges[j].end && ranges[j].start<off+sz)fail("overlapping pack segments");
  ranges[nranges].start=off;ranges[nranges++].end=off+sz;
  if(fread(rom+off,1,sz,f)!=sz)fail("segment read");
 }
 if(fgetc(f)!=EOF)fail("trailing pack data");
 fclose(f);
 validate_song(rom,song);
 mLogSetDefaultLogger(&checked_logger);
 core=GBACoreCreate();if(!core||!core->init(core))fail("init");mCoreInitConfig(core,NULL);
 mCoreConfigSetDefaultIntValue(&core->config,"mute",0);mCoreConfigSetDefaultIntValue(&core->config,"volume",256);mCoreConfigSetDefaultIntValue(&core->config,"sampleRate",RATE);mCoreConfigSetDefaultIntValue(&core->config,"audioBuffers",CAPACITY);mCoreLoadConfig(core);
 core->setVideoBuffer(core,video,240);if(!core->loadROM(core,VFileFromMemory(rom,ROM_SIZE)))fail("load");core->setAudioBufferSize(core,CAPACITY);core->reset(core);
 blip_set_rates(core->getAudioChannel(core,0),core->frequency(core),RATE);blip_set_rates(core->getAudioChannel(core,1),core->frequency(core),RATE);
 // Use the same diagnostic ROM closure guard, except the declared control.
 if(guarded)guards();
 frames_without_capture(60);
 fprintf(stderr,"boot=%08x sound=%08x ticks=%u\n",core->busRead32(core,0x0200001c),core->busRead32(core,0x03005f50),core->busRead32(core,0x02000018));
 if(core->busRead32(core,0x0200001c)!=0x41554449)fail("boot not ready");
 // The unchanged pack bootstrap initialized m4aSoundInit and stereo1.
 // A fresh song retains its original header volumes/pan/reverb; no gain fitting.
 uint32_t initial_reverb=core->busRead8(core,SOUND_INFO+5);
 FILE*out=fopen(argv[3],"wb");if(!out)fail("wav");wav_header(out,0);
 core->busWrite32(core,0x02000004,song_id);
 core->busWrite32(core,0x02000000,0x081dd0f5);
 clock_t t=clock();uint32_t total=0;
 for(int i=0;i<count;i++){core->runFrame(core);total+=drain(out);}
 double secs=(double)(clock()-t)/CLOCKS_PER_SEC;
 wav_header(out,total);if(fclose(out))fail("wav close");
 long vm_rss,vm_hwm;proc_snapshot(&vm_rss,&vm_hwm);
 struct rusage ru;if(getrusage(RUSAGE_SELF,&ru))fail("getrusage");
 printf("{\"songId\":%u,\"songAddress\":\"0x%08x\",\"originalPlayer\":%u,\"frames\":%u,\"emulatedFrames\":%d,\"cpuSeconds\":%.6f,\"audioSeconds\":%.6f,\"realtimeFactor\":%.3f,\"mailbox\":%u,\"guarded\":%s,\"romChecks\":%llu,\"maxRssKiB\":%ld,\"procVmRssKiB\":%ld,\"procVmHwmKiB\":%ld,\"initialReverb\":%u,\"finalReverb\":%u,\"pcmMaxChannels\":%u,\"pcmMasterVolume\":%u,\"pcmSamplesPerVBlank\":%u,\"pcmFreq\":%u,\"soundCntL\":%u,\"soundCntH\":%u}\n",song_id,song->address,song->player,total,count,secs,(double)total/RATE,(double)total/RATE/secs,core->busRead32(core,0x02000000),guarded?"true":"false",(unsigned long long)checks,ru.ru_maxrss,vm_rss,vm_hwm,initial_reverb,core->busRead8(core,SOUND_INFO+5),core->busRead8(core,SOUND_INFO+6),core->busRead8(core,SOUND_INFO+7),core->busRead32(core,SOUND_INFO+16),core->busRead32(core,SOUND_INFO+20),core->busRead16(core,0x04000080),core->busRead16(core,0x04000082));
 core->deinit(core);free(rom);return 0;
}
