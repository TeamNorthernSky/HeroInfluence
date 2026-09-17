"""High001 roof layers, High002/003 entrance trim and protected small parts.
Run after v7. Geometry bounds are in the exported prefab coordinate system.
"""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image,ImageFilter
import generate_masks_v7 as v7
v3=v7.v3;v4=v7.v4;v5=v7.v5;ROOT=v7.ROOT


def select(row,p,t):
    v=p[t];c=v.mean(1);lo=v.min(1);hi=v.max(1);groups=v3.geo.components(p,t)
    shell=np.zeros(len(t),bool);shell[groups[0]]=True
    if row['name']=='BGHigh002':
        canopy=shell&(lo[:,1]>.655)&(hi[:,1]<.767)&(lo[:,0]>-.30)&(hi[:,0]<.326)&(c[:,2]<-.51)
        band=shell&(lo[:,1]>.16)&(hi[:,1]<.302)&((c[:,0]<-.70)|(c[:,0]>.72)|(c[:,2]>.57)|((c[:,2]<-.49)&(np.abs(c[:,0]-.01)>.34)))
        fixed=shell&(lo[:,1]>.47)&(hi[:,1]<.60)&(lo[:,2]<-.62)&(((lo[:,0]>-.29)&(hi[:,0]<-.215))|((lo[:,0]>.22)&(hi[:,0]<.30)))
    else:
        canopy=np.zeros(len(t),bool);canopy[groups[5]]=True;canopy&=(lo[:,1]>.50)&(hi[:,1]<.604)
        band=shell&(lo[:,1]>.12)&(hi[:,1]<.254)&((c[:,0]<-.90)|(c[:,0]>.78)|(c[:,2]>.44)|(c[:,2]<-.475))
        fixed=np.zeros(len(t),bool);fixed[np.concatenate([groups[8],groups[10]])]=True
    return canopy|band,fixed


def main():
    out=Path(sys.argv[1]);rows=json.loads((ROOT/'Data/inventory_v7.json').read_text(encoding='utf8'))['bindings'];report=[]
    for row in rows:
        if row['name'] not in ['BGHigh001','BGHigh002','BGHigh003']:continue
        key=row['key'];p,u,t=v3.geo.load(key)[0]
        if row['palette']:
            roles=v7.roles_file(key)[0];before=roles.copy();c=p[t].mean(1);lo=p[t].min(1);hi=p[t].max(1)
            roles[np.isin(roles,[1,5,11])&(c[:,1]>1.0)]=1
            # Pink upper terrace slab, and the highest flat roof around the
            # antenna plinth. Lower cornice tops belong to the red roof trim.
            pink=(lo[:,1]>3.237)&(hi[:,1]<3.348)
            top=(np.abs(lo[:,1]-3.472)<.002)&(np.abs(hi[:,1]-3.472)<.002)
            roles[pink|top]=5
            groups=v3.geo.components(p,t);handles=np.concatenate([groups[i] for i in [23,24,27,28]]);roles[handles]=15
            v7.write_roles(key,[roles]);report.append(dict(key=key,changedFaces=int((roles!=before).sum()),protectedHandles=len(handles),roofFaces=int((roles==1).sum()),roofFloorFaces=int((roles==5).sum())))
            continue
        w=v4.read_weights(row)[:,:2048].copy();h,width,count=w.shape
        if row.get('geometryAtlas'):raise RuntimeError('Review page layout before changing '+key)
        size=width;selected,fixed=select(row,p,t)
        pick=v3.raster(u,t,np.where(selected,1,-1),size)>=0
        protect=v3.raster(u,t,np.where(fixed,1,-1),size)>=0
        other=v3.raster(u,t,np.where(~(selected|fixed),1,-1),size)>=0
        core=lambda a:np.asarray(Image.fromarray(a.astype('uint8')*255).filter(ImageFilter.MinFilter(3)))>0
        conflict=(core(pick)&core(other))|(core(protect)&core(other))|(core(pick)&core(protect))
        # Even edge-only UV contact leaks through bilinear/mip filtering.
        # Put edited trim and protected props on a separate UV page.
        if 'accent' not in row['partIds']:
            src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((width,h),Image.Resampling.LANCZOS))
            lab=v3.v2.rc.lab(src[pick]);anchor=np.median(lab,axis=0)
            initial=dict(hue=float(np.degrees(np.arctan2(anchor[2],anchor[1]))%360),lightness=float(anchor[0]),saturation=float(np.linalg.norm(anchor[1:])*250))
            row['partIds']+=['accent'];row['partNames']+=['보조도장'];row['initial']+=[initial];w=np.concatenate([w,np.zeros((h,width,1),np.uint8)],2)
        idx=row['partIds'].index('accent');row['partNames'][idx]='보조도장';w[:,:,idx]=0
        pages=np.where(selected|fixed,1,0).astype(np.int32)
        expanded=np.zeros((h,width*2,w.shape[2]),np.uint8);expanded[:,:width]=w
        expanded[:,width:,idx][pick]=255
        for page in [0,1]:
            occupied=v3.raster(u,t,np.where(pages==page,1,-1),size)>=0
            tile=expanded[:,page*width:(page+1)*width];tile[~occupied]=0;v5.grow(tile,occupied)
        w=expanded;v5.save(row,w);v7.write_roles(key,[pages])
        src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((size,size),Image.Resampling.LANCZOS))
        Image.fromarray(np.tile(src,(1,2,1))).save(ROOT/'Data'/(key+'_atlas.png'))
        row.update(geometryAtlas=True,atlasColumns=2,atlasRows=1)
        report.append(dict(key=key,accentTriangles=int(selected.sum()),fixedTriangles=int(fixed.sum()),accentTexels=int(pick.sum()),fixedTexels=int(protect.sum()),sharedInteriorTexels=int(conflict.sum())))
    (ROOT/'Data/inventory_v8.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf8')
    (out/'high_report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
