"""Deterministic building color studies. Reuses Recolor Forge's sRGB/OKLab math.
Run with bundled Python (numpy + Pillow), --mesh-root <export directory>.
Outputs are preview sources at 2048px, matching the existing Unity import cap.
Original textures/materials are never written. --size 8192 permits full-size export.
"""
from pathlib import Path
import argparse, json, hashlib, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[6]  # Assets
OUT = Path(__file__).resolve().parents[1]
M1=np.array([[.4122214708,.5363325363,.0514459929],[.2119034982,.6806995451,.1073969566],[.0883024619,.2817188376,.6299787005]],dtype=np.float32)
M2=np.array([[.2104542553,.7936177850,-.0040720468],[1.9779984951,-2.4285922050,.4505937099],[.0259040371,.7827717662,-.8086757660]],dtype=np.float32)
IM1=np.linalg.inv(M1); IM2=np.linalg.inv(M2)

def lab(rgb):
    v=np.asarray(rgb,dtype=np.float32)/255
    linear=np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4)
    return np.cbrt(linear@M1.T)@M2.T
def rgb(v):
    linear=np.clip(((v@IM2.T)**3)@IM1.T,0,1)
    srgb=np.where(linear<=.0031308,linear*12.92,1.055*linear**(1/2.4)-.055)
    return np.uint8(np.clip(srgb*255+.5,0,255))
def smooth(x,a,b):
    t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
def color(h):return lab(np.array([int(h[i:i+2],16) for i in (1,3,5)]))
def bounds_hue(h,a,b,feather=12):return smooth(h,a-feather,a)*(1-smooth(h,b,b+feather))

def geometry_mask(mesh_path,size):
    buf=mesh_path.read_bytes(); offset=0
    def read(n,dtype):
        nonlocal offset
        v=np.frombuffer(buf,dtype=dtype,count=n,offset=offset);offset+=v.nbytes;return v
    parts=int(read(1,'<i4')[0]); mask=Image.new('L',(size,size)); draw=ImageDraw.Draw(mask)
    count=0; total=0
    for _ in range(parts):
        n=int(read(1,'<i4')[0]);p=read(n*3,'<f4').reshape(-1,3);uv=read(n*2,'<f4').reshape(-1,2).copy();uv[:,1]=1-uv[:,1];tri=read(int(read(1,'<i4')[0]),'<i4').reshape(-1,3)
        pts=p[tri]; norm=np.cross(pts[:,1]-pts[:,0],pts[:,2]-pts[:,0]); lengths=np.linalg.norm(norm,axis=1)
        up=norm[:,1]/np.maximum(lengths,1e-10); rel=(pts[:,:,1].mean(axis=1)-p[:,1].min())/max(np.ptp(p[:,1]),1e-10)
        selected=(up>.55)&(rel>.38)&(lengths>1e-10); total+=len(tri); count+=int(selected.sum())
        for t in tri[selected]:draw.polygon([tuple(v) for v in uv[t]*(size-1)],fill=255)
    # Small outward dilation accommodates bilinear sampling at island edges.
    mask=mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(.7))
    return np.asarray(mask,dtype=np.float32)/255,dict(triangles=total,roofCandidateTriangles=count)

def classify(im,roof):
    v=lab(im); L=v[:,:,0]; C=np.linalg.norm(v[:,:,1:],axis=2); H=np.mod(np.rad2deg(np.arctan2(v[:,:,2],v[:,:,1])),360)
    visible=smooth(L,.12,.27)
    foliage=bounds_hue(H,100,151,10)*smooth(C,.045,.07)
    window=bounds_hue(H,155,244,18)*smooth(C,.015,.035)*visible
    wall=(1-smooth(C,.055,.11))*smooth(L,.47,.67)*(1-window)*(1-foliage)
    olive=bounds_hue(H,75,125,12)*(1-smooth(C,.07,.115))
    dark_neutral=(1-smooth(C,.035,.085))*(1-smooth(L,.48,.68))
    roofmask=np.maximum(roof*np.maximum(olive,dark_neutral),olive*.70*(1-smooth(L,.52,.69)))
    roofmask*=visible*(1-foliage)*(1-window)
    # Residual wall shadows follow their original source hue; nearly black details remain protected.
    wall=np.maximum(wall,bounds_hue(H,55,100,15)*(1-smooth(C,.04,.075))*smooth(L,.30,.50)*.5)
    wall*=1-roofmask
    weights=np.stack([wall,roofmask,window,foliage*.65],axis=-1)
    weights/=np.maximum(weights.sum(axis=-1,keepdims=True),1)
    return v,weights

