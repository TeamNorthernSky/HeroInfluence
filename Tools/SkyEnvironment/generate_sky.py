"""JC 스타일 하늘 제작. Blender --background --factory-startup --python generate_sky.py -- OUTPUT WIDTH
실제 제작용 3D 풍경을 Cycles panorama로 촬영. EXR는 scene-linear HDR, PNG는 검수용 톤맵.
"""
import bpy, math, random, sys, json
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
out=Path(args[0]);out.mkdir(parents=True,exist_ok=True);width=int(args[1]) if len(args)>1 else 2048
random.seed(42)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=int(args[2]) if len(args)>2 else 64;s.cycles.use_denoising=True
s.cycles.volume_bounces=2;s.cycles.volume_step_rate=.5;s.cycles.max_bounces=8;s.cycles.diffuse_bounces=2;s.cycles.glossy_bounces=2
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
 gpu=[d for d in prefs.devices if d.type=='OPTIX']
 if gpu:
  for d in prefs.devices:d.use=d.type=='OPTIX'
  s.cycles.device='GPU'
except Exception:pass
s.render.resolution_x=width;s.render.resolution_y=width//2;s.render.resolution_percentage=100
s.view_settings.view_transform='AgX';s.render.film_transparent=False
# 카메라
bpy.ops.object.camera_add(location=(0,0,180),rotation=(math.pi/2,0,0));cam=bpy.context.object;cam.name='Panorama';cam.data.type='PANO';cam.data.panorama_type='EQUIRECTANGULAR';cam.data.clip_end=100000;s.camera=cam
# 세계 하늘: 물리 절대 측광을 목표로 하지 않는 스타일 조명, 선형 HDR 에너지 보존.
w=bpy.data.worlds.new('Clear blue sky');s.world=w;w.use_nodes=True;n=w.node_tree.nodes;l=w.node_tree.links;n.clear()
t=n.new('ShaderNodeTexCoord');sep=n.new('ShaderNodeSeparateXYZ');l.new(t.outputs['Normal'],sep.inputs[0]);ramp=n.new('ShaderNodeValToRGB');flip=n.new('ShaderNodeMath');flip.operation='MULTIPLY';flip.inputs[1].default_value=-1;l.new(sep.outputs['Z'],flip.inputs[0]);l.new(flip.outputs[0],ramp.inputs[0])
ramp.color_ramp.elements[0].position=0;ramp.color_ramp.elements[0].color=(.44,.68,1,1)
ramp.color_ramp.elements[1].position=1;ramp.color_ramp.elements[1].color=(.04,.21,.72,1)
bg=n.new('ShaderNodeBackground');bg.inputs['Strength'].default_value=.8;l.new(ramp.outputs[0],bg.inputs[0]);wo=n.new('ShaderNodeOutputWorld');l.new(bg.outputs[0],wo.inputs[0])
# 재질

def mat(name,color,rough=1):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;return m
land=mat('Soft meadow',(.22,.39,.075));sand=mat('Pale shore',(.46,.43,.23));water=mat('Distant teal water',(.025,.22,.29),.22)
# 지면색에 저주파 변화를 추가
nt=land.node_tree;noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=.003;noise.inputs['Detail'].default_value=3;tex=nt.nodes.new('ShaderNodeTexCoord');nt.links.new(tex.outputs['Object'],noise.inputs['Vector']);cr=nt.nodes.new('ShaderNodeValToRGB');cr.color_ramp.elements[0].color=(.12,.28,.055,1);cr.color_ramp.elements[1].color=(.34,.49,.12,1);nt.links.new(noise.outputs['Fac'],cr.inputs[0]);nt.links.new(cr.outputs[0],nt.nodes.get('Principled BSDF').inputs['Base Color'])

nt=land.node_tree;n=nt.nodes;l=nt.links;geo=n.new('ShaderNodeNewGeometry');sep=n.new('ShaderNodeSeparateXYZ');l.new(geo.outputs['Position'],sep.inputs[0]);mr=n.new('ShaderNodeMapRange');mr.inputs['From Min'].default_value=-1;mr.inputs['From Max'].default_value=6;l.new(sep.outputs['Z'],mr.inputs[0]);mix=n.new('ShaderNodeMixRGB');mix.inputs[1].default_value=(.38,.37,.22,1);l.new(mr.outputs[0],mix.inputs[0]);l.new(cr.outputs[0],mix.inputs[2]);l.new(mix.outputs[0],n.get('Principled BSDF').inputs['Base Color'])

