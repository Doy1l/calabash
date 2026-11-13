"""
Персонажи Calabash — блочные болванки в стиле монохромного Roblox.
Белые тела, чёрный контур (inverted hull), лица, одежда и броня.

Запуск:
  blender.exe --background --factory-startup --python blockout.py
  blender.exe --background --factory-startup --python blockout.py -- guard

Стандарт: рост 1.75 м (Король 1.95), origin на полу между ступнями,
персонаж смотрит в -Y (в сторону камеры). Метры.

Пропорции блочные (R6-подобные), как в референсе:
  ноги 0.80 + торс 0.57 + голова 0.38 = 1.75
Голова крупная — это и есть узнаваемый силуэт.
"""

import bpy
import math
import sys
import mathutils

DEFAULT_HEIGHT = 1.75

# Палитра: только серые градации, никакого цвета
WHITE = 0.94   # тело
CLOTH = 0.86   # одежда
ARMOR = 0.74   # броня, металл
DARK = 0.10    # лицо, мелкие детали
BLACK = 0.02   # контур

W_MAT = {}


def mat(level):
    key = round(level, 3)
    if key in W_MAT:
        return W_MAT[key]
    m = bpy.data.materials.new(f"Grey{key}")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (level, level, level, 1)
    b.inputs["Roughness"].default_value = 1.0
    for k in ("Specular IOR Level", "Specular"):
        if k in b.inputs:
            b.inputs[k].default_value = 0.0
    W_MAT[key] = m
    return m


_OUTLINE = None


def outline_mat():
    """Чёрный материал с backface culling — из него делается контур."""
    global _OUTLINE
    if _OUTLINE:
        return _OUTLINE
    m = bpy.data.materials.new("Outline")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (BLACK, BLACK, BLACK, 1)
    b.inputs["Roughness"].default_value = 1.0
    for k in ("Specular IOR Level", "Specular"):
        if k in b.inputs:
            b.inputs[k].default_value = 0.0
    m.use_backface_culling = True
    _OUTLINE = m
    return m


def add_outline(ob, thickness=0.010):
    """Inverted hull: копия меша, раздутая наружу, с перевёрнутыми нормалями."""
    dup = ob.copy()
    dup.data = ob.data.copy()
    dup.name = ob.name + "_OL"
    dup.data.name = dup.name
    bpy.context.collection.objects.link(dup)
    dup.data.materials.clear()
    dup.data.materials.append(outline_mat())
    mod = dup.modifiers.new("OL", 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.use_even_offset = True
    return dup


def box(name, center, size, level=WHITE, outline=0.010, rot=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    ob = bpy.context.object
    ob.name = name
    ob.scale = size
    if rot:
        ob.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=bool(rot), scale=True)
    ob.data.materials.clear()
    ob.data.materials.append(mat(level))
    if outline:
        add_outline(ob, outline)
    return ob


def cyl(name, center, radius, depth, verts=12, level=WHITE, outline=0.010, rot=None):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth, location=center
    )
    ob = bpy.context.object
    ob.name = name
    if rot:
        ob.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=bool(rot), scale=True)
    ob.data.materials.clear()
    ob.data.materials.append(mat(level))
    if outline:
        add_outline(ob, outline)
    return ob


# ---------- Блочный человек ----------
# Раскладка снизу вверх: ноги / торс / голова.

LEG = 0.457      # 0.80 м при 1.75
TORSO = 0.326    # 0.57 м
HEAD = 0.217     # 0.38 м


