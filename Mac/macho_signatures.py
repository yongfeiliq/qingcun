"""Read-only Mach-O page-hash audit. This does not replace macOS trust assessment."""
import struct, hashlib, plistlib

def inspect_signature(data):
    if data[:4] == b'\xca\xfe\xba\xbe':
        count=struct.unpack_from('>I',data,4)[0]
        slices=[]
        for i in range(count):
            cpu,sub,offset,size,align=struct.unpack_from('>IIIII',data,8+20*i)
            assert offset+size<=len(data)
            slices.append(inspect_signature(data[offset:offset+size]))
        return {'fat':True,'slices':slices}
    assert data[:4]==b'\xcf\xfa\xed\xfe','Expected 64-bit Mach-O'
    cpu=struct.unpack_from('<I',data,4)[0];count=struct.unpack_from('<I',data,16)[0];at=32;sig=None
    for _ in range(count):
        cmd,size=struct.unpack_from('<II',data,at);assert size>=8 and at+size<=len(data)
        if cmd==0x1d:sig=struct.unpack_from('<II',data,at+8)
        at+=size
    if sig is None:return {'cpu':cpu,'signed':False}
    offset,size=sig;assert offset+size<=len(data)
    blob=data[offset:offset+size];magic,length,count=struct.unpack_from('>III',blob)
    assert magic==0xfade0cc0 and length<=size
    slots={}
    for i in range(count):
        kind,start=struct.unpack_from('>II',blob,12+i*8);_,length2=struct.unpack_from('>II',blob,start)
        assert start+length2<=length
        slots[kind]=blob[start:start+length2]
    dirs=[]
    for kind,cd in slots.items():
        if kind!=0 and not 0x1000<=kind<0x1006:continue
        magic,length2,version,flags,hofs,ident,special,pages,limit=struct.unpack_from('>9I',cd)
        assert magic==0xfade0c02
        hsize,htype,platform,psize=struct.unpack_from('>4B',cd,36)
        if version>=0x20300 and limit==0xffffffff:limit=struct.unpack_from('>Q',cd,56)[0]
        assert limit<=offset and psize<=30 and hsize>0
        unit=2**psize if psize else limit;assert unit>0
        assert pages==(limit+unit-1)//unit
        algorithm={1:'sha1',2:'sha256',3:'sha256',4:'sha384'}[htype]
        def digest(b):return hashlib.new(algorithm,b).digest()[:hsize]
        for i in range(pages):
            actual=digest(data[i*unit:min((i+1)*unit,limit)])
            assert actual==cd[hofs+i*hsize:hofs+(i+1)*hsize],f'Code page hash mismatch: {i}'
        for slot in [2,5,7]:
            if slot in slots and slot<=special:
                assert digest(slots[slot])==cd[hofs-slot*hsize:hofs-(slot-1)*hsize],f'Special slot {slot}'
        dirs.append({'flags':flags,'pages':pages,'pageBytes':unit,'codeBytes':limit,'hash':algorithm,'identifier':cd[ident:].split(b'\0')[0].decode()})
    assert dirs,'No CodeDirectory'
    entitlements=plistlib.loads(slots[5][8:]) if 5 in slots else {}
    return {'cpu':cpu,'signed':True,'directories':dirs,'entitlements':entitlements}
