# -*- coding: utf-8 -*-
"""
カゲツ(仮面の剣士)の3Dモデルを Blender で自動生成し、Unity 用 FBX に書き出すスクリプト。

使い方(PowerShell):
  & "D:\steam\steamapps\common\Blender\blender.exe" --background --python Art\Blender\build_kagetsu.py

出力:
  Assets/Models/Characters/Resources/Fighters/Kagetsu.fbx   … Unity が読む本体(Humanoid 対応の骨入り・アニメ入り)
  Art/Blender/Kagetsu.blend                      … Blender で開いて直す用
  Art/Blender/Kagetsu_preview_*.png              … 見た目確認用の画像

作り方(2026-11-22 に変更):
  - 体・服(上着・袖・袴・手袋・靴)は「骨組みに肉付けする」方法(char_builder.Builder.skin)で、
    なめらかな人間の形に作る。関節のまわりは自動の重みで自然に曲がる。
  - 仮面・剣・肩当てなどの硬い物は、前と同じく箱や球を1つの骨に100%で付ける。
  - 骨の名前と位置は前と同じなので、アニメ(kagetsu_anims.py)はそのまま使える。
  (前の作り方 = 箱と球だけで作った版は、git の履歴に残っている)
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import char_builder as cb  # noqa: E402
import kagetsu_anims  # noqa: E402

# ---------------------------------------------------------------
# 調整する数値はここに集める(すべて仮。仕様書にまだ無い値)
# ---------------------------------------------------------------
PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
FBX_PATH = os.path.join(PROJECT, "Assets", "Models", "Characters", "Resources", "Fighters", "Kagetsu.fbx")  # ゲームが Resources から読み込む
BLEND_PATH = os.path.join(HERE, "Kagetsu.blend")
PREVIEW_PREFIX = os.path.join(HERE, "Kagetsu_preview_")
HEIGHT = 1.95  # 身長(m)。確認画像のカメラに使う

# 色(sRGB。今の人形 DollBuilder.cs の色に合わせた)
COLORS = {
    "KG_Cloth":   ((0.12, 0.10, 0.20), 0.0, 0.8),   # 濃い紫紺の上着
    "KG_Hakama":  ((0.20, 0.18, 0.26), 0.0, 0.85),  # 袴(はかま)風のズボン(上着より少し明るく)
    "KG_Crimson": ((0.42, 0.08, 0.13), 0.0, 0.7),   # 暗い赤の帯・襟巻き
    "KG_Mask":    ((0.92, 0.90, 0.85), 0.0, 0.45),  # 骨のような白い仮面
    "KG_Black":   ((0.05, 0.05, 0.06), 0.0, 0.6),   # 手袋・靴・首の下着・目の切れ込み
    "KG_Metal":   ((0.55, 0.57, 0.63), 0.85, 0.35), # 籠手・すね当て・肩当て
    "KG_Gold":    ((0.80, 0.62, 0.28), 0.9, 0.3),   # 月蝕の輪(金)
    "KG_Hair":    ((0.82, 0.84, 0.90), 0.0, 0.5),   # 銀の髪
    "KG_Blade":   ((0.75, 0.80, 0.90), 1.0, 0.15),  # 刃
    "KG_Eclipse": ((0.02, 0.02, 0.03), 0.3, 0.3),   # 月蝕の黒い円盤
    "KG_Glow":    ((1.00, 0.18, 0.22), 0.0, 0.3),   # 仮面の奥で赤く光る目
}

# 骨(名前: (頭, しっぽ, 親))。Z が上、キャラは -Y を向く、左手は +X。前の版と同じ位置
BONES = {
    "Hips":  ((0, 0, 1.00), (0, 0, 1.10), None),
    "Spine": ((0, 0, 1.10), (0, 0, 1.30), "Hips"),
    "Chest": ((0, 0, 1.30), (0, 0, 1.52), "Spine"),
    "Neck":  ((0, 0, 1.55), (0, 0, 1.66), "Chest"),
    "Head":  ((0, 0, 1.66), (0, 0, 1.95), "Neck"),
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
# アニメ寄りの体つき(2026-11-22、Mさんの選択)。数値はすべて仮
# ---------------------------------------------------------------
LEG_STRETCH = 1.06      # 脚の長さの倍率(長く)
TORSO_SQUASH = 0.91     # 胴の長さの倍率(短く)
HEAD_SCALE = 1.15       # 頭(フード・仮面・髪)の大きさの倍率
LEG_THIN = 0.88         # 脚の太さの倍率(細く)。足も少し小さくなる
ARM_THIN = 0.90         # 腕の太さの倍率(細く)
HIP_Z, NECK_Z, SHOULDER_Z, HEAD_PIVOT_Z = 1.0, 1.55, 1.50, 1.66  # 元の体での、腰・首・肩・頭の付け根の高さ


def body_height(z):
    """元の高さ z を、アニメ寄りの体での高さに直す(脚を伸ばし、胴を縮める)。"""
    if z <= HIP_Z:
        return z * LEG_STRETCH
    top_of_legs = HIP_Z * LEG_STRETCH
    if z <= NECK_Z:
        return top_of_legs + (z - HIP_Z) * TORSO_SQUASH
    return top_of_legs + (NECK_Z - HIP_Z) * TORSO_SQUASH + (z - NECK_Z)


def anime_warp(p, bone):
    """位置 p(どの骨の場所か = bone)を、アニメ寄りの体つきに動かす。戻り値は (新しい位置, 太さの倍率)。"""
    from mathutils import Vector
    x, y, z = p
    if bone == "Head":
        pivot = Vector((0, 0, HEAD_PIVOT_Z))
        return Vector((0, 0, body_height(HEAD_PIVOT_Z))) + (Vector(p) - pivot) * HEAD_SCALE, HEAD_SCALE
    if bone.endswith("Hand"):
        return Vector((x, y, z + body_height(SHOULDER_Z) - SHOULDER_Z)), 1.0
    if bone.endswith(("UpperArm", "LowerArm")):
        return Vector((x, y * ARM_THIN, body_height(SHOULDER_Z) + (z - SHOULDER_Z) * ARM_THIN)), ARM_THIN
    if bone.endswith(("UpperLeg", "LowerLeg", "Foot", "Toes")):
        axis = 0.10 if x >= 0 else -0.10  # 脚の中心線
        return Vector((axis + (x - axis) * LEG_THIN, y * LEG_THIN, body_height(z))), LEG_THIN
    return Vector((x, y, body_height(z))), 1.0  # 腰・背骨・胸・首・肩: 高さだけ直す


def chain(points):
    """点を順番につなぐ線の組を作る。"""
    return [(i, i + 1) for i in range(len(points) - 1)]


def hand_graph(s, x0=0.68, z=1.50):
    """指の分かれた手の骨組み(点と線)を返す。s は左 1 / 右 -1。T ポーズで手のひらは下向き、指は横(X)へ伸びる。

    指は前(-Y)から 人さし指・中指・薬指・小指 の順に並ぶ。親指は前へ出す。
    """
    pts = [((s * x0, 0, z), 0.030),                      # 0 手首
           ((s * (x0 + 0.06), -0.002, z), (0.040, 0.018))]  # 1 手のひらの中心
    edges = [(0, 1)]
    # (前後の位置, 指の長さ, 太さ) [仮]
    fingers = [(-0.027, 0.085, 0.0105), (-0.009, 0.095, 0.011), (0.009, 0.088, 0.0105), (0.026, 0.070, 0.0095)]
    for fy, length, r in fingers:
        base = len(pts)
        kx = x0 + 0.10  # 指の付け根(こぶし)の位置
        pts += [((s * kx, fy, z), r),
                ((s * (kx + length * 0.45), fy, z - 0.002), r * 0.92),
                ((s * (kx + length), fy, z - 0.004), r * 0.8)]
        edges += [(1, base), (base, base + 1), (base + 1, base + 2)]
    t = len(pts)  # 親指: 手首の近くから前へ
    pts += [((s * (x0 + 0.035), -0.025, z - 0.005), 0.014),
            ((s * (x0 + 0.065), -0.050, z - 0.008), 0.012),
            ((s * (x0 + 0.090), -0.065, z - 0.010), 0.010)]
    edges += [(0, t), (t, t + 1), (t + 1, t + 2)]
    return pts, edges


def build_body(b):
    """なめらかな体と服(肉付け方式)を作る。半径は (左右, 前後) か丸の半径(m)。"""
    # --- 上着の胴(腰から首の付け根まで)。肩の所で左右に枝分かれして袖の付け根になる ---
    torso = [
        ((0, 0, 0.96), (0.140, 0.100)),   # 0 腰
        ((0, 0, 1.10), (0.125, 0.090)),   # 1 くびれ
        ((0, 0, 1.27), (0.150, 0.100)),   # 2 胸の下
        ((0, 0, 1.42), (0.165, 0.100)),   # 3 胸の上(肩幅を広く)
        ((0, 0.005, 1.53), (0.07, 0.06)), # 4 首の付け根
        ((0.18, 0, 1.475), 0.064),        # 5 左肩
        ((-0.18, 0, 1.475), 0.064),       # 6 右肩
        ((0.10, 0.008, 1.505), (0.06, 0.055)),   # 7 左の首と肩の間(なだらかな肩のライン)
        ((-0.10, 0.008, 1.505), (0.06, 0.055)),  # 8 右の首と肩の間
    ]
    b.skin("Torso", torso, chain(torso[:5]) + [(3, 7), (7, 5), (3, 8), (8, 6)], "KG_Cloth", region="Chest")

    # --- 首(黒い下着) ---
    neck = [((0, 0.005, 1.50), 0.05), ((0, 0, 1.70), 0.045)]
    b.skin("Neck", neck, chain(neck), "KG_Black", region="Neck")

    for side, s in (("Left", 1), ("Right", -1)):
        # --- 袖(肩から手首) 力こぶと前腕のふくらみを付ける ---
        sleeve = [
            ((s * 0.15, 0, 1.475), 0.066),
            ((s * 0.22, 0, 1.485), 0.058),   # 肩の丸み
            ((s * 0.32, 0, 1.495), 0.049),   # 力こぶ
            ((s * 0.45, 0, 1.50), 0.039),    # ひじ
            ((s * 0.53, 0, 1.50), 0.045),    # 前腕のふくらみ
            ((s * 0.69, 0, 1.50), 0.030),    # 手首
        ]
        b.skin(f"Sleeve{side}", sleeve, chain(sleeve), "KG_Cloth", region=f"{side}UpperArm")
        # --- 手袋: 手首 → 手のひら → 指4本(人さし指〜小指)+ 親指 ---
        b.skin(f"Glove{side}", *hand_graph(s), "KG_Black", region=f"{side}Hand")
        # --- ズボン(太ももから、すねの途中まで。長靴の中に入れる) ---
        x = s * 0.10
        hakama = [
            ((s * 0.09, 0, 1.00), 0.095),
            ((x, 0.005, 0.80), 0.085),   # 太もも
            ((x, 0.01, 0.55), 0.062),    # ひざ
            ((x, 0.02, 0.43), 0.066),    # ふくらはぎ
            ((x, 0.025, 0.30), 0.052),
        ]
        b.skin(f"Hakama{side}", hakama, chain(hakama), "KG_Hakama", region=f"{side}UpperLeg")
        # --- 長靴(すねの途中 → 足首 → かかと → つま先) ---
        boot = [
            ((x, 0.025, 0.37), 0.060),   # 口(少し広い)
            ((x, 0.03, 0.22), 0.050),
            ((x, 0.03, 0.08), 0.043),
            ((x, -0.06, 0.04), (0.044, 0.036)),
            ((x, -0.16, 0.03), (0.040, 0.026)),
        ]
        b.skin(f"Boot{side}", boot, chain(boot), "KG_Black", region=f"{side}LowerLeg")
    # 腰回り(左右の袴をつなぐ部分。股のすき間を埋める)
    seat = [((0, 0, 1.02), (0.15, 0.11)), ((0, 0.005, 0.86), (0.13, 0.10))]
    b.skin("HakamaSeat", seat, chain(seat), "KG_Hakama")


def coat_skirt(b):
    """腰からひざ下まで広がる上着のすそ。前は大きく割れていて、脚が見える。"""
    import bmesh
    import bpy
    o = b.limb("CoatSkirt", (0, 0, 1.06), (0, 0.04, 0.50), 0.150, 0.215, "KG_Cloth", "Hips", squash=0.8, seg=24)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    # 前を向いた面と、上下のふたを消して、前の割れた筒にする
    kill = [f for f in bm.faces if f.normal.y < -0.35 or abs(f.normal.z) > 0.9]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(o.data)
    bm.free()
    # 布に厚みを付ける(裏から見ても消えないように)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    mod = o.modifiers.new("Solidify", "SOLIDIFY")
    mod.thickness = 0.012
    bpy.ops.object.modifier_apply(modifier="Solidify")
    o.select_set(False)


# ---------------------------------------------------------------
# 顔(2026-11-22 作り込み)。仮面は鼻筋・眉・ほお骨・あごのある立体的な形にする。数値はすべて仮
# ---------------------------------------------------------------
MASK_CENTER = (0.0, -0.062, 1.785)   # 仮面の楕円の中心
MASK_RADIUS = (0.090, 0.060, 0.118)  # 仮面の大きさ(左右, 前後, 上下の半径)
EYE_X, EYE_Z = 0.42, 0.10            # 目の位置(仮面の大きさに対する割合)


def _bump(x, z, cx, cz, sx, sz):
    """(cx, cz) を中心にした、なだらかな山の高さ(0〜1)。sx, sz は山の広さ。"""
    import math
    return math.exp(-(((x - cx) / sx) ** 2 + ((z - cz) / sz) ** 2))


def mask_depth(x, z):
    """仮面の表面の前後位置(楕円の半径に対する割合。マイナスが前)。x, z は -1〜1 の割合。"""
    import math
    y = -math.sqrt(max(0.0, 1.0 - x * x - z * z))
    y -= 0.20 * _bump(x, z, 0.0, -0.18, 0.12, 0.30)             # 鼻筋
    y -= 0.08 * _bump(x, z, 0.0, 0.30, 0.70, 0.08)              # 眉の盛り上がり
    y -= 0.06 * (_bump(x, z, 0.55, -0.12, 0.18, 0.15) + _bump(x, z, -0.55, -0.12, 0.18, 0.15))  # ほお骨
    y += 0.07 * (_bump(x, z, EYE_X, EYE_Z, 0.20, 0.11) + _bump(x, z, -EYE_X, EYE_Z, 0.20, 0.11))  # 目のくぼみ
    return y


def mask_point(x, z, lift=0.0):
    """仮面の表面の点(ワールド座標)。lift だけ表面から前に出す(m)。"""
    cx, cy, cz = MASK_CENTER
    rx, ry, rz = MASK_RADIUS
    return (cx + x * rx, cy + mask_depth(x, z) * ry - lift, cz + z * rz)


def build_mask(b, nu=28, nv=24):
    """立体的な仮面のメッシュを作る。下に行くほど細く、あごがとがった形。"""
    import math
    verts, faces = [], []
    for j in range(nv + 1):
        z = 0.97 - 1.94 * j / nv
        width = math.sqrt(max(0.0, 1.0 - z * z)) * 0.98
        if z < -0.35:
            width *= 1.0 - 0.45 * ((-0.35 - z) / 0.62)  # あごをとがらせる
        for i in range(nu + 1):
            x = (-1.0 + 2.0 * i / nu) * width
            verts.append(mask_point(x, z))
    for j in range(nv):
        for i in range(nu):
            a = j * (nu + 1) + i
            faces.append((a, a + nu + 1, a + nu + 2, a + 1))  # 表が前を向く順番
    b.mesh_part("Mask", verts, faces, "KG_Mask", "Head", thickness=0.006)


def build_head(b):
    """カゲツの頭: フード、立体的な仮面、赤く光る目、前髪、模様、髪束。"""
    # フード(顔のまわりに縁取り)
    b.ball("Hood", (0, 0.03, 1.80), (0.115, 0.12, 0.132), "KG_Cloth", "Head")
    b.ring("HoodRim", (0, -0.065, 1.79), 0.104, 0.013, "KG_Cloth", "Head", rot=(90, 0, 0))
    b.limb("HoodDrape", (0, 0.07, 1.74), (0, 0.105, 1.56), 0.095, 0.125, "KG_Cloth", "Head", squash=0.6)
    b.limb("HoodPeak", (0, 0.06, 1.89), (0, 0.18, 1.90), 0.05, 0.004, "KG_Cloth", "Head")  # 後ろにとがったフードの先

    build_mask(b)
    import math
    for s, side in ((1, "L"), (-1, "R")):
        # 目の穴: 目尻がつり上がったアーモンド形の黒い穴
        ex, ey, ez = mask_point(s * EYE_X, EYE_Z, lift=0.003)
        b.ball(f"EyeHole{side}", (ex, ey, ez), (0.026, 0.008, 0.0095), "KG_Black", "Head", rot=(0, -s * 16, 0))
        # 穴の奥で赤く光る目(少し内側寄り)
        b.ball(f"EyeGlow{side}", (ex - s * 0.004, ey - 0.004, ez - 0.001), (0.0075, 0.004, 0.0065), "KG_Glow", "Head")
        # 目の下の赤い隈取り(下へ流れる線)
        tx, ty, tz = mask_point(s * 0.47, -0.22, lift=0.002)
        b.box(f"TearMark{side}", (tx, ty, tz), (0.0035, 0.004, 0.032), "KG_Crimson", "Head", rot=(0, -s * 12, 0))
    # 口元: 細い赤の縦線を3本(縫い目のような模様)
    for k, dx in enumerate((-0.012, 0.0, 0.012)):
        mx, my, mz = mask_point(dx / MASK_RADIUS[0], -0.55, lift=0.002)
        b.box(f"MouthLine{k}", (mx, my, mz), (0.003, 0.004, 0.028 if dx == 0 else 0.02), "KG_Crimson", "Head")
    # 左のこめかみのひび
    cx_, cy_, cz_ = mask_point(0.62, 0.42, lift=0.002)
    b.box("Crack", (cx_, cy_, cz_), (0.003, 0.004, 0.05), "KG_Black", "Head", rot=(0, 30, 0))
    # 額の月蝕の飾り
    fx, fy, fz = mask_point(0.0, 0.62, lift=0.004)
    b.disc("Eclipse", (fx, fy, fz), 0.012, 0.006, "KG_Eclipse", "Head", rot=(76, 0, 0))
    b.ring("EclipseRing", (fx, fy, fz), 0.0145, 0.003, "KG_Gold", "Head", rot=(76, 0, 0))
    # 前髪: フードの下から仮面の両わきにかかる銀の髪(アニメっぽいとがった束)
    for k, (x0, x1, ln) in enumerate(((0.072, 0.082, 0.10), (0.052, 0.068, 0.07), (-0.072, -0.082, 0.10),
                                      (-0.052, -0.068, 0.07))):
        top = (x0, -0.088, 1.885)
        tip = (x1, -0.112, 1.885 - ln)
        b.limb(f"Bang{k}", top, tip, 0.016, 0.002, "KG_Hair", "Head", seg=6)
    # 後ろの髪束と、赤い髪留め
    b.limb("Ponytail", (0, 0.11, 1.80), (0, 0.25, 1.38), 0.034, 0.006, "KG_Hair", "Head")
    b.limb("Ponytail2", (0.015, 0.12, 1.78), (0.05, 0.22, 1.47), 0.022, 0.004, "KG_Hair", "Head")
    b.ring("HairTie", (0, 0.125, 1.775), 0.03, 0.009, "KG_Crimson", "Head", rot=(70, 0, 0))


def build_gear(b):
    """仮面・フード・帯・肩当て・剣など、硬い物の部品(1つの骨に100%)。"""
    build_head(b)

    # --- 首まわり: 赤い襟巻き(後ろに長くたなびく) ---
    b.ring("Scarf", (0, 0.005, 1.585), 0.062, 0.04, "KG_Crimson", "Neck")
    b.box("ScarfTail", (0.05, 0.125, 1.30), (0.07, 0.015, 0.45), "KG_Crimson", "Chest", rot=(-12, 0, -8))

    # --- 胴: たすき掛けの赤い帯と胸当て ---
    b.box("CrossStrap", (0, -0.103, 1.32), (0.04, 0.018, 0.48), "KG_Crimson", "Chest", rot=(0, -33, 0))
    b.box("ChestPlate", (-0.06, -0.10, 1.43), (0.10, 0.018, 0.09), "KG_Metal", "Chest", rot=(0, 10, 0))

    # --- 腰: 赤い帯と、後ろに割れた上着のすそ・前垂れ ---
    b.limb("Sash", (0, 0, 1.02), (0, 0, 1.12), 0.146, 0.128, "KG_Crimson", "Hips", squash=0.78)
    b.box("SashKnot", (0.10, -0.10, 1.06), (0.06, 0.03, 0.06), "KG_Crimson", "Hips", rot=(0, 0, 20))
    b.box("SashTailA", (0.13, -0.10, 0.89), (0.035, 0.012, 0.30), "KG_Crimson", "Hips", rot=(0, -8, 15))
    b.box("SashTailB", (0.16, -0.08, 0.92), (0.03, 0.012, 0.24), "KG_Crimson", "Hips", rot=(0, -18, 25))
    coat_skirt(b)

    # --- 腕: 銀の籠手 ---
    for side, s in (("Left", 1), ("Right", -1)):
        b.limb(f"Bracer{side}", (s * 0.52, 0, 1.50), (s * 0.67, 0, 1.50), 0.050, 0.039, "KG_Metal",
               f"{side}LowerArm", smooth=False, seg=8)
    # 左肩だけの三日月の肩当て
    b.ball("Pauldron", (0.19, 0, 1.545), (0.10, 0.09, 0.042), "KG_Metal", "LeftUpperArm", rot=(0, -22, 0))
    b.ring("PauldronRim", (0.19, 0, 1.55), 0.088, 0.008, "KG_Gold", "LeftUpperArm", rot=(0, -22, 0))

    # --- 脚: すね当て ---
    for side, s in (("Left", 1), ("Right", -1)):
        b.box(f"Greave{side}", (s * 0.10, -0.01, 0.19), (0.055, 0.02, 0.15), "KG_Metal", f"{side}LowerLeg")

    # --- 剣: まっすぐな細身の片刃。つばは月蝕(黒い円盤+金の輪) ---
    hx, hy, hz = -0.765, 0.0, 1.50  # 右手の中心
    g = "sword"
    b.limb("Grip", (hx, hy + 0.07, hz), (hx, hy - 0.15, hz), 0.016, 0.016, "KG_Crimson", "RightHand",
           group=g, seg=8)
    b.ball("Pommel", (hx, hy + 0.08, hz), (0.022, 0.02, 0.022), "KG_Gold", "RightHand", group=g, seg=10)
    b.disc("Guard", (hx, hy - 0.16, hz), 0.045, 0.012, "KG_Eclipse", "RightHand", rot=(90, 0, 0), group=g)
    b.ring("GuardRing", (hx, hy - 0.16, hz), 0.045, 0.006, "KG_Gold", "RightHand", rot=(90, 0, 0), group=g)
    blade = b.limb("Blade", (hx, hy - 0.17, hz), (hx, hy - 1.00, hz), 0.026, 0.002, "KG_Blade", "RightHand",
                   smooth=False, group=g, seg=4)
    for v in blade.data.vertices:
        v.co.x = hx + (v.co.x - hx) * 0.30  # 刃の厚みを薄くする(刃の向きは上下)


def main():
    """カゲツを作って、FBX・blend・確認画像を書き出す。"""
    import bpy
    b = cb.Builder(COLORS, warp=anime_warp)
    arm = b.armature(BONES, "KagetsuRig")
    build_body(b)
    build_gear(b)
    b.join("body", "Kagetsu_Body")
    b.join("sword", "Kagetsu_Sword")
    cb.build_actions(arm, kagetsu_anims.ANIMS)
    b.export_fbx(FBX_PATH)
    b.toon_preview()  # 確認画像だけトゥーン調にする(FBX は書き出し済み)
    cam = b.render_previews(PREVIEW_PREFIX, HEIGHT, face=((0, -0.05, 1.86), 0.45))  # 顔のアップも出す
    cb.render_contact_sheet(arm, kagetsu_anims.ANIMS, cam, PREVIEW_PREFIX + "anims.png", HEIGHT)
    bpy.data.objects.remove(cam)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("KAGETSU_DONE", FBX_PATH)


main()
