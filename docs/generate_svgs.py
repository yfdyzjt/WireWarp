#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Regenerate editable SVGs from anonymized benchmark CSVs."""
from pathlib import Path
import csv, html
D=Path(__file__).resolve().parent; A=D/'assets'; A.mkdir(exist_ok=True)
BG='#101923'; FG='#e2e8f0'; MUT='#9cacc0'; TRACK='#202d3b'; GRAY='#70869c'; BLUE='#4c90d3'; GREEN='#42c69a'; GOLD='#edb85e'; PURPLE='#b2a4e8'
def txt(x,y,s,size=16,color=FG,weight=400,anchor='start'):
    return f'<text x="{x}" y="{y}" font-size="{size}" fill="{color}" font-weight="{weight}" text-anchor="{anchor}">{html.escape(str(s))}</text>'
def rect(x,y,w,h,c,rx=5): return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{c}"/>'
def svg(name,w,h,title,desc,parts):
    (A/name).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" role="img" aria-labelledby="title desc"><title id="title">{html.escape(title)}</title><desc id="desc">{html.escape(desc)}</desc><g font-family="system-ui,Segoe UI,Arial,sans-serif">'+rect(0,0,w,h,BG,12)+''.join(parts)+'</g></svg>\n',encoding='utf-8')
def load(name):
    with (D/'data'/name).open() as f: return list(csv.DictReader(f))
def head(title,sub): return [txt(30,41,title,26,weight=700),txt(30,68,sub,14,MUT)]
def legend(items,y=91):
    p=[]; x=30
    for label,color in items:
        p.extend([rect(x,y-11,12,12,color,2),txt(x+19,y,label,13)]); x+=19+len(label)*7.5+24
    return p
modes=['generic','v2','v3','v3.1','v3.2','v3.3','v3.5','v4.0','v4.1']
rows=load('backend-rc2-medians.csv'); old=load('backend-rc1-medians.csv'); live=load('gameplay-rc2.csv'); prior=load('gameplay-rc1-comparison.csv')
def med(work,mode,data=rows):return float(next(r['median_ns'] for r in data if r['workload']==work and r['mode']==mode))
base=med('rgb-mixed','generic'); last=med('rgb-mixed','v4.1'); peak=med('rgb-mixed','generic',old)/med('rgb-mixed','v4.1',old)
p=head('WireWarp | Mixed RGB backend','RC2 · 1,000 events · 7 rounds · median ms/event (lower is better)')
for j,m in enumerate(modes):
    y=91+j*37;v=med('rgb-mixed',m);p.extend([txt(30,y+19,m,16,weight=600),rect(125,y,510,23,TRACK),rect(125,y,510*v/max(med('rgb-mixed',i) for i in modes),23,GREEN if m=='v4.1' else GRAY if j==0 else BLUE),txt(651,y+18,f'{v/1e6:.3f}',16)])
p.extend([txt(30,454,f'{base/last:.3f}× faster  |  {(1-last/base)*100:.2f}% less backend time',22,GREEN,700),txt(30,481,f'Prior RC1 peak: {peak:.3f}×. Same engine; independent sessions vary.',15,MUT),txt(30,505,'Same graph, trace and executable within RC2. Not a universal speedup.',14,MUT)])
svg('backend-performance.svg',800,530,'Mixed RGB backend benchmark','RC2 nine-mode comparison with historical RC1 peak.',p)
p=head('WireWarp | Live RGB gameplay','Draw-callback Hz (FPS proxy) · nominal workload labels · separate 30 s runs')+legend([('RC1 generic',GRAY),('RC1 AOT',BLUE),('RC2 AOT',GREEN)])
for j,hz in enumerate(['60','120']):
    y=120+j*109; p.append(txt(30,y+31,hz+' Hz',16,weight=600))
    values=[float(next(r['draw_hz'] for r in prior if r['clock_label']==hz and r['backend_mode']==m)) for m in ['generic','v4.1']]+[float(next(r['draw_hz'] for r in live if r['clock_label']==hz))]
    for k,(v,c) in enumerate(zip(values,[GRAY,BLUE,GREEN])):
        yy=y+26*k;p.extend([rect(125,yy,515,19,TRACK),rect(125,yy,515*v/61,19,c),txt(655,yy+15,f'{v:.2f}',16)])
    p.append(txt(125,y+90,f'+{(values[-1]/values[0]-1)*100:.1f}% RC2 AOT vs RC1 generic (cross-session)',13,GREEN))
