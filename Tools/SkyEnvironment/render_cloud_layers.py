"""기존 제작 장면에서 고정 배경/움직이는 구름을 같은 좌표계로 분리 출력합니다."""
import bpy,sys,math,json
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];source=Path(args[0]);out=Path(args[1]);out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;s.cycles.samples=24
prefs=bpy.context.preferences.addons['cycles'].preferences
try:
 prefs.compute_device_type='OPTIX';prefs.get_devices()
 for d in prefs.devices:d.use=d.type=='OPTIX'
 s.cycles.device='GPU'
except Exception:pass
clouds=[o for o in s.objects if o.name.startswith('Cumulus density volume')]
# 높은 구름: 물의 반사 시야(대략 고도55도)에도 구름이 들어오도록 상공에 배치합니다.
for k,degrees in enumerate([0,70,140,210,280]):
 orig=clouds[k%len(clouds)];obj=orig.copy();obj.data=orig.data.copy();s.collection.objects.link(obj);obj.name='Reflection high cloud %02d'%k
 az=math.radians(degrees);obj.location=(math.cos(az)*2300,math.sin(az)*2300,3500);obj.rotation_euler[2]=az+math.pi/2;obj.scale=(950,500,850);clouds.append(obj)
s.render.image_settings.file_format='OPEN_EXR';s.render.image_settings.color_depth='16';s.render.image_settings.exr_codec='ZIP'
for o in clouds:o.hide_render=True
s.render.film_transparent=False;s.render.image_settings.color_mode='RGB';s.render.filepath=str(out/'JC_MeadowCoast_Base.exr');bpy.ops.render.render(write_still=True)
for o in clouds:o.hide_render=False
# 지형은 구름 조명에만 참여하고 카메라에는 보이지 않게 합니다. 하늘 알파는 볼륨 투과율입니다.
for o in s.objects:
 if o.type=='MESH' and o not in clouds:o.visible_camera=False
s.render.film_transparent=True;s.render.image_settings.color_mode='RGBA';s.render.filepath=str(out/'JC_MeadowCoast_Clouds.exr');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'MeadowCoast_Layers.blend'))
import numpy as np
im=bpy.data.images.load(str(out/'JC_MeadowCoast_Clouds.exr'));px=np.array(im.pixels[:],dtype=np.float32).reshape(-1,4)
(out/'layer-report.json').write_text(json.dumps({'maxRGB':float(px[:,:3].max()),'alphaMin':float(px[:,3].min()),'alphaMax':float(px[:,3].max()),'alphaMean':float(px[:,3].mean()),'width':s.render.resolution_x,'height':s.render.resolution_y},indent=2))
print('LAYERS_COMPLETE',flush=True)
