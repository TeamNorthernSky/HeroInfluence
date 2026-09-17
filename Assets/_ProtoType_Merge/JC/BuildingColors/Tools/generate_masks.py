from pathlib import Path
import sys,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
scratch=Path(__file__).parent/'Geometry'
project=Path(__file__).resolve().parents[5]
out=project/'Assets/_ProtoType_Merge/JC/BuildingColors/Data'
out.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(project/'Assets/_ProtoType_Merge/JC/JC_TestScenes/BuildingColorTestbed/ColorStudies/Recipe'))
import recolor_buildings as rc
rows=[]
for line in (scratch/'inventory.tsv').read_text(encoding='utf-8-sig').splitlines():
 name,key,prefab,material,source,width,height,shader=line.split('\t');size=min(1024,int(width))
 if source:
  im=Image.open(project/source).convert('RGB').resize((size,size),Image.Resampling.LANCZOS)
 else:im=Image.new('RGB',(size,size),'white')
 original=np.asarray(im)
 geo=scratch/(key+'.bin');roof,_=rc.geometry_mask(geo,size)
 v,w=rc.classify(original,roof)
 # Low connected geometry gives an initial floor mask. Sparse palette UVs may share roles.
 buf=geo.read_bytes();off=0;miny=1e9;maxy=-1e9
 def read(n,d):
  global off
  a=np.frombuffer(buf,dtype=d,count=n,offset=off);off+=a.nbytes;return a
 for _ in range(int(read(1,'<i4')[0])):
  n=int(read(1,'<i4')[0]);p=read(n*3,'<f4').reshape(-1,3);read(n*2,'<f4');read(int(read(1,'<i4')[0]),'<i4');miny=min(miny,float(p[:,1].min()));maxy=max(maxy,float(p[:,1].max()))
 try:floor=rc.floor_mask(geo,size,.20 if name=='BGHouse002' else (maxy-miny)*.085)
 except ValueError:floor=np.zeros((size,size),dtype=np.float32)
 floor*=1-np.clip(w[:,:,3]/.65,0,1)
 if size<=256 and source:
  # Expand sparse UV hits to complete 16x16 palette cells; each building has its own mask.
  floor=np.repeat(np.repeat(floor.reshape(16,16,16,16).max(axis=(1,3)),16,axis=0),16,axis=1)
 if not source:w[:]=0;w[:,:,0]=1;floor[:]=0
 weights=np.concatenate([w*(1-floor[:,:,None]),floor[:,:,None]],axis=2)
 weights/=np.maximum(weights.sum(2,keepdims=True),1)
 # House002 begins at the accepted coral/neutral-floor texture, all others at source.
 seed='Assets/_ProtoType_Merge/JC/JC_TestScenes/BuildingColorTestbed/ColorStudies/A_Coral/BGHouse002.png' if name=='BGHouse002' else source
 seedim=Image.open(project/seed).convert('RGB').resize((size,size),Image.Resampling.LANCZOS) if seed else im
 seedlab=rc.lab(np.asarray(seedim));anchors=[]
 for k in range(5):
  selected=weights[:,:,k]>.25;vals=seedlab[selected];anchor=np.median(vals,axis=0) if len(vals) else np.array([.7,0,0])
  anchors.append({'hue':float(np.degrees(np.arctan2(anchor[2],anchor[1]))%360),'lightness':float(anchor[0]),'saturation':float(np.linalg.norm(anchor[1:])*250)})
 Image.fromarray(np.uint8(np.clip(weights[:,:,:4]*255,0,255)),'RGBA').save(out/(key+'_parts.png'))
 Image.fromarray(np.uint8(weights[:,:,4]*255)).save(out/(key+'_floor.png'))
 rows.append(dict(name=name,key=key,prefab=prefab,material=material,source=source,seed=seed,initial=anchors,palette=size==256))
 print(name,key,flush=True)
(out/'inventory.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf-8')


