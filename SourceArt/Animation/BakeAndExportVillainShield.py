"""Bake and export the cleaned villain shield rig for Unity.

Expected scene objects:
  - SRC_Armature: source skeleton with the original actions
  - EXP_Armature: cleaned Unity export skeleton
  - tripo_node_57176ec5-0bab-4731-be23-6ce85935f196: character mesh
  - meshes[0]: rigid shield mesh parented to R_Hand

The script performs the following operations:
  1. Adds temporary world-space Copy Transforms constraints from SRC to EXP.
  2. Bakes Armature|idle and Armature|attack to EXP-only actions.
  3. Exports a rest/T-pose model FBX containing the character, shield, and EXP rig.
  4. Exports one FBX per baked animation, with NLA and All Actions disabled.
  5. Restores temporary scene changes. The new baked actions remain in the .blend.

Run this script from Blender's Scripting workspace while
Villan_Sheild.002.blend is open. Save the .blend manually after a successful run
if the baked actions should be retained in the source file.
"""

from __future__ import annotations

import os
from dataclasses import dataclass

import bpy


# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

DEFAULT_OUTPUT_DIRECTORY = r"D:\c 백업폴더 모음\모델링\0806\Villan_Sheild_Export"
OUTPUT_DIRECTORY = os.environ.get(
    "VILLAIN_SHIELD_OUTPUT_DIR",
    DEFAULT_OUTPUT_DIRECTORY,
)

SRC_ARMATURE_NAME = "SRC_Armature"
EXP_ARMATURE_NAME = "EXP_Armature"
CHARACTER_MESH_NAME = "tripo_node_57176ec5-0bab-4731-be23-6ce85935f196"
SHIELD_MESH_NAME = "meshes[0]"

INCLUDE_SHIELD_IN_MODEL = True

MODEL_FILENAME = "Villan_Sheild.EXP_Model.fbx"

# This control bone is still present in the inspected EXP rig. It is not deleted
# from the .blend; the pipeline simply excludes it from baking and FBX output.
EXCLUDED_EXPORT_BONES = {"L.PoleVectorRarm"}

TEMP_CONSTRAINT_NAME = "CODEX_CopyFromSRC"


@dataclass(frozen=True)
class AnimationSpec:
    key: str
    source_action: str
    baked_action: str
    output_filename: str


ANIMATIONS = (
    AnimationSpec(
        key="idle",
        source_action="Armature|idle",
        baked_action="EXP_Idle_Baked",
        output_filename="Villan_Sheild.Idle.fbx",
    ),
    AnimationSpec(
        key="attack1",
        source_action="Armature|attack",
        baked_action="EXP_Attack1_Baked",
        output_filename="Villan_Sheild.Attack1.fbx",
    ),
)


# -----------------------------------------------------------------------------
# Validation and state helpers
# -----------------------------------------------------------------------------


def require_object(name: str, expected_type: str):
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError(f"Required object not found: {name}")
    if obj.type != expected_type:
        raise RuntimeError(
            f"{name} must be {expected_type}, but its type is {obj.type}."
        )
    return obj


def require_action(name: str):
    action = bpy.data.actions.get(name)
    if action is None:
        raise RuntimeError(f"Required source action not found: {name}")
    return action


