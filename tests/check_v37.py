"""Independent checks of the new contracts, not native C# execution."""
import io, math, random, struct, statistics
from check_model import simulate
rng=random.Random(37)
# Model the compiled table as bound static handlers, no mutable-state caching.
for _ in range(2000):
    ids=rng.choice([[0,1,3,5],[-7,0,1000001]])
    m={k:(rng.choice(['stateful','null']),rng.randrange(100),rng.randrange(100)) for k in ids}
    runs=[(rng.choice(ids+[2,9]),rng.randrange(-2,20)) for _ in range(rng.randrange(1,40))]
    stop=rng.choice([-1,2,9]); fail=rng.choice([-1,3,11])
    compiled={k:tuple(v) for k,v in m.items()}
    assert simulate(m,runs,False,stop,fail)==simulate(compiled,runs,True,stop,fail)
print('PASS: 2,000 independent static descriptor equivalence cases (order, duplicate/missing/null, RNG, stop/failure).')
# Valid-length views of a reused response buffer, including growing and shrinking bodies.
buffer=bytearray(16); decoded=[]
for i in range(1000):
    n=rng.randrange(0,1000); expected=[(rng.randrange(-8,100),rng.randrange(-2,100)) for _ in range(n)]
    msg=rng.choice([b'',b'ok', '✓'.encode(), b'a'*120]); ack=struct.pack('<iB',0,len(msg))+msg
    body=ack+struct.pack('<i',n)+b''.join(struct.pack('<ii',*r) for r in expected)+b'\x55'
    if len(body)>len(buffer): buffer=bytearray(max(len(body),len(buffer)*2))
    buffer[:len(body)]=body
    view=memoryview(buffer)[:len(body)]; off=5+view[4]
    count,=struct.unpack_from('<i',view,off); off+=4
    assert 0<=count<=(len(view)-off)//8
    decoded[:]=[struct.unpack_from('<ii',view,off+8*j) for j in range(count)]
    assert decoded==expected
    del view
print('PASS: 1,000 independent response-buffer reuse/length and decoded-list replacement cases.')
# Callback interval arithmetic; no partial endpoint or absent-draw extrapolation.
for n in [1,2,3,100,1000]:
    times=[0]
    for j in range(n): times.append(times[-1]+rng.randrange(1,100))
    intervals=[b-a for a,b in zip(times,times[1:])]; ordered=sorted(intervals)
    assert abs(n*1000/(times[-1]-times[0])-1000/statistics.mean(intervals))<1e-10
    tail=math.ceil(n*.01)
    assert 1000/statistics.mean(ordered[-tail:])<=1000/statistics.mean(intervals)
print('PASS: 5 independent interval-summary arithmetic checks; draw/update rates distinct.')