def blocky(prefix, height=DEFAULT_HEIGHT, wide=1.0, thin=1.0):
    """Блочная фигура. wide — множитель ширины торса, thin — толщина конечностей."""
    leg_h = height * LEG
    torso_h = height * TORSO
    head_h = height * HEAD

    torso_w = 0.44 * wide
    torso_d = 0.26 * wide
    limb_w = 0.155 * thin
    arm_len = torso_h * 1.02

    objs = []

    # Ноги — два блока от пола до торса
    for side, sx in (("L", -1), ("R", 1)):
        objs.append(box(
            f"{prefix}_leg_{side}",
            (sx * torso_w * 0.25, 0, leg_h * 0.5),
            (limb_w * 1.15, torso_d * 0.75, leg_h),
        ))

    # Торс
    objs.append(box(
        f"{prefix}_torso",
        (0, 0, leg_h + torso_h * 0.5),
        (torso_w, torso_d, torso_h),
    ))

    # Руки — от плеч вниз, чуть отставлены
    for side, sx in (("L", -1), ("R", 1)):
        objs.append(box(
            f"{prefix}_arm_{side}",
            (sx * (torso_w * 0.5 + limb_w * 0.62), 0,
             leg_h + torso_h - arm_len * 0.5),
            (limb_w, limb_w * 1.05, arm_len),
        ))

    # Голова — крупный блок, макушка ровно на height
    head_center = (0, 0, height - head_h * 0.5)
    objs.append(box(
        f"{prefix}_head", head_center,
        (head_h, head_h * 0.92, head_h),
    ))

    return objs, {
        "head_center": head_center,
        "head_size": (head_h, head_h * 0.92, head_h),
        "torso_top": leg_h + torso_h,
        "leg_h": leg_h,
        "torso_h": torso_h,
        "torso_w": torso_w,
        "torso_d": torso_d,
    }


# ---------- Лица ----------

def add_face(prefix, meta, style="neutral"):
    """Лицо на передней грани головы (сторона -Y)."""
    hc = meta["head_center"]
    hs = meta["head_size"]
    front_y = hc[1] - hs[1] * 0.5 - 0.006
    eye_x = hs[0] * 0.22
    eye_z = hc[2] + hs[2] * 0.13
    mouth_z = hc[2] - hs[2] * 0.22

    objs = []
    # Глаза — вертикальные блоки, как пиксельные глаза
    for side, sx in (("L", -1), ("R", 1)):
        objs.append(box(
            f"{prefix}_eye_{side}",
            (sx * eye_x, front_y, eye_z),
            (hs[0] * 0.13, 0.012, hs[2] * 0.17),
            level=DARK, outline=0.0,
        ))

    if style == "neutral":
        objs.append(box(
            f"{prefix}_mouth", (0, front_y, mouth_z),
            (hs[0] * 0.30, 0.012, hs[2] * 0.045),
            level=DARK, outline=0.0,
        ))
    elif style == "open":
        objs.append(box(
            f"{prefix}_mouth", (0, front_y, mouth_z),
            (hs[0] * 0.20, 0.012, hs[2] * 0.13),
            level=DARK, outline=0.0,
        ))
    elif style == "smirk":
        objs.append(box(
            f"{prefix}_mouth", (0, front_y, mouth_z),
            (hs[0] * 0.28, 0.012, hs[2] * 0.045),
            level=DARK, outline=0.0, rot=(0, math.radians(9), 0),
        ))
    elif style == "grim":
        objs.append(box(
            f"{prefix}_mouth", (0, front_y, mouth_z - hs[2] * 0.02),
            (hs[0] * 0.34, 0.012, hs[2] * 0.05),
            level=DARK, outline=0.0,
        ))
        # Сдвинутые брови — злое лицо
        for side, sx in (("L", -1), ("R", 1)):
            objs.append(box(
                f"{prefix}_brow_{side}",
                (sx * eye_x, front_y, eye_z + hs[2] * 0.15),
                (hs[0] * 0.16, 0.012, hs[2] * 0.05),
                level=DARK, outline=0.0, rot=(0, math.radians(-14 * sx), 0),
            ))
    return objs


# ---------- Типы ----------