def floor_mask(mesh_path,size,max_height):
    """Largest connected low mesh component; excludes separate plants/decorations."""
    buf=mesh_path.read_bytes(); offset=0; mask=Image.new('L',(size,size)); draw=ImageDraw.Draw(mask)
    def read(n,dtype):
        nonlocal offset
        v=np.frombuffer(buf,dtype=dtype,count=n,offset=offset);offset+=v.nbytes;return v
    for _ in range(int(read(1,'<i4')[0])):
        n=int(read(1,'<i4')[0]);p=read(n*3,'<f4').reshape(-1,3);uv=read(n*2,'<f4').reshape(-1,2).copy();uv[:,1]=1-uv[:,1]
        tri=read(int(read(1,'<i4')[0]),'<i4').reshape(-1,3);pts=p[tri]
        chosen=np.flatnonzero(pts[:,:,1].max(axis=1)-p[:,1].min()<max_height)
        parents={int(k):int(k) for k in chosen};points={}
        def find(k):
            while parents[k]!=k:parents[k]=parents[parents[k]];k=parents[k]
            return k
        for k in chosen:
            for v in pts[k]:
                key=tuple(np.round(v,4))
                if key in points:parents[find(int(k))]=find(points[key])
                else:points[key]=int(k)
        groups={}
        for k in chosen:groups.setdefault(find(int(k)),[]).append(k)
        if not groups:raise ValueError('No floor component found')
        for k in max(groups.values(),key=len):draw.polygon([tuple(v) for v in uv[tri[k]]*(size-1)],fill=255)
    return np.asarray(mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(.5)),dtype=np.float32)/255

