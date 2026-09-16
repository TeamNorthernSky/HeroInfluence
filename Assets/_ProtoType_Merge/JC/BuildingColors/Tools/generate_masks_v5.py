"""Rebuild Store001/003 subdivisions from geometry; run with Python -B output_dir.

v4 inventory supplies stable references. Only these shops' generated masks and
Store001's derived UV roles change; original textures/models remain untouched.
"""
from pathlib import Path
import json, sys
import numpy as np
from PIL import Image, ImageFilter
import generate_masks_v4 as v4

v3=v4.v3; ROOT=v3.ROOT

def save(row,w):
    h,width,count=w.shape
    assert count<=13 and np.all(w.astype(np.int32).sum(2)<=255)
    for suffix,indices in [('parts',range(4)),('floor',[4]),('extra',range(5,9)),('extra2',range(9,13))]:
        a=np.stack([w[:,:,i] if i<count else np.zeros((h,width),np.uint8) for i in indices],2)
        Image.fromarray(a[:,:,0] if a.shape[2]==1 else a).save(ROOT/'Data'/(row['key']+'_'+suffix+'.png'))

def grow(w,occupied,steps=4):
    h,width=w.shape[:2];filled=occupied.copy()
    for _ in range(steps):
        prev=w.copy();have=filled.copy()
        for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
            sy=slice(max(0,-dy),min(h,h-dy));sx=slice(max(0,-dx),min(width,width-dx))
            ty=slice(max(0,dy),min(h,h+dy));tx=slice(max(0,dx),min(width,width+dx))
            sel=(~filled[ty,tx])&have[sy,sx]
            w[ty,tx][sel]=prev[sy,sx][sel];filled[ty,tx][sel]=True