# 완만한 구릉과 북쪽 만. 땅은 실제 연속 메시이며 건물/수목은 넣지 않습니다.
verts=[];faces=[];N=481;extent=12000
for j in range(N):
 y=-extent/2+extent*j/(N-1)
 for i in range(N):
  x=-extent/2+extent*i/(N-1)
  z=18+48*math.sin(x*.0018+.2)*math.cos(y*.0022)+18*math.sin(x*.004+y*.0017)
  z+=150*math.exp(-((x+1700)**2+(y+900)**2)/900000)+110*math.exp(-((x-1200)**2+(y+1700)**2)/600000)
  z+=5*math.sin(x*.022+y*.013)*math.sin(y*.019)+2*math.sin(x*.05-y*.029)
  coast=650+320*math.sin(x*.0011)+160*math.sin(x*.0027)
  sea=1/(1+math.exp(max(-60,min(60,-(y-coast)/180))))
  z=z*(1-sea)-35*sea
  verts.append((x,y,z))
for j in range(N-1):
 for i in range(N-1):
  a=j*N+i;faces.append((a,a+1,a+N+1,a+N))
mesh=bpy.data.meshes.new('Rolling terrain');mesh.from_pydata(verts,[],faces);mesh.update();ob=bpy.data.objects.new('Green hills and bay',mesh);s.collection.objects.link(ob);ob.data.materials.append(land);ob.data.materials.append(sand)
for p in mesh.polygons:
 p.use_smooth=True

bpy.ops.mesh.primitive_plane_add(size=80000,location=(0,0,-1));sea=bpy.context.object;sea.name='Distant sea';sea.data.materials.append(water)
# 바다: 연안 청록색에서 먼 군청색으로, 중간 물결과 가는 반사 요철을 분리합니다.
nt=water.node_tree;n=nt.nodes;l=nt.links;bs=n.get('Principled BSDF');bs.inputs['Roughness'].default_value=.19;bs.inputs['Specular IOR Level'].default_value=.32
tex=n.new('ShaderNodeTexCoord');sep=n.new('ShaderNodeSeparateXYZ');l.new(tex.outputs['Object'],sep.inputs[0]);mr=n.new('ShaderNodeMapRange');mr.inputs['From Min'].default_value=350;mr.inputs['From Max'].default_value=1900;l.new(sep.outputs['Y'],mr.inputs[0]);r=n.new('ShaderNodeValToRGB');r.color_ramp.elements[0].color=(.012,.27,.31,1);r.color_ramp.elements[1].color=(.006,.025,.16,1);l.new(mr.outputs[0],r.inputs[0]);l.new(r.outputs[0],bs.inputs['Base Color'])
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=.09;noise.inputs['Detail'].default_value=3;noise.inputs['Roughness'].default_value=.65;l.new(tex.outputs['Object'],noise.inputs['Vector']);bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.32;bump.inputs['Distance'].default_value=1.3;l.new(noise.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs['Normal'],bs.inputs['Normal'])
em=n.new('ShaderNodeEmission');l.new(r.outputs[0],em.inputs[0]);blend=n.new('ShaderNodeMixShader');blend.inputs[0].default_value=.65;l.new(bs.outputs[0],blend.inputs[1]);l.new(em.outputs[0],blend.inputs[2]);l.new(blend.outputs[0],n.get('Material Output').inputs['Surface'])
# 먼 능선: 녹색 사면·바위색·대기 원근을 갖는 연속적인 산지.
mountain=mat('Layered distant slopes',(.18,.26,.24));nt=mountain.node_tree;n=nt.nodes;l=nt.links;bs=n.get('Principled BSDF')
geom=n.new('ShaderNodeNewGeometry');sep=n.new('ShaderNodeSeparateXYZ');l.new(geom.outputs['Normal'],sep.inputs[0]);r=n.new('ShaderNodeValToRGB');r.color_ramp.elements[0].position=.65;r.color_ramp.elements[0].color=(.24,.23,.22,1);r.color_ramp.elements[1].position=.97;r.color_ramp.elements[1].color=(.15,.27,.10,1);l.new(sep.outputs[0],r.inputs[0]);l.new(r.outputs[0],bs.inputs['Base Color'])
path=n.new('ShaderNodeLightPath');mr=n.new('ShaderNodeMapRange');mr.inputs['From Min'].default_value=3000;mr.inputs['From Max'].default_value=18000;mr.inputs['To Min'].default_value=.12;mr.inputs['To Max'].default_value=.72;l.new(path.outputs['Ray Length'],mr.inputs[0]);em=n.new('ShaderNodeEmission');em.inputs[0].default_value=(.19,.29,.46,1);mix=n.new('ShaderNodeMixShader');l.new(mr.outputs[0],mix.inputs[0]);l.new(bs.outputs[0],mix.inputs[1]);l.new(em.outputs[0],mix.inputs[2]);l.new(mix.outputs[0],n.get('Material Output').inputs['Surface'])
for idx,(cx,cy,sx,sy,high) in enumerate([(-4000,6500,3000,1700,550),(2400,10500,4300,2200,760),(9000,1800,2900,3500,640),(-9000,-2500,3400,4000,850),(1500,-9500,4500,2700,900)]):
 vs=[];fs=[];size=161
 for j in range(size):
  y=(j/(size-1)*2-1)*sy
  for i in range(size):
   x=(i/(size-1)*2-1)*sx;d=(x/sx)**2+(y/sy)**2
   envelope=max(0,1-d)**1.6
   ridges=.65+.22*math.sin(x*.0017+y*.0009)+.13*math.sin(x*.0043-y*.0027)
   z=-20+high*envelope*ridges+envelope*(12*math.sin(x*.018+y*.006)+6*math.sin(y*.034-x*.02))
   vs.append((x+cx,y+cy,z))
 for j in range(size-1):
  for i in range(size-1):
   v=j*size+i;fs.append((v,v+1,v+size+1,v+size))
 me=bpy.data.meshes.new('Ridge mesh');me.from_pydata(vs,[],fs);me.update();obj=bpy.data.objects.new('Distant ridge %02d'%idx,me);s.collection.objects.link(obj);me.materials.append(mountain)
 for f in me.polygons:f.use_smooth=True
