"""Blender CLI: 명시한 FBX/Blend만 읽고 선택한 경로로 애니메이션을 내보냅니다.

blender --background --factory-startup --python export_animation.py -- \
  --source <fbx-or-blend> --out <fbx> [--rate 3] [--save-blend <blend>]
rate=1은 현재 동작 유지, rate=3은 현재 동작의 시간을 1/3로 압축합니다.
수정된 .blend를 다시 내보낼 때에는 --rate 1을 사용합니다.
"""
import argparse
import json
from pathlib import Path
import sys
import bpy

args = argparse.ArgumentParser()
args.add_argument('--source', required=True)
args.add_argument('--out', required=True)
args.add_argument('--rate', type=float, default=1.0)
args.add_argument('--save-blend')
options = args.parse_args(sys.argv[sys.argv.index('--') + 1:])
if options.rate <= 0:
    raise ValueError('재생 배수는 0보다 커야 합니다.')
source, output = Path(options.source).resolve(), Path(options.out).resolve()
if source == output:
    raise ValueError('입력 원본과 출력 경로를 분리하세요.')
if source.suffix.lower() == '.blend':
    bpy.ops.wm.open_mainfile(filepath=str(source))
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=False)
armatures = [obj for obj in bpy.data.objects if obj.type == 'ARMATURE']
if len(armatures) != 1:
    raise ValueError('이 내보내기는 단일 뼈대 작업 파일을 대상으로 합니다.')
armature = armatures[0]
action = armature.animation_data.action if armature.animation_data else None
if action is None:
    raise ValueError('뼈대의 활성 동작(Action)이 없습니다.')
start, end = map(float, action.frame_range)
curves = []
for layer in action.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            curves.extend(bag.fcurves)
# 키를 버리지 않고 프레임 속도를 높여 시간을 압축합니다.
# 30fps의 91개 포즈를 3배속이면 90fps의 91개 포즈로 보존합니다.
scene = bpy.context.scene
effective_fps = scene.render.fps / scene.render.fps_base * options.rate
scene.render.fps = round(effective_fps)
scene.render.fps_base = scene.render.fps / effective_fps
scene.frame_start = round(start)
scene.frame_end = round(end)
scene.frame_set(scene.frame_start)
bpy.ops.object.select_all(action='DESELECT')
armature.select_set(True)
bpy.context.view_layer.objects.active = armature
if options.save_blend:
    blend = Path(options.save_blend).resolve()
    if blend == source:
        raise ValueError('입력 파일을 덮어쓰지 말고 별도 작업 파일로 저장하세요.')
    blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=str(output), use_selection=True, object_types={'ARMATURE'},
    axis_forward='-Z', axis_up='Y', global_scale=1.0, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_NONE', add_leaf_bones=False,
    use_armature_deform_only=False, bake_anim=True,
    bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
    bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
print('JC_ANIMATION_EXPORT ' + json.dumps({
    'source': str(source), 'output': str(output), 'rate': options.rate,
    'frames': [scene.frame_start, scene.frame_end],
    'fps': scene.render.fps / scene.render.fps_base,
    'bones': len(armature.data.bones), 'curves': len(curves)}, ensure_ascii=True))
