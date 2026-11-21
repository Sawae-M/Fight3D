# -*- coding: utf-8 -*-
"""
カゲツ(仮面の剣士)の3Dモデルを Blender で自動生成し、Unity 用 FBX に書き出すスクリプト。

使い方(PowerShell):
  & "D:\steam\steamapps\common\Blender\blender.exe" --background --python Art\Blender\build_kagetsu.py

出力:
  Assets/Models/Characters/Kagetsu/Kagetsu.fbx   … Unity が読む本体(Humanoid 対応の骨入り)
  Art/Blender/Kagetsu.blend                      … Blender で開いて直す用
  Art/Blender/Kagetsu_preview_*.png              … 見た目確認用の画像

方針:
  - 骨の名前は Unity Humanoid の自動割り当てに合う名前(Hips, Spine, LeftUpperArm …)。
  - T ポーズ、身長 約1.95m(今の人形と同じくらい)、正面は Unity の +Z。
  - 体の各パーツは「1つの骨に100%」で付ける(鎧っぽい作り。重みが壊れにくい)。
  - 見た目はオリジナル。テーマ「月蝕の闘技場」: 月蝕の円盤と三日月をモチーフにする。
"""

import math
import os

import bpy
import bmesh
from mathutils import Vector

# ---------------------------------------------------------------
# 調整する数値はここに集める(すべて仮。仕様書にまだ無い値)
# ---------------------------------------------------------------
HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
FBX_PATH = os.path.join(PROJECT, "Assets", "Models", "Characters", "Kagetsu", "Kagetsu.fbx")
BLEND_PATH = os.path.join(HERE, "Kagetsu.blend")
PREVIEW_PREFIX = os.path.join(HERE, "Kagetsu_preview_")

SEGMENTS = 14  # 円柱の角の数(多いほど丸い。仮)

# 色(sRGB。今の人形 DollBuilder.cs の色に合わせた)
COLORS = {
    "KG_Cloth":   ((0.12, 0.10, 0.20), 0.0, 0.8),   # 濃い紫紺の上着
    "KG_Hakama":  ((0.20, 0.18, 0.26), 0.0, 0.85),  # 袴(はかま)風のズボン(上着より少し明るくして見分けやすく)
    "KG_Crimson": ((0.42, 0.08, 0.13), 0.0, 0.7),   # 暗い赤の帯・襟巻き
    "KG_Mask":    ((0.92, 0.90, 0.85), 0.0, 0.45),  # 骨のような白い仮面
    "KG_Black":   ((0.05, 0.05, 0.06), 0.0, 0.6),   # 手袋・靴・目の切れ込み
    "KG_Metal":   ((0.55, 0.57, 0.63), 0.85, 0.35), # 籠手・すね当て・肩当て
    "KG_Gold":    ((0.80, 0.62, 0.28), 0.9, 0.3),   # 月蝕の輪(金)
    "KG_Hair":    ((0.82, 0.84, 0.90), 0.0, 0.5),   # 銀の髪
    "KG_Blade":   ((0.75, 0.80, 0.90), 1.0, 0.15),  # 刃
    "KG_Eclipse": ((0.02, 0.02, 0.03), 0.3, 0.3),   # 月蝕の黒い円盤
}

