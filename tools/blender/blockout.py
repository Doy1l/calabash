"""
Персонажи Calabash — малополигональные гуманоиды в монохромной манга-стилизации.

НЕ блочный стиль и НЕ Roblox. Референс давал только монохром:
белые поверхности + чёрный контур. Форма — нормальный человек,
просто с малым числом полигонов (как в Ultrakill: не качество модели решает).

Запуск:
  blender.exe --background --factory-startup --python blockout.py
  blender.exe --background --factory-startup --python blockout.py -- guard

Стандарт: рост 1.75 м (Король 1.95), origin на полу между ступнями,
лицо смотрит в -Y, метры, человеческие пропорции (~7.5 голов).
"""

import bpy
import bmesh
import math
import sys
import mathutils

DEFAULT_HEIGHT = 1.75

# Палитра: только серые градации
WHITE = 0.94
CLOTH = 0.86
ARMOR = 0.74
DARK = 0.10
BLACK = 0.02

W_MAT = {}
_OUTLINE = None


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


def outline_mat():
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


def add_outline(ob, thickness=0.008):
    """Inverted hull: копия меша раздувается наружу, нормали перевёрнуты."""
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


def frustum(name, bw, bd, tw, td, h, level=WHITE, outline=0.008):
    """Усечённая призма: 8 вершин, 12 треугольников. Основа всех частей тела —
    даёт сужение (плечи шире талии) почти бесплатно по полигонам."""
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)

    z0, z1 = -h * 0.5, h * 0.5
    verts = [
        (-bw / 2, -bd / 2, z0), (bw / 2, -bd / 2, z0),
        (bw / 2, bd / 2, z0), (-bw / 2, bd / 2, z0),
        (-tw / 2, -td / 2, z1), (tw / 2, -td / 2, z1),
        (tw / 2, td / 2, z1), (-tw / 2, td / 2, z1),
    ]
    faces = [
        (0, 1, 2, 3), (7, 6, 5, 4),
        (0, 4, 5, 1), (1, 5, 6, 2),
        (2, 6, 7, 3), (3, 7, 4, 0),
    ]
    me.from_pydata(verts, [], faces)
    me.update()

    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()

    ob.data.materials.append(mat(level))
    if outline:
        add_outline(ob, outline)
    return ob


def place(ob, loc=(0, 0, 0), rot=None):
    ob.location = loc
    if rot:
        ob.rotation_euler = rot
    return ob


def lowsphere(name, radius, level=WHITE, outline=0.008, segs=10, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, segments=segs, ring_count=rings, location=(0, 0, 0)
    )
    ob = bpy.context.object
    ob.name = name
    ob.data.materials.clear()
    ob.data.materials.append(mat(level))
    for p in ob.data.polygons:
        p.use_smooth = True
    if outline:
        add_outline(ob, outline)
    return ob


def lowcyl(name, radius, depth, verts=8, level=WHITE, outline=0.008):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth, location=(0, 0, 0)
    )
    ob = bpy.context.object
    ob.name = name
    ob.data.materials.clear()
    ob.data.materials.append(mat(level))
    if outline:
        add_outline(ob, outline)
    return ob


# ---------------------------------------------------------------- гуманоид

def anatomy(height=DEFAULT_HEIGHT, build="normal"):
    """Чистая математика пропорций — без создания геометрии.
    Один источник истины для humanoid() и для одежды."""
    if build == "wide":
        sw, lw, bw = 1.14, 1.14, 1.06
    elif build == "thin":
        sw, lw, bw = 0.91, 0.86, 0.92
    else:
        sw, lw, bw = 1.0, 1.0, 1.0

    h = height
    head_h = 0.133 * h
    neck_h = 0.026 * h

    z_crotch = 0.475 * h
    z_knee = 0.270 * h
    z_ankle = 0.055 * h
    z_waist = 0.600 * h
    z_chest = 0.755 * h
    z_shoulder = 0.840 * h
    z_neck_top = z_shoulder + neck_h
    z_head_c = (z_neck_top + h) * 0.5

    hips_w = 0.185 * h * bw
    waist_w = 0.155 * h * bw
    chest_w = 0.205 * h * sw
    shoulder_w = 0.215 * h * sw
    body_d = 0.115 * h * bw

    arm_upper_w = 0.050 * h * lw
    shoulder_x = shoulder_w * 0.5 - arm_upper_w * 0.35
    head_r = head_h * 0.5

    return {
        "height": h,
        "head_h": head_h,
        "head_r": head_r,
        "neck_h": neck_h,
        "z_crotch": z_crotch,
        "z_knee": z_knee,
        "z_ankle": z_ankle,
        "z_waist": z_waist,
        "z_chest": z_chest,
        "z_shoulder": z_shoulder,
        "z_head_c": z_head_c,
        "hips_w": hips_w,
        "waist_w": waist_w,
        "chest_w": chest_w,
        "shoulder_w": shoulder_w,
        "body_d": body_d,
        "shoulder_x": shoulder_x,
        "arm_upper_w": arm_upper_w,
        "head_c": (0.0, 0.0, z_head_c),
        "front_y": -body_d * 0.55,
        # Плоскость лица ЧУТЬ впереди поверхности головы, иначе плашки
        # тонут внутри сферы и лицо не читается
        "head_front_y": -head_r * 1.005,
    }


