from pathlib import Path
import numpy as np,json
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
def load(key):
 b=(ROOT/'Tools/Geometry'/f'{key}.bin').read_bytes();o=0;parts=[]
 def rd(n,d):
  nonlocal o
  a=np.frombuffer(b,dtype=d,count=n,offset=o);o+=a.nbytes;return a
 for k in range(int(rd(1,'<i4')[0])):
  n=int(rd(1,'<i4')[0]);p=rd(n*3,'<f4').reshape(-1,3);uv=rd(n*2,'<f4').reshape(-1,2);t=rd(int(rd(1,'<i4')[0]),'<i4').reshape(-1,3);parts.append((p,uv,t))
 return parts
def components(p,t):
 _,vi=np.unique(np.round(p,4),axis=0,return_inverse=True);par=list(range(vi.max()+1))
 def find(k):
  while par[k]!=k:par[k]=par[par[k]];k=par[k]
  return k
 for a,b,c in vi[t]:
  a,b,c=find(a),find(b),find(c);par[b]=a;par[c]=a
 groups={}
 for i,v in enumerate(vi[t[:,0]]):groups.setdefault(find(v),[]).append(i)
 return sorted((np.array(v) for v in groups.values()),key=len,reverse=True)
def render(p,t,cols,path,groups=None,angle=-25):
 yaw=np.radians(angle);pitch=np.radians(32)
 rot=np.array([[np.cos(yaw),0,-np.sin(yaw)],[np.sin(pitch)*np.sin(yaw),np.cos(pitch),np.sin(pitch)*np.cos(yaw)],[np.cos(pitch)*np.sin(yaw),-np.sin(pitch),np.cos(pitch)*np.cos(yaw)]])
 q=p@rot.T;q[:,1]*=-1;span=np.ptp(q[:,:2],axis=0);s=680/max(span);q[:,:2]=(q[:,:2]-q[:,:2].min(0))*s+40
 im=Image.new('RGB',(780,780),(235,235,235));d=ImageDraw.Draw(im)
 for i in np.argsort(q[t,2].mean(1))[::-1]:d.polygon([tuple(x) for x in q[t[i],:2]],fill=tuple(cols[i]))
 if groups:
  for idx,g in enumerate(groups):
   if len(g)<4:continue
   x,y=q[np.unique(t[g]),:2].mean(0);d.text((x,y),str(idx),fill='black',stroke_width=1,stroke_fill='white')
 im.save(path)
