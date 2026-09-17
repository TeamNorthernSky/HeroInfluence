"""Geometry-bounded part masks. No global roof/foliage color fallback.

Run with Python -B. v2 remains the source of accepted extra-part boundaries and
stable color anchors. This pass changes coverage only, never profile values.
"""
from pathlib import Path
import json, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import generate_masks_v2 as v2
import geometry_parts as geo

ROOT=v2.ROOT
BODY={'BGFactory001':[0,10,14,16,26], 'BGFactory002':[0,22,24],
      'BGHigh002':[0], 'BGHigh003':[0,5], 'BGHigh004':[0,2,3,6,7,11,13,16,19,20,21,22,23],
      'BGHouse002':[0,39], 'BGOffice002':[0,3,9,10,17,18],
      'BGRowHouse001':[0,7,16], 'BGRowHouse002':[0],
      'BGStore002':[0,23,24,78,81], 'BGStore003':[0],
      'BGStore004':[6,10,13,14], 'BGStore005':[0,31,44,46,57,58,59],
      'BGWarehouse_001':[3,17,25,29,32,33,36,37,38,46,47,48],
      'BGWarehouse_002':[0,18,20,21,22,23,25,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45]+list(range(46,54))+[57,58]+list(range(61,86))}
BASE={'BGFactory001':8,'BGFactory002':3,'BGHigh002':1,'BGHigh003':1,'BGHigh004':1,'BGHouse002':26,'BGOffice002':2,'BGRowHouse001':1,'BGRowHouse002':1,'BGStore002':12,'BGStore003':4,'BGStore004':2,'BGStore005':12,'BGWarehouse_001':15,'BGWarehouse_002':19}
PLANTS={'BGFactory001':[13,15], 'BGFactory002':[11,12], 'BGHigh002':[3,4], 'BGHigh003':[2,3], 'BGHigh004':[4,5],
        'BGHouse002':[2,3,4,5,6,7,8,9,10,12,13,14,15,16,17,18,19,20,21,22,23,24,25,35,37,43,44],
        'BGOffice002':[4,5,11,12], 'BGRowHouse001':[], 'BGRowHouse002':[11,12,13,14,15,16],
        'BGStore002':list(range(21,23))+[26,28,29,30,31,32,33,34]+list(range(39,48))+[49,50,51,52],
        'BGStore003':list(range(11,23))}
# Roof rim heights apply to shell triangles only. Rooftop machines are excluded.
RIM={'BGFactory001':1.39,'BGFactory002':.82,'BGHigh002':3.35,'BGHigh004':3.32,
     'BGOffice002':1.86,'BGRowHouse001':2.185,'BGRowHouse002':1.952,
     'BGStore002':1.02,'BGStore003':.955,'BGStore004':1.31,'BGStore005':1.095}
ROOF_GROUPS={'BGFactory001':[26],'BGFactory002':[22,36,37,38],'BGHigh003':[4],
             'BGOffice002':[3,9,10,17,18],
             'BGRowHouse001':[31],
             'BGStore002':[1],'BGStore004':[13],'BGStore005':[20,31,44],
             'BGWarehouse_001':[20,21,22,23,25,36,37,38,46,47],
             'BGWarehouse_002':[20,21,35,36,38,39,40,41,42,43,45]}