# 여러 규모의 밀도 덩어리와 침식 노이즈를 합성한 적운. 경계 메시 표면은 렌더하지 않습니다.
cloud=bpy.data.materials.new('Cumulus sculpted density');cloud.use_nodes=True;n=cloud.node_tree.nodes;l=cloud.node_tree.links;n.clear();coord=n.new('ShaderNodeTexCoord')
def mathnode(op,a,b):
 node=n.new('ShaderNodeMath');node.operation=op
 for i,v in enumerate([a,b]):
  if isinstance(v,(int,float)):node.inputs[i].default_value=v
  else:l.new(v,node.inputs[i])
 return node.outputs[0]
warp=n.new('ShaderNodeTexNoise');warp.noise_dimensions='4D';warp.inputs['Scale'].default_value=5;warp.inputs['Detail'].default_value=2;l.new(coord.outputs['Generated'],warp.inputs['Vector']);info=n.new('ShaderNodeObjectInfo');l.new(mathnode('MULTIPLY',info.outputs['Random'],10),warp.inputs['W']);scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=.14;l.new(warp.outputs['Color'],scale.inputs[0]);add=n.new('ShaderNodeVectorMath');add.operation='ADD';padding=n.new('ShaderNodeVectorMath');padding.operation='MULTIPLY_ADD';padding.inputs[1].default_value=(1.5,1.5,1.5);padding.inputs[2].default_value=(-.25,-.25,-.25);l.new(coord.outputs['Generated'],padding.inputs[0]);l.new(padding.outputs[0],add.inputs[0]);l.new(scale.outputs[0],add.inputs[1])
# 정규화된 볼륨 내부에 낮은 바닥층과 솟아오른 탑을 배치합니다.
density=None
lobes=[(.16,.5,.19,.18),(.32,.5,.20,.23),(.5,.5,.20,.26),(.69,.5,.19,.23),(.84,.5,.17,.16),(.35,.5,.4,.23),(.52,.5,.47,.27),(.56,.5,.68,.23),(.7,.5,.4,.20),(.21,.5,.32,.17)]
random.seed(81)
for cx,cy,cz,radius in lobes:
 for j in range(4):
  center=(cx,cy,cz) if j==0 else (cx+random.uniform(-.11,.11),cy+random.uniform(-.14,.14),cz+random.uniform(-.09,.13))
  rad=radius if j==0 else radius*random.uniform(.45,.7)
  dist=n.new('ShaderNodeVectorMath');dist.operation='DISTANCE';l.new(add.outputs[0],dist.inputs[0]);dist.inputs[1].default_value=center
  field=mathnode('SUBTRACT',1,mathnode('DIVIDE',dist.outputs['Value'],rad))
  density=field if density is None else mathnode('MAXIMUM',density,field)
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=32;noise.inputs['Detail'].default_value=4;noise.inputs['Roughness'].default_value=.7;l.new(coord.outputs['Generated'],noise.inputs['Vector'])
density=mathnode('SUBTRACT',density,mathnode('MULTIPLY',noise.outputs['Fac'],.48));density=mathnode('MAXIMUM',density,0);density=mathnode('MULTIPLY',density,.12)
vol=n.new('ShaderNodeVolumePrincipled');vol.inputs['Color'].default_value=(1,1,1,1);vol.inputs['Anisotropy'].default_value=.15;l.new(density,vol.inputs['Density']);vol.inputs['Emission Color'].default_value=(.72,.82,1,1);l.new(mathnode('MULTIPLY',density,.20),vol.inputs['Emission Strength']);o=n.new('ShaderNodeOutputMaterial');l.new(vol.outputs[0],o.inputs['Volume'])
# 큰 적운군 사이에 맑은 하늘을 남깁니다. 위치는 밤 버전에도 그대로 재사용합니다.
for k,(degrees,dist,height,size,tall) in enumerate([(15,4800,2100,800,1.6),(65,5200,1750,1000,1.0),(125,4200,1600,650,1.1),(195,5600,2300,1250,1.4),(250,4500,1550,650,1.1),(310,5500,1750,800,1.2),(100,2400,1700,160,.8),(285,2600,1850,180,.9)]):
 az=math.radians(degrees);bpy.ops.mesh.primitive_cube_add(size=2,location=(math.cos(az)*dist,math.sin(az)*dist,height));obj=bpy.context.object;obj.name='Cumulus density volume %02d'%k;obj.scale=(size*2.8,size*1.2,size*tall*1.7);obj.rotation_euler[2]=az+math.pi/2;variant=cloud.copy();variant.name='Cloud density variant %02d'%k
 for node in variant.node_tree.nodes:
  if node.bl_idname=='ShaderNodeVectorMath' and node.operation=='DISTANCE':
   v=node.inputs[1].default_value;node.inputs[1].default_value=(v[0]+random.uniform(-.07,.07),v[1]+random.uniform(-.06,.06),v[2]+random.uniform(-.07,.07))
  if node.bl_idname=='ShaderNodeMath' and node.operation=='DIVIDE':node.inputs[1].default_value*=random.uniform(.85,1.15)
 obj.data.materials.append(variant)
