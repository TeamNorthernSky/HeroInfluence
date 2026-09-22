from pathlib import Path
import sys,json,numpy as np
from PIL import Image,ImageDraw
import geometry_parts as geo
ROOT=Path(__file__).resolve().parents[1]; PROJECT=ROOT.parents[3];OUT=ROOT/'Data';geo.ROOT=ROOT
sys.path.insert(0,str(PROJECT/'Assets/_ProtoType_Merge/JC/JC_TestScenes/BuildingColorTestbed/ColorStudies/Recipe'))
import recolor_buildings as rc
IDS=['wall','roof','window','foliage','floor','roof_floor','entrance_awning','sign_face','sign_trim','pot','soil']
NAMES=['벽','지붕·차양','창문','식생','바닥 블록','옥상 바닥면','현관 차양','옥상 간판 면','옥상 간판 테두리','화분','화분 토양']
# Heights are in the exported prefab orientation and scale, not raw FBX local coordinates.
ROOFS={'BGFactory001':[(1.395,.012)],'BGFactory002':[(.825,.014)],'BGHigh001':[(1.785,.006),(3.058,.008),(3.47,.008)],'BGHigh002':[(3.36,.015)],'BGHigh003':[(3.363,.008)],'BGHigh004':[(3.353,.009),(3.377,.009),(.732,.009)],'BGHouse001':[(1.127,.008)],'BGOffice001':[(1.841,.01)],'BGOffice002':[(1.953,.012)],'BGRowHouse001':[(2.191,.012)],'BGRowHouse002':[(1.963,.016)],'BGStore001':[(1.439,.012)],'BGStore002':[(1.318,.014)],'BGStore003':[(1.255,.012),(1.338,.008)],'BGStore004':[(1.328,.025)],'BGStore005':[(1.176,.015)]}
POTS={'BGFactory001':[6,7],'BGFactory002':[9,10],'BGHigh001':[9,11,13,15,17,18],'BGHigh002':[5,6],'BGHigh003':[6,7],'BGHigh004':[10,12],'BGHouse001':[5,6],'BGHouse002':[11,27,64,65],'BGOffice001':[23,24],'BGOffice002':[13,14],'BGRowHouse001':[6,8],'BGRowHouse002':[2,3,4],'BGStore001':[3,4],'BGStore002':[36,37,38]}
SOILS={'BGStore002':[76,79,80]}
# Geometry region rules precede color detection. Shared UV palette meshes receive one atlas tile per role.
def classify(row,p,u,t):
 name=row['name'];v=p[t];c=v.mean(1);cr=np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]);a=np.linalg.norm(cr,axis=1);n=cr/np.maximum(a[:,None],1e-9);groups=geo.components(p,t)
 old=np.concatenate([np.asarray(Image.open(ROOT/'Tools/LegacyMasks'/(row['key']+'_parts.png')).convert('RGBA')),np.asarray(Image.open(ROOT/'Tools/LegacyMasks'/(row['key']+'_floor.png')).convert('L'))[:,:,None]],2)/255
 uv=u[t].mean(1);sz=old.shape[0];xy=np.clip(np.c_[uv[:,0],1-uv[:,1]]*(sz-1),0,sz-1).astype(int);w=old[xy[:,1],xy[:,0]];role=np.where(w.max(1)>.2,w.argmax(1),15).astype(np.int32);override=np.full(len(t),-1,np.int32)
 if row['palette']:
  source=np.asarray(Image.open(PROJECT/row['source']).convert('RGB'));xy=np.clip(np.c_[uv[:,0],1-uv[:,1]]*255,0,255).astype(int);colors=rc.lab(source[xy[:,1],xy[:,0]]);hue=np.degrees(np.arctan2(colors[:,2],colors[:,1]))%360;chroma=np.linalg.norm(colors[:,1:],axis=1)
  role[:]=0;role[(hue>150)&(hue<290)&(chroma>.025)]=2
  vegetation={'BGHouse001':[0,1],'BGOffice001':[8,9,17],'BGStore001':[0,1],'BGHigh001':[4,5,6,7,10,14]}
  for idx in vegetation[name]:role[groups[idx]]=3
  base={'BGHouse001':3,'BGOffice001':1,'BGStore001':6,'BGHigh001':2};role[groups[base[name]]]=4
  if name=='BGHouse001':
   g=groups[2];role[g[v[g,:,1].min(1)>1.003]]=1
  if name=='BGOffice001':
   for idx in [21,22,26,27,29,30]:role[groups[idx]]=1
  if name=='BGStore001':
   for idx in [8,11,13]:role[groups[idx]]=1
  if name=='BGHigh001':role[((c[:,1]>3.0)&(c[:,1]<3.15))|((c[:,1]>1.74)&(c[:,1]<1.88))]=1
 for level,tol in ROOFS.get(name,[]):
  sel=(np.abs(c[:,1]-level)<tol)&(np.abs(n[:,1])>.90)&(np.ptp(v[:,:,1],axis=1)<tol*3)
  # High004: exclude rooftop equipment interiors by keeping the low floor faces.
  if name=='BGHigh004':sel&=c[:,1]<3.435
  override[sel]=5
 # The shop slab has uneven floor vertices and a cornice at nearly the same height.
 if name=='BGStore004':
  override[override==5]=-1;override[1202:1210]=5
 if name=='BGHigh004':
  override[(override==5)&(c[:,1]<1)&(~np.isin(np.arange(len(t)),[1248,1249]))]=-1
 if name=='BGHouse001':override[groups[9]]=6
 if name=='BGOffice001':
  sel=(c[:,0]>-.39)&(c[:,0]<.33)&(c[:,1]>.50)&(c[:,1]<.66)&(c[:,2]<-.66)
  override[sel]=6
 if name=='BGHouse002':
  override[groups[39]]=1
  frame=(v[:,:,1].min(1)>.49)&(v[:,:,1].max(1)<.54)&(v[:,:,0].min(1)>-.556)&(v[:,:,0].max(1)<-.162)&(v[:,:,2].min(1)>-.80)&(v[:,:,2].max(1)<-.689)
  override[frame]=1
 if name=='BGOffice002':
  # Roof assembly and entrance canopy only; never infer roof from olive paint elsewhere.
  role[:]=np.where(role==1,0,role);role[groups[3]]=1
  awn=(v[:,:,1].min(1)>.658)&(v[:,:,1].max(1)<.758)&(c[:,2]<-.43)&(c[:,0]>-.54)&(c[:,0]<.35);role[awn]=1;override[awn]=1
  g=groups[1];r=np.sqrt((c[g,0]+.003)**2+(c[g,1]-1.99)**2)
  override[g]=np.where((r>.422)|(np.abs(n[g,2])<.55),8,7)
 for idx in POTS.get(name,[]):
  g=groups[idx];vv=p[np.unique(t[g])];lo=vv.min(0);hi=vv.max(0);mid=(lo+hi)/2;span=hi-lo
  override[g]=9
  # Soil is the inner upward surface, with the rim and outside left as the pot body.
  inner=(np.abs(c[g,0]-mid[0])<span[0]*.36)&(np.abs(c[g,2]-mid[2])<span[2]*.36)
  soil=inner&(n[g,1]>.60)&(c[g,1]>lo[1]+span[1]*.4)
  override[g[soil]]=10
 for idx in SOILS.get(name,[]):override[groups[idx]]=10
 if name=='BGStore003' and row['source']:
  # Side planter troughs are connected to the main shell.
  sel=((c[:,0]<-1.03)|(c[:,0]>.98))&(c[:,1]>.194)&(c[:,1]<.255)&(c[:,2]>-.79)
  override[sel]=9;override[sel&(np.abs(n[:,1])>.6)]=10
 role[override>=0]=override[override>=0]
 return role,override,old

