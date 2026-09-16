"""Building region corrections and non-destructive mesh repairs after v9."""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image
import generate_masks_v8 as v8
import rowhouse_uv
ROOT=v8.ROOT;v3=v8.v3;v7=v8.v7

def main():
 if len(sys.argv)!=3:raise SystemExit('Usage: generate_masks_v10.py OUTPUT_DIR PRE_V10_SNAPSHOT_DIR')
 out=Path(sys.argv[1]);base=Path(sys.argv[2]);assert base.resolve()!=ROOT.resolve(), 'Use a separate pre-v10 snapshot'
 rows=json.loads((ROOT/'Data/inventory_v9.json').read_text(encoding='utf8'))['bindings'];report=[]
 # Required pre-v10 Data snapshot keeps repeated builds reproducible.
 v8.v4.ROOT=base;v7.BASE=base
 for row in rows:
  name=row['name'];key=row['key']
  if name not in ['BGOffice001','BGOffice002','BGStore002','BGRowHouse002','BGFactory002']:continue
  p,u,t=v3.geo.load(key)[0];v=p[t];lo=v.min(1);hi=v.max(1);c=v.mean(1);groups=v3.geo.components(p,t)
  if name=='BGRowHouse002':
   roles=rowhouse_uv.classify(row,p,u,t);data=rowhouse_uv.bake(row,p,u,t,roles,out);report.append(dict(key=key,**data));continue
  if name=='BGFactory002':
   cr=np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]);length=np.linalg.norm(cr,axis=1)
   flipped=np.where((np.abs(c[:,1]-.8252)<.002)&(np.ptp(v[:,:,1],axis=1)<.001)&(cr[:,1]/np.maximum(length,1e-10)<-.99))[0]
   assert len(flipped)==6
   (ROOT/'Data'/(key+'_repair.json')).write_text(json.dumps(dict(flipTriangles=flipped.tolist()),indent=2));v7.write_roles(key,[np.zeros(len(t),np.int32)])
   report.append(dict(key=key,flippedTriangles=flipped.tolist()));continue
  w=v8.v4.read_weights(row)
  if name=='BGOffice001':
   roles=v7.roles_file(key)[0];sel=np.zeros(len(t),bool);sel[groups[22]]=True;sel&=~((np.abs(lo[:,1]-1.8413)<.002)&(np.abs(hi[:,1]-1.8413)<.002));roles[sel]=12;v7.write_roles(key,[roles])
   src=np.asarray(Image.open(v3.v2.PROJECT/row['source']).convert('RGB'));uv=u[t].mean(1);xy=np.clip(np.c_[uv[:,0],1-uv[:,1]]*256,0,255).astype(int);lab=v3.v2.rc.lab(src[xy[sel,1],xy[sel,0]]);a=np.median(lab,axis=0)
   row['partIds']+=['roof_trim'];row['partNames']+=['옥상 테두리'];row['initial']+=[dict(hue=float(np.degrees(np.arctan2(a[2],a[1]))%360),lightness=float(a[0]),saturation=float(np.linalg.norm(a[1:])*250))]
   w=np.concatenate([w,np.zeros(w.shape[:2]+(1,),np.uint8)],2);w[:256,:256]=0;w[:256,:256,-1]=255;v8.v5.save(row,w)
   report.append(dict(key=key,roofTrimTriangles=int(sel.sum()),anchor=row['initial'][-1]));continue
  shell=np.zeros(len(t),bool);shell[groups[0]]=True
  if name=='BGOffice002':
   selected=shell&(lo[:,1]>.17)&(hi[:,1]<.353)&((c[:,0]<-.87)|(c[:,0]>.85)|(c[:,2]>.70)|((c[:,2]<-.45)&((hi[:,0]<-.475)|(lo[:,0]>.324))))
  else:
   selected=shell&(lo[:,1]>.12)&(hi[:,1]<.348)&((c[:,0]<-1.11)|(c[:,0]>.88)|(c[:,2]>1.12)|((c[:,2]<-.50)&((hi[:,0]<.005)|(lo[:,0]>.485))))
  count=w.shape[2];cols=row.get('atlasColumns',1);nr=row.get('atlasRows',1);size=2048;pages=v7.roles_file(key)[0] if row.get('geometryAtlas') else np.zeros(len(t),np.int32)
  accent=row['partIds'].index('accent');oldAccent=int((w[:,:,accent]>0).sum());w[:,:,accent]=0
  newpage=int(pages.max())+1;newcols=2;newrows=(newpage+2)//2;expanded=np.zeros((size*newrows,size*newcols,count),np.uint8)
  for page in range(newpage):
   tile=w[(nr-1-page//cols)*size:(nr-page//cols)*size,page%cols*size:(page%cols+1)*size].copy()
   occupied=v3.raster(u,t,np.where((pages==page)&~selected,1,-1),size)>=0;tile[~occupied]=0;v8.v5.grow(tile,occupied)
   xx=page%2*size;yy=(newrows-1-page//2)*size;expanded[yy:yy+size,xx:xx+size]=tile
  pages[selected]=newpage;occupied=v3.raster(u,t,np.where(selected,1,-1),size)>=0;tile=np.zeros((size,size,count),np.uint8);tile[:,:,accent][occupied]=255;v8.v5.grow(tile,occupied)
  xx=newpage%2*size;yy=(newrows-1-newpage//2)*size;expanded[yy:yy+size,xx:xx+size]=tile
  src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((size,size),Image.Resampling.LANCZOS));Image.fromarray(np.tile(src,(newrows,2,1))).save(ROOT/'Data'/(key+'_atlas.png'))
  v8.v5.save(row,expanded);v7.write_roles(key,[pages]);row.update(geometryAtlas=True,atlasColumns=2,atlasRows=newrows);row['partNames'][accent]='보조도장'
  # The destination changed from props to a painted base band; source reference
  # follows the actual band while scene/profile target values remain untouched.
  lab=v3.v2.rc.lab(src[occupied]);a=np.median(lab,axis=0);row['initial'][accent]=dict(hue=float(np.degrees(np.arctan2(a[2],a[1]))%360),lightness=float(a[0]),saturation=float(np.linalg.norm(a[1:])*250))
  np.save(out/(key+'_selected.npy'),selected);report.append(dict(key=key,baseBandTriangles=int(selected.sum()),oldAccentTexelsCleared=oldAccent,pages=newpage+1))
 (ROOT/'Data/inventory_v10.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf8');(out/'repair_report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if __name__=='__main__':main()