def make_peasant(prefix="CHR_Peasant_A", height=DEFAULT_HEIGHT):
    objs, m = blocky(prefix, height)
    objs += add_face(prefix, m, "neutral")
    # Фартук — панель на груди и до колен
    objs.append(box(
        f"{prefix}_apron",
        (0, -m["torso_d"] * 0.5 - 0.012,
         m["leg_h"] + m["torso_h"] * 0.45),
        (m["torso_w"] * 0.62, 0.024, m["torso_h"] * 1.15),
        level=CLOTH, outline=0.008,
    ))
    # Шапка — плоский блок на макушке
    objs.append(box(
        f"{prefix}_hat",
        (0, 0, height - m["head_size"][2] * 0.06),
        (m["head_size"][0] * 1.06, m["head_size"][1] * 1.06,
         m["head_size"][2] * 0.16),
        level=CLOTH, outline=0.008,
    ))
    return objs


def make_peasant_rag(prefix="CHR_Peasant_Rag", height=DEFAULT_HEIGHT):
    objs, m = blocky(prefix, height)
    objs += add_face(prefix, m, "neutral")
    objs.append(box(
        f"{prefix}_apron",
        (0, -m["torso_d"] * 0.5 - 0.012,
         m["leg_h"] + m["torso_h"] * 0.45),
        (m["torso_w"] * 0.62, 0.024, m["torso_h"] * 1.15),
        level=CLOTH, outline=0.008,
    ))
    # Мокрая тряпка в правой руке — свисает ниже кисти
    objs.append(box(
        f"{prefix}_rag",
        (m["torso_w"] * 0.5 + 0.13, -0.03, m["leg_h"] * 0.42),
        (0.075, 0.030, m["leg_h"] * 0.42),
        level=CLOTH, outline=0.008,
    ))
    return objs


def make_guard(prefix="CHR_Guard_A", height=DEFAULT_HEIGHT):
    objs, m = blocky(prefix, height, wide=1.12)
    # Шлем закрывает верх головы, но лицо видно
    hc, hs = m["head_center"], m["head_size"]
    objs.append(box(
        f"{prefix}_helm",
        (0, 0, hc[2] + hs[2] * 0.30),
        (hs[0] * 1.10, hs[1] * 1.10, hs[2] * 0.42),
        level=ARMOR, outline=0.009,
    ))
    objs.append(box(
        f"{prefix}_helm_brim",
        (0, -hs[1] * 0.52, hc[2] + hs[2] * 0.16),
        (hs[0] * 1.12, hs[1] * 0.14, hs[2] * 0.10),
        level=ARMOR, outline=0.009,
    ))
    # Нагрудник
    objs.append(box(
        f"{prefix}_chest",
        (0, -m["torso_d"] * 0.5 - 0.02,
         m["leg_h"] + m["torso_h"] * 0.62),
        (m["torso_w"] * 0.95, 0.045, m["torso_h"] * 0.62),
        level=ARMOR, outline=0.009,
    ))
    objs += add_face(prefix, m, "grim")
    # Копьё — вертикальный цилиндр рядом с правой рукой
    objs.append(cyl(
        f"{prefix}_spear",
        (m["torso_w"] * 0.5 + 0.20, 0, height * 0.52),
        0.020, height * 1.00, verts=8, level=ARMOR, outline=0.007,
    ))
    return objs


def make_guard_bucket(prefix="CHR_Guard_Bucket", height=DEFAULT_HEIGHT):
    objs, m = blocky(prefix, height, wide=1.12)
    hc, hs = m["head_center"], m["head_size"]
    objs.append(box(
        f"{prefix}_helm",
        (0, 0, hc[2] + hs[2] * 0.30),
        (hs[0] * 1.10, hs[1] * 1.10, hs[2] * 0.42),
        level=ARMOR, outline=0.009,
    ))
    objs.append(box(
        f"{prefix}_chest",
        (0, -m["torso_d"] * 0.5 - 0.02,
         m["leg_h"] + m["torso_h"] * 0.62),
        (m["torso_w"] * 0.95, 0.045, m["torso_h"] * 0.62),
        level=ARMOR, outline=0.009,
    ))
    objs += add_face(prefix, m, "grim")
    # Ведро — держит перед собой
    objs.append(cyl(
        f"{prefix}_bucket",
        (m["torso_w"] * 0.5 + 0.12, -0.14, m["leg_h"] * 0.55),
        0.11, 0.24, verts=12, level=ARMOR, outline=0.009,
    ))
    return objs


