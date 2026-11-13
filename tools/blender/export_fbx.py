"""
Экспорт персонажей Calabash в FBX для Unity.

Запуск:
  blender.exe --background --factory-startup --python export_fbx.py -- <out_dir>

Каждый тип — отдельный FBX. Плюс общий .blend со всеми.

Пресет экспорта (по стандарту проекта):
  Forward -Z, Up Y, Apply Transform, масштаб 1.0, без leaf bones.
"""

import bpy
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blockout import BUILDERS, reset_scene  # noqa: E402


def export_one(name, out_path):
    reset_scene()
    BUILDERS[name]()

    # Перед экспортом: применить масштабы, убрать контурные копии
    # (в Unity контур делает шейдер, а не геометрия)
    for ob in list(bpy.data.objects):
        if ob.name.endswith("_OL"):
            bpy.data.objects.remove(ob, do_unlink=True)

    for ob in bpy.data.objects:
        if ob.type == 'MESH':
            bpy.context.view_layer.objects.active = ob
            ob.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=out_path,
        use_selection=False,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_tspace=False,
        add_leaf_bones=False,
        primary_bone_axis='Y',
        secondary_bone_axis='X',
        bake_anim=False,
        path_mode='AUTO',
        embed_textures=False,
        batch_mode='OFF',
    )
    size = os.path.getsize(out_path)
    print(f"EXPORTED {name} -> {os.path.basename(out_path)} ({size} bytes)")


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    out_dir = args[0] if args else os.path.join(os.getcwd(), "fbx")
    os.makedirs(out_dir, exist_ok=True)

    for name in BUILDERS:
        export_one(name, os.path.join(out_dir, f"CHR_{name}.fbx"))

    print("=== ALL EXPORTED ===")
    for f in sorted(os.listdir(out_dir)):
        p = os.path.join(out_dir, f)
        print(f"{f:28s} {os.path.getsize(p):>8d} bytes")


if __name__ == "__main__":
    main()
