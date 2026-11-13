"""
Болванки персонажей Calabash — генератор серых боксов.

Запуск:
  blender.exe --background --factory-startup --python blockout.py
  blender.exe --background --factory-startup --python blockout.py -- peasant

Стандарт: рост 1.75 м (Король 1.95), origin на полу между ступнями,
взгляд в +Y, метры.

Пропорции выведены так, чтобы сумма долей давала ровно 1.0:
  ноги 0.47 + торс 0.355 + шея 0.042 + голова 0.133 = 1.0
Классическая схема «7.5 голов» — читаемый, не героический силуэт.
"""

import bpy
import math
import sys
import mathutils

DEFAULT_HEIGHT = 1.75

# Доли роста
LEG = 0.47
TORSO = 0.355
NECK = 0.042
HEAD_D = 0.133


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def box(name, center, size):
    """Куб с явными полными размерами."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    ob = bpy.context.object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return ob


def sphere(name, center, radius, segs=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, segments=segs, ring_count=rings, location=center
    )
    ob = bpy.context.object
    ob.name = name
    return ob


def cylinder(name, center, radius, depth, verts=10):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth, location=center
    )
    ob = bpy.context.object
    ob.name = name
    return ob


def humanoid(prefix, height=DEFAULT_HEIGHT, build="normal", hunch=0.0):
    """Человекоподобная болванка от пола до макушки ровно height.

    build: normal | wide | thin
    hunch: смещение головы вперёд по +Y, метры (сутулость)
    """
    if build == "wide":
        shoulder_w, depth, limb = 0.24, 0.19, 0.078
    elif build == "thin":
        shoulder_w, depth, limb = 0.15, 0.12, 0.050
    else:
        shoulder_w, depth, limb = 0.19, 0.15, 0.062

    leg_h = height * LEG
    torso_h = height * TORSO
    neck_h = height * NECK
    head_r = height * HEAD_D * 0.5

    objs = []

    # Ноги: от пола (z=0) до таза
    for side, sx in (("L", -1), ("R", 1)):
        objs.append(box(
            f"{prefix}_leg_{side}",
            (sx * shoulder_w * 0.45, 0, leg_h * 0.5),
            (limb * 1.3, limb * 1.3, leg_h),
        ))

    # Торс
    torso_z = leg_h + torso_h * 0.5
    objs.append(box(
        f"{prefix}_torso",
        (0, 0, torso_z),
        (shoulder_w * 2, depth, torso_h),
    ))

    # Руки
    arm_len = height * 0.29
    shoulder_z = leg_h + torso_h * 0.90
    for side, sx in (("L", -1), ("R", 1)):
        objs.append(box(
            f"{prefix}_arm_{side}",
            (sx * (shoulder_w + limb * 0.75), 0, shoulder_z - arm_len * 0.45),
            (limb, limb, arm_len),
        ))

    # Шея
    neck_z = leg_h + torso_h
    objs.append(cylinder(
        f"{prefix}_neck", (0, hunch * 0.4, neck_z + neck_h * 0.5),
        limb * 0.42, neck_h, verts=8,
    ))

    # Голова — макушка ровно на height
    head_z = height - head_r
    objs.append(sphere(
        f"{prefix}_head", (0, hunch, head_z), head_r, segs=14, rings=10
    ))

    return objs


def _head_of(objs, prefix):
    return [o for o in objs if o.name == f"{prefix}_head"][0]


def make_peasant(prefix="CHR_Peasant_A", height=DEFAULT_HEIGHT):
    objs = humanoid(prefix, height, build="normal", hunch=0.035)
    # Колпак — утоплен в макушку, чтобы не выходить за рост
    objs.append(cylinder(
        f"{prefix}_hat", (0, 0.035, height - 0.035), 0.10, 0.05, verts=10
    ))
    return objs


def make_peasant_rag(prefix="CHR_Peasant_Rag", height=DEFAULT_HEIGHT):
    objs = humanoid(prefix, height, build="normal", hunch=0.045)
    # Мокрая тряпка свисает из правой руки — главный опознавательный признак
    objs.append(box(
        f"{prefix}_rag", (0.215, 0.085, height * 0.36),
        (0.07, 0.02, height * 0.17),
    ))
    objs.append(cylinder(
        f"{prefix}_hat", (0, 0.045, height - 0.035), 0.10, 0.05, verts=10
    ))
    return objs


def make_guard(prefix="CHR_Guard_A", height=DEFAULT_HEIGHT):
    objs = humanoid(prefix, height, build="wide", hunch=0.0)
    head = _head_of(objs, prefix)
    # Шлем — ровно по голове, сверху не выступает
    objs.append(sphere(
        f"{prefix}_helm", (0, 0, head.location.z),
        head.dimensions.x * 0.5, segs=14, rings=10,
    ))
    # Копьё: горизонталь над плечом — читаемый признак сверху
    objs.append(cylinder(
        f"{prefix}_spear", (0.40, 0.0, height * 0.55), 0.018, height * 1.02, verts=8
    ))
    return objs


def make_guard_bucket(prefix="CHR_Guard_Bucket", height=DEFAULT_HEIGHT):
    objs = humanoid(prefix, height, build="wide", hunch=0.0)
    head = _head_of(objs, prefix)
    objs.append(sphere(
        f"{prefix}_helm", (0, 0, head.location.z),
        head.dimensions.x * 0.5, segs=14, rings=10,
    ))
    # Ведро на уровне груди — видно издалека
    objs.append(cylinder(
        f"{prefix}_bucket", (0.32, 0.16, height * 0.63), 0.10, 0.22, verts=12
    ))
    return objs


def make_archer(prefix="CHR_Archer", height=DEFAULT_HEIGHT):
    objs = humanoid(prefix, height, build="thin", hunch=0.02)
    # Лук в профиль — дуга, отведённая в сторону
    bpy.ops.mesh.primitive_torus_add(
        major_radius=height * 0.24, minor_radius=0.012,
        major_segments=16, minor_segments=6,
        location=(height * 0.15, height * 0.06, height * 0.60),
        rotation=(math.radians(90), 0, 0),
    )
    bow = bpy.context.object
    bow.name = f"{prefix}_bow"
    objs.append(bow)
    return objs


def make_king(prefix="CHR_King", height=1.95):
    objs = humanoid(prefix, height, build="wide", hunch=0.0)
    # Корона — единственный «широкий сверху» силуэт в игре
    bpy.ops.mesh.primitive_cone_add(
        vertices=8, radius1=height * 0.068, radius2=height * 0.046,
        depth=0.14, location=(0, 0, height - 0.05),
    )
    crown = bpy.context.object
    crown.name = f"{prefix}_crown"
    objs.append(crown)
    # Мантия — расширяется книзу, тяжёлый силуэт власти
    objs.append(box(
        f"{prefix}_mantle", (0, -0.02, height * 0.52),
        (0.64, 0.32, height * 0.60),
    ))
    return objs


def make_hands(prefix="CHR_Hands_Player"):
    """Руки для FPS. Не человекоподобная фигура — отдельная система координат:
    предплечья идут от камеры вперёд по +Y, кисти на высоте глаз."""
    objs = []
    for side, sx in (("L", -1), ("R", 1)):
        x = sx * 0.16
        # Предплечье
        objs.append(box(
            f"{prefix}_forearm_{side}", (x, 0.30, -0.10),
            (0.075, 0.34, 0.075),
        ))
        # Кисть
        objs.append(box(
            f"{prefix}_hand_{side}", (x, 0.50, -0.085),
            (0.085, 0.11, 0.045),
        ))
        # Большой палец
        objs.append(box(
            f"{prefix}_thumb_{side}", (x - sx * 0.048, 0.47, -0.065),
            (0.03, 0.075, 0.03),
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


# Оружие и носимые предметы — не входят в «рост» персонажа
WEAPON_SUFFIXES = ("_spear", "_bow", "_crown")


def _bounds(objs):
    corners = [
        o.matrix_world @ mathutils.Vector(c)
        for o in objs if o.type == 'MESH' for c in o.bound_box
    ]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return {
        "height": max(zs) - min(zs), "bottom": min(zs), "top": max(zs),
        "width": max(xs) - min(xs), "depth": max(ys) - min(ys),
    }


def measure(objs):
    tris = 0
    for o in objs:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
            tris += len(o.data.loop_triangles)

    body = [o for o in objs if not o.name.endswith(WEAPON_SUFFIXES)]
    held = [o for o in objs if o.name.endswith(WEAPON_SUFFIXES)]

    b = _bounds(body)
    b["tris"] = tris
    # Насколько предмет выступает над головой (для проверки силуэта сверху)
    b["prop_top"] = max((_bounds([o])["top"] for o in held), default=b["top"])
    return b


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []

    if args and args[0] in BUILDERS:
        name = args[0]
        reset_scene()
        objs = BUILDERS[name]()
        m = measure(objs)
        print(f"[{name}] objs={len(objs)} tris={m['tris']} "
              f"top={m['top']:.3f} bottom={m['bottom']:.3f}")
        return

    reset_scene()
    rows = []
    offset = 0.0
    for name, builder in BUILDERS.items():
        objs = builder()
        for o in objs:
            o.location.x += offset
        m = measure(objs)
        # Для рук габариты относительные — они не стоят на полу
        tag = "FPS-relative" if name == "hands" else (
            "OK" if abs(m["bottom"]) < 0.01 and m["top"] <= 2.0 else "CHECK"
        )
        rows.append((name, m, tag))
        offset += 1.3

    print("\n=== SUMMARY ===")
    print(f"{'type':16s} {'tris':>5s} {'height':>7s} {'bottom':>7s} {'width':>6s} {'propTop':>7s}  status")
    problems = 0
    for name, m, tag in rows:
        print(f"{name:16s} {m['tris']:5d} {m['height']:7.3f} "
              f"{m['bottom']:7.3f} {m['width']:6.3f} {m['prop_top']:7.3f}  {tag}")
        if tag == "CHECK":
            problems += 1
    print(f"\nPROBLEMS: {problems}")


if __name__ == "__main__":
    main()
