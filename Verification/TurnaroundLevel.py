import json, sys
# Turnaround level. Rule: a coloured beam kills only in its own colour, and every jump moves you one colour on.
# A two-screen shaft climbed by turning back on every jump, crossed edge to edge by a sheet of beams on every row.
# Each row's beams take the colours the route is never in on that row, so the beams over the next ledge are always
# the ones your jump just switched off. The ledge overhead is never your colour, so the climb never bonks on it.
W,H=40,44
R,B,G=1,2,4
COLOURS=[R,B,G]                          # phase after n jumps is COLOURS[n%3]
SPLIT=19                                 # A ledges end left of this column, B ledges start on it
FLOOR=43                                 # white floor row; the floor ends where the first jump takes off
FLOOR_END=17
LEDGES=12                                # ledge k sits 3 rows above ledge k-1; odd k on the B side
EXIT=FLOOR-3*(LEDGES+1)                  # a white shelf out to the right edge
def width(k): return 4 if k<=4 else 3 if k<=8 else 2   # A width; B is one wider, as the B landing sits further out

grid=[['.']*W for _ in range(H)]
for c in range(FLOOR_END): grid[FLOOR][c]='7'
for k in range(1,LEDGES+1):
    r=FLOOR-3*k; w=width(k)
    cols=range(SPLIT,SPLIT+w+1) if k%2 else range(SPLIT-w,SPLIT)
    for c in cols: grid[r][c]=str(COLOURS[k%3])
for c in range(SPLIT,W): grid[EXIT][c]='7'

# Beam colours per row. Ledge k (the floor is ledge 0, the shelf ledge LEDGES+1) is reached in COLOURS[k], and you
# leave it in the next colour. Its own row only meets you in its colour; the two rows above it meet you in its colour
# and the next one, so they take the colour before it. Over the shelf only the shelf colour is excluded.
objs=[{"type":"spawn","cell":[2,FLOOR-2]}]
def laser(c,r,d,m): objs.append({"type":"laser","cell":[c,r],"dir":d,"mask":m})
for k in range(LEDGES+2):
    s=FLOOR-3*k; here=k%3
    for r in (s-1,s-2):
        free=[COLOURS[(here+1)%3],COLOURS[(here+2)%3]] if k>LEDGES else [COLOURS[(here+2)%3]]
        left,right=free[0],free[-1]
        if r>=0: laser(0,r,'right',left); laser(W-1,r,'left',right)
    if 0<k<=LEDGES:                      # the ledge row: the band colour below on the ledge's side, the one above opposite
        below,above=COLOURS[(here+1)%3],COLOURS[(here+2)%3]
        left,right=(above,below) if k%2 else (below,above)
        laser(0,s,'right',left); laser(W-1,s,'left',right)
    elif k>LEDGES: laser(0,s,'right',COLOURS[(here+1)%3])   # the shelf fills the right side of its row

room={"at":[0,0],"size":[1,2],"terrain":[''.join(r) for r in grid],"objects":objs}
out=sys.argv[1] if len(sys.argv)>1 else 'Assets/Phase/Imported/turnaround.json'
json.dump({"format":"verto-map","version":3,"generator":"Verto 0.3","name":"Turnaround","rooms":[room]},open(out,'w'))