def humanoid(prefix, height=DEFAULT_HEIGHT, build="normal"):
    """Нормальный человек, 7.5 голов, всё из усечённых призм."""
    m = anatomy(height, build)
    h = m["height"]
    z_crotch, z_knee, z_ankle = m["z_crotch"], m["z_knee"], m["z_ankle"]
    z_waist, z_chest, z_shoulder = m["z_waist"], m["z_chest"], m["z_shoulder"]
    neck_h = m["neck_h"]
    hips_w, waist_w = m["hips_w"], m["waist_w"]
    chest_w, shoulder_w = m["chest_w"], m["shoulder_w"]
    body_d = m["body_d"]
    arm_upper_w = m["arm_upper_w"]
    shoulder_x = m["shoulder_x"]
    head_r = m["head_r"]

    objs = []

    # --- Таз и торс (три призмы, сужение к талии) ---
    pelvis = frustum(f"{prefix}_pelvis", hips_w, body_d,
                     waist_w, body_d * 0.95, z_waist - z_crotch)
    place(pelvis, (0, 0, (z_crotch + z_waist) * 0.5))
    objs.append(pelvis)

    belly = frustum(f"{prefix}_belly", waist_w, body_d * 0.95,
                    chest_w, body_d * 1.05, z_chest - z_waist)
    place(belly, (0, 0, (z_waist + z_chest) * 0.5))
    objs.append(belly)

    chest = frustum(f"{prefix}_chest", chest_w, body_d * 1.05,
                    shoulder_w, body_d * 1.1, z_shoulder - z_chest)
    place(chest, (0, 0, (z_chest + z_shoulder) * 0.5))
    objs.append(chest)

    # --- Шея ---
    neck = lowcyl(f"{prefix}_neck", 0.030 * h * (shoulder_w / (0.215 * h)),
                  neck_h, verts=8)
    place(neck, (0, 0, z_shoulder + neck_h * 0.5))
    objs.append(neck)

    # --- Голова ---
    head = lowsphere(f"{prefix}_head", head_r, segs=10, rings=6)
    head.scale = (0.92, 1.0, 1.0)
    place(head, (0, 0, m["z_head_c"]))
    objs.append(head)

    # --- Руки: плечо → локоть → кисть ---
    upper_len = 0.185 * h
    lower_len = 0.175 * h
    arm_lower_w = 0.040 * h * (shoulder_w / (0.215 * h))
    tilt = math.radians(4.0)

    for side, sx in (("L", -1), ("R", 1)):
        up = frustum(f"{prefix}_arm_up_{side}",
                     arm_upper_w, arm_upper_w,
                     arm_lower_w, arm_lower_w, upper_len)
        place(up, (sx * shoulder_x, 0, z_shoulder - upper_len * 0.5),
              (0, sx * -tilt, 0))
        objs.append(up)

        elbow_z = z_shoulder - upper_len
        lo = frustum(f"{prefix}_arm_lo_{side}",
                     arm_lower_w, arm_lower_w,
                     arm_lower_w * 0.85, arm_lower_w * 0.85, lower_len)
        place(lo, (sx * shoulder_x, 0, elbow_z - lower_len * 0.5),
              (0, sx * -tilt, 0))
        objs.append(lo)

        wrist_z = elbow_z - lower_len
        hand = frustum(f"{prefix}_hand_{side}",
                       arm_lower_w * 1.05, arm_lower_w * 0.55,
                       arm_lower_w * 0.8, arm_lower_w * 0.45, 0.055 * h)
        place(hand, (sx * shoulder_x, 0, wrist_z - 0.027 * h),
              (0, sx * -tilt, 0))
        objs.append(hand)

    # --- Ноги ---
    thigh_w = 0.072 * h * (shoulder_w / (0.215 * h))
    shin_w = 0.052 * h * (shoulder_w / (0.215 * h))
    leg_x = 0.048 * h * (hips_w / (0.185 * h))

    for side, sx in (("L", -1), ("R", 1)):
        thigh = frustum(f"{prefix}_thigh_{side}",
                        thigh_w, thigh_w * 1.05,
                        shin_w * 1.15, shin_w * 1.15,
                        z_crotch - z_knee)
        place(thigh, (sx * leg_x, 0, (z_crotch + z_knee) * 0.5))
        objs.append(thigh)

        shin = frustum(f"{prefix}_shin_{side}",
                       shin_w * 1.15, shin_w * 1.15,
                       shin_w * 0.85, shin_w * 0.95,
                       z_knee - z_ankle)
        place(shin, (sx * leg_x, 0, (z_knee + z_ankle) * 0.5))
        objs.append(shin)

        foot = frustum(f"{prefix}_foot_{side}",
                       shin_w * 0.95, 0.075 * h,
                       shin_w * 0.85, 0.050 * h,
                       z_ankle)
        place(foot, (sx * leg_x, -0.022 * h, z_ankle * 0.5))
        objs.append(foot)

    return objs, m


