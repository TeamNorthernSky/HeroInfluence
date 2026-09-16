"""Geometry-scoped Store004/005 paint, props, signage and warning paint.

Run with Python -B and an external review directory. Other building outputs are
retained from inventory_v5; no original texture or model is edited.
"""
from pathlib import Path
import json,sys
import numpy as np
from PIL import Image,ImageFilter
import generate_masks_v5 as v5
v3=v5.v3;v4=v5.v4;ROOT=v3.ROOT

def anchor(lab,selected):
    a=np.median(lab[selected],axis=0)
    return dict(hue=float(np.degrees(np.arctan2(a[2],a[1]))%360),lightness=float(a[0]),saturation=float(np.linalg.norm(a[1:])*250))

def main():
    out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
    rows=json.loads((ROOT/'Data/inventory_v5.json').read_text(encoding='utf8'))['bindings'];reports=[]
    for row in rows:
        name=row['name'];key=row['key']
        if name not in ['BGStore004','BGStore005']:continue
        store4=name=='BGStore004';oldAccent=row['initial'][row['partIds'].index('accent')].copy()
        ids=['mailbox','crates'] if store4 else ['sign_face','standing_sign','side_entrance','warning_paint']
        names=['우체통','적재 박스'] if store4 else ['상단 간판','측면 입간판','측면 출입구','경고 도장']
        row['partIds']+=ids;row['partNames']+=names;row['initial'] += [oldAccent.copy() for _ in ids]
        if store4:row['partNames'][row['partIds'].index('accent')]='보조도장'
        count=len(row['partIds']);part=lambda id:row['partIds'].index(id)
        size=2048;src=np.asarray(Image.open(v3.v2.PROJECT/row['seed']).convert('RGB').resize((size,size),Image.Resampling.LANCZOS))
        lab=v3.v2.rc.lab(src);hue=np.degrees(np.arctan2(lab[:,:,2],lab[:,:,1]))%360;chroma=np.linalg.norm(lab[:,:,1:],axis=2)
        domain=np.full((size,size),-1,np.int32);warningDomain=np.zeros((size,size),bool);data=[]
        for p,u,t in v3.geo.load(key):
            original,old=v3.classify(row,p,u,t);groups=v3.geo.components(p,t)
            role=np.array([part(v3.v2.IDS[r]) if r<11 and v3.v2.IDS[r] in row['partIds'] else 15 for r in original],np.int32)
            def put(gs,id):
                for g in gs:role[groups[g]]=part(id)
            if store4:
                put([8],'mailbox');put([0,1],'crates')
                # Unassigned branding, bollards and equipment cannot receive
                # secondary paint, even when antialiased text looks olive.
                bary=np.array([[1/3]*3,[.6,.2,.2],[.2,.6,.2],[.2,.2,.6],[.45,.45,.1],[.45,.1,.45],[.1,.45,.45]])
                uv=np.einsum('sj,tjk->tsk',bary,u[t]);xx=np.clip(uv[:,:,0]*size,0,size-1).astype(int);yy=np.clip((1-uv[:,:,1])*size,0,size-1).astype(int)
                solid=((hue[yy,xx]>=94)&(hue[yy,xx]<=145)&(chroma[yy,xx]>=.025)).mean(1)>=.85
                role[np.isin(role,[0,2])&solid]=part('accent')
            else:
                put([13],'sign_face');put([23,40,47,48],'standing_sign');put([38,46],'side_entrance')
                v=p[t];frame=(v[:,:,0].max(1)<-.89)&(v[:,:,1].min(1)>.15)&(v[:,:,1].max(1)<.72)&(v[:,:,2].min(1)>.24)&(v[:,:,2].max(1)<.64)
                role[frame&np.isin(role,[0,2,15])]=part('side_entrance')
                warningGroups=[14,15,19,21,22,24,32,37,41,42,43]
                warningTris=np.concatenate([groups[g] for g in warningGroups])
                # Black stripes stay original too; the accent slider must never
                # recolor their yellow/black antialiased boundary.
                role[warningTris]=14
                mask=np.zeros(len(t),bool);mask[warningTris]=True;mask[groups[12]]=True
                warningDomain|=v3.raster(u,t,np.where(mask,1,-1),size)>=0
            rr=v3.raster(u,t,role,size);domain[rr>=0]=rr[rr>=0];data.append((u,t,role))
        # Reject shared interiors instead of silently painting another object.
        cores={}
        for gid in np.unique(domain[domain>=0]):
            cover=np.zeros_like(domain,dtype=bool)
            for u,t,role in data:cover|=v3.raster(u,t,np.where(role==gid,gid,-1),size)>=0
            cores[int(gid)]=np.asarray(Image.fromarray(cover.astype('uint8')*255).filter(ImageFilter.MinFilter(3)))>0
        conflicts=[(a,b,int((ma&mb).sum())) for a,ma in cores.items() for b,mb in cores.items() if a<b and (ma&mb).any()]
        if conflicts:raise RuntimeError(key+' shared UV: '+repr(conflicts))
        w=np.zeros((size,size,count),np.uint8);window=np.asarray(Image.fromarray(old[:,:,2]).resize((size,size),Image.Resampling.BILINEAR))>.12
        w[:,:,0][(domain==0)&~window]=255;w[:,:,2][(domain==0)&window]=255
        for i in range(1,count):w[:,:,i][domain==i]=255
        if store4:
            paint=(domain==part('accent'))|(np.isin(domain,[0,2])&(hue>=94)&(hue<=145)&(chroma>=.025))
            w[paint]=0;w[:,:,part('accent')][paint]=255
            row['initial'][part('accent')]=anchor(lab,paint)
        else:
            w[:,:,part('accent')][(domain==15)&v4.olive(src)]=255
            # Warm gray pavement has the same hue as yellow paint; curb pixels
            # require its stronger chroma, not just a yellow hue angle.
            yellow=warningDomain&(hue>=60)&(hue<=102)&(chroma>=.025)
            yellow&=(domain!=4)|((chroma>=.065)&(src[:,:,0]>src[:,:,1].astype(float)*1.12))
            w[yellow]=0;w[:,:,part('warning_paint')][yellow]=255
            row['initial'][part('warning_paint')]=anchor(lab,yellow)
        v5.grow(w,domain>=0);v5.save(row,w)
        reports.append(dict(key=key,interiorUVConflicts=conflicts,partTexels={id:int((w[:,:,i]>0).sum()) for i,id in enumerate(row['partIds'])}))
    (ROOT/'Data/inventory_v6.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf8')
    (out/'store45_parts_report.json').write_text(json.dumps(reports,indent=2),encoding='utf8');print(json.dumps(reports,indent=2))

if __name__=='__main__':main()