def make_archer(prefix="CHR_Archer", height=DEFAULT_HEIGHT):
    objs, m = blocky(prefix, height, thin=0.88)
    hc, hs = m["head_center"], m["head_size"]
    # Капюшон — рамка вокруг лица: верх и бока, лицо открыто
    objs.append(box(
        f"{prefix}_hood_top",
        (0, 0, hc[2] + hs[2] * 0.32),
        (hs[0] * 1.12, hs[1] * 1.12, hs[2] * 0.36),
        level=CLOTH, outline=0.009,
    ))
    for side, sx in (("L", -1), ("R", 1)):
        objs.append(box(
            f"{prefix}_hood_{side}",
            (sx * hs[0] * 0.53, 0.02, hc[2]),
            (hs[0] * 0.12, hs[1] * 1.02, hs[2] * 0.92),
            level=CLOTH, outline=0.009,
        ))
    objs += add_face(prefix, m, "neutral")
    # Колчан за спиной
    objs.append(cyl(
        f"{prefix}_quiver",
        (0.10, m["torso_d"] * 0.5 + 0.05,
         m["leg_h"] + m["torso_h"] * 0.75),
        0.065, m["torso_h"] * 0.95, verts=10,
        level=CLOTH, outline=0.008,
        rot=(math.radians(12), 0, 0),
    ))
    # Лук в правой руке — вертикальная дуга
    bpy.ops.mesh.primitive_torus_add(
        major_radius=m["torso_h"] * 0.52, minor_radius=0.016,
        major_segments=20, minor_segments=6,
        location=(m["torso_w"] * 0.5 + 0.22, 0, m["leg_h"] + m["torso_h"] * 0.45),
        rotation=(math.radians(90), 0, 0),
    )
    bow = bpy.context.object
    bow.name = f"{prefix}_bow"
    bow.data.materials.clear()
    bow.data.materials.append(mat(ARMOR))
    add_outline(bow, 0.007)
    objs.append(bow)
    return objs


def make_king(prefix="CHR_King", height=1.95):
    objs, m = blocky(prefix, height, wide=1.20)
    hc, hs = m["head_center"], m["head_size"]
    # Корона — блок с зубцами: единственный «широкий сверху» силуэт
    objs.append(box(
        f"{prefix}_crown_band",
        (0, 0, hc[2] + hs[2] * 0.52),
        (hs[0] * 1.16, hs[1] * 1.16, hs[2] * 0.20),
        level=ARMOR, outline=0.009,
    ))
    for i, sx in enumerate((-0.36, 0.0, 0.36)):
        objs.append(box(
            f"{prefix}_crown_spike_{i}",
            (sx * hs[0], 0, hc[2] + hs[2] * 0.72),
            (hs[0] * 0.18, hs[1] * 0.9, hs[2] * 0.26),
            level=ARMOR, outline=0.008,
        ))
    objs += add_face(prefix, m, "grim")
    # Мантия — широкая за спиной и по бокам
    objs.append(box(
        f"{prefix}_mantle",
        (0, m["torso_d"] * 0.5 + 0.03, m["leg_h"] * 0.85),
        (m["torso_w"] * 1.35, 0.06, height * 0.72),
        level=CLOTH, outline=0.010,
    ))
    # Скипетр
    objs.append(cyl(
        f"{prefix}_scepter",
        (m["torso_w"] * 0.5 + 0.22, -0.05, height * 0.50),
        0.026, height * 0.78, verts=8, level=ARMOR, outline=0.008,
    ))
    return objs


