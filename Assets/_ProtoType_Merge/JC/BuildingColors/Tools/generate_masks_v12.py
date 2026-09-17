"""RowHouse002 central projecting block including sides and roof cap, after v11."""
from pathlib import Path
import json,sys
import numpy as np
import rowhouse_uv as rowuv

ROOT=rowuv.v8.ROOT

def main():
    out=Path(sys.argv[1])
    rows=json.loads((ROOT/'Data/inventory_v10.json').read_text(encoding='utf8'))['bindings']
    row=next(r for r in rows if r['name']=='BGRowHouse002')
    p,u,t=rowuv.v3.geo.load(row['key'])[0]
    roles=rowuv.classify(row,p,u,t);before=roles.copy()
    v=p[t];lo=v.min(1);hi=v.max(1)
    shell=np.isin(np.arange(len(t)),rowuv.v3.geo.components(p,t)[0])
    # The side faces extend back to the facade and flare at the entrance canopy.
    body=shell&(lo[:,0]>-.370)&(hi[:,0]<.335)&(lo[:,1]>.761)&(hi[:,1]>.77)&(hi[:,2]<-.480)
    cap=shell&(lo[:,0]>-.292)&(hi[:,0]<.257)&(lo[:,1]>1.898)&(hi[:,2]<-.084)
    selected=(body|cap)&~np.isin(roles,[2,5])
    # Glazing and the terrace under the cap retain their existing controls.
    roles[selected]=12
    assert not np.any((before==11)&(lo[:,1]>.76)&~selected)
    row['partIds'].append('central_block');row['partNames'].append('중앙 블록')
    row['initial'].append(dict(row['initial'][row['partIds'].index('accent')]))
    result=rowuv.bake(row,p,u,t,roles,out,rowuv.v3.v2.IDS+['accent','central_block'])
    report=dict(centralBlockFaces=int(selected.sum()),previousRoles={str(k):int((before[selected]==k).sum()) for k in np.unique(before[selected])},**result)
    (ROOT/'Data/inventory_v12.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf8')
    (out/'central_block_report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