def find_armature_modifier(mesh):
    modifiers = [modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"]
    if len(modifiers) != 1:
        raise RuntimeError(
            f"{mesh.name} must have exactly one Armature modifier; found {len(modifiers)}."
        )
    return modifiers[0]


def ensure_object_mode():
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")


def save_parent_state(obj):
    return {
        "parent": obj.parent,
        "parent_type": obj.parent_type,
        "parent_bone": obj.parent_bone,
        "matrix_parent_inverse": obj.matrix_parent_inverse.copy(),
        "matrix_world": obj.matrix_world.copy(),
    }


def restore_parent_state(obj, state):
    obj.parent = state["parent"]
    obj.parent_type = state["parent_type"]
    obj.parent_bone = state["parent_bone"]
    obj.matrix_parent_inverse = state["matrix_parent_inverse"]
    obj.matrix_world = state["matrix_world"]


def parent_to_object_keep_world(obj, parent):
    world_matrix = obj.matrix_world.copy()
    obj.parent = parent
    obj.parent_type = "OBJECT"
    obj.parent_bone = ""
    obj.matrix_world = world_matrix


def parent_to_bone_keep_world(obj, armature, bone_name: str):
    if bone_name not in armature.data.bones:
        raise RuntimeError(f"Bone not found on {armature.name}: {bone_name}")
    world_matrix = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world_matrix


def select_only(objects, active):
    ensure_object_mode()
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active
    bpy.context.view_layer.update()


def validate_export_hierarchy(exp_armature):
    expected_parents = {
        "Pelvis": "Root",
        "Spine01": "Pelvis",
        "Spine02": "Spine01",
        "Neck": "Spine02",
        "Head": "Neck",
        "L_Upperarm": "L_Clavicle",
        "L_Forearm": "L_Upperarm",
        "L_Hand": "L_Forearm",
        "R_Upperarm": "R_Clavicle",
        "R_Forearm": "R_Upperarm",
        "R_Hand": "R_Forearm",
        "L_Thigh": "Pelvis",
        "L_Calf": "L_Thigh",
        "L_Foot": "L_Calf",
        "R_Thigh": "Pelvis",
        "R_Calf": "R_Thigh",
        "R_Foot": "R_Calf",
    }

    errors = []
    for bone_name, expected_parent in expected_parents.items():
        bone = exp_armature.data.bones.get(bone_name)
        if bone is None:
            errors.append(f"Missing bone: {bone_name}")
            continue
        actual_parent = bone.parent.name if bone.parent else None
        if actual_parent != expected_parent:
            errors.append(
                f"{bone_name}: expected parent {expected_parent}, got {actual_parent}"
            )

    if errors:
        raise RuntimeError("Invalid EXP hierarchy:\n" + "\n".join(errors))


def validate_matching_bones(src_armature, exp_armature):
    missing = [
        bone.name
        for bone in exp_armature.data.bones
        if bone.name not in EXCLUDED_EXPORT_BONES
        and bone.name not in src_armature.data.bones
    ]
    if missing:
        raise RuntimeError(
            "EXP bones missing from SRC: " + ", ".join(sorted(missing))
        )


# -----------------------------------------------------------------------------
# Baking
# -----------------------------------------------------------------------------


def remove_temporary_constraints(exp_armature):
    for pose_bone in exp_armature.pose.bones:
        constraint = pose_bone.constraints.get(TEMP_CONSTRAINT_NAME)
        if constraint is not None:
            pose_bone.constraints.remove(constraint)


def add_temporary_constraints(src_armature, exp_armature):
    remove_temporary_constraints(exp_armature)

    for pose_bone in exp_armature.pose.bones:
        if pose_bone.name in EXCLUDED_EXPORT_BONES:
            continue
        if pose_bone.name not in src_armature.pose.bones:
            continue

        constraint = pose_bone.constraints.new("COPY_TRANSFORMS")
        constraint.name = TEMP_CONSTRAINT_NAME
        constraint.target = src_armature
        constraint.subtarget = pose_bone.name
        constraint.target_space = "WORLD"
        constraint.owner_space = "WORLD"
        constraint.mix_mode = "REPLACE"
        constraint.influence = 1.0


def remove_existing_generated_action(action_name: str, exp_armature):
    action = bpy.data.actions.get(action_name)
    if action is None:
        return

    if exp_armature.animation_data and exp_armature.animation_data.action == action:
        exp_armature.animation_data.action = None
    bpy.data.actions.remove(action)


def select_export_pose_bones(exp_armature):
    ensure_object_mode()
    select_only([exp_armature], exp_armature)
    bpy.ops.object.mode_set(mode="POSE")

    # Blender 5.x exposes pose selection on PoseBone rather than Bone.
    for pose_bone in exp_armature.pose.bones:
        pose_bone.select = pose_bone.name not in EXCLUDED_EXPORT_BONES

    active_bone = exp_armature.data.bones.get("Pelvis")
    if active_bone is not None:
        exp_armature.data.bones.active = active_bone


def bake_action(spec: AnimationSpec, src_armature, exp_armature):
    source_action = require_action(spec.source_action)
    remove_existing_generated_action(spec.baked_action, exp_armature)

    src_armature.animation_data_create()
    exp_armature.animation_data_create()
    src_armature.animation_data.action = source_action
    exp_armature.animation_data.action = None

    src_armature.data.pose_position = "POSE"
    exp_armature.data.pose_position = "POSE"

    frame_start = int(round(source_action.frame_range[0]))
    frame_end = int(round(source_action.frame_range[1]))
    bpy.context.scene.frame_start = frame_start
    bpy.context.scene.frame_end = frame_end
    bpy.context.scene.frame_set(frame_start)
    bpy.context.view_layer.update()

    select_export_pose_bones(exp_armature)

    result = bpy.ops.nla.bake(
        frame_start=frame_start,
        frame_end=frame_end,
        step=1,
        only_selected=True,
        visual_keying=True,
        clear_constraints=False,
        clear_parents=False,
        use_current_action=False,
        clean_curves=False,
        bake_types={"POSE"},
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Bake failed for {spec.key}: {result}")

    baked_action = exp_armature.animation_data.action
    if baked_action is None:
        raise RuntimeError(f"Bake produced no action for {spec.key}")

    baked_action.name = spec.baked_action
    baked_action.use_fake_user = True
    ensure_object_mode()

    print(
        f"Baked {spec.source_action} -> {baked_action.name} "
        f"({frame_start}-{frame_end})"
    )
    return baked_action


# -----------------------------------------------------------------------------
# FBX export
# -----------------------------------------------------------------------------


def export_fbx_common(filepath: str, object_types, bake_animation: bool):
    os.makedirs(os.path.dirname(filepath), exist_ok=True)

    kwargs = dict(
        filepath=filepath,
        use_selection=True,
        object_types=object_types,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        add_leaf_bones=False,
        bake_anim=bake_animation,
        path_mode="AUTO",
    )

    if bake_animation:
        kwargs.update(
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=False,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0,
        )
    else:
        kwargs.update(use_mesh_modifiers=True)

    result = bpy.ops.export_scene.fbx(**kwargs)
    if "FINISHED" not in result:
        raise RuntimeError(f"FBX export failed: {filepath}: {result}")


def export_model(exp_armature, src_armature, character_mesh, shield_mesh):
    character_modifier = find_armature_modifier(character_mesh)

    exp_pose_before = exp_armature.data.pose_position
    src_pose_before = src_armature.data.pose_position
    exp_action_before = (
        exp_armature.animation_data.action
        if exp_armature.animation_data is not None
        else None
    )
    modifier_target_before = character_modifier.object
    character_parent_before = save_parent_state(character_mesh)
    shield_parent_before = save_parent_state(shield_mesh) if shield_mesh else None

    export_objects = [exp_armature, character_mesh]
    if shield_mesh is not None:
        export_objects.append(shield_mesh)

    try:
        ensure_object_mode()
        exp_armature.data.pose_position = "REST"
        src_armature.data.pose_position = "REST"
        exp_armature.animation_data_create()
        exp_armature.animation_data.action = None
        bpy.context.view_layer.update()

        character_modifier.object = exp_armature
        parent_to_object_keep_world(character_mesh, exp_armature)

        if shield_mesh is not None:
            parent_to_bone_keep_world(shield_mesh, exp_armature, "R_Hand")

        select_only(export_objects, exp_armature)

        filepath = os.path.join(OUTPUT_DIRECTORY, MODEL_FILENAME)
        export_fbx_common(
            filepath=filepath,
            object_types={"MESH", "ARMATURE"},
            bake_animation=False,
        )
        print("Exported model:", filepath)

    finally:
        character_modifier.object = modifier_target_before
        restore_parent_state(character_mesh, character_parent_before)
        if shield_mesh is not None and shield_parent_before is not None:
            restore_parent_state(shield_mesh, shield_parent_before)
        exp_armature.data.pose_position = exp_pose_before
        src_armature.data.pose_position = src_pose_before
        exp_armature.animation_data.action = exp_action_before
        bpy.context.view_layer.update()


def export_animation(spec: AnimationSpec, action, exp_armature):
    ensure_object_mode()
    exp_armature.data.pose_position = "POSE"
    exp_armature.animation_data_create()
    exp_armature.animation_data.action = action

    frame_start = int(round(action.frame_range[0]))
    frame_end = int(round(action.frame_range[1]))
    bpy.context.scene.frame_start = frame_start
    bpy.context.scene.frame_end = frame_end
    bpy.context.scene.frame_set(frame_start)

    select_only([exp_armature], exp_armature)

    filepath = os.path.join(OUTPUT_DIRECTORY, spec.output_filename)
    export_fbx_common(
        filepath=filepath,
        object_types={"ARMATURE"},
        bake_animation=True,
    )
    print("Exported animation:", filepath)


# -----------------------------------------------------------------------------
# Pipeline entry point
# -----------------------------------------------------------------------------


def main():
    ensure_object_mode()

    src_armature = require_object(SRC_ARMATURE_NAME, "ARMATURE")
    exp_armature = require_object(EXP_ARMATURE_NAME, "ARMATURE")
    character_mesh = require_object(CHARACTER_MESH_NAME, "MESH")
    shield_mesh = (
        require_object(SHIELD_MESH_NAME, "MESH")
        if INCLUDE_SHIELD_IN_MODEL
        else None
    )

    validate_export_hierarchy(exp_armature)
    validate_matching_bones(src_armature, exp_armature)
    for spec in ANIMATIONS:
        require_action(spec.source_action)

    selected_before = list(bpy.context.selected_objects)
    active_before = bpy.context.view_layer.objects.active
    frame_before = bpy.context.scene.frame_current
    frame_start_before = bpy.context.scene.frame_start
    frame_end_before = bpy.context.scene.frame_end
    src_action_before = (
        src_armature.animation_data.action
        if src_armature.animation_data is not None
        else None
    )
    exp_action_before = (
        exp_armature.animation_data.action
        if exp_armature.animation_data is not None
        else None
    )
    exp_pose_before = exp_armature.data.pose_position
    src_pose_before = src_armature.data.pose_position
    excluded_deform_before = {
        bone_name: exp_armature.data.bones[bone_name].use_deform
        for bone_name in EXCLUDED_EXPORT_BONES
        if bone_name in exp_armature.data.bones
    }

    baked_actions = {}
    succeeded = False

    try:
        # Exclude remaining controller/pole bones without deleting them.
        for bone_name in excluded_deform_before:
            exp_armature.data.bones[bone_name].use_deform = False

        add_temporary_constraints(src_armature, exp_armature)

        for spec in ANIMATIONS:
            baked_actions[spec.key] = bake_action(
                spec,
                src_armature,
                exp_armature,
            )

        remove_temporary_constraints(exp_armature)

        export_model(
            exp_armature,
            src_armature,
            character_mesh,
            shield_mesh,
        )

        for spec in ANIMATIONS:
            export_animation(spec, baked_actions[spec.key], exp_armature)

        succeeded = True

    finally:
        ensure_object_mode()
        remove_temporary_constraints(exp_armature)

        for bone_name, deform_value in excluded_deform_before.items():
            exp_armature.data.bones[bone_name].use_deform = deform_value

        src_armature.data.pose_position = src_pose_before
        exp_armature.data.pose_position = exp_pose_before

        src_armature.animation_data_create()
        exp_armature.animation_data_create()
        src_armature.animation_data.action = src_action_before

        if succeeded and "idle" in baked_actions:
            # Leave the freshly baked idle active for easy visual inspection.
            exp_armature.animation_data.action = baked_actions["idle"]
            bpy.context.scene.frame_start = int(
                round(baked_actions["idle"].frame_range[0])
            )
            bpy.context.scene.frame_end = int(
                round(baked_actions["idle"].frame_range[1])
            )
            bpy.context.scene.frame_set(bpy.context.scene.frame_start)
        else:
            exp_armature.animation_data.action = exp_action_before
            bpy.context.scene.frame_start = frame_start_before
            bpy.context.scene.frame_end = frame_end_before
            bpy.context.scene.frame_set(frame_before)

        for obj in bpy.context.selected_objects:
            obj.select_set(False)
        for obj in selected_before:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = active_before
        bpy.context.view_layer.update()

    print("\n=== Villain Shield pipeline complete ===")
    print("Output directory:", OUTPUT_DIRECTORY)
    print("Model:", MODEL_FILENAME)
    for spec in ANIMATIONS:
        print(f"{spec.key}: {spec.output_filename}")
    print("Save the .blend manually if the baked actions should be retained.")


if __name__ == "__main__":
    main()