# 骨(名前: (頭の位置, しっぽの位置, 親))。Blender は Z が上、キャラは -Y を向く。左手は +X。
BONES = {
    "Hips":          ((0, 0, 1.00), (0, 0, 1.10), None),
    "Spine":         ((0, 0, 1.10), (0, 0, 1.30), "Hips"),
    "Chest":         ((0, 0, 1.30), (0, 0, 1.52), "Spine"),
    "Neck":          ((0, 0, 1.55), (0, 0, 1.66), "Chest"),
    "Head":          ((0, 0, 1.66), (0, 0, 1.95), "Neck"),
}
for side, s in (("Left", 1), ("Right", -1)):
    BONES.update({
        f"{side}Shoulder": ((s * 0.03, 0, 1.49), (s * 0.17, 0, 1.50), "Chest"),
        f"{side}UpperArm": ((s * 0.17, 0, 1.50), (s * 0.45, 0, 1.50), f"{side}Shoulder"),
        f"{side}LowerArm": ((s * 0.45, 0, 1.50), (s * 0.71, 0, 1.50), f"{side}UpperArm"),
        f"{side}Hand":     ((s * 0.71, 0, 1.50), (s * 0.82, 0, 1.50), f"{side}LowerArm"),
        f"{side}UpperLeg": ((s * 0.10, 0, 0.98), (s * 0.10, 0.01, 0.54), "Hips"),
        f"{side}LowerLeg": ((s * 0.10, 0.01, 0.54), (s * 0.10, 0.03, 0.09), f"{side}UpperLeg"),
        f"{side}Foot":     ((s * 0.10, 0.03, 0.09), (s * 0.10, -0.10, 0.03), f"{side}LowerLeg"),
        f"{side}Toes":     ((s * 0.10, -0.10, 0.03), (s * 0.10, -0.18, 0.03), f"{side}Foot"),
    })


# ---------------------------------------------------------------
# 道具
# ---------------------------------------------------------------
def srgb_to_linear(c):
    """sRGB の色を Blender 内部(リニア)の色に直す。"""
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


def clear_scene():
    """最初からある立方体・カメラ・ライトなどを全部消す。"""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_materials():
    """COLORS からマテリアルを作って辞書で返す。"""
    mats = {}
    for name, (rgb, metal, rough) in COLORS.items():
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        lin = srgb_to_linear(rgb) + (1.0,)
        bsdf.inputs["Base Color"].default_value = lin
        bsdf.inputs["Metallic"].default_value = metal
        bsdf.inputs["Roughness"].default_value = rough
        m.diffuse_color = lin  # 確認画像(Workbench)用の色
        mats[name] = m
    return mats


PARTS = []  # (オブジェクト, グループ名) 作ったパーツの一覧


def finish(obj, mat, bone, smooth, group="body"):
    """作ったパーツに色と骨を付け、変形を確定して一覧に入れる。"""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(MATS[mat])
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    for p in obj.data.polygons:
        p.use_smooth = smooth
    obj.select_set(False)
    PARTS.append((obj, group))
    return obj


def box(name, loc, size, mat, bone, rot=(0, 0, 0), group="body"):
    """箱のパーツ。size は (幅X, 奥行Y, 高さZ) メートル、rot は度。"""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc,
                                    rotation=[math.radians(r) for r in rot])
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    return finish(o, mat, bone, False, group)


def ball(name, loc, size, mat, bone, rot=(0, 0, 0), group="body", seg=16):
    """球のパーツ。size は X/Y/Z 方向の半径。"""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1,
                                         location=loc, rotation=[math.radians(r) for r in rot])
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    return finish(o, mat, bone, True, group)


def limb(name, p0, p1, r0, r1, mat, bone, squash=1.0, smooth=True, group="body", seg=SEGMENTS):
    """p0 から p1 へ伸びる先細りの筒。r0/r1 は両端の半径、squash は前後(Y)のつぶし具合。"""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bpy.ops.mesh.primitive_cone_add(vertices=seg, radius1=r0, radius2=r1, depth=d.length,
                                    location=(p0 + p1) / 2)
    o = bpy.context.active_object
    o.name = name
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = q
    o.scale = (1, 1, 1)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.select_set(False)
    if squash != 1.0:
        for v in o.data.vertices:
            v.co.y *= squash  # 原点はパーツの中心なので、その場で前後につぶれる
    return finish(o, mat, bone, smooth, group)


def ring(name, loc, major, minor, mat, bone, rot=(0, 0, 0), group="body"):
    """輪っかのパーツ(月蝕の輪・襟巻きなど)。"""
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=24, minor_segments=8, location=loc,
                                     rotation=[math.radians(r) for r in rot])
    o = bpy.context.active_object
    o.name = name
    return finish(o, mat, bone, True, group)


