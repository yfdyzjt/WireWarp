"""Independent model of v3.9 footprint execution, not native C# or Terraria."""
import random
from copy import deepcopy
rng=random.Random(39)
def div(a,b): return (abs(a)//b)*(1 if a>=0 else -1)
def rem(a,b): return a-div(a,b)*b
def short(v): return (v+32768)%65536-32768
fixed={2:((0,0),(0,1),(1,0),(1,1),(2,0),(2,1)),3:((0,0),(0,1),(0,2),(1,0),(1,1),(1,2),(2,0),(2,1),(2,2))}
vals=[-32768,-32760,-109,-108,-54,-36,-18,-1,0,17,18,35,36,53,54,107,108,161,162,32760,32767]
def run(world, events, mode, legacy, fast):
    state=deepcopy(world); trace=[]; activation=0; rejected=0
    def get(x,y):
        if not 0<=x<12 or not 0<=y<12: raise IndexError
        return state[x][y]
    for typ,i,j in events:
        try:
            fx,fy,t,active=get(i,j)
            if typ=='campfire':
                if t not in (215,999) or not active: continue
                activation+=1
                if not legacy or t!=215:
                    trace.append(('native-campfire',i,j,fy));continue
                h=2; x=i-div(rem(fx,54),18);y=j-div(rem(fy,36),18)
                if x<0 or y<0 or x>9 or y>10:
                    rejected+=1
                    if rejected<=8: trace.append(('rejected',x,y))
                    continue
                delta=-36 if get(x,y)[1]>=36 else 36
            elif typ=='chimney':
                h=3;x=i-div(rem(fx,54),18);y=j-div(rem(fy,54),18)
                delta=-108 if get(x,y)[1]>=108 else 54
            else:
                trace.append((typ,i,j,fy));continue
            offsets=fixed[h] if fast else ((a,b) for a in range(3) for b in range(h))
            for a,b in offsets:
                cell=get(x+a,y+b);cell[1]=short(cell[1]+delta)
                trace.append(('write',x+a,y+b,cell[1]))
            if h==3: trace.append(('net',x+1,y+1,3))
            elif mode==2: trace.append(('net',x,y,3,2))
        except IndexError: trace.append(('exception',))
    return state,trace,activation,rejected
for seed in range(3000):
    world=[[[rng.choice(vals),rng.choice(vals),rng.choice([215,999,406,0,546]),rng.randrange(4)!=0] for y in range(12)] for x in range(12)]
    world[5][5][2:]=[215,True];world[6][6][2:]=[999,True];world[0][0]=[36,18,215,True]
    events=[('campfire',5,5),('campfire',6,6),('campfire',0,0),('chimney',5,5),('chimney',5,5)]
    events += [(rng.choice(['chimney','campfire','monolith','lamp','lantern','grate']),rng.randrange(12),rng.randrange(12)) for _ in range(35)]
    assert run(world,events,seed%3,seed%2==0,False)==run(world,events,seed%3,seed%2==0,True),seed
print('PASS: 3,000 independent mixed-footprint sequences / 120,000 activations per path; signed frame math, short overflow, overlap, bounds, partial writes, native routing, net notifications and final state.')