def raster(p,u,t,values,size,base=None):
 im=Image.new('I',(size,size),-1);d=ImageDraw.Draw(im)
 for tri,value in zip(u[t],values):
  if value<0:continue
  d.polygon([(float(x*(size-1)),float((1-y)*(size-1))) for x,y in tri],fill=int(value))
 return np.array(im)

def main():
 rows=json.loads((ROOT/'Tools/LegacyMasks/inventory.json').read_text())['bindings'];allrows=[]
 for row in rows:
  key=row['key'];data=[];present=set()
  for p,u,t in geo.load(key):
   role,over,old=classify(row,p,u,t);data.append((p,u,t,role,over,old));present.update(role[role<11].tolist())
  slots=list(range(5))+sorted(present.intersection(range(5,11)));mapping={g:i for i,g in enumerate(slots)};count=len(slots)
  if row['palette']:
   original=np.asarray(Image.open(PROJECT/row['source']).convert('RGB'));size=original.shape[0]*4;seed=np.tile(original,(4,4,1));weights=np.zeros((size,size,count),np.float32)
   for globalid,localid in mapping.items():
    x=(globalid%4)*256;y=(3-globalid//4)*256;weights[y:y+256,x:x+256,localid]=1
   Image.fromarray(seed).save(OUT/(key+'_atlas.png'))
   # Store global roles in the exact exported renderer/submesh triangle order.
   with (OUT/(key+'_roles.bytes')).open('wb') as f:
    f.write(np.array([len(data)],'<i4').tobytes())
    for p,u,t,role,over,old in data:f.write(np.array([len(role)],'<i4').tobytes());f.write(role.astype('<i4').tobytes())
  else:
   size=2048 if row['source'] else 32;seed=np.asarray(Image.open(PROJECT/row['seed']).convert('RGB').resize((size,size),Image.Resampling.LANCZOS)) if row['seed'] else np.full((size,size,3),255,np.uint8)
   old=data[0][-1];weights=np.zeros((size,size,count),np.float32)
   for i in range(5):weights[:,:,i]=np.asarray(Image.fromarray(old[:,:,i]).resize((size,size),Image.Resampling.BILINEAR))
   if row['name']=='BGOffice002':
    allowed=np.full((size,size),-1,np.int32)
    for p,u,t,role,over,old in data:
     rr=raster(p,u,t,np.where(role==1,1,-1),size);allowed[rr>=0]=1
    displaced=weights[:,:,1]*(allowed<0);weights[:,:,0]+=displaced;weights[:,:,1]*=allowed>=0
   changes=np.full((size,size),-1,np.int32)
   for p,u,t,role,over,old in data:
    rr=raster(p,u,t,over,size);changes[rr>=0]=rr[rr>=0]
   # Two-texel guard band prevents bilinear sampling from pulling the old category into a new part.
   for step in range(2):
    prev=changes.copy()
    for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
     shifted=np.roll(prev,(dy,dx),(0,1));sel=(changes<0)&(shifted>=0);changes[sel]=shifted[sel]
   for gid in slots:
    sel=changes==gid;weights[sel]=0;weights[:,:,mapping[gid]][sel]=1
  if row['name']=='BGHouse002':
   # The accepted coral seed still had olive paint baked into the misclassified sill.
   # Normalize only the reassigned sill to the roof anchor, retaining local shading.
   selected=changes==1;seed=seed.copy();lab=rc.lab(seed);vals=lab[selected];anchor=row['initial'][1]
   if len(vals):
    target=lab[selected].copy();target[:,0]+=anchor['lightness']-np.median(vals[:,0]);chroma=np.linalg.norm(target[:,1:],axis=1);chroma*=anchor['saturation']*.004/max(float(np.median(chroma)),1e-5);angle=np.radians(anchor['hue']);target[:,1]=chroma*np.cos(angle);target[:,2]=chroma*np.sin(angle);seed[selected]=rc.rgb(target)
   Image.fromarray(seed).save(OUT/(key+'_seed.png'));row['derivedSeed']=key+'_seed.png'
  weights/=np.maximum(weights.sum(2,keepdims=True),1)
  anchors=list(row['initial'])
  lab=rc.lab(seed)
  for i in range(5,count):
   if row['palette']:
    samples=[];areas=[]
    for p,u,t,role,over,old in data:
     chosen=role==slots[i];uv=u[t[chosen]].mean(1);xy=np.clip(np.c_[uv[:,0],1-uv[:,1]]*255,0,255).astype(int);samples.extend(rc.lab(original[xy[:,1],xy[:,0]]));v=p[t[chosen]];areas.extend(np.linalg.norm(np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]),axis=1))
    vals=np.asarray(samples);area=np.asarray(areas);a=[]
    for channel in range(3):
     order=np.argsort(vals[:,channel]);a.append(vals[order[np.searchsorted(np.cumsum(area[order]),area.sum()/2)],channel])
    a=np.array(a)
   else:
    vals=lab[weights[:,:,i]>.5];a=np.median(vals,axis=0) if len(vals) else np.array([.5,0,0])
   anchors.append(dict(hue=float(np.degrees(np.arctan2(a[2],a[1]))%360),lightness=float(a[0]),saturation=float(np.linalg.norm(a[1:])*250)))
  for suffix,indices in [('parts',range(4)),('floor',[4]),('extra',range(5,9)),('extra2',range(9,13))]:
   arr=np.stack([weights[:,:,i] if i<count else np.zeros((size,size)) for i in indices],2);arr=np.uint8(np.clip(arr*255,0,255));Image.fromarray(arr[:,:,0] if arr.shape[2]==1 else arr).save(OUT/(key+'_'+suffix+'.png'))
  row.update(partIds=[IDS[i] for i in slots],partNames=[NAMES[i] for i in slots],initial=anchors,partsVersion=2);allrows.append(row)
  debug=np.array([[210,205,195],[240,80,50],[60,150,210],[60,190,60],[110,110,110],[185,185,195],[245,180,30],[185,75,200],[65,70,180],[190,115,65],[70,40,20],[0,0,0],[0,0,0],[0,0,0],[0,0,0],[220,220,220]],np.uint8)
  # Optional review output, kept outside Assets.
  if len(sys.argv)>1:
   for k,(p,u,t,role,over,old) in enumerate(data):geo.render(p,t,debug[role],Path(sys.argv[1])/(key+f'_classified{k}.png'))
  print(row['name'],row['partIds'],flush=True)
 (OUT/'inventory_v2.json').write_text(json.dumps({'bindings':allrows},ensure_ascii=False,indent=2),encoding='utf-8')
if __name__=='__main__':main()