def disc(name, loc, radius, depth, mat, bone, rot=(0, 0, 0), group="body", seg=24):
    """平たい円盤(月蝕の円盤など)。"""
    bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=radius, depth=depth, location=loc,
                                        rotation=[math.radians(r) for r in rot])
    o = bpy.context.active_object
    o.name = name
    return finish(o, mat, bone, False, group)


# ---------------------------------------------------------------
# 骨を作る
# ---------------------------------------------------------------
def build_armature():
    """Humanoid 用の骨組みを作って返す。"""
    arm_data = bpy.data.armatures.new("KagetsuRig")
    arm = bpy.data.objects.new("Armature", arm_data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    for name, (h, t, parent) in BONES.items():
        b = eb.new(name)
        b.head = h
        b.tail = t
        if parent:
            b.parent = eb[parent]
            # 親のしっぽと同じ位置なら「つながった骨」にする
            if (Vector(eb[parent].tail) - Vector(h)).length < 1e-4:
                b.use_connect = True
    # 腕と脚の骨のロール(ねじれ)をそろえる
    bpy.ops.armature.select_all(action="SELECT")
    bpy.ops.armature.calculate_roll(type="GLOBAL_POS_Z")
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


# ---------------------------------------------------------------
# 体を作る
# ---------------------------------------------------------------
def build_body():
    """カゲツの体・服・仮面・剣のパーツを全部作る。"""
    # --- 頭: フードと白い仮面、月蝕の額飾り ---
    ball("Hood", (0, 0.025, 1.80), (0.118, 0.12, 0.13), "KG_Cloth", "Head")
    limb("HoodDrape", (0, 0.07, 1.74), (0, 0.11, 1.56), 0.10, 0.13, "KG_Cloth", "Head", squash=0.6)
    ball("Mask", (0, -0.082, 1.785), (0.098, 0.045, 0.125), "KG_Mask", "Head")
    # 三日月形に見える、つり上がった目の切れ込み
    box("MaskEyeL", (0.042, -0.124, 1.80), (0.055, 0.012, 0.012), "KG_Black", "Head", rot=(0, -18, 0))
    box("MaskEyeR", (-0.042, -0.124, 1.80), (0.055, 0.012, 0.012), "KG_Black", "Head", rot=(0, 18, 0))
    box("MaskLine", (0, -0.126, 1.72), (0.008, 0.01, 0.07), "KG_Crimson", "Head")  # 口元の赤い一本線
    disc("Eclipse", (0, -0.122, 1.862), 0.020, 0.01, "KG_Eclipse", "Head", rot=(90, 0, 0))
    ring("EclipseRing", (0, -0.122, 1.862), 0.024, 0.0045, "KG_Gold", "Head", rot=(90, 0, 0))
    # 後ろに流れる銀の髪束
    limb("Ponytail", (0, 0.11, 1.80), (0, 0.24, 1.40), 0.035, 0.008, "KG_Hair", "Head")

    # --- 首: 赤い襟巻き(後ろに長くたなびく) ---
    limb("Neck", (0, 0, 1.52), (0, 0, 1.68), 0.045, 0.042, "KG_Black", "Neck")
    ring("Scarf", (0, 0, 1.60), 0.068, 0.048, "KG_Crimson", "Neck")
    box("ScarfTail", (0.05, 0.13, 1.30), (0.07, 0.015, 0.45), "KG_Crimson", "Chest", rot=(-12, 0, -8))

    # --- 胴: 細身の上着、たすき掛けの赤い帯 ---
    limb("Chest", (0, 0, 1.30), (0, 0, 1.55), 0.140, 0.165, "KG_Cloth", "Chest", squash=0.72)
    limb("Abdomen", (0, 0, 1.08), (0, 0, 1.31), 0.130, 0.140, "KG_Cloth", "Spine", squash=0.75)
    box("CrossStrap", (0, -0.112, 1.34), (0.045, 0.02, 0.50), "KG_Crimson", "Chest", rot=(0, -33, 0))
    box("ChestPlate", (-0.06, -0.110, 1.45), (0.11, 0.02, 0.10), "KG_Metal", "Chest", rot=(0, 10, 0))

    # --- 腰: 赤い帯と、後ろに割れた上着のすそ ---
    limb("Pelvis", (0, 0, 0.90), (0, 0, 1.09), 0.150, 0.135, "KG_Hakama", "Hips", squash=0.8)
    limb("Sash", (0, 0, 1.03), (0, 0, 1.12), 0.150, 0.145, "KG_Crimson", "Hips", squash=0.8)
    box("SashKnot", (0.10, -0.105, 1.07), (0.06, 0.03, 0.06), "KG_Crimson", "Hips", rot=(0, 0, 20))
    box("SashTailA", (0.13, -0.10, 0.90), (0.035, 0.012, 0.30), "KG_Crimson", "Hips", rot=(0, -8, 15))
    box("SashTailB", (0.16, -0.08, 0.93), (0.03, 0.012, 0.24), "KG_Crimson", "Hips", rot=(0, -18, 25))
    # 前に垂れる布(脚の付け根のすき間を隠す)
    box("FrontApron", (0, -0.115, 0.80), (0.17, 0.02, 0.36), "KG_Cloth", "Hips", rot=(6, 0, 0))
    for s, side in ((1, "L"), (-1, "R")):
        box(f"CoatTail{side}", (s * 0.075, 0.135, 0.76), (0.13, 0.018, 0.58), "KG_Cloth", "Hips",
            rot=(-10, 0, s * -4))

    # --- 腕 ---
    for side, s in (("Left", 1), ("Right", -1)):
        y = 1.50
        ball(f"ShoulderCap{side}", (s * 0.18, 0, y), (0.065, 0.06, 0.065), "KG_Cloth", f"{side}UpperArm")
        limb(f"UpperArm{side}", (s * 0.18, 0, y), (s * 0.46, 0, y), 0.055, 0.045, "KG_Cloth", f"{side}UpperArm")
        limb(f"LowerArm{side}", (s * 0.45, 0, y), (s * 0.71, 0, y), 0.044, 0.036, "KG_Cloth", f"{side}LowerArm")
        limb(f"Bracer{side}", (s * 0.52, 0, y), (s * 0.69, 0, y), 0.052, 0.044, "KG_Metal", f"{side}LowerArm",
             smooth=False, seg=8)
        box(f"Hand{side}", (s * 0.765, -0.005, y), (0.10, 0.085, 0.04), "KG_Black", f"{side}Hand")
        box(f"Thumb{side}", (s * 0.735, -0.05, y - 0.005), (0.045, 0.025, 0.025), "KG_Black", f"{side}Hand",
            rot=(0, 0, s * 30))
    # 左肩だけの三日月の肩当て(左右で違う形にしてシルエットを覚えやすく)
    ball("Pauldron", (0.20, 0, 1.56), (0.11, 0.095, 0.045), "KG_Metal", "LeftUpperArm", rot=(0, -22, 0))
    ring("PauldronRim", (0.20, 0, 1.565), 0.095, 0.009, "KG_Gold", "LeftUpperArm", rot=(0, -22, 0))

    # --- 脚: すそが広がった袴と、黒い脚甲 ---
    for side, s in (("Left", 1), ("Right", -1)):
        x = s * 0.10
        limb(f"Thigh{side}", (x, 0, 1.00), (x, 0.01, 0.53), 0.088, 0.075, "KG_Hakama", f"{side}UpperLeg")
        limb(f"HakamaLow{side}", (x, 0.01, 0.55), (x, 0.03, 0.20), 0.076, 0.105, "KG_Hakama", f"{side}LowerLeg")
        limb(f"Boot{side}", (x, 0.03, 0.26), (x, 0.03, 0.04), 0.050, 0.055, "KG_Black", f"{side}LowerLeg")
        box(f"Greave{side}", (x, -0.03, 0.17), (0.06, 0.02, 0.17), "KG_Metal", f"{side}LowerLeg")
        box(f"Foot{side}", (x, -0.03, 0.04), (0.08, 0.16, 0.07), "KG_Black", f"{side}Foot")
        box(f"Toe{side}", (x, -0.14, 0.03), (0.075, 0.07, 0.05), "KG_Black", f"{side}Toes")

    # --- 剣: まっすぐな細身の片刃。つばは月蝕(黒い円盤+金の輪) ---
    hx, hy, hz = -0.765, 0.0, 1.50  # 右手の中心
    g = "sword"
    limb("Grip", (hx, hy + 0.07, hz), (hx, hy - 0.15, hz), 0.016, 0.016, "KG_Crimson", "RightHand",
         group=g, seg=8)
    ball("Pommel", (hx, hy + 0.08, hz), (0.022, 0.02, 0.022), "KG_Gold", "RightHand", group=g, seg=10)
    disc("Guard", (hx, hy - 0.16, hz), 0.045, 0.012, "KG_Eclipse", "RightHand", rot=(90, 0, 0), group=g)
    ring("GuardRing", (hx, hy - 0.16, hz), 0.045, 0.006, "KG_Gold", "RightHand", rot=(90, 0, 0), group=g)
    # 刃: 4角の先細りを横に薄くつぶして、ひし形断面の刃にする
    blade = limb("Blade", (hx, hy - 0.17, hz), (hx, hy - 1.00, hz), 0.026, 0.002, "KG_Blade", "RightHand",
                 smooth=False, group=g, seg=4)
    for v in blade.data.vertices:
        v.co.x *= 0.30  # 刃の厚みを薄くする(刃の向きは上下)


def join_group(group, name, arm):
    """同じグループのパーツを1つのメッシュにまとめ、骨で動くようにする。"""
    objs = [o for o, g in PARTS if g == group]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    # 原点を足元に(Unity で扱いやすい)
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    # 重なった頂点はくっつけない(パーツ同士の境目を保つ)
    mod = o.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    o.parent = arm
    o.select_set(False)
    return o


# ---------------------------------------------------------------
# 確認画像
# ---------------------------------------------------------------
def render_previews():
    """正面と斜めの確認画像を Workbench で出力する。"""
    scn = bpy.context.scene
    scn.render.engine = "BLENDER_WORKBENCH"
    scn.display.shading.light = "STUDIO"
    scn.display.shading.color_type = "MATERIAL"
    scn.display.shading.show_cavity = True
    scn.display.shading.show_shadows = False
    scn.render.resolution_x = 900
    scn.render.resolution_y = 1200
    scn.render.film_transparent = False
    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (0.25, 0.27, 0.32)
    scn.world = world

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.35
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scn.collection.objects.link(cam)
    scn.camera = cam
    target = Vector((0, 0, 1.0))
    views = {"front": (0, -6, 1.0), "angle": (-4.2, -4.2, 1.6), "back": (2.5, 5.5, 1.3)}
    for key, pos in views.items():
        cam.location = pos
        direction = target - Vector(pos)
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scn.render.filepath = PREVIEW_PREFIX + key + ".png"
        bpy.ops.render.render(write_still=True)
    return cam


# ---------------------------------------------------------------
# 本体
# ---------------------------------------------------------------
def main():
    """カゲツを作って、FBX・blend・確認画像を書き出す。"""
    global MATS
    clear_scene()
    MATS = make_materials()
    arm = build_armature()
    build_body()
    join_group("body", "Kagetsu_Body", arm)
    join_group("sword", "Kagetsu_Sword", arm)
    # アニメーション(kagetsu_anims.py)を入れる
    import sys
    sys.path.insert(0, HERE)
    import kagetsu_anims
    kagetsu_anims.build_actions(arm)

    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",  # Unity で拡大率 1 になるように
        bake_space_transform=True,            # Z上 → Unity の Y上 に直して書き出す
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,                 # Unity では余分な末端の骨はいらない
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        bake_anim=True,                       # アニメを入れる
        bake_anim_use_all_actions=True,       # アクション1つ = Unity のクリップ1つ
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,        # キーを間引かない(技のタイミングを正確に)
        mesh_smooth_type="FACE",
    )
    cam = render_previews()
    kagetsu_anims.render_contact_sheet(arm, PREVIEW_PREFIX + "anims.png")
    bpy.data.objects.remove(cam)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("KAGETSU_DONE", FBX_PATH)


MATS = {}
main()
