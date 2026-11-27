"""Independent wire-contract tests. Does not execute C# or Zig."""
import itertools, random, struct
rng = random.Random(38)
def runs(ids):
    return [(port, len(list(group))) for port, group in itertools.groupby(ids)]
def encode(ids, compact=True):
    if compact and all(0 <= x <= 65535 for x in ids) and all(a != b for a,b in zip(ids, ids[1:])):
        return struct.pack('<i', ~len(ids)) + b''.join(struct.pack('<H', x) for x in ids)
    r = runs(ids)
    return struct.pack('<i', len(r)) + b''.join(struct.pack('<ii', *x) for x in r)
def decode(data, capable=True):
    if len(data)<4: raise ValueError('truncated')
    n,=struct.unpack_from('<i',data)
    if n<0:
        if not capable or len(data)!=4+2*(~n): raise ValueError('compact length')
        return [(p,1) for (p,) in struct.iter_unpack('<H',data[4:])]
    if len(data)!=4+8*n: raise ValueError('RLE length')
    return list(struct.iter_unpack('<ii',data[4:]))
assert encode([0,65535,17,0]).hex()=='fbffffff0000ffff11000000'
assert encode([]).hex()=='ffffffff'
assert encode([7,7,9,7]) == encode([7,7,9,7], False)
assert encode([65536]) == encode([65536], False)
assert encode([-1]) == encode([-1], False)
counts={'compact':0,'fallback':0}
for i in range(5000):
    n=rng.randrange(300)
    if i%3==0: ids=[rng.randrange(65536) for _ in range(n)]
    elif i%3==1: ids=[rng.choice([0,17,65535,-1,65536,2147483647,-2147483648]) for _ in range(n)]
    else: ids=[j%1317 for j in range(n)]
    expected=runs(ids)
    packed=encode(ids)
    assert decode(packed)==decode(encode(ids,False),False)==expected
    assert [p for p,c in decode(packed) for _ in range(c)]==ids
    compact=packed[3]&128!=0
    counts['compact' if compact else 'fallback']+=1
    if compact:
        assert len(packed)==4+2*len(ids)
    else: assert packed==encode(ids,False)
    for bad in [packed[:-1],packed+b'\x00']:
        try: decode(bad); raise AssertionError('malformed accepted')
        except ValueError: pass
print('PASS: 5,000 randomized streams, exact run boundaries and activation order, '+str(counts))
print('PASS: 10,000 malformed-length checks; signed/large ID and duplicate fallback; golden wire bytes.')
# Full-size borrowed views with shrinking/growing responses. Simulate sync consumption before next send.
store=bytearray(4)
for n in [0,263082,1,150324,0,65536,17]:
    ids=[i%1317 for i in range(n)]
    packed=encode(ids)
    if len(store)<len(packed): store=bytearray(len(packed))
    store[:len(packed)]=packed
    assert decode(memoryview(store)[:len(packed)])==runs(ids)
print('PASS: 7 buffer-growth/shrink cases up to 263,082 records, including zero-length record lists.')