def classify(row,p,u,t):
    oldrole,over,old=v2.classify(row,p,u,t)
    if row['palette']:
        equipment={'BGHouse001':[4],'BGOffice001':[0,33],'BGStore001':[7,22,23,24,25,26],'BGHigh001':[8,12]}
        groups=geo.components(p,t)
        for idx in equipment[row['name']]:oldrole[groups[idx]]=15
        if row['name']=='BGOffice001':
            c=p[t].mean(1);outer=(oldrole==5)&((c[:,0]<-.78)|(c[:,0]>.74)|(c[:,2]<-.568)|(c[:,2]>.716));oldrole[outer]=1
        return oldrole,old
    if not row['source']:return np.full(len(t),15,np.int32),old
    name=row['name'];groups=geo.components(p,t);v=p[t];c=v.mean(1)
    cr=np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]);n=cr/np.maximum(np.linalg.norm(cr,axis=1)[:,None],1e-9)
    role=np.full(len(t),15,np.int32)
    def put(ids,part):
        for i in ids:role[groups[i]]=part
    put(BODY[name],0);put([BASE[name]],4);put(PLANTS.get(name,[]),3)
    if name in RIM:
        shell=groups[6] if name=='BGStore004' else groups[0]
        role[shell[v[shell,:,1].min(1)>RIM[name]]]=1
    put(ROOF_GROUPS.get(name,[]),1)
    if name=='BGHouse002':
        shell=groups[0]
        # Gable walls remain walls; sloping roof and its narrow edge strips only.
        roof=(c[shell,1]>1.22)&(np.abs(n[shell,1])>.25)
        role[shell[roof]]=1
        # Small entrance/window canopies, limited to shallow projecting faces.
        awn=(v[:,:,1].min(1)>.81)&(v[:,:,1].max(1)<1.01)&(c[:,2]<-.73)&(c[:,0]>0)&(c[:,0]<.56)
        role[awn&(role==0)]=1
    if name=='BGWarehouse_001':
        shell=groups[3];role[shell[(c[shell,1]>.70)&(np.abs(n[shell,1])>.55)]]=1
    if name=='BGWarehouse_002':
        shell=groups[0];role[shell[(c[shell,1]>.725)&(np.abs(n[shell,1])>.10)]]=1
    if name=='BGRowHouse001':
        # Plant crowns are disconnected from pots, lamps, balcony rails and shell.
        for g in groups:
            lo=p[t[g]].min((0,1));hi=p[t[g]].max((0,1))
            if lo[1]>.30 and hi[1]<.70 and hi[2]<-.64 and (hi[0]<-.7 or lo[0]>.7):role[g]=3
        awn=(v[:,:,1].min(1)>.72)&(v[:,:,1].max(1)<.84)&(c[:,2]<-.45)
        role[awn&(role==0)]=1
    if name=='BGRowHouse002':
        awn=(v[:,:,1].min(1)>.66)&(v[:,:,1].max(1)<.79)&(c[:,2]<-.48)
        role[awn&(role==0)]=1
    # Explicit v2 roof floors, sign face/rim, pots, soil and corrected sill win.
    if name=='BGStore002':
        over[(over==5)&(~np.isin(np.arange(len(t)),groups[77]))]=-1
    if name=='BGStore003':
        # The two-level inner slab belongs to both shell and separate slab meshes.
        inner=(c[:,0]>-1.04)&(c[:,0]<.98)&(c[:,2]>-.68)&(c[:,2]<1.05)
        over[(over==5)&(~inner)]=-1
    role[over>=0]=over[over>=0]
    if name=='BGHigh004':
        # Removing the floor disconnects the small rooftop machines from each rim.
        roof=np.where(role==1)[0]
        for group in geo.components(p,t[roof]):
            tri=roof[group];span=np.ptp(p[t[tri]].reshape(-1,3),axis=0)
            if span[0]<.30 and span[2]<.30:role[tri]=15
    if name=='BGStore004':
        # Branding stripes are not glass, even where their green matches windows.
        g=groups[6];role[g[(role[g]==0)&(c[g,1]<1.31)]]=15
    # A separate modeled glass face remains glass through baked shadows.
    # For polygons with mixed paint, the pixel mask below resolves the boundary.
    bary=np.array([[1/3]*3,[.6,.2,.2],[.2,.6,.2],[.2,.2,.6],[.45,.45,.1],[.45,.1,.45],[.1,.45,.45]])
    sample=np.einsum('sj,tjk->tsk',bary,u[t]);sz=old.shape[0]
    xx=np.clip(sample[:,:,0]*sz,0,sz-1).astype(int);yy=np.clip((1-sample[:,:,1])*sz,0,sz-1).astype(int)
    glass=np.percentile(old[yy,xx,2],25,axis=1)>.12
    role[(role==0)&glass]=2
    return role,old

def raster(uv,tri,values,size):
    im=Image.new('I',(size,size),-1);d=ImageDraw.Draw(im)
    for v,k in zip(uv[tri],values):
        if k<0:continue
        d.polygon([(float(x*size-.5),float((1-y)*size-.5)) for x,y in v],fill=int(k))
    return np.array(im)

