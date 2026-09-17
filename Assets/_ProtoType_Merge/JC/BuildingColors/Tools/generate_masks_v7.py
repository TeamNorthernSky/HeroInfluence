"""Complete parapets and repair RowHouse002's glass/olive paint geometry.

Coverage-only refinement after v6. Keeps color references and all profile values.
"""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image,ImageFilter
import generate_masks_v6 as v6
v5=v6.v5;v4=v5.v4;v3=v5.v3;ROOT=v3.ROOT
BASE=ROOT

def roles_file(key):
    data=(BASE/'Data'/(key+'_roles.bytes')).read_bytes();offset=4;rows=[]
    for _ in range(int(np.frombuffer(data,'<i4',1)[0])):
        n=int(np.frombuffer(data,'<i4',1,offset)[0]);offset+=4;rows.append(np.frombuffer(data,'<i4',n,offset).copy());offset+=n*4
    return rows

def write_roles(key,rows):
    with (ROOT/'Data'/(key+'_roles.bytes')).open('wb') as f:
        f.write(np.array([len(rows)],'<i4').tobytes())
        for row in rows:f.write(np.array([len(row)],'<i4').tobytes());f.write(row.astype('<i4').tobytes())

def planar_regions(p,t,allowed):
    # Recessed window faces are separate planes from the surrounding frames.
    # Grow over shared edges, never a shared corner or a differently facing trim.
    _,verts=np.unique(np.round(p,4),axis=0,return_inverse=True)
    v=p[t];cross=np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]);length=np.linalg.norm(cross,axis=1);normal=cross/np.maximum(length[:,None],1e-9)
    parent=np.arange(len(t));edges={}
    def find(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    for i in np.where(allowed)[0]:
        a,b,c=verts[t[i]]
        for pair in [(a,b),(b,c),(c,a)]:
            edge=tuple(sorted(pair))
            for j in edges.get(edge,[]):
                if np.dot(normal[i],normal[j])>.995:parent[find(i)]=find(j)
            edges.setdefault(edge,[]).append(i)
    result={}
    for i in np.where(allowed)[0]:result.setdefault(find(i),[]).append(i)
    return [np.array(g) for g in result.values()],length

def rim_faces(row,p,u,t,roles):
    v=p[t];c=v.mean(1);selected=np.zeros(len(t),bool)
    if row['name'] not in v3.v2.ROOFS:return selected
    # Horizontal floor islands determine each parapet footprint. Close levels
    # are handled separately, including the two towers and terrace buildings.
    for level,tolerance in v3.v2.ROOFS[row['name']]:
        floor=np.where((roles==5)&(np.abs(c[:,1]-level)<tolerance*1.5))[0]
        if not len(floor):continue
        for group in v3.geo.components(p,t[floor]):
            fv=v[floor[group]];lo=fv.min((0,1));hi=fv.max((0,1))
            if hi[0]-lo[0]<.35 or hi[2]-lo[2]<.35:continue
            near=np.minimum.reduce([np.abs(c[:,0]-lo[0]),np.abs(c[:,0]-hi[0]),np.abs(c[:,2]-lo[2]),np.abs(c[:,2]-hi[2])])<.22
            footprint=(c[:,0]>lo[0]-.23)&(c[:,0]<hi[0]+.23)&(c[:,2]>lo[2]-.23)&(c[:,2]<hi[2]+.23)
            height=(v[:,:,1].min(1)>level-.18)&(v[:,:,1].max(1)>level+.025)&(v[:,:,1].max(1)<level+.30)&(c[:,1]>level-.045)
            # A second, higher roof-floor level is still floor, not the first
            # level's parapet. Never convert accepted horizontal floor faces.
            selected|=near&footprint&height&np.isin(roles,[0,1,2])
    return selected

def main():
    global BASE
    out=Path(sys.argv[1])
    if len(sys.argv)>2:BASE=Path(sys.argv[2]);v4.ROOT=BASE
    inventory=BASE/'Data/inventory_v7.json'
    rows=json.loads((inventory if inventory.exists() else BASE/'Data/inventory_v6.json').read_text(encoding='utf8'))['bindings'];report=[]
    for row in rows:
        name=row['name'];key=row['key']
        if name not in v3.v2.ROOFS:continue
        w=v4.read_weights(row);h,width,count=w.shape;cols=row.get('atlasColumns',1);nr=row.get('atlasRows',1)
        # A full v4->v5->v6 regeneration may have reset a previously expanded
        # atlas. Its current pixel dimensions are authoritative for page layout.
        if row.get('geometryAtlas'):
            cols=width//2048;nr=h//2048;row.update(atlasColumns=cols,atlasRows=nr)
        records=roles_file(key) if row['palette'] or row.get('geometryAtlas') else None
        patches=np.zeros((h,width),bool);occupied=np.zeros((h,width),bool);unselected=np.zeros((h,width),bool);picked=0;changedRoles=0;geometry=[]
        for k,(p,u,t) in enumerate(v3.geo.load(key)):
            roles,old=v3.classify(row,p,u,t);sel=rim_faces(row,p,u,t,roles);picked+=int(sel.sum())
            if row['palette']:
                changedRoles+=int((records[k][sel]!=1).sum());records[k][sel]=1;continue
            size=width//cols;pages=records[k] if records is not None else np.zeros(len(t),np.int32)
            geometry.append((p,u,t,sel,pages))
            for page in np.unique(pages):
                x=page%cols*size;y=(nr-1-page//cols)*size
                patch=v3.raster(u,t,np.where(sel&(pages==page),1,-1),size)>=0
                used=v3.raster(u,t,np.where(pages==page,1,-1),size)>=0
                other=v3.raster(u,t,np.where(~sel&(pages==page),1,-1),size)>=0
                patches[y:y+size,x:x+size]|=patch;occupied[y:y+size,x:x+size]|=used;unselected[y:y+size,x:x+size]|=other
        if row['palette']:
            if changedRoles:write_roles(key,records)
            report.append(dict(key=key,rimTriangles=picked,changedFaces=changedRoles));continue
        # Do not overwrite another surface sharing the same UV. Edge texels are
        # allowed; interior conflicts require an explicit derived UV split.
        core=lambda m:np.asarray(Image.fromarray(m.astype('uint8')*255).filter(ImageFilter.MinFilter(3)))>0
        conflict=core(patches)&core(unselected)&(w[:,:,1]<128)
        isolated=0
        if conflict.any():
            # The RowHouse001 rim shares interior UVs with a different surface.
            # Give it its own page instead of changing the other surface's mask.
            isolated=int(conflict.sum());page=max(int(g[4].max()) for g in geometry)+1
            if page>=4:raise RuntimeError(key+' exceeds four UV pages')
            newcols=2;newnr=(page+2)//2;nh=newnr*size;nw=newcols*size
            expanded=np.zeros((nh,nw,count),np.uint8)
            for oldpage in range(page):
                ox=oldpage%cols*size;oy=(nr-1-oldpage//cols)*size;nx=oldpage%newcols*size;ny=(newnr-1-oldpage//newcols)*size
                expanded[ny:ny+size,nx:nx+size]=w[oy:oy+size,ox:ox+size]
            w=expanded;h=nh;width=nw;cols=newcols;nr=newnr;records=[]
            patches=np.zeros((h,width),bool);occupied=np.zeros((h,width),bool)
            for p,u,t,sel,pages in geometry:
                pages=pages.copy();pages[sel]=page;records.append(pages)
                for pp in np.unique(pages):
                    x=pp%cols*size;y=(nr-1-pp//cols)*size
                    occupied[y:y+size,x:x+size]|=v3.raster(u,t,np.where(pages==pp,1,-1),size)>=0
                    patches[y:y+size,x:x+size]|=v3.raster(u,t,np.where(sel&(pages==pp),1,-1),size)>=0
            seed=ROOT/'Data'/row['derivedSeed'] if row.get('derivedSeed') else v3.v2.PROJECT/row['seed']
            source=np.asarray(Image.open(seed).convert('RGB').resize((size,size),Image.Resampling.LANCZOS))
            Image.fromarray(np.tile(source,(nr,cols,1))).save(ROOT/'Data'/(key+'_atlas.png'))
            write_roles(key,records);row.update(geometryAtlas=True,atlasColumns=cols,atlasRows=nr)
        before=w.copy();w[patches]=0;w[:,:,1][patches]=255
        if name=='BGRowHouse002':
            p,u,t=v3.geo.load(key)[0];roles,old=v3.classify(row,p,u,t);src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((width,h),Image.Resampling.LANCZOS))
            lab=v3.v2.rc.lab(src);hue=np.degrees(np.arctan2(lab[:,:,2],lab[:,:,1]))%360;chroma=np.linalg.norm(lab[:,:,1:],axis=2)
            bary=np.array([[1/3]*3,[.6,.2,.2],[.2,.6,.2],[.2,.2,.6],[.45,.45,.1],[.45,.1,.45],[.1,.45,.45]])
            uv=np.einsum('sj,tjk->tsk',bary,u[t]);xx=np.clip(uv[:,:,0]*width,0,width-1).astype(int);yy=np.clip((1-uv[:,:,1])*h,0,h-1).astype(int)
            glassSample=(hue[yy,xx]>=145)&(hue[yy,xx]<=270)&(chroma[yy,xx]>.008)
            shell=np.isin(np.arange(len(t)),v3.geo.components(p,t)[0])&np.isin(roles,[0,2])&~rim_faces(row,p,u,t,roles)
            glassTris=shell&(glassSample.mean(1)>=.57)
            planes,area=planar_regions(p,t,shell)
            for plane in planes:
                if area[plane].sum()<1e-8:continue
                span=np.ptp(p[t[plane]].reshape(-1,3),axis=0)
                if span[1]<.8 and max(span[0],span[2])<.9 and area[plane[glassTris[plane]]].sum()/area[plane].sum()>.25:glassTris[plane]=True
            glass=v3.raster(u,t,np.where(glassTris,1,-1),width)>=0
            body=v3.raster(u,t,np.where(shell,1,-1),width)>=0
            # Entire recessed glass faces remain windows through their shadows.
            w[glass]=0;w[:,:,2][glass]=255
            accent=row['partIds'].index('accent');w[:,:,accent]=0
            paint=body&~glass&(hue>=94)&(hue<=145)&(chroma>=.025)
            solidPaint=shell&~glassTris&(((hue[yy,xx]>=94)&(hue[yy,xx]<=145)&(chroma[yy,xx]>=.025)).mean(1)>=.85)
            for plane in planes:
                if glassTris[plane].any() or area[plane].sum()<1e-8:continue
                if area[plane[solidPaint[plane]]].sum()/area[plane].sum()>.35:solidPaint[plane]=True
            v=p[t]
            solidPaint|=shell&~glassTris&(v[:,:,1].min(1)>.17)&(v[:,:,1].max(1)<.40)
            paint|=v3.raster(u,t,np.where(solidPaint,1,-1),width)>=0
            paint&=~patches;w[paint]=0;w[:,:,accent][paint]=255
            row['partNames'][accent]='보조도장'
        # Refill the existing four-texel guard band only from actual faces.
        w[~occupied]=0;v5.grow(w,occupied);v5.save(row,w)
        report.append(dict(key=key,rimTriangles=picked,changedTexels=int(np.any(w!=before,axis=2).sum()),isolatedSharedTexels=isolated,rimInteriorConflicts=0))
    (ROOT/'Data/inventory_v7.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf8')
    (out/'rim_report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
