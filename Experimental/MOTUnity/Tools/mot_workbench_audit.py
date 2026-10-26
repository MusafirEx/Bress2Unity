#!/usr/bin/env python3
"""Standalone non-destructive GNTA axis-angle/slot checks; no Unity required.
Run: python mot_workbench_audit.py 0000.mot 0010.mot 0030.mot
"""
from pathlib import Path
import json
import math
import struct
import sys

def u16(b,p): return struct.unpack_from('>H',b,p)[0]
def u32(b,p): return struct.unpack_from('>I',b,p)[0]
def f32(b,p): return struct.unpack_from('>f',b,p)[0]
def audit(path):
    b=Path(path).read_bytes()
    if len(b)<16 or u32(b,8)!=16 or u32(b,12)!=len(b):
        raise ValueError('MOT header or declared file size invalid: '+str(path))
    slots=u16(b,6)
    if slots < 1 or 16+slots*4>len(b): raise ValueError('Invalid slot table')
    offsets=[u32(b,16+4*i) for i in range(slots)]
    boundaries=sorted(set(o for o in offsets if o))
    if not boundaries or boundaries[0]<16+slots*4 or boundaries[-1]>=len(b):raise ValueError('Invalid clip offsets')
    ends={v:(boundaries[i+1] if i+1<len(boundaries) else len(b)) for i,v in enumerate(boundaries)}
    result={'file':Path(path).name,'slot_capacity':slots,'occupied_slots':0,'tracks_2d_rotation':0,'tracks_4d_rotation':0,'accepted_4d_axis_angle':0,'sentinel_only_4d':0,'invalid_4d':0,'translation_tracks':0,'control_tracks':0,'max_angle_degrees':0.0}
    for slot,off in enumerate(offsets):
        if not off:continue
        result['occupied_slots']+=1
        limit=ends[off]
        if limit-off<16: raise ValueError('Truncated GNTA header')
        count=u16(b,off+2)
        if u32(b,off+4)!=16 or off+16+count*32>limit:raise ValueError('Bad GNTA track directory')
        for t in range(count):
            p=off+16+32*t
            flag,typ,bone,n=struct.unpack_from('>HHHH',b,p)
            comp=flag&255
            if not (typ&8) and not (typ&1):result['control_tracks']+=1;continue
            if comp not in (2,4):raise ValueError(f'Unexpected component count {comp} in {slot:04X}')
            po=off+u32(b,p+20)
            if po+n*4*comp>limit:raise ValueError('Track values exceed GNTA clip')
            if typ&1:result['translation_tracks']+=1;continue
            if not typ&8:continue
            if comp==2:result['tracks_2d_rotation']+=1;continue
            result['tracks_4d_rotation']+=1
            valid=0
            failed=False
            for k in range(n):
                a,bv,c,deg=struct.unpack_from('>ffff',b,po+k*16)
                if (a,bv,c,deg)==(100.,0.,0.,0.):continue
                unit=a*a+bv*bv+c*c
                if not math.isfinite(unit) or not .90<=unit<=1.10 or not math.isfinite(deg):failed=True;break
                valid+=1
                result['max_angle_degrees']=max(result['max_angle_degrees'],abs(deg))
            if failed:result['invalid_4d']+=1
            elif not valid:result['sentinel_only_4d']+=1
            else:result['accepted_4d_axis_angle']+=1
    assert result['accepted_4d_axis_angle']+result['sentinel_only_4d']+result['invalid_4d']==result['tracks_4d_rotation']
    return result

if __name__=='__main__':
    if len(sys.argv)<2: raise SystemExit(__doc__)
    result=[audit(p) for p in sys.argv[1:]]
    print(json.dumps(result,indent=2))