# ---------------------------------------------------------------- лицо

def add_face(prefix, m, style="neutral"):
    """Лицо геометрией из тонких призм, прилегающих к сфере головы.
    Y каждой плашки считается из радиуса сферы на её высоте — иначе плашки
    либо тонут внутри головы, либо висят в воздухе."""
    objs = []
    hc = m["head_c"]
    hr = m["head_r"]
    hh = m["head_h"]

    eye_x = hr * 0.38
    eye_z = hc[2] + hh * 0.075
    brow_z = eye_z + hh * 0.135
    mouth_z = hc[2] - hh * 0.185

    def surf_y(z):
        """Передняя поверхность сферы на высоте z (минус чуть наружу)."""
        dz = z - hc[2]
        inner = max(hr * hr - dz * dz, 0.0)
        return -math.sqrt(inner) - 0.004

    def plate(name, x, z, w, hgt, level=DARK, rot=None):
        ob = frustum(name, w, 0.010, w * 0.92, 0.010, hgt,
                     level=level, outline=0.0)
        place(ob, (x, surf_y(z), z), rot)
        objs.append(ob)
        return ob

    for side, sx in (("L", -1), ("R", 1)):
        plate(f"{prefix}_eye_{side}", sx * eye_x, eye_z, hr * 0.30, hh * 0.17)

    if style == "neutral":
        plate(f"{prefix}_mouth", 0, mouth_z, hr * 0.30, hh * 0.030)
    elif style == "open":
        plate(f"{prefix}_mouth", 0, mouth_z, hr * 0.20, hh * 0.115)
    elif style == "smirk":
        plate(f"{prefix}_mouth", 0, mouth_z, hr * 0.32, hh * 0.030,
              rot=(0, math.radians(10), 0))
    elif style == "grim":
        plate(f"{prefix}_mouth", 0, mouth_z, hr * 0.36, hh * 0.040)
        for side, sx in (("L", -1), ("R", 1)):
            plate(f"{prefix}_brow_{side}", sx * eye_x, brow_z,
                  hr * 0.34, hh * 0.045,
                  rot=(0, math.radians(-16 * sx), 0))

    # Нос — маленький клин, выступает чуть сильнее лица
    nose_z = hc[2] - hh * 0.045
    nose = frustum(f"{prefix}_nose", hr * 0.13, 0.030,
                   hr * 0.09, 0.010, hh * 0.13, level=WHITE, outline=0.006)
    place(nose, (0, surf_y(nose_z) - 0.012, nose_z))
    objs.append(nose)
    return objs


# ---------------------------------------------------------------- типы

