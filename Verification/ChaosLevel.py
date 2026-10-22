import json, math, sys
# Chaos level v4: segmented laser walls. Rule: a beam reaches only as far as the first block solid in its colour.
W,H=160,22
FP={16:(24,54),17:(17,61),18:(10,66)}   # px a hop's body spans in each beam band, from the x where jump is pressed
CELL={16:(21,57),17:(15,63),18:(6,67)}   # same for whole cells (spiked stoppers reach .5px past their cell)
TOL=8                                   # takeoff timing slack, px
R,B,G=1,2,4
# (takeoff px, spike obstacle under the hop, wall after this hop)
# (spike obstacle under the hop, wall after this hop); takeoffs follow from the gaps
PLAN=[(1,1),(1,0),(1,1),(1,0),(0,1),(1,0),(1,1),(0,0),(1,1),(1,0),(1,0),(1,1)]
ROUTE=[]; x=40
for o,w in PLAN: ROUTE.append((x,o,w)); x+=112 if w else 80
SPAWN=(2,19)
# row colour order on each wall face, rotated per wall for variety
PERMS=[(R,B,G),(G,R,B),(B,G,R),(R,G,B),(B,R,G),(G,B,R)]
grid=[['.']*W for _ in range(H)]; grid[21]=['7']*W
objs=[{"type":"spawn","cell":list(SPAWN)}]
hops=[(x,o,(i+1)%3) for i,(x,o,_) in enumerate(ROUTE)]  # phase index after hop: 0 R,1 B,2 G
col=lambda p:1<<p
walls=[]
for i,(x,o,w) in enumerate(ROUTE):
    if o:  # 1-cell white spike block centred under the hop
        grid[20][(x+33)//8]='G'
    if w:
        end=x+TOL+67; nxt=ROUTE[i+1][0]-TOL+10 if i+1<len(ROUTE) else 8*(W-1)
        c=math.ceil(end/8)+1; walls.append(c)   # wall cols c,c+1; emitters c-1 (left) and c+2 (right)
        assert 8*(c-1)>=end and 8*(c+3)<=nxt, (x,c)
        for r in range(0,19): grid[r][c]=grid[r][c+1]='7'
bounds=[-3]+walls+[W]   # zone i spans between wall bounds[i] and bounds[i+1]
beams=[]  # (row, colour, from px, to px)
for z in range(len(bounds)-1):
    lw,rw=bounds[z],bounds[z+1]
    zh=[h for h in hops if 8*(lw+2)<h[0]<8*rw]
    perm=PERMS[z%len(PERMS)]
    faces=[]
    if lw>=0: faces.append(('right',lw+2,perm))
    if rw<W: faces.append(('left',rw-1,perm[::-1]))
    for d,ec,pm in faces:
        for r,m in zip((16,17,18),pm):
            same=[h for h in zh if col(h[2])==m]
            lo,hi=CELL[r]
            if d=='right':
                stop=8*rw
                if same:
                    h=min(same); sc=(h[0]-TOL+lo)//8-1
                    if sc<=ec: continue      # no room for a stopper: leave this segment out
                    grid[r][sc]='ABCDEFG'[m-1] if r!=16 else str(m); stop=8*sc
                beams.append((r,m,8*ec+8,stop))
            else:
                stop=8*(lw+2)
                if same:
                    h=max(same); sc=math.ceil((h[0]+TOL+hi)/8)
                    if sc>=ec: continue
                    grid[r][sc]='ABCDEFG'[m-1] if r!=16 else str(m); stop=8*sc+8
                beams.append((r,m,stop,8*ec))
            objs.append({"type":"laser","cell":[ec,r],"dir":d,"mask":m})
# every hop: own colour clear, both other colours hit
for x,o,p in hops:
    for q in range(3):
        hit=any(m==col(q) and a<x+TOL+FP[r][1] and b>x-TOL+FP[r][0] for r,m,a,b in beams)
        tag='OK' if hit!=(q==p) else 'BAD'
        if tag=='BAD': print('hop',x,'phase',p,'colour',q,'hit' if hit else 'unguarded')
json.dump({"format":"verto-map","version":3,"generator":"Verto 0.3","name":"Chaos","rooms":[{"at":[0,0],"size":[4,1],"terrain":[''.join(r) for r in grid],"objects":objs}]},open(sys.argv[1] if len(sys.argv)>1 else 'chaos.json','w'))
print(len(objs),'objects; walls',walls)
for r in grid[12:]: print(''.join(r))
