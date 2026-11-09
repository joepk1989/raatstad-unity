# Een auto uit een .blend (Blender 2.8x, niet gecomprimeerd) omzetten voor Raatstad, zonder Blender:
#   python Tools/blend_naar_auto.py <auto.blend> Assets/Resources/Autos/<naam>.txt
# Leest het .blend-formaat zelf (SDNA), neemt de carrosserie (OBCube*) en de wielen (OBCircle*), kleuren uit de Principled BSDF,
# en schrijft alles op ware grootte (5,68 m lang) in auto-coördinaten (x rechts, y op, z vooruit). Inlezen: ModelCar.cs.
import struct, sys, re, math
class Blend:
    def __init__(s, path):
        d = open(path,'rb').read(); s.d = d
        assert d[:7]==b'BLENDER'; s.ps = 8 if d[7:8]==b'-' else 4; s.le = d[8:9]==b'v'; s.e = '<' if s.le else '>'
        s.ver = d[9:12]; o = 12; s.blocks=[]
        P = 'Q' if s.ps==8 else 'I'
        while o < len(d):
            code = d[o:o+4]; ln, = struct.unpack_from(s.e+'i', d, o+4); old, = struct.unpack_from(s.e+P, d, o+8)
            sdna, cnt = struct.unpack_from(s.e+'ii', d, o+8+s.ps); hs = 16+s.ps
            s.blocks.append(dict(code=code.rstrip(b'\0').decode('latin1'), len=ln, old=old, sdna=sdna, cnt=cnt, off=o+hs))
            o += hs+ln
            if code==b'ENDB': break
        s.byaddr = {b['old']:b for b in s.blocks if b['old']}
        dna = [b for b in s.blocks if b['code']=='DNA1'][0]; s.parse_dna(dna['off'])
    def parse_dna(s, o):
        d=s.d; assert d[o:o+4]==b'SDNA'; o+=4
        def strs(o, tag):
            assert d[o:o+4]==tag,(tag,d[o:o+4]); o+=4; n,=struct.unpack_from(s.e+'i',d,o); o+=4; out=[]
            for _ in range(n):
                e=d.index(b'\0',o); out.append(d[o:e].decode('latin1')); o=e+1
            return out,(o+3)&~3
        s.names,o=strs(o,b'NAME'); s.types,o=strs(o,b'TYPE')
        assert d[o:o+4]==b'TLEN'; o+=4; s.tlen=list(struct.unpack_from(s.e+'%dh'%len(s.types),d,o)); o+=2*len(s.types); o=(o+3)&~3
        assert d[o:o+4]==b'STRC'; o+=4; n,=struct.unpack_from(s.e+'i',d,o); o+=4; s.structs=[]
        for _ in range(n):
            t,nf=struct.unpack_from(s.e+'hh',d,o); o+=4; fs=[]
            for _ in range(nf):
                ft,fn=struct.unpack_from(s.e+'hh',d,o); o+=4; fs.append((s.types[ft],s.names[fn]))
            off=0; lay={}
            for ft,fn in fs:
                ptr='*' in fn; dims=[int(x) for x in re.findall(r'\[(\d+)\]',fn)]; m=1
                for x in dims: m*=x
                base=re.sub(r'\[.*','',fn).replace('*','').replace('(','').replace(')','')
                size=(s.ps if ptr else s.tlen[s.types.index(ft)])*m
                lay[base]=(off,ft,ptr,dims,size); off+=size
            s.structs.append((s.types[t],lay))
        s.sidx={nm:i for i,(nm,_) in enumerate(s.structs)}
    def lay(s,name): return s.structs[s.sidx[name]][1]
    def get(s, off, sname, field):
        o,ft,ptr,dims,size=s.lay(sname)[field]; o+=off
        if ptr:
            if dims: return list(struct.unpack_from(s.e+('Q' if s.ps==8 else 'I')*(size//s.ps),s.d,o))
            return struct.unpack_from(s.e+('Q' if s.ps==8 else 'I'),s.d,o)[0]
        fmt={'float':'f','int':'i','short':'h','char':'b','uchar':'B','double':'d','ushort':'H'}.get(ft)
        if fmt is None: return o
        n=size//struct.calcsize(fmt)
        if ft=='char' and dims: return s.d[o:o+size].split(b'\0')[0].decode('latin1')
        v=struct.unpack_from(s.e+fmt*n,s.d,o); return v if n>1 else v[0]
    def sname(s,b): return s.structs[b['sdna']][0]
    def ptrs(s, addr):
        b=s.byaddr.get(addr)
        if not b: return []
        return list(struct.unpack_from(s.e+('Q' if s.ps==8 else 'I')*(b['len']//s.ps),s.d,b['off']))

B=Blend(sys.argv[1]); out=sys.argv[2]; e=B.e
def idname(o): return B.d[o+B.lay('ID')['name'][0]:][:66].split(b'\0')[0].decode()
def lb(off, sname, field):
    o=B.lay(sname)[field][0]+off; first=B.get(o,'ListBase','first'); r=[]
    while first: b=B.byaddr[first]; r.append(b); first=B.get(b['off'],'Link','next')
    return r
def mat_info(addr):
    b=B.byaddr[addr]; o=b['off']; nt=B.get(o,'Material','nodetree'); col=(0.8,0.8,0.8); met=0; rough=0.5; emit=False
    for n in lb(B.byaddr[nt]['off'],'bNodeTree','nodes'):
        idn=B.get(n['off'],'bNode','idname')
        for sk in lb(n['off'],'bNode','inputs'):
            nm=B.get(sk['off'],'bNodeSocket','name'); dv=B.get(sk['off'],'bNodeSocket','default_value')
            if not dv or dv not in B.byaddr or B.byaddr[dv]['len']<16: continue
            v=struct.unpack_from('<4f',B.d,B.byaddr[dv]['off'])
            if idn=='ShaderNodeEmission' and nm=='Color': col=v[:3]; emit=True
            elif nm in ('Base Color','Color') and not emit: col=v[:3]
            elif nm=='Metallic': met=v[1]
            elif nm=='Roughness': rough=v[1]
    srgb=tuple(round(min(1,max(0,c))**(1/2.2),4) for c in col)
    return idname(o), srgb, met, rough, emit
parts=[]
for b in B.blocks:
    if b['code']!='OB' or B.get(b['off'],'Object','type')!=1: continue
    o=b['off']; name=idname(o); mat=B.get(o,'Object','obmat'); M=[mat[i*4:(i+1)*4] for i in range(4)]  # M[col][row]
    me=B.byaddr[B.get(o,'Object','data')]; mo=me['off']
    nv=B.get(mo,'Mesh','totvert'); npoly=B.get(mo,'Mesh','totpoly')
    vl=B.lay('MVert'); vs=B.tlen[B.types.index('MVert')]; vb=B.byaddr[B.get(mo,'Mesh','mvert')]['off']
    V=[];N=[]
    for i in range(nv):
        x,y,z=struct.unpack_from(e+'3f',B.d,vb+i*vs); nx,ny,nz=struct.unpack_from(e+'3h',B.d,vb+i*vs+12)
        w=[M[0][k]*x+M[1][k]*y+M[2][k]*z+M[3][k] for k in range(3)]
        n=[M[0][k]*nx+M[1][k]*ny+M[2][k]*nz for k in range(3)]; L=math.sqrt(sum(c*c for c in n)) or 1
        V.append(w); N.append([c/L for c in n])
    ps=B.tlen[B.types.index('MPoly')]; pb=B.byaddr[B.get(mo,'Mesh','mpoly')]['off']; pl=B.lay('MPoly')
    ls=B.tlen[B.types.index('MLoop')]; lbo=B.byaddr[B.get(mo,'Mesh','mloop')]['off']
    P=[]
    for i in range(npoly):
        q=pb+i*ps; st=B.get(q,'MPoly','loopstart'); tl=B.get(q,'MPoly','totloop'); mn=B.get(q,'MPoly','mat_nr'); fl=B.get(q,'MPoly','flag')
        idx=[struct.unpack_from(e+'I',B.d,lbo+(st+k)*ls)[0] for k in range(tl)]
        P.append((idx,mn,fl&1))
    mats=B.ptrs(B.get(mo,'Mesh','mat'))[:B.get(mo,'Mesh','totcol')]
    omats=B.ptrs(B.get(o,'Object','mat'))
    mats=[a if a in B.byaddr else (omats[i] if i<len(omats) else 0) for i,a in enumerate(mats)]
    mats=[mat_info(a) if a in B.byaddr else ('leeg',(0.5,0.5,0.5),0,0.5,False) for a in mats]
    parts.append(dict(name=name,V=V,N=N,P=P,mats=mats))
# assen: Blender Z op. Lengte-as = de horizontale as met de grootste maat van de carrosserie
body=[p for p in parts if p['name'].startswith('OBCube')][0]; wheels=[p for p in parts if p['name'].startswith('OBCircle')]
xs=[v[0] for v in body['V']]; ys=[v[1] for v in body['V']]; zs=[v[2] for v in body['V']]
long_y=(max(ys)-min(ys))>(max(xs)-min(xs))
# achterkant: waar de rode lampen zitten
red=[i for i,(n,c,m,r,em) in enumerate(body['mats']) if em and c[0]>0.8 and c[1]<0.3]
rv=[body['V'][k] for (idx,mn,f) in body['P'] if mn in red for k in idx]
cx=(max(xs)+min(xs))/2; cy=(max(ys)+min(ys))/2
la=1 if long_y else 0
rear=sum(v[la] for v in rv)/len(rv); sgn=-1 if rear>(cy if long_y else cx) else 1   # vooruit = weg van de achterlichten
length=(max(ys)-min(ys)) if long_y else (max(xs)-min(xs)); S=5.68/length
gz=min(min(v[2] for v in w['V']) for w in wheels)
def conv(v):  # naar auto-frame: z vooruit, y op, x rechts (linkshandig)
    fwd=(v[la]-(cy if long_y else cx))*sgn; side=v[1-la]-(cx if long_y else cy); up=v[2]-gz
    # rechts: Unity x = cross(up,fwd) richting. Blender rechtshandig: met fwd=+Y, rechts=+X; met fwd=+X, rechts=-Y
    if long_y: right=side*sgn
    else: right=-side*sgn
    return (right*S, up*S, fwd*S)
def convn(n):
    fwd=n[la]*sgn; side=n[1-la]; up=n[2]; right=side*sgn if long_y else -side*sgn; return (right,up,fwd)
lines=[]
def dump(p, center=None):
    V=[conv(v) for v in p['V']]; Nn=[convn(n) for n in p['N']]
    if center: V=[(a-center[0],b-center[1],c-center[2]) for a,b,c in V]
    lines.append(f"part {p['name']} {len(V)} {len(p['P'])} {len(p['mats'])}")
    for (nm,c,m,r,em) in p['mats']: lines.append(f"mat {nm} {c[0]} {c[1]} {c[2]} {m:.3f} {r:.3f} {1 if em else 0}")
    for (a,b,c),(x,y,z) in zip(V,Nn): lines.append(f"v {a:.4f} {b:.4f} {c:.4f} {x:.3f} {y:.3f} {z:.3f}")
    for idx,mn,sm in p['P']: lines.append("f %d %d %s"%(mn,sm," ".join(map(str,idx))))
dump(body)
centers=[]
for w in wheels:
    V=[conv(v) for v in w['V']]; c=tuple((max(v[k] for v in V)+min(v[k] for v in V))/2 for k in range(3)); centers.append(c)
    lines.append(f"wheel {c[0]:.4f} {c[1]:.4f} {c[2]:.4f}")
# één rechterwiel als vorm (buitenkant naar +x)
ri=max(range(len(wheels)),key=lambda i:centers[i][0]); dump(wheels[ri], centers[ri])
open(out,'w').write("\n".join(lines)+"\n")
bx=[conv(v) for v in body['V']]
print('long_y',long_y,'sgn',sgn,'S',round(S,3),'bbox',[ (round(min(v[k] for v in bx),2),round(max(v[k] for v in bx),2)) for k in range(3)])
print('wheels',[tuple(round(x,2) for x in c) for c in centers])
for m in body['mats']: print(m)
print(wheels[0]['mats'])