def make_peasant(prefix="CHR_Peasant_A", height=DEFAULT_HEIGHT):
    objs, m = humanoid(prefix, height)
    objs += add_face(prefix, m, "neutral")

    # Фартук: панель на груди, спускается до колен
    apron_top = m["z_waist"] + 0.05 * m["height"]
    apron_h = 0.36 * m["height"]
    ap = frustum(f"{prefix}_apron",
                 m["waist_w"] * 0.95, 0.018,
                 m["chest_w"] * 0.72, 0.018, apron_h, level=CLOTH, outline=0.006)
    place(ap, (0, m["front_y"] - 0.008, apron_top - apron_h * 0.5))
    objs.append(ap)

    # Рубаха: короткие рукава поверх плеч
    for side, sx in (("L", -1), ("R", 1)):
        sl = frustum(f"{prefix}_sleeve_{side}",
                     0.070 * m["height"], 0.070 * m["height"],
                     0.058 * m["height"], 0.058 * m["height"],
                     0.085 * m["height"], level=CLOTH, outline=0.006)
        place(sl, (sx * m["shoulder_x"], 0, m["z_shoulder"] - 0.030 * m["height"]))
        objs.append(sl)

    return objs


def make_peasant_rag(prefix="CHR_Peasant_Rag", height=DEFAULT_HEIGHT):
    objs = make_peasant(prefix, height)
    m = anatomy(height)

    # Мокрая тряпка свисает из правой кисти
    rag = frustum(f"{prefix}_rag",
                  0.045, 0.012, 0.030, 0.010, 0.17 * height,
                  level=CLOTH, outline=0.006)
    place(rag, (m["shoulder_x"] + 0.01, 0.005,
                m["z_shoulder"] - 0.42 * height))
    objs.append(rag)
    return objs


def make_guard(prefix="CHR_Guard_A", height=DEFAULT_HEIGHT):
    objs, m = humanoid(prefix, height, build="wide")
    objs += add_face(prefix, m, "grim")

    # Шлем: купол + поля, лицо открыто
    hr, hh, hc = m["head_r"], m["head_h"], m["head_c"]
    dome = lowsphere(f"{prefix}_helm", hr * 1.06, level=ARMOR,
                     outline=0.007, segs=10, rings=6)
    dome.scale = (0.94, 1.0, 0.78)
    place(dome, (0, 0, hc[2] + hh * 0.22))
    objs.append(dome)

    brim = frustum(f"{prefix}_brim",
                   hr * 2.35, hr * 1.95, hr * 2.45, hr * 2.05, 0.020,
                   level=ARMOR, outline=0.006)
    place(brim, (0, -0.006, hc[2] + hh * 0.11))
    objs.append(brim)

    # Нагрудник
    cuir = frustum(f"{prefix}_cuirass",
                   m["chest_w"] * 1.06, m["body_d"] * 1.16,
                   m["shoulder_w"] * 1.02, m["body_d"] * 1.16,
                   (m["z_shoulder"] - m["z_waist"]) * 0.82,
                   level=ARMOR, outline=0.007)
    place(cuir, (0, 0, m["z_waist"] + (m["z_shoulder"] - m["z_waist"]) * 0.48))
    objs.append(cuir)

    # Копьё — длинный тонкий цилиндр у правого плеча
    spear = lowcyl(f"{prefix}_spear", 0.016, height * 1.05, verts=6,
                   level=ARMOR, outline=0.005)
    place(spear, (m["shoulder_x"] + 0.075, -0.03, height * 0.53))
    objs.append(spear)
    return objs


def make_guard_bucket(prefix="CHR_Guard_Bucket", height=DEFAULT_HEIGHT):
    objs, m = humanoid(prefix, height, build="wide")
    objs += add_face(prefix, m, "grim")

    hr, hh, hc = m["head_r"], m["head_h"], m["head_c"]
    dome = lowsphere(f"{prefix}_helm", hr * 1.06, level=ARMOR,
                     outline=0.007, segs=10, rings=6)
    dome.scale = (0.94, 1.0, 0.78)
    place(dome, (0, 0, hc[2] + hh * 0.22))
    objs.append(dome)

    cuir = frustum(f"{prefix}_cuirass",
                   m["chest_w"] * 1.06, m["body_d"] * 1.16,
                   m["shoulder_w"] * 1.02, m["body_d"] * 1.16,
                   (m["z_shoulder"] - m["z_waist"]) * 0.82,
                   level=ARMOR, outline=0.007)
    place(cuir, (0, 0, m["z_waist"] + (m["z_shoulder"] - m["z_waist"]) * 0.48))
    objs.append(cuir)

    # Ведро — держит на уровне пояса, чуть впереди
    bucket = lowcyl(f"{prefix}_bucket", 0.105, 0.20, verts=10,
                    level=ARMOR, outline=0.007)
    place(bucket, (m["shoulder_x"] + 0.02, -0.115, m["z_waist"] - 0.02))
    objs.append(bucket)
    return objs


