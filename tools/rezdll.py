"""Cuts the PE images (DLLs) out of a LithTech REZ archive by scanning for MZ/PE headers.
usage: rezdll.py <file.rez> <outdir>"""
import struct, sys, os

data = open(sys.argv[1], 'rb').read()
out = sys.argv[2]
os.makedirs(out, exist_ok=True)
i = 0
n = 0
while True:
    i = data.find(b'MZ', i)
    if i < 0:
        break
    try:
        pe = i + struct.unpack_from('<I', data, i + 0x3c)[0]
        if data[pe:pe + 4] == b'PE\0\0':
            nsec = struct.unpack_from('<H', data, pe + 6)[0]
            optsz = struct.unpack_from('<H', data, pe + 20)[0]
            end = 0
            for s in range(nsec):
                o = pe + 24 + optsz + s * 40
                rsz, raw = struct.unpack_from('<II', data, o + 16)
                end = max(end, raw + rsz)
            path = os.path.join(out, 'pe_%d_%x.dll' % (n, i))
            open(path, 'wb').write(data[i:i + end])
            print(path, end)
            n += 1
    except struct.error:
        pass
    i += 2