def make_hands(prefix="CHR_Hands_Player"):
    """Руки для FPS — отдельная система координат, не стоят на полу."""
    objs = []
    for side, sx in (("L", -1), ("R", 1)):
        x = sx * 0.17
        objs.append(box(
            f"{prefix}_forearm_{side}", (x, 0.30, -0.10),
            (0.085, 0.34, 0.085),
        ))
        objs.append(box(
            f"{prefix}_hand_{side}", (x, 0.50, -0.085),
            (0.105, 0.13, 0.060),
        ))
        objs.append(box(
            f"{prefix}_thumb_{side}", (x - sx * 0.055, 0.46, -0.060),
            (0.038, 0.085, 0.038),
        ))
    return objs


BUILDERS = {
    "peasant": make_peasant,
    "peasant_rag": make_peasant_rag,
    "guard": make_guard,
    "guard_bucket": make_guard_bucket,
    "archer": make_archer,
    "king": make_king,
    "hands": make_hands,
}

# Предметы, не входящие в «рост» персонажа
PROP_SUFFIXES = ("_spear", "_bow", "_scepter", "_crown_spike_0",
                 "_crown_spike_1", "_crown_spike_2")


def reset_scene():
    """Полный сброс. Кэши материалов и контура обнуляются — иначе после
    read_factory_settings ссылки на материалы становятся висячими."""
    global _OUTLINE
    W_MAT.clear()
    _OUTLINE = None
    bpy.ops.wm.read_factory_settings(use_empty=True)


def _bounds(objs):
    corners = [
        o.matrix_world @ mathutils.Vector(c)
        for o in objs if o.type == 'MESH' for c in o.bound_box
    ]
    xs = [c.x for c in corners]
    zs = [c.z for c in corners]
    return {"height": max(zs) - min(zs), "bottom": min(zs), "top": max(zs),
            "width": max(xs) - min(xs)}


def measure(objs):
    visible = [o for o in objs if not o.name.endswith("_OL")]
    tris = 0
    for o in visible:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
            tris += len(o.data.loop_triangles)
    body = [o for o in visible if not o.name.endswith(PROP_SUFFIXES)]
    b = _bounds(body)
    b["tris"] = tris
    return b


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []

    reset_scene()

    if args and args[0] in BUILDERS:
        name = args[0]
        before = set(bpy.data.objects.keys())
        BUILDERS[name]()
        new = [bpy.data.objects[n] for n in set(bpy.data.objects.keys()) - before]
        m = measure(new)
        print(f"[{name}] tris={m['tris']} top={m['top']:.3f} bottom={m['bottom']:.3f}")
        return

    rows = []
    offset = 0.0
    for name, builder in BUILDERS.items():
        before = set(bpy.data.objects.keys())
        objs = builder()
        for o in objs:
            o.location.x += offset
        new = [bpy.data.objects[n] for n in set(bpy.data.objects.keys()) - before]
        m = measure(new)
        if name == "hands":
            tag = "FPS-relative"
        elif abs(m["bottom"]) > 0.01:
            tag = "FLOATING"
        elif m["height"] > 2.10:
            tag = "TOO TALL"
        else:
            tag = "OK"
        rows.append((name, m, tag))
        offset += 1.15

    print("\n=== SUMMARY ===")
    print(f"{'type':16s} {'tris':>5s} {'height':>7s} {'bottom':>7s} "
          f"{'width':>6s}  status")
    bad = 0
    for name, m, tag in rows:
        print(f"{name:16s} {m['tris']:5d} {m['height']:7.3f} "
              f"{m['bottom']:7.3f} {m['width']:6.3f}  {tag}")
        if tag not in ("OK", "FPS-relative"):
            bad += 1
    print(f"\nPROBLEMS: {bad}")


if __name__ == "__main__":
    main()