p.extend([txt(30,354,'Fast frontend and historical legacy campfires in every run shown.',14,MUT),txt(30,376,'Not GPU presentation or actual input frequency; no new V4.2 measurement.',14,MUT)])
svg('gameplay-performance.svg',800,400,'Live RGB gameplay','RC1 generic, RC1 AOT and RC2 AOT for 60/120 workloads.',p)
p=head('WireWarp | Workload speedups','RC2 · ratio of same-executable medians (generic / V4.1) · higher is better')
works=['rgb-port-2','rgb-port-3','rgb-port-8','rgb-port-9','rgb-mixed','synthetic-chain','synthetic-fanout','synthetic-hybrid','synthetic-mixed']; ratios=[med(w,'generic')/med(w,'v4.1') for w in works];maxv=max(ratios)*1.05
for j,(w,v) in enumerate(zip(works,ratios)):
    y=91+j*37;p.extend([txt(30,y+18,w.replace('rgb-','RGB ').replace('synthetic-','Synthetic '),15,weight=600),rect(205,y,440,22,TRACK),rect(205,y,440*v/maxv,22,GREEN if j<5 else BLUE),txt(660,y+17,f'{v:.2f}×',16)])
p.extend([txt(30,453,'Fixed ports: 100 events; mixed RGB: 1,000; synthetic: 100,000.',14,MUT),txt(30,477,'Seven rounds each. Fallback remains available; not every circuit benefits.',14,MUT)])
svg('workload-performance.svg',800,505,'Workload speedups','RC2 fixed ports and synthetic workload medians.',p)
stages=[r for r in load('backend-rc2-stages.csv') if r['workload']=='mixed']; fields=['wire_ns','gates_ns','flush_ns','clear_ns','dispatch_ns','commit_ns'];colors=[BLUE,GREEN,GOLD,PURPLE,GRAY,'#d16e84'];labs=['Wire','Gate','Flush','Clear','Dispatch','Commit']
p=head('WireWarp | Backend stage profile','RC2 · instrumented mixed RGB trace · total ms over 1,000 events')+legend(list(zip(labs,colors)))
maxv=max(sum(float(r[f]) for f in fields)/1e6 for r in stages)
for i,r in enumerate(stages):
    y=124+i*52; x=125; total=sum(float(r[f]) for f in fields)/1e6;p.append(txt(30,y+19,r['mode'],16,weight=600));p.append(rect(x,y,530,25,TRACK))
    for field,c in zip(fields,colors):
        v=float(r[field])/1e6; w=530*v/maxv;p.append(rect(x,y,w,25,c,0)); x+=w
    p.append(txt(670,y+18,f'{total:.1f}',15))
p.extend([txt(30,404,'Raw instrumented phases; clock overhead not subtracted.',14,MUT),txt(30,427,'Stage profiles are separate runs, not the headline uninstrumented medians.',14,MUT)])
svg('backend-stages.svg',800,450,'Backend stage profile','RC2 mixed stage sums across five modes.',p)
p=head('WireWarp | Gameplay bottleneck','RC2 · average update time · approximate stage budget; backend compute overlaps')+legend([('Apply outputs',BLUE),('Wait / decode',GOLD),('Other',GRAY)])
for j,r in enumerate(live):
    hz=r['clock_label'];total=1000/float(r['update_hz']);apply=float(r['apply_ms_per_update']);wait=float(r['wait_decode_ms_per_update']);other=total-apply-wait;y=120+j*130;x=30
    p.append(txt(30,y,hz+f' Hz nominal  |  {total:.2f} ms/update',18,weight=600))
    for v,c in zip([apply,wait,other],[BLUE,GOLD,GRAY]):
        w=740*v/total;p.append(rect(x,y+15,w,30,c,0));x+=w
    p.append(txt(30,y+70,f'Apply {apply:.3f} ms  ·  Wait/decode {wait:.3f} ms  ·  Other ~{other:.2f} ms',15))
    p.append(txt(30,y+92,f'Output application: {100*apply/total:.1f}% of update interval',14,MUT))
