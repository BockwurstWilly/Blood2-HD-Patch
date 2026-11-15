import struct, sys, capstone

path, needles = sys.argv[1], sys.argv[2:]
d = open(path, 'rb').read()
pe = struct.unpack_from('<I', d, 0x3c)[0]
nsec = struct.unpack_from('<H', d, pe + 6)[0]
optsz = struct.unpack_from('<H', d, pe + 20)[0]
base = struct.unpack_from('<I', d, pe + 52)[0]
secs = []
for i in range(nsec):
    o = pe + 24 + optsz + i * 40
    name = d[o:o + 8].rstrip(b'\0').decode()
    vsz, va, rsz, raw = struct.unpack_from('<IIII', d, o + 8)
    secs.append((name, va, vsz, raw, rsz))

def off2va(off):
    for n, va, vsz, raw, rsz in secs:
        if raw <= off < raw + rsz:
            return base + va + off - raw

def va2off(v):
    for n, va, vsz, raw, rsz in secs:
        if base + va <= v < base + va + rsz:
            return raw + v - base - va

text = next(s for s in secs if s[0] == '.text')
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)

def strat(v):
    o = va2off(v)
    if o is None:
        return None
    e = d.find(b'\0', o)
    s = d[o:e]
    if 3 <= len(s) < 120 and all(32 <= c < 127 for c in s):
        return s.decode()

for nd in needles:
    o = d.find(nd.encode() + b'\0')
    while o != -1:
        v = off2va(o)
        print(f'== {nd!r} @ {v:#x}')
        ref = struct.pack('<I', v)
        r = d.find(ref, text[3], text[3] + text[4])
        while r != -1:
            start = r - 0x180
            code = d[start:r + 0x120]
            for ins in md.disasm(code, off2va(start)):
                if ins.address < off2va(r) - 0x100:
                    continue
                note = ''
                for tok in ins.op_str.replace('[', ' ').replace(']', ' ').split():
                    if tok.startswith('0x'):
                        s = strat(int(tok.rstrip(','), 16))
                        if s:
                            note = f'   ; "{s}"'
                print(f'{ins.address:08x} {ins.mnemonic} {ins.op_str}{note}')
            print('----')
            r = d.find(ref, r + 1, text[3] + text[4])
        o = d.find(nd.encode() + b'\0', o + 1)
