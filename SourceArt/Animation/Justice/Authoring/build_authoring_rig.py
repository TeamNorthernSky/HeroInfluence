"""Blender 5.2: 원본 메시/31본을 보존하는 저스티스 최소 제작 리그 생성.

--out 신규.blend 경로를 명시합니다. 기존 작업 파일은 덮어쓰지 않습니다.
"""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--out', required=True)
parser.add_argument('--report', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
out = Path(args.out).resolve()
if out.exists():
    raise FileExistsError('기존 작업 파일을 보호합니다. 새 출력 경로를 지정하세요: ' + str(out))
project = Path(__file__).resolve().parents[4]
source = project / 'Assets/Resources/FBXModel/Charactor/Fighter/Fighter.001.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=False)
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
rig.name = 'Justice_제작리그'
mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
mesh.name = 'Justice_원본메시'
mesh.hide_select = True
for image in bpy.data.images:
    if image.source == 'FILE':
        image.pack()
original = list(rig.data.bones.keys())
rig['jc_original_bones'] = json.dumps(original)
rig['jc_source_fbx'] = str(source.relative_to(project)).replace('\\', '/')
rig['jc_rig_version'] = 1
rig['설명'] = '색상 도형만 Pose Mode에서 조절합니다. 파랑=L, 빨강=R, 노랑=중앙. 기존 게임용 뼈대는 숨겨져 있습니다.'
rig.show_in_front = True
rig.data.display_type = 'OCTAHEDRAL'
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
original_vertices = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
controls = {}
fk_sources = ['Root', 'Pelvis', 'Spine01', 'Spine02', 'Neck', 'Head', 'L_Clavicle', 'R_Clavicle',
              'L_Finger', 'R_Finger', 'L_Thumb', 'R_Thumb']

def duplicate(name, source_name, parent=None):
    src = eb[source_name]
    head, tail, roll = src.head.copy(), src.tail.copy(), src.roll
    bone = eb.new(name)
    bone.head, bone.tail, bone.roll = head, tail, roll
    bone.use_deform = False
    if parent:
        bone.parent = eb[parent]
    return bone

# 중앙 FK와 독립적인 손/발 IK 목표. 뼈대의 원래 계층은 변경하지 않습니다.
for source_name in fk_sources[:8]:
    parent = eb[source_name].parent
    parent_ctrl = controls.get(parent.name) if parent else None
    control = duplicate('CTRL_' + source_name, source_name, parent_ctrl)
    controls[source_name] = control.name

limbs = []
for side in ('L', 'R'):
    for kind, upper, lower, end, parent in [
        ('Arm', 'Upperarm', 'Forearm', 'Hand', 'Clavicle'),
        ('Leg', 'Thigh', 'Calf', 'Foot', 'Pelvis')]:
        upper, lower, end = [side + '_' + x for x in (upper, lower, end)]
        parent = 'CTRL_' + (side + '_' + parent if parent != 'Pelvis' else parent)
        goal = duplicate('CTRL_' + end + '_IK', end, 'CTRL_Root')
        controls[end] = goal.name
        a, b, c = (eb[n].head.copy() for n in (upper, lower, end))
        chain = []
        for index, (src, head, tail) in enumerate(((upper, a, b), (lower, b, c))):
            solver = eb.new('MCH_' + src)
            solver.head, solver.tail = head, tail
            solver.align_roll(eb[src].z_axis)
            solver.parent = eb[parent if index == 0 else chain[0]]
            solver.use_connect = index == 1
            solver.use_deform = False
            chain.append(solver.name)
            # 원본 축을 유지하는 추종 뼈로 발목 위치와 원래 Calf 끝점의 차이를 보정합니다.
            duplicate('MCH_Relay_' + src, src, solver.name)
        duplicate('MCH_Relay_' + end, end, chain[1])
        axis = (c - a).normalized()
        bend = b - (a + axis * (b - a).dot(axis))
        if bend.length < 1e-5:
            bend = Vector((0, -1, 0))
        bend.normalize()
        pole = eb.new('CTRL_' + side + ('_Elbow' if kind == 'Arm' else '_Knee'))
        pole.head = b + bend * (0.20 if kind == 'Arm' else 0.24)
        pole.tail = pole.head + Vector((0, 0, .045))
        pole.parent = eb['CTRL_Root']
        pole.use_deform = False
        limbs.append(dict(upper=upper, lower=lower, end=end, goal=goal.name,
                          pole=pole.name, solver_upper=chain[0], solver_lower=chain[1]))

for source_name in fk_sources[8:]:
    parent = 'MCH_Relay_' + eb[source_name].parent.name
    controls[source_name] = duplicate('CTRL_' + source_name, source_name, parent).name
bpy.ops.object.mode_set(mode='POSE')

def copy_pose(owner, target):
    c = rig.pose.bones[owner].constraints.new('COPY_TRANSFORMS')
    c.name = 'JC 제작용 추종 — 출력 시 베이크'
    c.target, c.subtarget = rig, target
    c.owner_space = c.target_space = 'POSE'

end_bones = {limb['end'] for limb in limbs}
for src, ctrl in controls.items():
    if src not in end_bones:
        copy_pose(src, ctrl)
for limb in limbs:
    for part in ('upper', 'lower'):
        src = limb[part]
        copy_pose(src, 'MCH_Relay_' + src)
    end_relay = 'MCH_Relay_' + limb['end']
    copy_pose(limb['end'], end_relay)
    rotation = rig.pose.bones[end_relay].constraints.new('COPY_ROTATION')
    rotation.target, rotation.subtarget = rig, limb['goal']
    rotation.owner_space = rotation.target_space = 'POSE'
    c = rig.pose.bones[limb['solver_lower']].constraints.new('IK')
    c.name = 'JC 2본 IK'
    c.target, c.subtarget = rig, limb['goal']
    c.pole_target, c.pole_subtarget = rig, limb['pole']
    c.chain_count, c.use_stretch, c.iterations = 2, False, 100
    for key in ('solver_upper', 'solver_lower'):
        rig.pose.bones[limb[key]].ik_stretch = 0
    # Bone roll이 서로 다른 기존 모델의 원래 굽힘 방향으로 Pole Angle을 보정합니다.
    def error(angle):
        c.pole_angle = angle
        bpy.context.view_layer.update()
        return sum(rig.pose.bones[limb[k]].matrix.to_quaternion().rotation_difference(
            rig.data.bones[limb[k]].matrix_local.to_quaternion()).angle ** 2
                   for k in ('solver_upper', 'solver_lower'))
    best = min((math.radians(i) for i in range(-180, 180, 2)), key=error)
    best = min((best + math.radians(i / 100) for i in range(-200, 201)), key=error)
    c.pole_angle = best
    limb['pole_angle'] = best
bpy.context.view_layer.update()
rest_error = max((rig.pose.bones[n].matrix.translation - rest[n].translation).length for n in original)
angle_error = max(rig.pose.bones[n].matrix.to_quaternion().rotation_difference(rest[n].to_quaternion()).angle for n in original)

# 조절값이 0일 때 원본 웨이트/형상과 일치하는지 확인합니다.
deps = bpy.context.evaluated_depsgraph_get()
evaluated = mesh.evaluated_get(deps)
eval_mesh = evaluated.to_mesh()
mesh_error = max((evaluated.matrix_world @ v.co - original_vertices[v.index]).length for v in eval_mesh.vertices)
evaluated.to_mesh_clear()
report = dict(rest_position_error=rest_error, rest_angle_degrees=math.degrees(angle_error),
              rest_mesh_error=mesh_error, original_bones=original, limbs=limbs)
report['bone_errors'] = {n: dict(position=(rig.pose.bones[n].matrix.translation-rest[n].translation).length,
    angle=math.degrees(rig.pose.bones[n].matrix.to_quaternion().rotation_difference(rest[n].to_quaternion()).angle)) for n in original}
if rest_error > .0001 or angle_error > .0035 or mesh_error > .0002:
    Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
    raise RuntimeError('초기 자세가 원본과 다릅니다: ' + json.dumps(report))

bpy.ops.object.mode_set(mode='OBJECT')
shape_collection = bpy.data.collections.new('리그도형_편집금지')
bpy.context.scene.collection.children.link(shape_collection)
shape_collection.hide_render = True

def shape(name, kind):
    if kind == 'ring':
        vertices = [(math.cos(i * math.tau / 32), 0, math.sin(i * math.tau / 32)) for i in range(32)]
        edges = [(i, (i + 1) % 32) for i in range(32)]
    else:
        vertices = [(x, y, z) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
        edges = [(i, j) for i, a in enumerate(vertices) for j, b in enumerate(vertices) if i < j and sum(x != y for x, y in zip(a, b)) == 1]
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, edges, [])
    obj = bpy.data.objects.new(name, data)
    shape_collection.objects.link(obj)
    obj.hide_set(True)
    obj.hide_render = True
    return obj

ring, box = shape('JC_WGT_Ring', 'ring'), shape('JC_WGT_Box', 'box')
for collection in list(rig.data.collections):
    rig.data.collections.remove(collection)
visible = rig.data.collections.new('01_조절장치')
deform = rig.data.collections.new('90_원본뼈대_직접편집금지')
mechanism = rig.data.collections.new('99_IK계산용_직접편집금지')
deform.is_visible = mechanism.is_visible = False
for bone in rig.data.bones:
    (visible if bone.name.startswith('CTRL_') else deform if bone.name in original else mechanism).assign(bone)
    if not bone.name.startswith('CTRL_'):
        continue
    pb = rig.pose.bones[bone.name]
    pb.rotation_mode = 'XYZ'
    pb.lock_scale = (True, True, True)
    central = bone.name in ('CTRL_Root', 'CTRL_Pelvis', 'CTRL_Spine01', 'CTRL_Spine02', 'CTRL_Neck', 'CTRL_Head')
    pb.custom_shape = ring if central else box
    pb.use_custom_shape_bone_size = False
    size = .085 if central else .034 if '_IK' in bone.name else .018
    if bone.name == 'CTRL_Root': size = .29
    if bone.name == 'CTRL_Pelvis': size = .105
    if bone.name == 'CTRL_Head': size = .115
    pb.custom_shape_scale_xyz = (size, size, size)
    if bone.name == 'CTRL_Root': pb.custom_shape_rotation_euler = (math.pi / 2, 0, 0)
    if bone.name.endswith('_Foot_IK'): pb.custom_shape_scale_xyz = (.055, .085, .028)
    if '_IK' not in bone.name and not bone.name.endswith(('Root', 'Pelvis', 'Elbow', 'Knee')):
        pb.lock_location = (True, True, True)
    pb.color.palette = 'CUSTOM'
    color = (.25, .65, 1) if bone.name.startswith('CTRL_L_') else (1, .3, .3) if bone.name.startswith('CTRL_R_') else (1, .72, .12)
    pb.color.custom.normal = color
    pb.color.custom.select = (1, .85, .4)
    pb.color.custom.active = (1, 1, .7)
    pb['조작'] = 'G: 위치 / R: 회전 / Alt+G, Alt+R: 기본값 / I: 키프레임. L/R는 캐릭터 자신의 좌우입니다.'

# 사용자가 새로 제작할 4구간. 동작은 만들어 넣지 않고 초기 자세 키 하나만 준비합니다.
scene = bpy.context.scene
scene.render.fps = 30
scene.render.fps_base = 1
scene.frame_start, scene.frame_end = 1, 31
scene.frame_set(1)
rig.animation_data_create()
actions = []
for name in ('JE_01_Approach', 'JE_02_Strike', 'JE_03_Return', 'JE_04_Taunt'):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data.action = action
    for pb in rig.pose.bones:
        if pb.name.startswith('CTRL_'):
            pb.keyframe_insert('location', frame=1, group=pb.name)
            pb.keyframe_insert('rotation_euler', frame=1, group=pb.name)
    action.use_frame_range = True
    action.frame_start, action.frame_end = 1, 31
    actions.append(action)
rig.animation_data.action = actions[1]
scene['제작상태'] = '새 동작 제작 전. JE_02_Strike(타격) 선택됨. 프레임 1의 초기 자세 키만 존재합니다.'
scene['기준'] = '정면은 -Y, 위는 +Z. 월드 접근/복귀는 Unity에서 담당. 30fps, 1~31프레임=1초.'
rig['jc_control_bones'] = json.dumps([b.name for b in rig.data.bones if b.name.startswith('CTRL_')])

# 열자마자 전체 메시와 조절 도형이 보이는 화면을 준비합니다.
target = Vector((0, 0, .42))
eye = Vector((1.15, -2.4, 1.15))
orientation = (target - eye).to_track_quat('-Z', 'Y')
for workspace in bpy.data.workspaces:
    for screen in workspace.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_rotation = orientation
                space.region_3d.view_distance = 1.55
                space.region_3d.view_location = target
                space.region_3d.view_perspective = 'ORTHO'
                space.shading.type = 'SOLID'
                space.shading.color_type = 'TEXTURE'
                space.overlay.show_relationship_lines = False
            elif area.type == 'DOPESHEET_EDITOR':
                area.spaces.active.mode = 'ACTION'
scene.tool_settings.use_keyframe_insert_auto = False
scene.tool_settings.transform_pivot_point = 'MEDIAN_POINT'
scene.keying_sets_all.active = next(k for k in scene.keying_sets_all if k.bl_idname == 'BUILTIN_KSI_LocRot')
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x, scene.render.resolution_y = 1100, 1000
scene.render.resolution_percentage = 100
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.background_type = 'WORLD'
scene.world = bpy.data.worlds.new('작업배경')
scene.world.color = (.055, .055, .065)
camera_data = bpy.data.cameras.new('검증용카메라')
camera = bpy.data.objects.new('검증용카메라', camera_data)
scene.collection.objects.link(camera)
camera.location, camera.rotation_euler = eye, orientation.to_euler()
camera_data.type, camera_data.ortho_scale = 'ORTHO', 1.12
camera.hide_set(True)
scene.camera = camera
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
for bone in rig.pose.bones:
    bone.select = False
rig.pose.bones['CTRL_Pelvis'].select = True
rig.data.bones.active = rig.data.bones['CTRL_Pelvis']
report.update(controls=json.loads(rig['jc_control_bones']), actions=[a.name for a in actions],
              vertices=len(mesh.data.vertices), packed_images=sum(i.packed_file is not None for i in bpy.data.images))
Path(args.report).write_text(json.dumps(report, indent=2), encoding='utf-8')
out.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out))
print('JC_RIG_READY', json.dumps(report))
