import json, math, sys
# Chaos level v5. Rule: a beam reaches only as far as the first block solid in your colour.
# Laser walls hang from the ceiling over an uneven floor; every beat reads the floor, the stoppers and the shields
# differently, so an obstacle on the floor is not by itself a reason to jump.
W,H=160,22
R,B,G=1,2,4
TOL=6                                   # takeoff timing slack, px
# A full hop measured in the controller: (dx, dy) of the player's top-left per frame after jump is pressed
ARC=[(2,-2),(4,-4),(6,-5),(8,-7),(10,-9),(12,-11),(13,-12),(15,-14),(17,-16),(18,-18),(20,-19),(21,-21),(23,-23),(24,-24),
     (26,-25),(27,-25),(29,-26),(30,-26),(32,-27),(33,-27),(35,-27),(36,-27),(38,-26),(39,-25),(41,-24),(42,-23),(44,-21),
     (45,-20),(47,-18),(48,-16),(50,-13),(51,-10),(53,-8),(54,-5),(56,-2),(57,0)]
col=lambda p:1<<p
grid=[['.']*W for _ in range(H)]
ground=[21]*W                           # top floor row per column; None is a pit
def floor(a,b,g):
    for c in range(a,b): ground[c]=g
def cell(c,r,ch): grid[r][c]=ch
objs=[]
def laser(c,r,d,m): objs.append({"type":"laser","cell":[c,r],"dir":d,"mask":m})

# --- layout --------------------------------------------------------------------------------------------------------
SPAWN=(2,19)
floor(33,58,20)                          # a raised shelf: a step up costs a jump, the walk off keeps the colour
for c in range(86,98): ground[c]=None    # the pit the moving platform crosses
floor(128,141,20)
WALLS=[15,26,56,82,104,121,143]          # left column of each two-column white wall
# Floor obstacles: white spikes must be jumped; a coloured block matters only in its own colour.
cell(9,20,'G')                           # white spike
cell(21,20,'A')                          # red spike: you are blue here, so walk through it
cell(110,20,'B')                         # blue spike: you are blue, jump it
cell(116,20,'A')                         # red spike: you are green, walk through it
for r in (18,19): cell(50,r,'4')         # green block: you arrive green, but the red beam waits above it
cell(134,19,'D')                         # cyan spike on the shelf: blue or green kills, so stay red
cell(150,20,'G')                         # white spike
# Ceiling lasers with shields: under a shield you must be its colour.
SHIELDS=[(64,R),(77,G),(126,R)]
# Coloured rain over stretches you walk in another colour: one extra jump there is fatal.
RAIN=[(23,G),(102,R),(137,B)]
# Intended route: (takeoff px, colour after the hop, 'rule' = both other colours must be fatal on this arc)
HOPS=[(40,B,1),(232,G,1),(328,R,1),(528,B,0),(588,G,0),(672,R,0),(736,B,0),(848,G,1),(990,R,0),(1168,B,1)]
# Naive jumps the level must punish: every takeoff in [a, b] px, with the colour after
TRAPS=[(138,200,G),(360,376,R),(890,930,R),(1060,1090,B)]
PLATFORM=dict(cells=[(91,21),(87,21)],mask=R)   # stop 0 catches the hop over the pit; it alternates with every jump

# --- build ---------------------------------------------------------------------------------------------------------
for c in range(W):
    g=ground[c]
    if g is not None:
        for r in range(g,H): grid[r][c]='7'
for w in WALLS:
    bottom=min(ground[c] for c in (w,w+1) if ground[c] is not None)-3
    for r in range(0,bottom+1): grid[r][w]=grid[r][w+1]='7'
for c,m in SHIELDS:
    laser(c,0,'down',7); cell(c,11,str(m))
for c,m in RAIN: laser(c,0,'down',m)

MASK={**{str(i):i for i in range(1,8)},**{ch:i+1 for i,ch in enumerate('ABCDEFG')}}
def solid(c,r,p): return 0<=c<W and 0<=r<H and grid[r][c]!='.' and MASK[grid[r][c]]&col(p)
def beams(p):
    """Beam rectangles (x0,y0,x1,y1) of every laser lethal in phase p."""
    out=[]
    for o in objs:
        if o['type']!='laser' or not o['mask']&col(p): continue
        c,r=o['cell']; dc,dr={'left':(-1,0),'right':(1,0),'up':(0,-1),'down':(0,1)}[o['dir']]
        k=1
        while 0<=c+dc*k<W and 0<=r+dr*k<H and not solid(c+dc*k,r+dr*k,p): k+=1
        if k==1: continue
        a,b=(c+dc,c+dc*(k-1)) if dc else (r+dr,r+dr*(k-1))
        lo,hi=min(a,b),max(a,b)
        out.append((8*lo,8*r+2,8*hi+8,8*r+6) if dc else (8*c+2,8*lo,8*c+6,8*hi+8))
    return out