def process(source,size,mesh_root,styles,overrides=None):
    path=ROOT/'_ProtoType_Merge/DH/AlphaAsset/AIAsset/Building/Texture'/f'{source}.jpg'
    im=Image.open(path).convert('RGB').resize((size,size),Image.Resampling.LANCZOS); arr=np.asarray(im)
    roof,geo=geometry_mask(mesh_root/f'{source}_mesh.bin',size)
    v,w=classify(arr,roof); roles=['wall','roof','window','foliage']; anchors=[]
    for k in range(4):
        samples=v[w[:,:,k]>.5]
        if not len(samples): samples=v[w[:,:,k]>.1]
        anchors.append(np.median(samples,axis=0) if len(samples) else np.array([.5,0,0]))
    (OUT/'Masks').mkdir(exist_ok=True,parents=True)
    mask_vis=np.clip(w[:,:,:3]*255,0,255).astype('uint8');Image.fromarray(mask_vis).save(OUT/'Masks'/f'{source}_wall_roof_window.png')
    Image.fromarray(np.uint8(w[:,:,3]*255)).save(OUT/'Masks'/f'{source}_foliage.png')
    Image.fromarray(np.uint8(roof*255)).save(OUT/'Masks'/f'{source}_uv_roof.png')
    report={'source':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sourceSize':Image.open(path).size,'outputSize':[size,size],'geometry':geo,'roles':{r:{'anchorOklab':a.tolist(),'meanWeight':float(w[:,:,k].mean())} for k,(r,a) in enumerate(zip(roles,anchors))},'variants':{}}
    for name,style in styles.items():
        settings=(overrides or {}).get(source,{}).get(name,{})
        dest=OUT/name;dest.mkdir(exist_ok=True)
        result=v.copy()
        for k,role in enumerate(roles):
            target=color(style[role]); anchor=anchors[k]
            if role=='roof':target[0]+=settings.get('roofLightnessOffset',0)
            # Anchor-to-target mapping preserves L differences; mild chroma tightening only.
            edited=v.copy();edited[:,:,0]=v[:,:,0]+target[0]-anchor[0]
            edited[:,:,1:]=(v[:,:,1:]-anchor[1:])*.55+target[1:]
            result+=(edited-v)*w[:,:,k,None]
        if 'floorTarget' in settings:
            floor=floor_mask(mesh_root/f'{source}_mesh.bin',size,settings['floorMaxHeight'])
            # The base top also contains painted lawn: keep its green color.
            floor*=1-np.clip(w[:,:,3]/.65,0,1)
            target=color(settings['floorTarget']);anchor=np.median(v[:,:,0][floor>.99])
            edited=np.zeros_like(v);edited[:,:,0]=v[:,:,0]-anchor+target[0]
            result=result*(1-floor[:,:,None])+edited*floor[:,:,None]
            Image.fromarray(np.uint8(floor*255)).save(OUT/'Masks'/f'{source}_{name}_floor.png')
        result[:,:,0]=np.clip(result[:,:,0],0,.97)
        out=rgb(result);Image.fromarray(out).save(dest/f'{source}.png',compress_level=6)
        protected=w.sum(axis=-1)<1e-6
        if 'floorTarget' in settings:protected &= floor<1e-6
        max_error=int(np.abs(out.astype('int16')-arr.astype('int16'))[protected].max()) if protected.any() else 0
        assert max_error<=1,'Unselected pixels altered'
        report['variants'][name]={'changedPixelFraction':float((np.max(np.abs(out.astype('int16')-arr.astype('int16')),axis=-1)>2).mean()),'protectedPixelMaxError':max_error,'channelClipFraction':float(((out==0)|(out==255)).mean()),'overrides':settings}
        preview=Image.fromarray(out);preview.thumbnail((700,700));preview.save(OUT/'Masks'/f'{source}_{name}_preview.png')
    print(source,report['roles'],flush=True)
    return report

def palette(styles):
    path=ROOT/'_ProtoType_Merge/DH/Resources/Building/256ColorNormal_2605201600.png'
    arr=np.asarray(Image.open(path).convert('RGBA')).copy()
    # This model samples discrete palette cells. Preserve every unused texel and alpha.
    # Source cells: neutral facade at x=160..207, olive/grey roofs at x=208..255,
    # teal windows at x=160..175. UV/mesh inspection identifies cell 15 as low foliage.
    # Keep its role green even when the roof switches to coral or lavender.
    cells={10:'window',11:'wall',12:'wall',13:'roof',14:'roof',15:'foliage'}
    for name,style in styles.items():
        out=arr.copy()
        for index,role in cells.items():
            x=index*16;sl=arr[0:16,x:x+16,:3];orig=lab(sl);target=color(style[role]);a=np.median(orig.reshape(-1,3),axis=0)
            target_l=target[0]+({11:.0,12:.03,13:-.08,14:-.01}.get(index,0))
            edited=orig-a+target;edited[:,:,0]=orig[:,:,0]-a[0]+target_l
            out[0:16,x:x+16,:3]=rgb(edited)
        assert np.array_equal(out[:,:,3],arr[:,:,3]);assert np.array_equal(out[16:],arr[16:])
        Image.fromarray(out).save(OUT/name/'BGHouse001_palette.png')
    return {'source':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'cells':cells}

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--mesh-root',type=Path,required=True);ap.add_argument('--size',type=int,default=2048)
    ap.add_argument('--building',choices=['BGHouse002','BGHigh002','BGFactory001','BGHouse001']);ap.add_argument('--style');args=ap.parse_args()
    styles=json.loads((Path(__file__).parent/'palettes.json').read_text())
    if args.style:styles={args.style:styles[args.style]}
    overrides_path=Path(__file__).parent/'building_overrides.json'
    overrides=json.loads(overrides_path.read_text()) if overrides_path.exists() else {}
    names=[n for n in ['BGHouse002','BGHigh002','BGFactory001'] if not args.building or n==args.building]
    reports=[process(n,args.size,args.mesh_root,styles,overrides) for n in names]
    report={'algorithm':'Forge-derived OKLab anchor mapping + soft color gates + UV roof candidates','sourcePixelsUnmodified':True,'previewOnly':args.size<8192,'buildings':reports}
    if not args.building or args.building=='BGHouse001':report['palette']=palette(styles)
    filename='report.json' if not args.building and not args.style else f'report_{args.building or "all"}_{args.style or "all"}.json'
    (OUT/filename).write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
if __name__=='__main__':main()
