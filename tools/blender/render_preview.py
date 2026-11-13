"""
Рендер персонажей — проверка стиля.

Рендерит ряд персонажей спереди, из FPS-угла, и крупный план лиц.
Фон серый (как коридор в референсе), персонажи белые с чёрным контуром.

Запуск:
  blender.exe --background --factory-startup --python render_preview.py -- <out_dir>
"""

import bpy
import sys
import os
import math
import mathutils

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from blockout import BUILDERS, reset_scene  # noqa: E402

BG = 0.42      # средний серый: белое тело читается, чёрный контур виден


def setup(scene_bg=BG):
    """EEVEE + эмиссивные материалы: цвет виден КАК ЕСТЬ (свет не влияет),
    и при этом работают модификаторы — значит inverted-hull контур рендерится."""
    scene = bpy.context.scene

    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (scene_bg, scene_bg, scene_bg, 1)
    bg.inputs[1].default_value = 1.0

    scene.render.engine = 'BLENDER_EEVEE'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 16


def flatten_materials():
    """Переводит все материалы на Emission того же цвета.
    Так освещение и тени перестают влиять — получается чистая плоская заливка."""
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        nt = m.node_tree
        base = None
        for n in nt.nodes:
            if n.type == 'BSDF_PRINCIPLED':
                base = n.inputs["Base Color"].default_value[:]
                break
        if base is None:
            continue
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = base
        em.inputs["Strength"].default_value = 1.0
        nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
        m.use_backface_culling = m.name == "Outline"


def add_lights():
    """Не нужны в flat-режиме — оставлено для совместимости."""
    return


def build_row(names, spacing=1.10):
    reset_scene()
    setup()
    add_lights()
    total = 0.0
    for name in names:
        objs = BUILDERS[name]()
        # Найти смещение по X для группы (OL-копии смещаются вместе)
        for o in objs:
            o.location.x += total
        total += spacing
    return total


def camera(loc, look_at, ortho=None, lens=45):
    cd = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cd)
    bpy.context.collection.objects.link(cam)
    cam.location = loc
    d = mathutils.Vector(look_at) - mathutils.Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho
    else:
        cd.lens = lens
    bpy.context.scene.camera = cam


def render(path, w, h, samples=24):
    sc = bpy.context.scene
    sc.render.resolution_x = w
    sc.render.resolution_y = h
    sc.render.resolution_percentage = 100
    sc.render.filepath = path
    if hasattr(sc, "eevee"):
        sc.eevee.taa_render_samples = samples
    bpy.ops.render.render(write_still=True)
    print(f"WROTE {path}")


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    out = args[0] if args else os.path.join(os.getcwd(), "renders")
    os.makedirs(out, exist_ok=True)

    # 1. Все персонажи в ряд (без рук — у них другая система координат)
    names = [n for n in BUILDERS if n != "hands"]
    total = build_row(names)
    flatten_materials()

    cx = (total - 1.10) * 0.5
    camera((cx, -6.0, 0.92), (cx, 0, 0.92), ortho=total + 0.25)
    render(os.path.join(out, "lineup_front.png"), 1600, 900)

    # 2. FPS-угол на одного персонажа (стража с ведром)
    reset_scene()
    setup()
    BUILDERS["guard_bucket"]()
    flatten_materials()
    # 4.2 м от персонажа — видно всю фигуру, как в бою на средней дистанции
    camera((0.0, -4.2, 1.60), (0, 0, 1.00), lens=50)
    render(os.path.join(out, "preview_fpv.png"), 900, 1200)

    # 3. Крупный план головы — проверка лица
    reset_scene()
    setup()
    BUILDERS["peasant"]()
    flatten_materials()
    camera((0.0, -1.75, 1.60), (0, 0, 1.52), lens=60)
    render(os.path.join(out, "preview_face.png"), 900, 900)

    # 4. Читаемость на скорости: 480 px
    reset_scene()
    setup()
    total = build_row(names)
    flatten_materials()
    cx = (total - 1.10) * 0.5
    camera((cx, -6.0, 0.92), (cx, 0, 0.92), ortho=total + 0.25)
    render(os.path.join(out, "lineup_480.png"), 480, 270)

    print("DONE")


if __name__ == "__main__":
    main()