def make_archer(prefix="CHR_Archer", height=DEFAULT_HEIGHT):
    objs, m = humanoid(prefix, height, build="thin")
    objs += add_face(prefix, m, "neutral")

    hr, hh, hc = m["head_r"], m["head_h"], m["head_c"]

    # Капюшон: задник + боковины, лицо открыто
    back = frustum(f"{prefix}_hood_back",
                   hr * 2.15, hr * 1.10, hr * 1.85, hr * 0.9, hh * 1.35,
                   level=CLOTH, outline=0.007)
    place(back, (0, hr * 0.52, hc[2] + hh * 0.06))
    objs.append(back)

    for side, sx in (("L", -1), ("R", 1)):
        flap = frustum(f"{prefix}_hood_side_{side}",
                       hr * 0.22, hr * 1.5, hr * 0.20, hr * 1.1, hh * 1.05,
                       level=CLOTH, outline=0.006)
        place(flap, (sx * hr * 0.95, hr * 0.25, hc[2] + hh * 0.02))
        objs.append(flap)

    top = frustum(f"{prefix}_hood_top",
                  hr * 1.9, hr * 1.7, hr * 0.9, hr * 0.9, hh * 0.55,
                  level=CLOTH, outline=0.006)
    place(top, (0, hr * 0.28, hc[2] + hh * 0.55))
    objs.append(top)

    # Колчан за спиной
    quiver = lowcyl(f"{prefix}_quiver", 0.055, 0.42, verts=8,
                    level=CLOTH, outline=0.006)
    place(quiver, (0.07, m["body_d"] * 0.72, m["z_chest"]),
          (math.radians(14), 0, 0))
    objs.append(quiver)

    # Лук — тонкий тор в профиль
    bpy.ops.mesh.primitive_torus_add(
        major_radius=height * 0.30, minor_radius=0.012,
        major_segments=14, minor_segments=5,
        location=(0, 0, 0),
        rotation=(math.radians(90), 0, 0),
    )
    bow = bpy.context.object
    bow.name = f"{prefix}_bow"
    bow.data.materials.clear()
    bow.data.materials.append(mat(ARMOR))
    add_outline(bow, 0.005)
    place(bow, (m["shoulder_x"] + 0.085, -0.02, m["z_waist"] + 0.06),
          (math.radians(90), 0, 0))
    objs.append(bow)
    return objs


def make_king(prefix="CHR_King", height=1.95):
    objs, m = humanoid(prefix, height, build="wide")
    objs += add_face(prefix, m, "grim")

    hr, hh, hc = m["head_r"], m["head_h"], m["head_c"]

    # Корона: обод + пять зубцов
    band = frustum(f"{prefix}_crown_band",
                   hr * 2.25, hr * 2.05, hr * 2.15, hr * 1.95, hh * 0.24,
                   level=ARMOR, outline=0.006)
    place(band, (0, 0, hc[2] + hh * 0.60))
    objs.append(band)

    for i in range(5):
        a = (i / 4.0 - 0.5) * math.radians(200)
        px = math.sin(a) * hr * 0.85
        py = -math.cos(a) * hr * 0.80
        spike = frustum(f"{prefix}_crown_{i}",
                        hr * 0.26, hr * 0.26, 0.001, 0.001, hh * 0.34,
                        level=ARMOR, outline=0.005)
        place(spike, (px, py, hc[2] + hh * 0.86))
        objs.append(spike)

    # Мантия: расширяющаяся книзу фигура за спиной
    mantle = frustum(f"{prefix}_mantle",
                     m["shoulder_w"] * 1.55, m["body_d"] * 1.5,
                     m["shoulder_w"] * 0.96, m["body_d"] * 1.05,
                     height * 0.62, level=CLOTH, outline=0.008)
    place(mantle, (0, m["body_d"] * 0.42, height * 0.56))
    objs.append(mantle)

    # Скипетр
    scep = lowcyl(f"{prefix}_scepter", 0.020, height * 0.72, verts=6,
                  level=ARMOR, outline=0.005)
    place(scep, (m["shoulder_x"] + 0.09, -0.02, height * 0.50))
    objs.append(scep)
    return objs


