"""
Проверка экспортированных FBX: реимпорт и замер.

Запуск:
  blender.exe --background --factory-startup --python verify_fbx.py -- <fbx_dir>
"""

import bpy
import sys
import os
import mathutils

TOL = 0.02


def measure_current():
    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    if not objs:
        return None
    corners = [
        o.matrix_world @ mathutils.Vector(c)
        for o in objs for c in o.bound_box
    ]
    zs = [c.z for c in corners]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    tris = 0
    for o in objs:
        o.data.calc_loop_triangles()
        tris += len(o.data.loop_triangles)
    return {
        "objs": len(objs),
        "tris": tris,
        "height": max(zs) - min(zs),
        "bottom": min(zs),
        "width": max(xs) - min(xs),
        "depth": max(ys) - min(ys),
    }


def rounded_dims(objs):
    """Blender после FBX-реимпорта может повернуть объект — проверяем
    габариты по осям и определяем, какая ось стала вертикалью."""
    corners = [
        o.matrix_world @ mathutils.Vector(c)
        for o in objs for c in o.bound_box
    ]
    for ax, i in (("X", 0), ("Y", 1), ("Z", 2)):
        vals = [c[i] for c in corners]
        print(f"    {ax}: {min(vals):+.3f} .. {max(vals):+.3f}  (span {max(vals)-min(vals):.3f})")


def main():
    argv = sys.argv
    args = argv[argv.index("--") + 1:] if "--" in argv else []
    if not args:
        print("need fbx dir")
        return
    d = args[0]

    files = sorted(f for f in os.listdir(d) if f.endswith(".fbx"))
    if not files:
        print("NO FBX FOUND")
        return

    problems = 0
    print(f"=== VERIFY {len(files)} FBX ===")
    for f in files:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = os.path.join(d, f)
        bpy.ops.import_scene.fbx(filepath=path)
        m = measure_current()
        if not m:
            print(f"{f:28s} EMPTY!")
            problems += 1
            continue

        flags = []
        # Для рук габариты относительные, они не стоят на полу
        if f.startswith("CHR_hands"):
            flags.append("fps-relative (ok)")
        else:
            if abs(m["bottom"]) > TOL:
                flags.append(f"FLOATING bottom={m['bottom']:+.3f}")
            # Король 1.95 м + корона сверху — это норма, порог выше
            hi = 2.20 if f.startswith("CHR_king") else 2.05
            if not (1.70 <= m["height"] <= hi):
                flags.append(f"HEIGHT {m['height']:.3f} (allowed 1.70..{hi})")
        status = "OK" if not flags or flags == ["fps-relative (ok)"] else "PROBLEM"
        if status == "PROBLEM":
            problems += 1
        print(f"{f:28s} tris={m['tris']:5d} h={m['height']:.3f} "
              f"bottom={m['bottom']:+.3f} objs={m['objs']} {status} {'; '.join(flags) if status=='PROBLEM' else ''}")
        if status == "PROBLEM":
            rounded_dims([o for o in bpy.data.objects if o.type == 'MESH'])

    print(f"\nFBX PROBLEMS: {problems}")


if __name__ == "__main__":
    main()