# 낮 태양: 현재 게임 라이트 고도에 맞춰 조형과 조명을 함께 배치.
az=math.radians(30);el=math.radians(41.5);direction=Vector((math.sin(az)*math.cos(el),math.cos(az)*math.cos(el),math.sin(el)))
bpy.ops.object.light_add(type='SUN');sun=bpy.context.object;sun.name='Day sun';sun.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler();sun.data.energy=4.0;sun.data.angle=math.radians(2);sun.data.color=(1,.94,.83)
bpy.ops.mesh.primitive_uv_sphere_add(segments=48,ring_count=24,radius=110,location=direction*15000+Vector((0,0,180)));orb=bpy.context.object;orb.name='Visible HDR sun';em=bpy.data.materials.new('Sun radiance');em.use_nodes=True;en=em.node_tree.nodes;en.clear();e=en.new('ShaderNodeEmission');e.inputs[0].default_value=(1,.94,.78,1);e.inputs[1].default_value=24;o=en.new('ShaderNodeOutputMaterial');em.node_tree.links.new(e.outputs[0],o.inputs[0]);orb.data.materials.append(em)
# 검수용 제작 원본. 게임 Assets 밖에 보관합니다.
bpy.ops.wm.save_as_mainfile(filepath=str(out/'MeadowCoast.blend'))
s.render.image_settings.file_format='OPEN_EXR';s.render.image_settings.color_depth='16';s.render.image_settings.color_mode='RGB';s.render.image_settings.exr_codec='ZIP';s.render.filepath=str(out/'JC_MeadowCoast_Day.exr');bpy.ops.render.render(write_still=True)
s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.render.image_settings.color_depth='8';bpy.data.images['Render Result'].save_render(str(out/'day-preview.png'),scene=s)
# HDR 검증값. EXR에 실제 1 초과 에너지가 존재해야 합니다.
im=bpy.data.images.load(str(out/'JC_MeadowCoast_Day.exr'));import numpy as np
pixels=np.array(im.pixels[:],dtype=np.float32).reshape(-1,4)[:,:3]
(out/'render-report.json').write_text(json.dumps({'width':width,'height':width//2,'min':float(pixels.min()),'max':float(pixels.max()),'mean':pixels.mean(axis=0).tolist(),'sun_elevation':41.5,'seed':42},indent=2))
print('SKY_RENDER_COMPLETE',flush=True)




