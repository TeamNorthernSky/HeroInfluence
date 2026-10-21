"""같은 풍경에서 천체를 분리한 시간대 미리보기용 HDR 레이어를 생성합니다."""
import bpy,sys,math
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];day=Path(args[0]);night=Path(args[1]);out=Path(args[2]);out.mkdir(parents=True,exist_ok=True)
def setup(source):
 bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;s.cycles.samples=24
 try:
  p=bpy.context.preferences.addons['cycles'].preferences;p.compute_device_type='OPTIX';p.get_devices()
  for d in p.devices:d.use=d.type=='OPTIX'
  s.cycles.device='GPU'
 except Exception:pass
 for o in s.objects:
  if o.type=='MESH':o.visible_camera=True
  if o.name.startswith('Visible HDR'):o.hide_render=True
 s.render.resolution_x=4096;s.render.resolution_y=2048;s.render.resolution_percentage=100;s.render.image_settings.file_format='OPEN_EXR';s.render.image_settings.color_depth='16';s.render.image_settings.exr_codec='ZIP'
 return s,[o for o in s.objects if o.name.startswith(('Cumulus density volume','Reflection high cloud'))]
def renderbase(s,clouds,name):
 for o in clouds:o.hide_render=True
 s.render.film_transparent=False;s.render.image_settings.color_mode='RGB';s.render.filepath=str(out/name);bpy.ops.render.render(write_still=True)
s,clouds=setup(day);renderbase(s,clouds,'JC_Time_Day_Base.exr')
# 暖색 잔광 원경. 해/달 자체는 게임 셰이더에서 독립 렌더합니다.
n=s.world.node_tree.nodes;r=next(x for x in n if x.bl_idname=='ShaderNodeValToRGB');r.color_ramp.elements[0].color=(.34,.12,.055,1);r.color_ramp.elements[1].color=(.045,.065,.18,1)
sun=bpy.data.objects['Day sun'];d=Vector((.5*math.cos(math.radians(-2)),.866*math.cos(math.radians(-2)),math.sin(math.radians(-2))));sun.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler();sun.data.energy=.35;sun.data.color=(1,.24,.07)
for mat in bpy.data.materials:
 if not mat.use_nodes:continue
 for node in mat.node_tree.nodes:
  if node.bl_idname=='ShaderNodeEmission':node.inputs['Strength'].default_value*=.3
  if node.bl_idname=='ShaderNodeVolumePrincipled':node.inputs['Emission Color'].default_value=(1,.32,.13,1)
renderbase(s,clouds,'JC_Time_Twilight_Base.exr')
for o in clouds:o.hide_render=False
for o in s.objects:
 if o.type=='MESH' and o not in clouds:o.visible_camera=False
s.render.film_transparent=True;s.render.image_settings.color_mode='RGBA';s.render.filepath=str(out/'JC_Time_Twilight_Clouds.exr');bpy.ops.render.render(write_still=True)
s,clouds=setup(night);renderbase(s,clouds,'JC_Time_Night_Base.exr')
print('TIME_LAYERS_COMPLETE',flush=True)