def gy(x): return 8*(ground[int(x+4)//8] or 21)   # over the pit, the platform's top
def body(x,y,dx,dy): return (x+dx,y+dy,x+dx+8,y+dy+11)
def hit(box,bs): return any(box[0]<b[2] and box[2]>b[0] and box[1]<b[3] and box[3]>b[1] for b in bs)
def spikes(p): return [(8*c-.5,8*r-.5,8*c+8.5,8*r+8.5) for r in range(H) for c in range(W) if grid[r][c] in 'ABCDEFG' and MASK[grid[r][c]]&col(p)]
def arc_hit(x,p,slack=TOL):
    y=gy(x)-11; bs=beams(p)+spikes(p)
    return any(hit(body(x+t,y,dx,dy),bs) for t in range(-slack,slack+1,2) for dx,dy in ARC)
def arc_clear_everywhere(x,p): return not any(hit(body(x+t,gy(x)-11,dx,dy),beams(p)) for t in range(-TOL,TOL+1) for dx,dy in ARC)

# Wall faces fire three segments into each zone; a stopper of colour m sits just past the arc of the zone's hop in m.
def span(x,r):   # x range the hop's body covers in row r (a spiked stopper reaches half a pixel past its cell), with slack
    y=gy(x)-11; xs=[x+dx for dx,dy in ARC if y+dy<8*r+9 and y+dy+11>8*r-1]
    return (min(xs)-TOL-2,max(xs)+8+TOL+2) if xs else None
PERMS=[(R,B,G),(G,R,B),(B,G,R),(R,G,B),(B,R,G),(G,B,R),(R,B,G),(G,R,B)]
bounds=[-3]+WALLS+[W]
for z in range(len(bounds)-1):
    lw,rw=bounds[z],bounds[z+1]
    zh=[(x,m) for x,m,_ in HOPS if 8*(lw+2)<=x<8*rw]
    faces=[]
    if lw>=0: faces.append(('right',lw+2,PERMS[z]))
    if rw<W: faces.append(('left',rw-1,PERMS[z][::-1]))
    g=min(ground[c] for c in range(max(lw,0),min(rw+2,W)) if ground[c] is not None)   # beams pass over the highest floor
    for d,ec,pm in faces:
        for r,m in zip((g-5,g-4,g-3),pm):
            if grid[r][ec]!='.': continue
            sp=[span(x,r) for x,mm in zh if mm==m]; sp=[s for s in sp if s]
            if d=='right':
                if sp:
                    sc=min(s[0] for s in sp)//8-1
                    if sc<=ec: continue      # no room for a stopper: leave this segment out
                    cell(sc,r,'ABCDEFG'[m-1])
                laser(ec,r,'right',m)
            else:
                if sp:
                    sc=math.ceil(max(s[1] for s in sp)/8)
                    if sc>=ec: continue
                    cell(sc,r,'ABCDEFG'[m-1])
                laser(ec,r,'left',m)
px,pr=zip(*PLATFORM['cells'])
objs.append({"type":"platform","orientation":"horizontal","length":3,"pivots":[list(c) for c in PLATFORM['cells']],"reverse":False,"mask":PLATFORM['mask'],"spiked":False,"spikes":[]})
objs.insert(0,{"type":"spawn","cell":list(SPAWN)})

# --- checks: fail on a broken jump ----------------------------------------------------------------------------------
err=[]
phase=0
for i,(x,m,rule) in enumerate(HOPS):
    p=int(math.log2(m))
    if p!=(i+1)%3: err.append(f"hop {x}: colour {m} breaks the red-blue-green order")
    if arc_hit(x,p): err.append(f"hop {x}: its own colour is hit")
    if rule:
        for q in range(3):
            if q!=p and not arc_hit(x,q,0): err.append(f"hop {x}: colour {col(q)} is not fatal")
for a,b,m in TRAPS:
    for x in range(a,b+1):
        if not arc_hit(x,int(math.log2(m)),0): err.append(f"trap {x}: colour {m} survives"); break
# walking between hops: the body at ground level must stay out of the beams of the current colour
stops=[16]+[x for x,_,_ in HOPS]+[8*W-12]
for i in range(len(stops)-1):
    p=i%3; a=stops[i]+(57 if i else 0); b=stops[i+1]
    for x in range(int(a),int(b)):
        if ground[(x+4)//8] is None: continue
        bs=beams(p)+spikes(p)
        if hit((x,gy(x)-11,x+8,gy(x)),bs): err.append(f"walk at {x} in colour {col(p)} is hit by {[b for b in bs if hit((x,gy(x)-11,x+8,gy(x)),[b])]}"); break
if err:
    for r in grid[8:]: print(''.join(r))
    sys.exit('\n'.join(err))
json.dump({"format":"verto-map","version":3,"generator":"Verto 0.3","name":"Chaos","rooms":[{"at":[0,0],"size":[4,1],"terrain":[''.join(r) for r in grid],"objects":objs}]},open(sys.argv[1] if len(sys.argv)>1 else 'chaos.json','w'))
print(len(objs),'objects; route',','.join(str(x) for x,_,_ in HOPS))
for r in grid: print(''.join(r))
