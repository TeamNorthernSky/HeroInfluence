"""High001 upper glazing/walls/roof layers; run after v8.
Only the highest horizontal roof plane is roof_floor. Preserve target colors.
"""
from pathlib import Path
import sys,json
import numpy as np
from PIL import Image
import generate_masks_v8 as v8
ROOT=v8.ROOT

def main():
 rows=json.loads((ROOT/'Data/inventory_v8.json').read_text(encoding='utf8'))['bindings'];row=next(r for r in rows if r['name']=='BGHigh001');key=row['key']
 p,u,t=v8.v3.geo.load(key)[0];v=p[t];lo=v.min(1);hi=v.max(1);c=v.mean(1);roles=v8.v7.roles_file(key)[0];before=roles.copy()
 src=np.asarray(Image.open(v8.v3.v2.PROJECT/row['source']).convert('RGB'));uv=u[t].mean(1);xy=np.clip(np.c_[uv[:,0],1-uv[:,1]]*256,0,255).astype(int);rgb=src[xy[:,1],xy[:,0]]
 band=(lo[:,1]>3.057)&(hi[:,1]<3.247)
 glass=band&np.all(rgb==[121,150,141],axis=1)
 wall=band&(np.all(rgb==[227,212,189],axis=1)|np.all(rgb==[228,200,155],axis=1))
 roles[glass]=2;roles[wall]=0
 lowerSlab=(lo[:,1]>3.237)&(hi[:,1]<3.348);roles[lowerSlab]=1
 beigeBlock=(lo[:,1]>3.346)&(hi[:,1]>3.348)&(hi[:,1]<3.473);roles[beigeBlock]=0
 top=(np.abs(lo[:,1]-3.472)<.002)&(np.abs(hi[:,1]-3.472)<.002);roles[top]=5
 v8.v7.write_roles(key,[roles])
 # Prior roof-floor anchor was copied from olive trim although these faces
 # sample a different beige palette swatch. Anchor to the actual surface.
 lab=v8.v3.v2.rc.lab(rgb[top]);anchor=np.median(lab,axis=0)
 row['initial'][row['partIds'].index('roof_floor')]=dict(hue=float(np.degrees(np.arctan2(anchor[2],anchor[1]))%360),lightness=float(anchor[0]),saturation=float(np.linalg.norm(anchor[1:])*250))
 (ROOT/'Data/inventory_v9.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf8')
 report=dict(changedFaces=int((roles!=before).sum()),upperGlass=int(glass.sum()),upperWall=int(wall.sum()),lowerSlab=int(lowerSlab.sum()),beigeBlock=int(beigeBlock.sum()),topFloor=int(top.sum()),roofFloorAnchor=row['initial'][row['partIds'].index('roof_floor')])
 (Path(sys.argv[1])/'high1_report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if __name__=='__main__':main()