p.append(txt(30,376,'120 Hz historical circuit remains frontend-bound; V4.2 retains the RC2 engine.',14,MUT))
svg('gameplay-budget.svg',800,402,'Gameplay bottleneck','Approximate output application and wait/decode shares from RC2.',p)
p=head('WireWarp | Ordered execution pipeline','V4.1 engine retained in V4.2 · graph-bound specialization · lossless IPC')
steps=[('Terraria frontend','Capture inputs • existing tick schedule'),('Named-pipe request','Ordered input activations'),('macha backend','V3.5 wire plans • V4.0 packed gate evaluation'),('V4.1 continuation queue','Ordered 1–3 wires or original list • next wave only'),('Compact16 / original RLE','Lossless response • fallback for large IDs / repeats'),('Terraria frontend','Ordered device application • native behavior retained')]
for j,(a,b) in enumerate(steps):
    y=91+j*70;p.extend([rect(105,y,590,52,'#1d3344' if j!=3 else '#194a40'),txt(400,y+21,a,17,FG,700,'middle'),txt(400,y+40,b,13,'#b8c9d6',400,'middle')]);
    if j<len(steps)-1:p.append(f'<path d="M400 {y+52} v15 l-5 -5 m5 5 l5 -5" fill="none" stroke="{MUT}" stroke-width="2"/>')
p.extend([txt(30,535,'SHA-256 mismatch → generic backend. No tick merging or cross-wave fusion.',14,MUT),txt(30,557,'Fast frontend starts automatically; native campfires default; reference selectable.',14,MUT)])
svg('architecture.svg',800,580,'WireWarp architecture','Ordered pipeline with graph-specialized simulation and lossless IPC.',p)
p=head('WireWarp | Optimization milestones','Architecture improvements · measured claims scoped to tested workloads')
items=[('V3.1','Lazy control parity','Deferred redundant control writes; original wave semantics.'),('V3.2','Selective trigger masks','98.62% of mask-block storage avoided on the RGB graph.'),('V3.5','Compiled wire execution','128,407 / 131,748 wires eligible in RC2 RGB.'),('V3.6–7','Indexed / compiled frontend','Direct dispatch, reusable buffers, reduced allocations.'),('V3.8','Compact IPC','2 vs 8 bytes per eligible singleton record (75% smaller).'),('V3.9','Device specialization','Ordered chimney and legacy campfire writes; native fallback.'),('V4.0','Packed gate descriptors','8 bytes per descriptor; 327,611 gates eligible in RGB.'),('V4.1','Wave-local continuations','8 vs 16 bytes per eligible x64 queue record; ordered fallback.'),('V4.2','Release and startup','Validated setup/launch, fast default and graph-mismatch fallback.')]
for j,(v,a,b) in enumerate(items):
    y=91+j*57;p.extend([rect(30,y,118,45,'#1d3344' if j<8 else '#194a40'),txt(89,y+27,v,14,FG,700,'middle'),txt(166,y+17,a,16,FG,700),txt(166,y+36,b,13,MUT)])
p.append(txt(30,622,'Structural storage/encoding improvements do not equal total RAM or FPS savings.',13,MUT))
svg('optimization-milestones.svg',800,645,'Optimization milestones','Version milestones with scoped structural improvements.',p)