def make_hands(prefix="CHR_Hands_Player"):
    """Руки для FPS — отдельная система координат, не стоят на полу."""
    objs = []
    for side, sx in (("L", -1), ("R", 1)):
        x = sx * 0.17
        fore = frustum(f"{prefix}_forearm_{side}",
                       0.075, 0.085, 0.060, 0.065, 0.32)
        place(fore, (x, 0.30, -0.10), (math.radians(84), 0, 0))
        objs.append(fore)

        hand = frustum(f"{prefix}_hand_{side}",
                       0.055, 0.080, 0.048, 0.055, 0.11)
        place(hand, (x, 0.47, -0.085), (math.radians(78), 0, 0))
        objs.append(hand)

        thumb = frustum(f"{prefix}_thumb_{side}",
                        0.026, 0.030, 0.020, 0.024, 0.070)
        place(thumb, (x - sx * 0.050, 0.44, -0.070),
              (math.radians(52), 0, sx * math.radians(18)))
        objs.append(thumb)
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

PROP_SUFFIXES = ("_spear", "_bow", "_scepter", "_quiver")


def reset_scene():
    global _OUTLINE
    W_MAT.clear()
    _OUTLINE = None
    bpy.ops.wm.read_factory_settings(use_empty=True)


def _bounds(objs):
    corners = [o.matrix_world @ mathutils.Vector(c)
               for o in objs if o.type == 'MESH' for c in o.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return {"height": max(zs) - min(zs), "bottom": min(zs), "top": max(zs),
            "width": max(xs) - min(xs), "depth": max(ys) - min(ys)}


def measure(objs):
    # Обязательно: без этого matrix_world остаётся устаревшим и замеры врут
    bpy.context.view_layer.update()

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
        before = set(bpy.data.objects.keys())
        BUILDERS[args[0]]()
        new = [bpy.data.objects[n] for n in set(bpy.data.objects.keys()) - before]
        m = measure(new)
        print(f"[{args[0]}] tris={m['tris']} top={m['top']:.3f} "
              f"bottom={m['bottom']:.3f}")
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
        rows.append((name, m))
        offset += 1.15

    print("\n=== SUMMARY ===")
    print(f"{'type':16s} {'tris':>5s} {'height':>7s} {'bottom':>7s} "
          f"{'width':>6s}  status")
    bad = 0
    for name, m in rows:
        issues = []
        if name == "hands":
            tag = "fps-relative"
        else:
            if abs(m["bottom"]) > 0.012:
                issues.append(f"floating {m['bottom']:+.3f}")
            hi = 2.20 if name == "king" else 1.90
            if not (1.70 <= m["height"] <= hi):
                issues.append(f"height {m['height']:.2f}")
            if m["tris"] > 2600:
                issues.append(f"too many tris {m['tris']}")
            tag = "OK" if not issues else "PROBLEM"
        if tag == "PROBLEM":
            bad += 1
        print(f"{name:16s} {m['tris']:5d} {m['height']:7.3f} "
              f"{m['bottom']:7.3f} {m['width']:6.3f}  {tag} "
              f"{'; '.join(issues) if issues else ''}")

    # Проверка человеческих пропорций (7-8 голов — норма)
    print("\n--- anatomy (peasant) ---")
    before = set(bpy.data.objects.keys())
    make_peasant("CHK", DEFAULT_HEIGHT)
    new = [bpy.data.objects[n] for n in set(bpy.data.objects.keys()) - before]
    head = [o for o in new if o.name == "CHK_head"][0]
    hb = _bounds([head])
    ratio = DEFAULT_HEIGHT / hb["height"]
    print(f"heads tall: {ratio:.2f}  (human range 7.0-8.0)")
    print(f"head height: {hb['height']:.3f} m")

    print(f"\nPROBLEMS: {bad}")


if __name__ == "__main__":
    main()
