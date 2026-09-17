"""RowHouse002 face charts with separate UV gutters at original texel density."""
import json
import numpy as np
from PIL import Image
import generate_masks_v8 as v8
v7=v8.v7;v3=v8.v3
def classify(row,p,u,t):
    roles,old=v3.classify(row,p,u,t);rim=v7.rim_faces(row,p,u,t,roles);src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((2048,2048)));lab=v3.v2.rc.lab(src);hue=np.degrees(np.arctan2(lab[:,:,2],lab[:,:,1]))%360;chroma=np.linalg.norm(lab[:,:,1:],axis=2)
    bary=np.array([[1/3]*3,[.6,.2,.2],[.2,.6,.2],[.2,.2,.6],[.45,.45,.1],[.45,.1,.45],[.1,.45,.45]])
    uv=np.einsum('sj,tjk->tsk',bary,u[t]);xx=np.clip(uv[:,:,0]*2048,0,2047).astype(int);yy=np.clip((1-uv[:,:,1])*2048,0,2047).astype(int)
    glassSample=(hue[yy,xx]>=145)&(hue[yy,xx]<=270)&(chroma[yy,xx]>.008)
    shell=np.isin(np.arange(len(t)),v3.geo.components(p,t)[0])&np.isin(roles,[0,2])&~rim;glass=shell&(glassSample.mean(1)>=.57)
    planes,area=v7.planar_regions(p,t,shell)
    for plane in planes:
     if area[plane].sum()<1e-8:continue
     span=np.ptp(p[t[plane]].reshape(-1,3),axis=0)
     if span[1]<.8 and max(span[0],span[2])<.9 and area[plane[glass[plane]]].sum()/area[plane].sum()>.25:glass[plane]=True
    paint=shell&~glass&(((hue[yy,xx]>=94)&(hue[yy,xx]<=145)&(chroma[yy,xx]>=.025)).mean(1)>=.85)
    for plane in planes:
     if glass[plane].any() or area[plane].sum()<1e-8:continue
     if area[plane[paint[plane]]].sum()/area[plane].sum()>.35:paint[plane]=True
    v=p[t];paint|=shell&~glass&(v[:,:,1].min(1)>.17)&(v[:,:,1].max(1)<.40)
    lo=v.min(1);hi=v.max(1);c=v.mean(1)
    central=shell&~glass&(lo[:,0]>-.40)&(hi[:,0]<.35)&(lo[:,1]>.76)&(c[:,2]<-.59)
    base=shell&~glass&(lo[:,1]>.17)&(hi[:,1]<.40)
    paint=central|base
    roles[shell]=0;roles[glass]=2;roles[paint]=11;roles[rim]=1
    # Low roof candidates outside the actual entrance canopy are facade seams.
    roles[(roles==1)&(hi[:,1]<1.9)&~((lo[:,0]>-.50)&(hi[:,0]<.47))]=0
    return roles

def charts(p,u,t,roles):
    v=p[t];cr=np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]);norm=cr/np.maximum(np.linalg.norm(cr,axis=1)[:,None],1e-10)
    _,vi=np.unique(np.c_[np.round(p,4),np.round(u,6)],axis=0,return_inverse=True);parent=np.arange(len(t));edges={}
    def find(i):
     while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
     return i
    for i in range(len(t)):
     a,b,c=vi[t[i]]
     for edge in [(a,b),(b,c),(c,a)]:
      key=(int(roles[i]),)+tuple(sorted(edge))
      for j in edges.get(key,[]):
       if np.dot(norm[i],norm[j])>.999:parent[find(i)]=find(j)
      edges.setdefault(key,[]).append(i)
    charts={}
    for i in range(len(t)):charts.setdefault(find(i),[]).append(i)
    rects=[]
    for tris in charts.values():
     uv=u[t[tris]];lo=np.floor(uv.min((0,1))*2048).astype(int);hi=np.ceil(uv.max((0,1))*2048).astype(int);wh=np.maximum(hi-lo,1)
     rects.append(dict(tris=tris,lo=lo.tolist(),hi=hi.tolist(),w=int(wh[0]+16),h=int(wh[1]+16),role=int(roles[tris[0]])))
    return rects

def bake(row,p,u,t,roles,out,role_ids=None):
    rects=charts(p,u,t,roles);rects.sort(key=lambda r:(-r['h'],-r['w'],r['tris'][0]))
    size=4096;x=0;y=0;shelf=0
    for r in rects:
        if x+r['w']>size:x=0;y+=shelf;shelf=0
        r['x']=x;r['y']=y;x+=r['w'];shelf=max(shelf,r['h'])
    if y+shelf>size:raise RuntimeError('RowHouse UV packing exceeds 4096')
    src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((2048,2048),Image.Resampling.LANCZOS))
    atlas=np.zeros((size,size,3),np.uint8);w=np.zeros((size,size,len(row['partIds'])),np.uint8);uv=np.zeros((len(t),3,2),np.float32)
    ids=role_ids if role_ids is not None else v3.v2.IDS+['accent']
    for r in rects:
        lx,ly=r['lo'];hx,hy=r['hi'];xx=r['x'];yy=r['y'];ww=r['w'];hh=r['h']
        # Clamped source sampling handles degenerate sliver charts at UV edges.
        sx=np.clip(np.arange(lx-8,hx+8 if hx>lx else lx+9),0,2047)
        sy=np.clip(np.arange(2048-hy-8,2048-ly+8 if hy>ly else 2048-ly+9),0,2047)
        atlas[yy:yy+hh,xx:xx+ww]=src[sy[:,None],sx[None,:]]
        if r['role']!=15:w[yy:yy+hh,xx:xx+ww,row['partIds'].index(ids[r['role']])]=255
        q=u[t[r['tris']]].copy();q[:,:,0]=(q[:,:,0]*2048-lx+xx+8)/size;q[:,:,1]=1-(hy-q[:,:,1]*2048+yy+8)/size;uv[r['tris']]=q
    Image.fromarray(atlas).save(v8.ROOT/'Data'/(row['key']+'_atlas.png'));v8.v5.save(row,w)
    with (v8.ROOT/'Data'/(row['key']+'_uv.bytes')).open('wb') as f:
        f.write(np.array([1,len(t)*3],'<i4').tobytes());f.write(uv.astype('<f4').tobytes())
    v7.write_roles(row['key'],[np.zeros(len(t),np.int32)]);row.update(geometryAtlas=True,atlasColumns=2,atlasRows=2)
    (out/'row_uv_charts.json').write_text(json.dumps(rects))
    np.save(out/'row_roles.npy',roles)
    return dict(charts=len(rects),atlasSize=size,packedHeight=y+shelf,gutter=8,sourceTexelDensity=2048)
