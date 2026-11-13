"""
Рендер силуэтных проверок болванок.

Собирает все типы в ряд, рендерит:
  1. вид спереди (общий)
  2. вид спереди в 480 px шириной (тест читаемости на скорости)
  3. вид сверху-спереди, как из FPS-камеры

Запуск:
  blender.exe --background --factory-startup --python silhouette.py -- <output_dir>
"""

import bpy
import sys
import os
import math
import mathutils

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blockout import BUILDERS, reset_scene  # noqa: E402


def setup_world(bg=1.0):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    bg_node = world.node_tree.nodes["Background"]
    bg_node.inputs[0].default_value = (bg, bg, bg, 1)
    bg_node.inputs[1].default_value = 1.0

    # Плоский контраст: без тонмаппинга, чисто чёрное на чисто белом
    try:
        scene.view_settings.view_transform = 'Standard'
        scene.view_settings.look = 'None'
        scene.view_settings.exposure = 0.0
        scene.view_settings.gamma = 1.0
    except Exception as exc:
        print(f"view_settings: {exc}")


def make_black_material():
    mat = bpy.data.materials.new("SilhouetteBlack")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1)
    bsdf.inputs["Roughness"].default_value = 1.0
    return mat


def build_row():
    """Все типы в ряд по X. Возвращает список имён объектов."""
    reset_scene()
    setup_world()
    mat = make_black_material()

    all_objs = []
    offset = 0.0
    for name, builder in BUILDERS.items():
        if name == "hands":
            continue  # руки рендерим отдельно, у них другая система координат
        objs = builder()
        for o in objs:
            o.location.x += offset
            if o.type == 'MESH':
                o.data.materials.clear()
                o.data.materials.append(mat)
        all_objs.extend(objs)
        offset += 1.1
    return all_objs, offset


def add_camera(loc, look_at, ortho_scale=None):
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)

    cam.location = loc
    direction = mathutils.Vector(look_at) - mathutils.Vector(loc)
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    if ortho_scale:
        cam_data.type = 'ORTHO'
        cam_data.ortho_scale = ortho_scale
    bpy.context.scene.camera = cam
    return cam


def add_sun():
    sun_data = bpy.data.lights.new("Sun", type='SUN')
    sun_data.energy = 1.0
    sun_data.color = (1, 1, 1)
    sun = bpy.data.objects.new("Sun", sun_data)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(55), 0, math.radians(20))


def render(path, width, height, samples=16, border=None):
    """border=(x_min, x_max, z_min, z_max) — кадрирование по ортокамере."""
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.filepath = path
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = samples
    # Чистый ч/б без сглаживания по краям кадра
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    print(f"WROTE {path}")


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    out_dir = args[0] if args else os.path.join(os.getcwd(), "renders")
    os.makedirs(out_dir, exist_ok=True)

    objs, total_w = build_row()
    add_sun()

    center_x = (total_w - 1.1) * 0.5   # центр ряда
    center_z = 0.95

    # 1. Спереди, общий вид — ортокамера, плотно по ряду
    add_camera(
        loc=(center_x, -8.0, center_z),
        look_at=(center_x, 0, center_z),
        ortho_scale=total_w,
    )
    render(os.path.join(out_dir, "silhouette_front.png"), 1400, 900)

    # 2. Тест читаемости: 480 px, как на скорости в бою
    render(os.path.join(out_dir, "silhouette_480.png"), 480, 320)

    # 3. Вид из FPS: сверху-спереди, низко
    add_camera(
        loc=(center_x, -3.2, 1.75),
        look_at=(center_x, 0, 1.05),
        ortho_scale=None,
    )
    render(os.path.join(out_dir, "silhouette_fps_angle.png"), 1400, 900)

    print("DONE")


if __name__ == "__main__":
    main()