def main():
    out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
    rows=json.loads((ROOT/'Data/inventory_v4.json').read_text(encoding='utf8'))['bindings'];report=[]
    for row in rows:
        if row['name'] not in ['BGStore001','BGStore003'] or not row['source']:continue
        oldAccent=row['initial'][row['partIds'].index('accent')].copy()
        if row['name']=='BGStore001':
            row['partIds']+=['sign_face'];row['partNames']+=['간판'];row['initial']+=[row['initial'][1].copy()]
            records=[];src=np.asarray(Image.open(v3.v2.PROJECT/row['source']).convert('RGB'))
            for p,u,t in v3.geo.load(row['key']):
                role,_=v3.classify(row,p,u,t);uv=u[t].mean(1)
                xy=np.clip(np.c_[uv[:,0],1-uv[:,1]]*256,0,255).astype(int)
                role[np.isin(role,[0,15])&v4.olive(src[xy[:,1],xy[:,0]])]=11
                role[v3.geo.components(p,t)[8]]=7;records.append(role)
            with (ROOT/'Data'/(row['key']+'_roles.bytes')).open('wb') as f:
                f.write(np.array([len(records)],'<i4').tobytes())
                for r in records:f.write(np.array([len(r)],'<i4').tobytes());f.write(r.astype('<i4').tobytes())
            w=np.zeros((1024,1024,len(row['partIds'])),np.uint8)
            ids=v3.v2.IDS+['accent']
            for gid,id in enumerate(ids):
                if id not in row['partIds']:continue
                x=gid%4*256;y=(3-gid//4)*256
                w[y:y+256,x:x+256,row['partIds'].index(id)]=255
            save(row,w);report.append(dict(key=row['key'],signTriangles=int(sum((r==7).sum() for r in records))))
            continue
        row['partNames'][row['partIds'].index('accent')]='보조도장'
        row['partIds']+=['sign_face','sign_symbol','accent2']
        row['partNames']+=['간판','햄버거','보조도장 2']
        row['initial'] += [oldAccent.copy() for _ in range(3)]
        size=2048;domain=np.full((size,size),-1,np.int32);geodata=[]
        src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((size,size),Image.Resampling.LANCZOS))
        for p,u,t in v3.geo.load(row['key']):
            role,old=v3.classify(row,p,u,t);groups=v3.geo.components(p,t)
            role[groups[8]]=7;role[np.concatenate([groups[6],groups[10]])]=12
            # Solid olive polygons retain their baked shadow/highlight pixels.
            # Only polygons mixing glass/paint need the pixel color boundary.
            bary=np.array([[1/3]*3,[.6,.2,.2],[.2,.6,.2],[.2,.2,.6],[.45,.45,.1],[.45,.1,.45],[.1,.45,.45]])
            samples=np.einsum('sj,tjk->tsk',bary,u[t]);xx=np.clip(samples[:,:,0]*size,0,size-1).astype(int);yy=np.clip((1-samples[:,:,1])*size,0,size-1).astype(int)
            ll=v3.v2.rc.lab(src[yy,xx]);hh=np.degrees(np.arctan2(ll[:,:,2],ll[:,:,1]))%360;cc=np.linalg.norm(ll[:,:,1:],axis=2)
            solid=((hh>=94)&(hh<=140)&(cc>=.025)).mean(1)>=.85
            role[np.isin(role,[0,2])&solid]=11
            rr=v3.raster(u,t,role,size);domain[rr>=0]=rr[rr>=0];geodata.append((u,t,role))
        # New sign roles must not share interior UV texels with other parts.
        cores={}
        for gid in np.unique(domain[domain>=0]):
            cover=np.zeros_like(domain,dtype=bool)
            for u,t,role in geodata:cover|=v3.raster(u,t,np.where(role==gid,gid,-1),size)>=0
            cores[int(gid)]=np.asarray(Image.fromarray(cover.astype('uint8')*255).filter(ImageFilter.MinFilter(3)))>0
        conflicts=[(a,b,int((ma&mb).sum())) for a,ma in cores.items() for b,mb in cores.items() if a<b and (ma&mb).any()]
        if conflicts:raise RuntimeError('Store subdivision shares UV interiors: '+repr(conflicts))
        w=np.zeros((size,size,len(row['partIds'])),np.uint8)
        window=np.asarray(Image.fromarray(old[:,:,2]).resize((size,size),Image.Resampling.BILINEAR))>.12
        w[:,:,0][(domain==0)&~window]=255;w[:,:,2][(domain==0)&window]=255
        for gid,id in enumerate(v3.v2.IDS):
            if gid>0 and id in row['partIds']:w[:,:,row['partIds'].index(id)][domain==gid]=255
        w[:,:,row['partIds'].index('sign_symbol')][domain==12]=255
        # Former accessory control stays on the bollard metal, planter trim,
        # handle and small equipment accents. It cannot reach the sign anymore.
        w[:,:,row['partIds'].index('accent2')][(domain==15)&v4.olive(src)]=255
        # Olive paint on the actual wall/door shell, excluding glass, cream
        # columns, the canopy and all independent prop/plant geometry.
        lab=v3.v2.rc.lab(src);hue=np.degrees(np.arctan2(lab[:,:,2],lab[:,:,1]))%360;chroma=np.linalg.norm(lab[:,:,1:],axis=2)
        paint=(domain==11)|(np.isin(domain,[0,2])&(hue>=94)&(hue<=140)&(chroma>=.025))
        w[paint]=0;w[:,:,row['partIds'].index('accent')][paint]=255
        anchorPixels=np.isin(domain,[0,2,11])&(hue>=94)&(hue<=140)&(chroma>=.025)
        anchor=np.median(lab[anchorPixels],axis=0)
        row['initial'][row['partIds'].index('accent')]=dict(hue=float(np.degrees(np.arctan2(anchor[2],anchor[1]))%360),lightness=float(anchor[0]),saturation=float(np.linalg.norm(anchor[1:])*250))
        grow(w,domain>=0);save(row,w)
        report.append(dict(key=row['key'],interiorUVConflicts=conflicts,partTexels={id:int((w[:,:,i]>0).sum()) for i,id in enumerate(row['partIds'])}))
    (ROOT/'Data/inventory_v5.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf8')
    (out/'store_parts_report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
