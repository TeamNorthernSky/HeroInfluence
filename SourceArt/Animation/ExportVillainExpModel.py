"""Export the cleaned villain model and EXP armature as a Unity-ready FBX.

Run this file from Blender's Scripting workspace while Villan_Gun.002.blend
is open. Scene changes made for export are restored when the export finishes.
"""

import os

import bpy


# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

OUTPUT_PATH = r"D:\c 백업폴더 모음\모델링\0806\Villan_Gun.EXP_Model.fbx"

EXP_ARMATURE_NAME = "EXP_Armature"
SRC_ARMATURE_NAME = "SRC_Armature"
CHARACTER_MESH_NAME = "tripo_node_57176ec5-0bab-4731-be23-6ce85935f196"
WEAPON_MESH_NAME = "meshes[0]"

# Set this to False when the weapon should be exported as a separate FBX.
INCLUDE_WEAPON = True


def require_object(name, expected_type):
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError(f"Required object not found: {name}")
    if obj.type != expected_type:
        raise RuntimeError(
            f"{name} must be {expected_type}, but its type is {obj.type}."
        )
    return obj


def find_armature_modifier(mesh):
    modifiers = [modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE"]
    if len(modifiers) != 1:
        raise RuntimeError(
            f"{mesh.name} must have exactly one Armature modifier; found {len(modifiers)}."
        )
    return modifiers[0]


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


def set_object_parent_keep_world(obj, parent):
    world_matrix = obj.matrix_world.copy()
    obj.parent = parent
    obj.parent_type = "OBJECT"
    obj.parent_bone = ""
    obj.matrix_world = world_matrix


def set_bone_parent_keep_world(obj, armature, bone_name):
    if bone_name not in armature.data.bones:
        raise RuntimeError(f"Bone not found on {armature.name}: {bone_name}")

    world_matrix = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world_matrix


def main():
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    exp_armature = require_object(EXP_ARMATURE_NAME, "ARMATURE")
    src_armature = require_object(SRC_ARMATURE_NAME, "ARMATURE")
    character_mesh = require_object(CHARACTER_MESH_NAME, "MESH")
    character_modifier = find_armature_modifier(character_mesh)

    weapon_mesh = None
    if INCLUDE_WEAPON:
        weapon_mesh = require_object(WEAPON_MESH_NAME, "MESH")

    selected_before = list(bpy.context.selected_objects)
    active_before = bpy.context.view_layer.objects.active
    frame_before = bpy.context.scene.frame_current

    exp_pose_position = exp_armature.data.pose_position
    src_pose_position = src_armature.data.pose_position
    exp_action = (
        exp_armature.animation_data.action
        if exp_armature.animation_data is not None
        else None
    )

    modifier_target_before = character_modifier.object
    character_parent_before = save_parent_state(character_mesh)
    weapon_parent_before = save_parent_state(weapon_mesh) if weapon_mesh else None

    export_objects = [exp_armature, character_mesh]
    if weapon_mesh:
        export_objects.append(weapon_mesh)

    hidden_before = {
        obj: (obj.hide_get(), obj.hide_viewport)
        for obj in export_objects
    }

    try:
        # Use the same bind/rest skeleton for the model and all animation FBXs.
        exp_armature.data.pose_position = "REST"
        src_armature.data.pose_position = "REST"
        if exp_armature.animation_data is not None:
            exp_armature.animation_data.action = None
        bpy.context.view_layer.update()

        # Temporarily bind the character and fixed weapon to the export armature.
        character_modifier.object = exp_armature
        set_object_parent_keep_world(character_mesh, exp_armature)

        if weapon_mesh:
            set_bone_parent_keep_world(weapon_mesh, exp_armature, "R_Hand")

        for obj in bpy.context.selected_objects:
            obj.select_set(False)

        for obj in export_objects:
            obj.hide_set(False)
            obj.hide_viewport = False
            obj.select_set(True)

        bpy.context.view_layer.objects.active = exp_armature
        bpy.context.view_layer.update()

        os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)

        bpy.ops.export_scene.fbx(
            filepath=OUTPUT_PATH,
            use_selection=True,
            object_types={"MESH", "ARMATURE"},
            global_scale=1.0,
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_NONE",
            use_space_transform=True,
            bake_space_transform=False,
            axis_forward="-Z",
            axis_up="Y",
            use_mesh_modifiers=True,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            use_armature_deform_only=True,
            add_leaf_bones=False,
            bake_anim=False,
            path_mode="AUTO",
        )

        print("Villain model export complete:", OUTPUT_PATH)

    finally:
        # Restore the open .blend scene so running the exporter is non-destructive.
        character_modifier.object = modifier_target_before
        restore_parent_state(character_mesh, character_parent_before)

        if weapon_mesh and weapon_parent_before:
            restore_parent_state(weapon_mesh, weapon_parent_before)

        exp_armature.data.pose_position = exp_pose_position
        src_armature.data.pose_position = src_pose_position
        if exp_armature.animation_data is not None:
            exp_armature.animation_data.action = exp_action

        for obj, (hidden, hide_viewport) in hidden_before.items():
            obj.hide_set(hidden)
            obj.hide_viewport = hide_viewport

        for obj in bpy.context.selected_objects:
            obj.select_set(False)
        for obj in selected_before:
            obj.select_set(True)

        bpy.context.view_layer.objects.active = active_before
        bpy.context.scene.frame_set(frame_before)
        bpy.context.view_layer.update()


if __name__ == "__main__":
    main()
