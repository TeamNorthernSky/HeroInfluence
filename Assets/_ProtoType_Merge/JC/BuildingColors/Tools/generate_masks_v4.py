"""Append an independent accent control to the geometry-bounded v3 masks.

Always regenerate v3 first. Palette-painted trim is separated by mesh face;
normal textures admit only unused prop geometry, never roof/plant/glass pixels.
"""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image
import generate_masks_v3 as v3

ROOT=v3.ROOT

def olive(rgb):
    lab=v3.v2.rc.lab(rgb);h=np.degrees(np.arctan2(lab[...,2],lab[...,1]))%360;c=np.linalg.norm(lab[...,1:],axis=-1)
    return (h>=78)&(h<=145)&(c>=.025)&(c<=.14)&(lab[...,0]>.15)&(lab[...,0]<.77)

def read_weights(row):
    key=row['key'];parts=np.asarray(Image.open(ROOT/'Data'/(key+'_parts.png')).convert('RGBA'))
    floor=np.asarray(Image.open(ROOT/'Data'/(key+'_floor.png')).convert('L'))[:,:,None]
    extra=np.asarray(Image.open(ROOT/'Data'/(key+'_extra.png')).convert('RGBA'))
    extra2=np.asarray(Image.open(ROOT/'Data'/(key+'_extra2.png')).convert('RGBA'))
    return np.concatenate([parts,floor,extra,extra2],2)[:,:,:len(row['partIds'])].copy()

def main():
    previousPath=ROOT/'Data/inventory_v4.json'
    previous={r['key']:r for r in json.loads(previousPath.read_text(encoding='utf-8'))['bindings']} if previousPath.exists() else {}
    v3.main()
    out=Path(sys.argv[1]);rows=json.loads((ROOT/'Data/inventory_v3.json').read_text(encoding='utf-8'))['bindings'];report=[]
    for row in rows:
        key=row['key'];w=read_weights(row);h,width=w.shape[:2];new=np.zeros((h,width),np.uint8);samples=[]
        if row['palette']:
            src=np.asarray(Image.open(v3.v2.PROJECT/row['source']).convert('RGB'));records=[]
            for p,u,t in v3.geo.load(key):
                role,_=v3.classify(row,p,u,t);uv=u[t].mean(1)
                xy=np.clip(np.c_[uv[:,0],1-uv[:,1]]*256,0,255).astype(int);color=src[xy[:,1],xy[:,0]]
                selected=np.isin(role,[0,15])&olive(color);role[selected]=11;samples.extend(color[selected]);records.append(role)
            with (ROOT/'Data'/(key+'_roles.bytes')).open('wb') as f:
                f.write(np.array([len(records)],'<i4').tobytes())
                for role in records:f.write(np.array([len(role)],'<i4').tobytes());f.write(role.astype('<i4').tobytes())
            # Dedicated tile 11; every other tile and pre-existing mask is unchanged.
            new[256:512,768:1024]=255
            srcForAnchor=np.asarray(samples) if samples else np.empty((0,3),np.uint8)
        else:
            if row.get('geometryAtlas'):
                src=np.asarray(Image.open(ROOT/'Data'/(key+'_atlas.png')).convert('RGB'))
            elif row.get('derivedSeed'):
                src=np.asarray(Image.open(ROOT/'Data'/row['derivedSeed']).convert('RGB').resize((width,h),Image.Resampling.LANCZOS))
            elif row['seed']:
                src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((width,h),Image.Resampling.LANCZOS))
            else:src=np.full((h,width,3),255,np.uint8)
            # Restrict to actual unassigned faces. Existing wall/window/roof/floor
            # masks, including their guard bands, remain exclusive.
            allowed=np.zeros((h,width),bool);cols=row.get('atlasColumns',1);nrows=row.get('atlasRows',1);size=width//cols
            pageMap=next((r.get('pageByRole',{}) for r in json.loads((out/'coverage_report.json').read_text()) if r['key']==key),{})
            for p,u,t in v3.geo.load(key):
                role,_=v3.classify(row,p,u,t);mask=v3.raster(u,t,np.where(role==15,15,-1),size)>=0
                page=int(pageMap.get('15',0));x=page%cols*size;y=(nrows-1-page//cols)*size;allowed[y:y+size,x:x+size]|=mask
            paint=olive(src);free=w.max(2)==0;selected=allowed&free&paint;new[selected]=255;srcForAnchor=src[selected]
            # Extend the new mask only over unused texture space, never another part.
            for step in range(2):
                prev=new.copy()
                for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
                    sy=slice(max(0,-dy),min(h,h-dy));sx=slice(max(0,-dx),min(width,width-dx));ty=slice(max(0,dy),min(h,h+dy));tx=slice(max(0,dx),min(width,width+dx))
                    sel=(prev[sy,sx]>0)&free[ty,tx]&paint[ty,tx];new[ty,tx][sel]=255
        # Ignore isolated compression noise; do not expose a slider for one pixel.
        if len(srcForAnchor)<32:
            report.append(dict(key=key,accentPixels=0,sourceSamples=len(srcForAnchor),noAdditionalRegion=True));print(key,'no additional olive region',flush=True);continue
        lab=v3.v2.rc.lab(srcForAnchor);anchor=np.median(lab,axis=0)
        initial=dict(hue=float(np.degrees(np.arctan2(anchor[2],anchor[1]))%360),lightness=float(anchor[0]),saturation=float(np.linalg.norm(anchor[1:])*250))
        # Once exposed in profiles, keep the color reference stable during later
        # boundary repairs, just as v3 preserves the original part references.
        before=previous.get(key)
        if before and 'accent' in before['partIds']:initial=before['initial'][before['partIds'].index('accent')]
        row['partIds']=row['partIds']+['accent'];row['partNames']=row['partNames']+['보조 도장·소품'];row['initial']=row['initial']+[initial]
        w=np.concatenate([w,new[:,:,None]],2);count=w.shape[2]
        if count>13:raise RuntimeError('Part capacity exceeded')
        for suffix,indices in [('parts',range(4)),('floor',[4]),('extra',range(5,9)),('extra2',range(9,13))]:
            arr=np.stack([w[:,:,i] if i<count else np.zeros((h,width),np.uint8) for i in indices],2)
            Image.fromarray(arr[:,:,0] if arr.shape[2]==1 else arr).save(ROOT/'Data'/(key+'_'+suffix+'.png'))
        report.append(dict(key=key,accentPixels=int((new>0).sum()),sourceSamples=len(srcForAnchor),anchor=initial));print(key,'accent samples',len(srcForAnchor),flush=True)
    (ROOT/'Data/inventory_v4.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'accent_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
if __name__=='__main__':main()
