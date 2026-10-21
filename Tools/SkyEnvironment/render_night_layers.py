"""Blender --background --python render_night_layers.py -- DAY_LAYERS_BLEND OUTPUT
낮 제작 원본의 카메라·지형·구름 위치를 그대로 재사용한 별도 밤 HDR 환경맵.
"""
import bpy,sys,math,json
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];source=Path(args[0]);out=Path(args[1]);out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;s.cycles.samples=32
try:
 prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='OPTIX';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='OPTIX'
 s.cycles.device='GPU'
except Exception:pass
clouds=[o for o in s.objects if o.name.startswith(('Cumulus density volume','Reflection high cloud'))]
layout={o.name:[list(o.location),list(o.rotation_euler),list(o.scale)] for o in s.objects if o.type in ('MESH','CAMERA')}
# 달 방향은 낮 태양과 같게 두어 배경 지형의 회전 없이 비교할 수 있습니다.
sun=bpy.data.objects['Day sun'];sun.name='Night moon light';sun.data.energy=.18;sun.data.color=(.58,.72,1)
orb=bpy.data.objects['Visible HDR sun'];orb.name='Visible HDR moon'
m=orb.data.materials[0];n=m.node_tree.nodes;l=m.node_tree.links;e=next(x for x in n if x.bl_idname=='ShaderNodeEmission');e.inputs[0].default_value=(.72,.84,1,1);e.inputs[1].default_value=3
tex=n.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=7;tex.inputs['Detail'].default_value=3
r=n.new('ShaderNodeValToRGB');r.color_ramp.elements[0].color=(.32,.4,.56,1);r.color_ramp.elements[1].color=(.8,.88,1,1);l.new(tex.outputs['Fac'],r.inputs[0]);l.new(r.outputs[0],e.inputs[0])
# 낮에 사용한 연출용 발광 채움도 밤의 에너지로 낮춥니다.
for mat in bpy.data.materials:
 if not mat.use_nodes or mat==m:continue
 for node in mat.node_tree.nodes:
  if node.bl_idname=='ShaderNodeEmission':node.inputs['Strength'].default_value*=.055
  if node.bl_idname=='ShaderNodeVolumePrincipled':
   sock=node.inputs['Emission Strength']
   if sock.is_linked:
    link=sock.links[0];prev=link.from_socket;mat.node_tree.links.remove(link);scale=mat.node_tree.nodes.new('ShaderNodeMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=.10;mat.node_tree.links.new(prev,scale.inputs[0]);mat.node_tree.links.new(scale.outputs[0],sock)
# 남청색 하늘, 은은한 지평선과 작은 별. 별은 고정 레이어에만 존재합니다.
n=s.world.node_tree.nodes;l=s.world.node_tree.links;ramp=next(x for x in n if x.bl_idname=='ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.025,.043,.085,1);ramp.color_ramp.elements[1].color=(.004,.009,.03,1)
bg=next(x for x in n if x.bl_idname=='ShaderNodeBackground');bg.inputs['Strength'].default_value=1
coord=next(x for x in n if x.bl_idname=='ShaderNodeTexCoord');v=n.new('ShaderNodeTexVoronoi');v.inputs['Scale'].default_value=95;l.new(coord.outputs['Normal'],v.inputs['Vector']);limit=n.new('ShaderNodeMath');limit.operation='LESS_THAN';limit.inputs[1].default_value=.025;l.new(v.outputs['Distance'],limit.inputs[0]);mix=n.new('ShaderNodeMixRGB');mix.blend_type='ADD';mix.inputs[0].default_value=1;l.new(ramp.outputs[0],mix.inputs[1]);l.new(limit.outputs[0],mix.inputs[2]);l.new(mix.outputs[0],bg.inputs['Color'])
s.render.resolution_x=4096;s.render.resolution_y=2048;s.render.resolution_percentage=100;s.render.image_settings.file_format='OPEN_EXR';s.render.image_settings.color_depth='16';s.render.image_settings.exr_codec='ZIP'
for o in s.objects:
 if o.type=='MESH':o.visible_camera=True
for o in clouds:o.hide_render=True
s.render.film_transparent=False;s.render.image_settings.color_mode='RGB';s.render.filepath=str(out/'JC_MeadowCoast_Night_Base.exr');bpy.ops.render.render(write_still=True)
for o in clouds:o.hide_render=False
for o in s.objects:
 if o.type=='MESH' and o not in clouds:o.visible_camera=False
s.render.film_transparent=True;s.render.image_settings.color_mode='RGBA';s.render.filepath=str(out/'JC_MeadowCoast_Night_Clouds.exr');bpy.ops.render.render(write_still=True)
for o in s.objects:
 if o.type=='MESH':o.visible_camera=True
s.render.film_transparent=False
bpy.ops.wm.save_as_mainfile(filepath=str(out/'MeadowCoast_Night.blend'))
# 검수용 합성은 생성된 선형 레이어를 Blender 합성기로 결합합니다.
import numpy as np
base=bpy.data.images.load(str(out/'JC_MeadowCoast_Night_Base.exr'));cloud=bpy.data.images.load(str(out/'JC_MeadowCoast_Night_Clouds.exr'));a=np.array(base.pixels[:],dtype=np.float32).reshape(-1,4);b=np.array(cloud.pixels[:],dtype=np.float32).reshape(-1,4);a[:,:3]=a[:,:3]*(1-b[:,3:4])+b[:,:3];a[:,3]=1
im=bpy.data.images.new('Night composite preview',width=4096,height=2048,float_buffer=True);im.pixels.foreach_set(a.ravel());s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGB';s.render.image_settings.color_depth='8';im.save_render(str(out/'night-preview.png'),scene=s)
(out/'night-report.json').write_text(json.dumps({'width':4096,'height':2048,'cloudCount':len(clouds),'source':str(source),'sameLayout':all([list(o.location),list(o.rotation_euler),list(o.scale)]==layout[o.name] for o in s.objects if o.name in layout),'maxHDR':float(a[:,:3].max()),'meanRGB':a[:,:3].mean(axis=0).tolist(),'cloudAlphaRange':[float(b[:,3].min()),float(b[:,3].max())]},indent=2))
print('NIGHT_LAYERS_COMPLETE',flush=True)