def main():
    out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
    rows=json.loads((ROOT/'Data/inventory_v2.json').read_text(encoding='utf-8'))['bindings'];report=[]
    debug=np.array([[210,205,195],[240,60,45],[60,150,210],[60,190,60],[110,110,110],[185,185,195],[245,180,30],[185,75,200],[65,70,180],[190,115,65],[70,40,20],[0,0,0],[0,0,0],[0,0,0],[0,0,0],[225,220,210]],np.uint8)
    for row in rows:
        key=row['key'];data=[]
        for p,u,t in geo.load(key):
            role,old=classify(row,p,u,t);data.append((p,u,t,role,old))
            geo.render(p,t,debug[role],out/(key+'_regions.png'))
        if row['palette']:
            with (ROOT/'Data'/(key+'_roles.bytes')).open('wb') as f:
                f.write(np.array([len(data)],'<i4').tobytes())
                for p,u,t,role,old in data:
                    f.write(np.array([len(role)],'<i4').tobytes());f.write(role.astype('<i4').tobytes())
            report.append(dict(key=key,paletteIsolated=True));continue
        size=2048 if row['source'] else 32;count=len(row['partIds'])
        coverage={};conflicts=0;pairs=[]
        for p,u,t,role,old in data:
            for gid in np.unique(role):
                mask=raster(u,t,np.where(role==gid,gid,-1),size)>=0
                coverage[int(gid)]=coverage.get(int(gid),False)|mask
        cores={i:np.asarray(Image.fromarray(m.astype('uint8')*255).filter(ImageFilter.MinFilter(3)))>0 for i,m in coverage.items()}
        graph={i:set() for i in coverage}
        for i,a in cores.items():
            for j,b in cores.items():
                if j<=i:continue
                overlap=int(np.count_nonzero(a&b))
                if overlap:graph[i].add(j);graph[j].add(i);conflicts+=overlap;pairs.append([i,j,overlap])
        # Color only the conflict graph: two pages for House002 / RowHouse001,
        # three pages for Warehouse002. Original texel density is retained.
        pages={}
        for gid in sorted(graph,key=lambda i:(-len(graph[i]),i)):
            used={pages[j] for j in graph[gid] if j in pages};page=0
            while page in used:page+=1
            pages[gid]=page
        pagecount=max(pages.values())+1;cols=2 if pagecount>1 else 1;rowsCount=(pagecount+cols-1)//cols
        if pagecount>4:raise RuntimeError('Unexpected UV conflict graph: '+key)
        width=size*cols;height=size*rowsCount
        weights=np.zeros((height,width,count),np.float32);domains=[]
        for page in range(pagecount):
            domain=np.full((size,size),-1,np.int32)
            for p,u,t,role,old in data:
                values=np.array([r if pages[int(r)]==page else -1 for r in role])
                rr=raster(u,t,values,size);domain[rr>=0]=rr[rr>=0]
            w=np.zeros((size,size,count),np.float32);old=data[0][-1]
            window=(np.asarray(Image.fromarray(old[:,:,2]).resize((size,size),Image.Resampling.BILINEAR))>.12).astype(np.float32)
            # Glass color detail is permitted only inside the building shell.
            shell=domain==0;w[:,:,2]=window*shell;w[:,:,0]=(1-window)*shell
            for gid in np.unique(domain):
                if gid<=0 or gid==15:continue
                id=v2.IDS[gid]
                if id in row['partIds']:w[:,:,row['partIds'].index(id)][domain==gid]=1
            # Grow into empty space only, never into another occupied UV chart.
            filled=domain>=0
            for step in range(4):
                prev=w.copy();have=filled.copy()
                for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
                    sy=slice(max(0,-dy),min(size,size-dy));sx=slice(max(0,-dx),min(size,size-dx))
                    ty=slice(max(0,dy),min(size,size+dy));tx=slice(max(0,dx),min(size,size+dx))
                    sel=(~filled[ty,tx])&have[sy,sx]
                    w[ty,tx][sel]=prev[sy,sx][sel];filled[ty,tx][sel]=True
            x=page%cols*size;y=(rowsCount-1-page//cols)*size
            weights[y:y+size,x:x+size]=w;domains.append(domain)
        if pagecount>1:
            seedPath=ROOT/'Data'/row['derivedSeed'] if row.get('derivedSeed') else v2.PROJECT/row['seed']
            source=np.asarray(Image.open(seedPath).convert('RGB').resize((size,size),Image.Resampling.LANCZOS))
            Image.fromarray(np.tile(source,(rowsCount,cols,1))).save(ROOT/'Data'/(key+'_atlas.png'))
            with (ROOT/'Data'/(key+'_roles.bytes')).open('wb') as f:
                f.write(np.array([len(data)],'<i4').tobytes())
                for p,u,t,role,old in data:
                    f.write(np.array([len(role)],'<i4').tobytes());f.write(np.array([pages[int(r)] for r in role],'<i4').tobytes())
            row.update(geometryAtlas=True,atlasColumns=cols,atlasRows=rowsCount)
        for suffix,indices in [('parts',range(4)),('floor',[4]),('extra',range(5,9)),('extra2',range(9,13))]:
            arr=np.stack([weights[:,:,i] if i<count else np.zeros((height,width)) for i in indices],2)
            arr=np.uint8(np.clip(np.round(arr*255),0,255));Image.fromarray(arr[:,:,0] if arr.shape[2]==1 else arr).save(ROOT/'Data'/(key+'_'+suffix+'.png'))
        record=dict(key=key,interiorOverlapTexels=conflicts,conflictPairs=pairs,pages=pagecount,pageByRole=pages,
                    remainingInteriorConflicts=sum(n for i,j,n in pairs if pages[i]==pages[j]),
                    coveredTexels=sum(int((d>=0).sum()) for d in domains),partTexels={id:int((weights[:,:,i]>.5).sum()) for i,id in enumerate(row['partIds'])})
        report.append(record);print(key,'overlap',conflicts,'pages',pagecount,flush=True)
    (ROOT/'Data/inventory_v3.json').write_text(json.dumps({'bindings':rows},ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'coverage_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
if __name__=='__main__':main()
