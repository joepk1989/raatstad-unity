# Maakt twee naadloze grijswaardentextures (RGBA, elk kanaal een bodemsoort) voor de terreinshader van Raatstad.
# Zonder numpy: alles met lijsten. Gebruik: python maaktex.py <uitmap>
import sys, os, math, random, struct, zlib

N = 256

def lattice(p, seed):
    rnd = random.Random(seed)
    return [[rnd.random() for _ in range(p)] for _ in range(p)]

def value_noise(p, seed):
    """naadloze waarderuis met periode p cellen over N pixels"""
    L = lattice(p, seed); s = p / N; out = [[0.0] * N for _ in range(N)]
    for y in range(N):
        fy = y * s; iy = int(fy); ty = fy - iy; ty = ty * ty * (3 - 2 * ty); y0 = iy % p; y1 = (iy + 1) % p
        r0 = L[y0]; r1 = L[y1]; row = out[y]
        for x in range(N):
            fx = x * s; ix = int(fx); tx = fx - ix; tx = tx * tx * (3 - 2 * tx); x0 = ix % p; x1 = (ix + 1) % p
            a = r0[x0] + (r0[x1] - r0[x0]) * tx; b = r1[x0] + (r1[x1] - r1[x0]) * tx
            row[x] = a + (b - a) * ty
    return out

def fbm(p0, octaves, seed, gain=0.5, ridge=False):
    out = [[0.0] * N for _ in range(N)]; amp = 1.0; tot = 0.0; p = p0
    for o in range(octaves):
        n = value_noise(p, seed * 31 + o)
        for y in range(N):
            ro = out[y]; rn = n[y]
            for x in range(N):
                v = rn[x]
                if ridge: v = 1.0 - abs(2.0 * v - 1.0)
                ro[x] += v * amp
        tot += amp; amp *= gain; p *= 2
        if p > N: break
    for y in range(N):
        ro = out[y]
        for x in range(N): ro[x] /= tot
    return out

def norm(a, lo=0.0, hi=1.0):
    mn = min(min(r) for r in a); mx = max(max(r) for r in a); k = (hi - lo) / (mx - mn + 1e-9)
    return [[lo + (v - mn) * k for v in r] for r in a]

def comb(f, *arrs):
    return [[f(*[a[y][x] for a in arrs]) for x in range(N)] for y in range(N)]

def dots(count, rmin, rmax, seed):
    """naadloze vlekjes (bloempjes, steentjes): hoogte 0..1"""
    rnd = random.Random(seed); out = [[0.0] * N for _ in range(N)]
    for _ in range(count):
        cx = rnd.random() * N; cy = rnd.random() * N; r = rmin + rnd.random() * (rmax - rmin); h = 0.6 + rnd.random() * 0.4
        for dy in range(int(-r) - 1, int(r) + 2):
            for dx in range(int(-r) - 1, int(r) + 2):
                d = math.sqrt(dx * dx + dy * dy) / r
                if d >= 1: continue
                yy = int(cy + dy) % N; xx = int(cx + dx) % N; v = h * (1 - d * d)
                if v > out[yy][xx]: out[yy][xx] = v
    return out

def ripples(per, warp, seed):
    w = fbm(4, 3, seed)
    return [[0.5 + 0.5 * math.sin(2 * math.pi * (per * y / N + warp * (w[y][x] - 0.5) + 0.15 * math.sin(2 * math.pi * 2 * x / N))) for x in range(N)] for y in range(N)]

def clamp(v): return 0.0 if v < 0 else 1.0 if v > 1 else v

def png(path, chans):
    raw = bytearray()
    for y in range(N):
        raw.append(0)
        for x in range(N):
            for c in chans: raw.append(int(clamp(c[y][x]) * 255 + 0.5))
    def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", N, N, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(data)

out = sys.argv[1]; os.makedirs(out, exist_ok=True)
# A: gras, aarde, rots, zand
fine = fbm(32, 3, 1); blades = fbm(64, 2, 2); clump = fbm(8, 3, 3)
gras = norm(comb(lambda a, b, c: 0.45 * a + 0.3 * b + 0.25 * c, fine, blades, clump), 0.15, 0.95)
peb = dots(260, 1.2, 3.2, 4); soil = fbm(8, 5, 5)
aarde = norm(comb(lambda s, p: 0.75 * s + 0.35 * p, soil, peb), 0.1, 0.95)
def sample(L, p, fx, fy):
    ix = math.floor(fx); iy = math.floor(fy); tx = fx - ix; ty = fy - iy; tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty)
    x0 = ix % p; x1 = (ix + 1) % p; y0 = iy % p; y1 = (iy + 1) % p
    a = L[y0][x0] + (L[y0][x1] - L[y0][x0]) * tx; b = L[y1][x0] + (L[y1][x1] - L[y1][x0]) * tx; return a + (b - a) * ty
def warped(p0, octaves, seed, wx, wy, amt, ridge):
    out = [[0.0] * N for _ in range(N)]; amp = 1.0; tot = 0.0; p = p0
    for o in range(octaves):
        L = lattice(p, seed * 31 + o); s = p / N
        for y in range(N):
            for x in range(N):
                v = sample(L, p, (x + amt * (wx[y][x] - 0.5) * N / p0) * s, (y + amt * (wy[y][x] - 0.5) * N / p0) * s)
                if ridge: v = 1.0 - abs(2.0 * v - 1.0)
                out[y][x] += v * amp
        tot += amp; amp *= 0.5; p *= 2
    return [[v / tot for v in r] for r in out]
wxx = fbm(4, 4, 20); wyy = fbm(4, 4, 21)
rid = warped(4, 5, 6, wxx, wyy, 1.6, True); strata = [[0.5 + 0.5 * math.sin(2 * math.pi * (6 * y / N + 0.6 * rid[y][x])) for x in range(N)] for y in range(N)]
cr = warped(16, 3, 7, wxx, wyy, 3.0, True)
rots = norm(comb(lambda r, s, c: 0.5 * r + 0.2 * s + 0.3 * c * c, rid, strata, cr), 0.05, 0.95)
grain = fbm(64, 2, 8); rip = ripples(10, 1.2, 9)
zand = norm(comb(lambda g, r: 0.6 * g + 0.4 * r, grain, rip), 0.2, 0.9)
png(os.path.join(out, "BodemA.png"), [gras, aarde, rots, zand])
# B: sneeuw, heide, woestijn, grote vlekken
soft = fbm(8, 4, 10); spark = fbm(128, 1, 11)
sneeuw = norm(comb(lambda s, k: 0.8 * s + 0.2 * k, soft, spark), 0.3, 0.9)
flow = dots(700, 1.0, 2.6, 12); hb = fbm(16, 3, 13)
heide = norm(comb(lambda f, b: 0.65 * f + 0.35 * b, flow, hb), 0.0, 1.0)
dune = ripples(5, 2.0, 14); dg = fbm(32, 2, 15)
woest = norm(comb(lambda d, g: 0.7 * d + 0.3 * g, dune, dg), 0.1, 0.9)
macro = norm(fbm(4, 4, 16), 0.0, 1.0)
png(os.path.join(out, "BodemB.png"), [sneeuw, heide, woest, macro])
print("klaar")
