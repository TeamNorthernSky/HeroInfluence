"""Blender 제작 리그의 최종 포즈를 원래 31본에 베이크하여 FBX 출력.

--source 작업.blend --out 출력.fbx [--action JE_02_Strike] [--start 1 --end 31]
작업 .blend를 저장/수정하지 않습니다. Unity에 자동 연결하지 않습니다.
"""
import argparse
import json
from pathlib import Path
import sys
import bpy


def export_animation(output, action_name=None, start=None, end=None):
    candidates = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE' and o.get('jc_rig_version') == 1]
    if len(candidates) != 1:
        raise ValueError('JC 제작 리그 하나가 필요합니다.')
    source = candidates[0]
    names = json.loads(source['jc_original_bones'])
    if action_name:
        action = bpy.data.actions.get(action_name)
        if action is None:
            raise ValueError('지정한 Action이 없습니다: ' + action_name)
        source.animation_data_create()
        source.animation_data.action = action
    action = source.animation_data.action if source.animation_data else None
    if action is None:
        raise ValueError('출력할 활성 Action이 없습니다.')
    scene = bpy.context.scene
    first = scene.frame_start if start is None else start
    last = scene.frame_end if end is None else end
    if last <= first:
        raise ValueError('출력 끝 프레임은 시작보다 커야 합니다.')
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    # 실제 변형 뼈대만 별도 사본으로 만듭니다. 제작용 제약/도형은 FBX에 포함하지 않습니다.
    result = bpy.data.objects.new('Armature', source.data.copy())
    scene.collection.objects.link(result)
    result.matrix_world = source.matrix_world.copy()
    result.animation_data_clear()
    bpy.ops.object.select_all(action='DESELECT')
    result.select_set(True)
    bpy.context.view_layer.objects.active = result
    for collection in result.data.collections:
        collection.is_visible = True
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in list(result.data.edit_bones):
        if bone.name not in names:
            result.data.edit_bones.remove(bone)
    bpy.ops.object.mode_set(mode='POSE')
    for bone in result.pose.bones:
        bone.select = True
        constraint = bone.constraints.new('COPY_TRANSFORMS')
        constraint.target, constraint.subtarget = source, bone.name
        constraint.owner_space = constraint.target_space = 'POSE'
    scene.frame_set(first)
    bpy.ops.nla.bake(frame_start=first, frame_end=last, step=1, only_selected=False,
                    visual_keying=True, clear_constraints=True, clear_parents=False,
                    use_current_action=False, clean_curves=False, bake_types={'POSE'})
    result.animation_data.action.name = action.name + '_Baked'
    bpy.ops.object.mode_set(mode='OBJECT')
    # 베이크 전후를 비교합니다. 제작 리그는 원본 Action/NLA를 그대로 평가합니다.
    max_position, max_rotation = 0.0, 0.0
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        for name in names:
            a, b = source.pose.bones[name].matrix, result.pose.bones[name].matrix
            max_position = max(max_position, (a.translation - b.translation).length)
            q = a.to_quaternion().rotation_difference(b.to_quaternion())
            max_rotation = max(max_rotation, min(q.angle, abs(6.283185307179586 - q.angle)))
    if max_position > .0001 or max_rotation > .002:
        raise RuntimeError(f'베이크 검증 실패: 위치 {max_position}m, 회전 {max_rotation}rad')
    scene.frame_start, scene.frame_end = first, last
    scene.frame_set(first)
    bpy.ops.object.select_all(action='DESELECT')
    result.select_set(True)
    bpy.context.view_layer.objects.active = result
    output = Path(output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(output), use_selection=True, object_types={'ARMATURE'},
        axis_forward='-Z', axis_up='Y', global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE', add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=True,
        bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1, bake_anim_simplify_factor=0)
    return dict(output=str(output), action=action.name, frames=[first, last], bones=len(names),
                fps=scene.render.fps / scene.render.fps_base,
                max_bake_position_error=max_position, max_bake_rotation_error_radians=max_rotation)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', required=True)
    parser.add_argument('--out', required=True)
    parser.add_argument('--action')
    parser.add_argument('--start', type=int)
    parser.add_argument('--end', type=int)
    parser.add_argument('--report')
    options = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source_path, output_path = Path(options.source).resolve(), Path(options.out).resolve()
    if source_path == output_path or output_path.suffix.lower() != '.fbx':
        raise ValueError('작업 파일과 분리된 .fbx 출력 경로를 지정하세요.')
    bpy.ops.wm.open_mainfile(filepath=str(source_path))
    report = export_animation(output_path, options.action, options.start, options.end)
    if options.report:
        Path(options.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('JC_AUTHORING_EXPORT', json.dumps(report))
