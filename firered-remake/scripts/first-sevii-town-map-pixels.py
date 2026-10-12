"""Bounded PNG and atlas decoding reused from fixed main 41a0e4f importers. No upstream execution."""
import binascii,re,struct,zlib
def indexed_png(data):
    if data[:8]!=b'\x89PNG\r\n\x1a\n':raise ValueError('PNG signature differs')
    pos=8; chunks=[]
    while pos<len(data):
        if pos+12>len(data):raise ValueError('Truncated PNG')
        n=struct.unpack_from('>I',data,pos)[0];tag=data[pos+4:pos+8];payload=data[pos+8:pos+8+n];end=pos+12+n
        if end>len(data) or binascii.crc32(tag+payload)&0xffffffff!=struct.unpack_from('>I',data,end-4)[0]:raise ValueError('PNG CRC differs')
        chunks.append((tag,payload));pos=end
    if not chunks or chunks[0][0]!=b'IHDR' or chunks[-1]!=(b'IEND',b''):raise ValueError('PNG framing differs')
    w,h,depth,color,comp,filt,inter=struct.unpack('>IIBBBBB',chunks[0][1])
    if depth not in (4,8) or (color,comp,filt,inter)!=(3,0,0,0) or w%8 or h%8 or not 0<w*h<=32768:raise ValueError('Unsupported PNG profile')
    palettes=[p for t,p in chunks if t==b'PLTE'];streams=[p for t,p in chunks if t==b'IDAT']
    if len(palettes)!=1 or len(streams)!=1 or len(palettes[0])%3:raise ValueError('Unsupported PNG chunks')
    stride=w*depth//8;expected=(stride+1)*h;decomp=zlib.decompressobj();raw=decomp.decompress(streams[0],expected+1)
    if len(raw)!=expected or not decomp.eof or decomp.unused_data or decomp.unconsumed_tail:raise ValueError('PNG stream length differs')
    result=bytearray();previous=[0]*stride
    for y in range(h):
        method=raw[y*(stride+1)];row=[]
        if method not in range(5):raise ValueError('Unsupported PNG filter')
        for x,b in enumerate(raw[y*(stride+1)+1:(y+1)*(stride+1)]):
            left=row[x-1] if x else 0;up=previous[x];ul=previous[x-1] if x else 0;p=left+up-ul;ds=[abs(p-left),abs(p-up),abs(p-ul)]
            pred=[0,left,up,(left+up)//2,[left,up,ul][ds.index(min(ds))]][method];v=(b+pred)&255;row.append(v)
            result.extend((v>>4,v&15) if depth==4 else (v&15,)) # Upstream ConvertBitDepth reduces mod16.
        previous=row
    return w,h,bytes(result),[tuple(palettes[0][i:i+3]) for i in range(0,len(palettes[0]),3)]

def colors(rgb):return [tuple(((c//8)<<3)|((c//8)>>2) for c in row) for row in rgb]
def palette(data):
    lines=data.decode('ascii').splitlines()
    if lines[:2]!=['JASC-PAL','0100'] or len(lines)!=int(lines[2])+3 or int(lines[2]) not in (16,48):raise ValueError('Unsupported palette')
    rgb=[]
    for line in lines[3:]:
        if not re.fullmatch(r'\d+ \d+ \d+',line):raise ValueError('Malformed palette')
        row=tuple(map(int,line.split()))
        if any(c>255 for c in row):raise ValueError('Palette range differs')
        rgb.append(row)
    return colors(rgb)
def tile_list(png):
    w,h,indices,_=indexed_png(png)
    return [bytes(indices[(y+dy)*w+x+dx] for dy in range(8) for dx in range(8)) for y in range(0,h,8) for x in range(0,w,8)]
def rgba(indices,pal,transparent=True):return bytes(c for i in indices for c in (*pal[i],0 if i==0 and transparent else 255))
def canvas(w,h,color=(0,0,0,0)):return bytearray(color*(w*h))
def blit(dest,dw,source,sw,sh,x,y):
    dh=len(dest)//dw//4
    if x<0 or y<0 or x+sw>dw or y+sh>dh:raise ValueError('Blit out of bounds')
    for row in range(sh):dest[((y+row)*dw+x)*4:((y+row)*dw+x+sw)*4]=source[row*sw*4:(row+1)*sw*4]
def tile_rgba(tile,pal,hflip=False,vflip=False,transparent=True):return rgba([tile[(7-y if vflip else y)*8+(7-x if hflip else x)] for y in range(8) for x in range(8)],pal,transparent)
def chunk(tag,data):return struct.pack('>I',len(data))+tag+data+struct.pack('>I',binascii.crc32(tag+data)&0xffffffff)
def png(w,h,pixels):
    if len(pixels)!=w*h*4:raise ValueError('RGBA length differs')
    scan=b''.join(b'\0'+pixels[y*w*4:(y+1)*w*4] for y in range(h));blocks=[]
    for start in range(0,len(scan),65535):
        block=scan[start:start+65535];n=len(block);blocks.append(bytes([1 if start+n==len(scan) else 0])+struct.pack('<HH',n,n^65535)+block)
    stream=b'\x78\x01'+b''.join(blocks)+struct.pack('>I',zlib.adler32(scan)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',stream)+chunk(b'IEND',b'')


def palette(data, count):
    # Shared decoder's RGB555 expansion, extended here for the original 80-color map palette.
    lines=data.decode('ascii').splitlines()
    if lines[:3] != ['JASC-PAL','0100',str(count)] or len(lines)!=count+3:
        raise ValueError('Unexpected Town Map palette profile')
    rows=[]
    for line in lines[3:]:
        if not re.fullmatch(r'\d+ \d+ \d+',line): raise ValueError('Malformed palette')
        row=tuple(map(int,line.split()))
        if any(v<0 or v>255 for v in row): raise ValueError('Palette range differs')
        rows.append(row)
    return colors(rows)


def compressed_png(w,h,rgba):
    # Reuse PNG framing / CRC rules; standard DEFLATE avoids duplicating 150 KiB per static frame.
    scan=b''.join(b'\0'+rgba[y*w*4:(y+1)*w*4] for y in range(h))
    return (b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))
            +chunk(b'IDAT',zlib.compress(scan,9))+chunk(b'IEND',b''))


def render_tilemap(tiles, entries, palettes):
    if len(entries)!=600: raise ValueError('Unexpected Town Map tilemap dimensions')
    result=canvas(240,160)
    for offset,entry in enumerate(entries):
        number,bank=entry&1023,entry>>12
        if number>=len(tiles) or bank not in palettes: raise ValueError('Map tile/palette outside source')
        blit(result,240,tile_rgba(tiles[number],palettes[bank],bool(entry&1024),bool(entry&2048)),
                    8,8,(offset%30)*8,(offset//30)*8)
    return bytes(result)


def overlay(destination,source):
    if len(destination)!=len(source): raise ValueError('Map overlay size differs')
    for i in range(0,len(source),4):
        if source[i+3]: destination[i:i+4]=source[i:i+4]
