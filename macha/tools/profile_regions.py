#!/usr/bin/env python3
"""Deterministic real-graph reference vs wave-local mask model (untimed)."""
import argparse, collections, hashlib, json, random
from pathlib import Path
from aot_generate import parse, topology


def compile_regions(targets, faulty, normal, cap=None, allow=()):
    programs = {}
    for wi, ts in enumerate(targets):
        first = next((i for i, (k, _) in enumerate(ts) if k == 2), len(ts))
        end = first
        masks = {}
        while end < len(ts) and ts[end][0] == 2:
            gi = ts[end][1]
            if not faulty[gi] or len(normal[gi]) != 1:
                break
            masks[gi // 64] = masks.get(gi // 64, 0) | (1 << (gi % 64))
            end += 1
        if end < len(ts) and ts[end][0] == 2:
            continue
        if end-first >= 8 and len(masks) < end-first and (cap is None or end-first <= cap or wi in allow):
            programs[wi] = (first, end, tuple(masks.items()))
    return programs


class Model:
    def __init__(self, groups, targets, owners, normal, faulty, programs, lazy=False, direct=False, fast=False):
        self.targets, self.owners, self.normal, self.faulty = targets, owners, normal, faulty
        self.programs = programs
        self.direct = direct
        self.controls = {}
        self.outputs = {}
        if programs is not None:
            for wi, ts in enumerate(targets):
                cf = next((i for i,(k,j) in enumerate(ts) if k==1),len(ts))
                ce = cf
                while ce<len(ts) and ts[ce][0]==1: ce+=1
                if ce-cf>=8: self.controls[wi]=(cf,ce)
                of=len(ts)
                while of>0 and ts[of-1][0]==3: of-=1
                if len(ts)-of>=2: self.outputs[wi]=(of,[j for k,j in ts[of:]])
        self.fast_plans = {}
        if fast:
            for wi, ts in enumerate(targets):
                if self.controls.get(wi) == (0,len(ts)):
                    self.fast_plans[wi] = ('control',len(ts))
                elif wi in self.outputs and self.outputs[wi][0] == 0:
                    self.fast_plans[wi] = ('outputs',self.outputs[wi][1])
                elif len(ts)==1 and ts[0][0]==3:
                    self.fast_plans[wi] = ('output1',[ts[0][1]])
                elif 0<len(ts)<=7 and all(k<3 and (k!=2 or faulty[j] and len(normal[j])==1) for k,j in ts) and all(a[0]<=b[0] for a,b in zip(ts,ts[1:])):
                    self.fast_plans[wi] = ('short',tuple((j,owners[j]) for k,j in ts if k==0),tuple(j for k,j in ts if k==1),tuple(j for k,j in ts if k==2))
        self.lazy_groups = {}
        self.lazy_gate = {}
        if lazy:
            memberships = collections.Counter(j for wi,(cf,ce) in self.controls.items() for k,j in targets[wi][cf:ce])
            for wi,(cf,ce) in self.controls.items():
                members = [j for k,j in targets[wi][cf:ce]]
                if all(memberships[j]==1 and faulty[owners[j]] and normal[owners[j]]==[j] for j in members):
                    self.lazy_groups[wi] = members
                    for j in members: self.lazy_gate[owners[j]]=wi
        wb = sum(map(len, groups[:4]))
        self.fan = [tuple(sorted(w-wb for w in n.edges)) for n in groups[3]]
        self.inputs = [tuple(sorted(w-wb for w in n.edges)) for n in groups[0]]
        self.lamps = bytearray(n.kind == 1 for n in groups[2])
        self.kinds = [n.kind for n in groups[3]]
        self.rng = random.Random(0xC0FFEE)
        for gi, ls in enumerate(normal):
            if not faulty[gi] and self.kinds[gi] in (3,4):
                for li in ls: self.lamps[li] ^= 1
        self.state = bytearray(0 if faulty[gi] else self.normal_state(gi) for gi in range(len(normal)))

    def normal_state(self,gi):
        ls = self.normal[gi]
        if len(ls)==1: return self.lamps[ls[0]]
        on = sum(self.lamps[li] for li in ls)
        return on==1 if self.kinds[gi] in (5,6) else on==len(ls)

    def try_packed_gate(self,gi,lazy_parity):
        return None

    def append_continuation(self, queue, gi):
        queue.extend(self.fan[gi])

    def expand_continuations(self, queue):
        return queue

    def event(self, port):
        pending = self.inputs[port]; fired = set(); visited = set(); out = []; stats = collections.Counter(); wave_sizes=[]; visits=collections.Counter(); self.wave_order=[]; self.wire_wave_order=[]; lazy_parity={}
        while pending:
            dirty = []; flags = set(); words = {}; control_parity = {}; hits=collections.Counter(); stats['waves'] += 1
            def push(gi):
                if self.programs is None:
                    if gi in flags: return
                    flags.add(gi)
                else:
                    word, bit = gi//64, 1 << (gi%64)
                    old = words.get(word,0)
                    if old & bit: return
                    words[word] = old | bit
                dirty.append(gi)
            def apply(ts):
                for k,j in ts:
                    if k == 0: self.lamps[j] ^= 1; push(self.owners[j])
                    elif k == 1: self.lamps[j] ^= 1
                    elif k == 2:
                        if len(self.normal[j])>1: hits[j]+=1
                        push(j)
                    else: out.append(j)
            self.wire_wave_order.append(tuple(pending))
            for wi in pending:
                visits[wi] += 1; ts = self.targets[wi]; stats['wire_visits'] += 1; stats['targets'] += len(ts)
                cursor=0
                if wi in self.fast_plans:
                    plan=self.fast_plans[wi]; stats['fast_wire_visits']+=1
                    if plan[0]=='control':
                        control_parity[wi]=not control_parity.get(wi,False)
                        stats['control_visits']+=1; stats['control_toggles_original']+=plan[1]
                    elif plan[0]=='outputs':
                        out.extend(plan[1]); stats['output_batches']+=1; stats['batched_outputs']+=len(plan[1])
                    elif plan[0]=='output1': out.extend(plan[1])
                    else:
                        for li,gi in plan[1]: self.lamps[li]^=1; push(gi)
                        for li in plan[2]: self.lamps[li]^=1
                        for gi in plan[3]: push(gi)
                        stats['fast_short_ops']+=len(ts)
                    continue
                if self.direct and wi in self.lazy_groups and self.controls[wi]==(0,len(ts)):
                    lazy_parity[wi]=not lazy_parity.get(wi,False)
                    stats['direct_lazy_visits']+=1
                    stats['control_visits']+=1; stats['control_toggles_original']+=len(ts)
                    continue
                if wi in self.controls:
                    cf,ce=self.controls[wi]; apply(ts[:cf]); cursor=ce
                    control_parity[wi]=not control_parity.get(wi,False)
                    stats['control_visits']+=1; stats['control_toggles_original']+=ce-cf
                if self.programs is not None and wi in self.programs:
                    first,end,blocks = self.programs[wi]
                    stats['region_visits'] += 1; stats['region_triggers'] += end-first; stats['mask_blocks'] += len(blocks)
                    apply(ts[cursor:first]); cursor=end
                    for word,mask in blocks:
                        old = words.get(word,0); fresh = mask & ~old; words[word] = old | mask
                        while fresh:
                            bit = fresh & -fresh; dirty.append(word*64 + bit.bit_length()-1); fresh ^= bit
                if wi in self.outputs:
                    of,outputs=self.outputs[wi]; apply(ts[cursor:of]); out.extend(outputs)
                    stats['output_batches']+=1; stats['batched_outputs']+=len(outputs)
                else: apply(ts[cursor:])
            for wi,odd in control_parity.items():
                if odd:
                    if wi in self.lazy_groups:
                        lazy_parity[wi]=not lazy_parity.get(wi,False)
                        stats['lazy_wave_toggles']+=1
                    else:
                        cf,ce=self.controls[wi]; apply(self.targets[wi][cf:ce]); stats['control_toggles_executed']+=ce-cf
            wave_sizes.append(len(dirty)); self.wave_order.append(tuple(dirty)); next_wires=[]
            for gi in dirty:
                stats['checks'] += 1; visited.add(gi)
                packed_result = self.try_packed_gate(gi,lazy_parity)
                if packed_result is not None:
                    emit=packed_result
                    if self.faulty[gi]: stats['fault_checks'] += 1
                elif self.faulty[gi]:
                    stats['fault_checks'] += 1
                    ls=self.normal[gi]
                    if len(ls)==1:
                        if self.direct and gi in fired:
                            stats['skipped_fault_checks']+=1
                            continue
                        emit=self.lamps[ls[0]] ^ lazy_parity.get(self.lazy_gate.get(gi),False)
                    else:
                        on=sum(self.lamps[li] for li in ls)
                        emit=any(self.rng.randrange(len(ls))<on for _ in range(hits[gi]))
                else:
                    cur=self.normal_state(gi); emit=cur != self.state[gi]; self.state[gi]=cur
                if emit and gi not in fired: fired.add(gi); self.append_continuation(next_wires,gi)
                elif emit: stats['suppressed_fires'] += 1
            pending = self.expand_continuations(next_wires)
        for wi,odd in lazy_parity.items():
            if odd:
                for j in self.lazy_groups[wi]: self.lamps[j] ^= 1
                stats['control_toggles_executed']+=len(self.lazy_groups[wi]); stats['lazy_commits']+=1
        stats['distinct_gates'] = len(visited); stats['outputs'] = len(out); stats['max_wave_checks'] = max(wave_sizes,default=0)
        self.hot = [(wi,n,len(self.targets[wi]),dict(collections.Counter(k for k,j in self.targets[wi])),wi in (self.programs or {})) for wi,n in sorted(visits.items(),key=lambda kv:kv[1]*len(self.targets[kv[0]]),reverse=True)[:12]]
        self.all_visits = visits
        return out, stats, visited


def main():
    ap=argparse.ArgumentParser(); ap.add_argument('wwir',type=Path); ap.add_argument('--lazy',action='store_true'); ap.add_argument('--pulses',type=int,default=2); ap.add_argument('--ports',type=int,nargs='+',default=[8,9,2,3,6]); a=ap.parse_args()
    data=a.wwir.read_bytes(); groups=parse(data); targets,owners,normal,faulty=topology(groups); programs=compile_regions(targets,faulty,normal)
    print(json.dumps(dict(sha256=hashlib.sha256(data).hexdigest(), nodes=list(map(len,groups)), gate_classes={str(k):v for k,v in collections.Counter((bool(f),len(ls)) for f,ls in zip(faulty,normal)).items()}, target_counts=dict(collections.Counter(k for ts in targets for k,j in ts)), regions=len(programs), region_triggers=sum(e-s for s,e,b in programs.values()), mask_blocks=sum(len(b) for s,e,b in programs.values()))),flush=True)
    seen={}
    for p in a.ports:
        ref=Model(groups,targets,owners,normal,faulty,None); opt=Model(groups,targets,owners,normal,faulty,programs,a.lazy); seen[p]=set()
        if a.lazy: print(json.dumps(dict(lazy_groups=len(opt.lazy_groups),lazy_lamps=sum(map(len,opt.lazy_groups.values())))),flush=True)
        for i in range(a.pulses):
            x,r,v=ref.event(p); y,s,w=opt.event(p)
            assert x==y and ref.lamps==opt.lamps and ref.state==opt.state and r['checks']==s['checks'] and v==w and ref.wave_order==opt.wave_order and ref.rng.getstate()==opt.rng.getstate()
            seen[p].update(v)
            print(json.dumps(dict(port=p,event=i,parity='PASS',**s,hot=opt.hot)),flush=True)
    print(json.dumps({'overlap': {f'{p}/{q}':len(seen[p]&seen[q]) for i,p in enumerate(a.ports) for q in a.ports[i+1:]}}),flush=True)
    # Preserve state across alternating input ports as well.
    ref=Model(groups,targets,owners,normal,faulty,None); opt=Model(groups,targets,owners,normal,faulty,programs,a.lazy)
    for p in a.ports:
        x,r,_=ref.event(p); y,s,_=opt.event(p)
        assert x==y and ref.lamps==opt.lamps and ref.state==opt.state and r['checks']==s['checks'] and ref.wave_order==opt.wave_order and ref.rng.getstate()==opt.rng.getstate()
        print(json.dumps(dict(mixed_port=p,parity='PASS',**s)),flush=True)
if __name__=='__main__': main()
