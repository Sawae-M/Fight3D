# -*- coding: utf-8 -*-
"""
ゴウガ(熊獣人・粘り型の剛腕)の3Dモデルを Blender で自動生成し、Unity 用 FBX に書き出すスクリプト。

使い方(PowerShell):
  & "D:\steam\steamapps\common\Blender\blender.exe" --background --python Art\Blender\build_gouga.py

出力:
  Assets/Models/Characters/Gouga/Gouga.fbx   … Unity が読む本体(Humanoid 対応の骨入り・アニメ入り)
  Art/Blender/Gouga.blend                    … Blender で開いて直す用
  Art/Blender/Gouga_preview_*.png            … 見た目確認用の画像

デザイン(オリジナル。テーマ「月蝕の闘技場」):
  こげ茶の毛の大きな熊。両腕に岩の籠手(こて)、左肩だけに岩の肩当て(今の人形と同じ)。
  額に三日月形の白い毛の模様、胸に月蝕の石のメダル。腰に縄の帯と暗い赤の前垂れ。
  身長 約2.2m、肩幅が広く腕が長い、前かがみの体つき。
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import char_builder as cb  # noqa: E402
import gouga_anims  # noqa: E402

# ---------------------------------------------------------------
# 調整する数値はここに集める(すべて仮。仕様書にまだ無い値)
# ---------------------------------------------------------------
PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
FBX_PATH = os.path.join(PROJECT, "Assets", "Models", "Characters", "Gouga", "Gouga.fbx")
BLEND_PATH = os.path.join(HERE, "Gouga.blend")
PREVIEW_PREFIX = os.path.join(HERE, "Gouga_preview_")
HEIGHT = 2.2  # 身長(m)。確認画像のカメラに使う

# 色(sRGB。今の人形 DollBuilder.cs の色に合わせた)
COLORS = {
    "GG_Fur":     ((0.36, 0.24, 0.15), 0.0, 0.9),   # こげ茶の毛
    "GG_Belly":   ((0.55, 0.42, 0.30), 0.0, 0.9),   # お腹と鼻先の明るい茶色
    "GG_Rock":    ((0.42, 0.42, 0.45), 0.1, 0.8),   # 岩の籠手と肩当て
    "GG_Dark":    ((0.08, 0.06, 0.05), 0.0, 0.5),   # 目・鼻・爪
    "GG_Cream":   ((0.90, 0.85, 0.72), 0.0, 0.8),   # 額の三日月の模様
    "GG_Crimson": ((0.42, 0.08, 0.13), 0.0, 0.7),   # 前垂れ・籠手の帯
    "GG_Rope":    ((0.60, 0.50, 0.34), 0.0, 0.9),   # 縄の帯・すねの布
    "GG_Gold":    ((0.80, 0.62, 0.28), 0.9, 0.3),   # 月蝕の輪(金)
    "GG_Eclipse": ((0.02, 0.02, 0.03), 0.3, 0.3),   # 月蝕の黒い円盤
}

# 骨(名前: (頭, しっぽ, 親))。Z が上、キャラは -Y を向く、左手は +X。脚は短く、腕は長い
BONES = {
    "Hips":  ((0, 0, 0.95), (0, 0, 1.08), None),
    "Spine": ((0, 0, 1.08), (0, 0, 1.32), "Hips"),
    "Chest": ((0, 0, 1.32), (0, 0, 1.62), "Spine"),
    "Neck":  ((0, 0, 1.66), (0, 0, 1.78), "Chest"),
    "Head":  ((0, 0, 1.78), (0, 0, 2.15), "Neck"),
}
for side, s in (("Left", 1), ("Right", -1)):
    BONES.update({
        f"{side}Shoulder": ((s * 0.06, 0, 1.60), (s * 0.28, 0, 1.62), "Chest"),
        f"{side}UpperArm": ((s * 0.28, 0, 1.62), (s * 0.62, 0, 1.62), f"{side}Shoulder"),
        f"{side}LowerArm": ((s * 0.62, 0, 1.62), (s * 0.94, 0, 1.62), f"{side}UpperArm"),
        f"{side}Hand":     ((s * 0.94, 0, 1.62), (s * 1.10, 0, 1.62), f"{side}LowerArm"),
        f"{side}UpperLeg": ((s * 0.20, 0, 0.92), (s * 0.20, 0.01, 0.50), "Hips"),
        f"{side}LowerLeg": ((s * 0.20, 0.01, 0.50), (s * 0.20, 0.03, 0.10), f"{side}UpperLeg"),
        f"{side}Foot":     ((s * 0.20, 0.03, 0.10), (s * 0.20, -0.12, 0.03), f"{side}LowerLeg"),
        f"{side}Toes":     ((s * 0.20, -0.12, 0.03), (s * 0.20, -0.22, 0.03), f"{side}Foot"),
    })


# ---------------------------------------------------------------
# アニメ寄りの体つき(2026-11-22、Mさんの選択)。数値はすべて仮
# ---------------------------------------------------------------
HEAD_SCALE = 1.18       # 頭の大きさの倍率(大きく)
FIST_SCALE = 1.12       # こぶし(手)の大きさの倍率(大きく、力強く)
HEAD_PIVOT_Z = 1.78     # 頭の付け根の高さ


def anime_warp(p, bone):
    """位置 p(どの骨の場所か = bone)を、アニメ寄りの体つきに動かす。戻り値は (新しい位置, 太さの倍率)。"""
    from mathutils import Vector
    if bone == "Head":
        pivot = Vector((0, 0, HEAD_PIVOT_Z))
        return pivot + (Vector(p) - pivot) * HEAD_SCALE, HEAD_SCALE
    if bone.endswith("Hand"):
        wrist = Vector((0.94 if p[0] >= 0 else -0.94, 0, 1.62))  # 手首の位置
        return wrist + (Vector(p) - wrist) * FIST_SCALE, FIST_SCALE
    return Vector(p), 1.0


def chain(points):
    """点を順番につなぐ線の組を作る。"""
    return [(i, i + 1) for i in range(len(points) - 1)]


def build_body(b):
    """ゴウガの体の部品を全部作る。"""
    # --- 頭: 丸い熊の頭、鼻先、耳、額の三日月 ---
    b.ball("Head", (0, -0.02, 1.93), (0.20, 0.19, 0.19), "GG_Fur", "Head")
    b.ball("Muzzle", (0, -0.18, 1.87), (0.10, 0.10, 0.075), "GG_Belly", "Head")
    b.ball("Nose", (0, -0.272, 1.905), (0.042, 0.03, 0.03), "GG_Dark", "Head")
    b.box("Mouth", (0, -0.252, 1.835), (0.08, 0.012, 0.01), "GG_Dark", "Head")
    for s, side in ((1, "L"), (-1, "R")):
        # 怒った目(内側が下がった細い目)と、太いまゆ
        b.box(f"Eye{side}", (s * 0.075, -0.178, 1.965), (0.04, 0.012, 0.013), "GG_Dark", "Head", rot=(0, -s * 20, 0))
        b.box(f"Brow{side}", (s * 0.075, -0.183, 1.99), (0.07, 0.035, 0.024), "GG_Dark", "Head", rot=(0, -s * 22, 0))
        # 下あごからのぞく牙
        b.limb(f"Fang{side}", (s * 0.032, -0.25, 1.845), (s * 0.032, -0.254, 1.805), 0.012, 0.002, "GG_Cream", "Head", seg=6)
        b.ball(f"Ear{side}", (s * 0.14, 0.0, 2.085), (0.065, 0.035, 0.065), "GG_Fur", "Head")
        b.ball(f"EarIn{side}", (s * 0.14, -0.025, 2.085), (0.035, 0.015, 0.035), "GG_Belly", "Head")
    b.box("Scar", (-0.075, -0.188, 1.965), (0.008, 0.01, 0.09), "GG_Cream", "Head", rot=(0, 20, 0))  # 右目の古傷
    # 三日月の模様: 白い円盤の上に毛の色の円盤を少しずらして重ね、三日月に見せる
    b.disc("MoonMark", (0, -0.192, 2.04), 0.04, 0.012, "GG_Cream", "Head", rot=(80, 0, 0))
    b.disc("MoonMarkCut", (0.016, -0.198, 2.047), 0.035, 0.012, "GG_Fur", "Head", rot=(80, 0, 0))

    # --- 胴: 骨組みに肉付けした、なめらかな太い胴(胸・腹・首の後ろの盛り上がり・肩) ---
    torso = [
        ((0, 0, 0.96), (0.27, 0.22)),       # 0 腰
        ((0, -0.02, 1.15), (0.30, 0.26)),   # 1 おなか
        ((0, 0, 1.40), (0.34, 0.25)),       # 2 胸
        ((0, 0.02, 1.58), (0.30, 0.21)),    # 3 胸の上
        ((0, 0.04, 1.72), (0.15, 0.14)),    # 4 首(太く短い)
        ((0.17, 0.04, 1.67), (0.13, 0.12)), # 5 左の首と肩の間(盛り上がった筋肉)
        ((-0.17, 0.04, 1.67), (0.13, 0.12)),# 6 右
        ((0.30, 0, 1.62), 0.14),            # 7 左肩
        ((-0.30, 0, 1.62), 0.14),           # 8 右肩
    ]
    b.skin("Torso", torso, chain(torso[:5]) + [(3, 5), (5, 7), (3, 6), (6, 8)], "GG_Fur", region="Chest")
    b.ball("ChestPatch", (0, -0.205, 1.45), (0.22, 0.05, 0.16), "GG_Belly", "Chest")
    b.ball("BellyPatch", (0, -0.235, 1.14), (0.22, 0.05, 0.18), "GG_Belly", "Spine")    # 胸の月蝕のメダル
    b.disc("Medal", (0, -0.258, 1.45), 0.06, 0.02, "GG_Eclipse", "Chest", rot=(90, 0, 0))
    b.ring("MedalRing", (0, -0.258, 1.45), 0.062, 0.009, "GG_Gold", "Chest", rot=(90, 0, 0))

    # --- 腰: 縄の帯と暗い赤の前垂れ、短いしっぽ ---
    b.ring("RopeBelt", (0, 0, 1.0), 0.27, 0.03, "GG_Rope", "Hips")
    b.box("FrontCloth", (0, -0.25, 0.80), (0.24, 0.02, 0.38), "GG_Crimson", "Hips", rot=(8, 0, 0))
    b.box("BackCloth", (0, 0.24, 0.80), (0.26, 0.02, 0.36), "GG_Crimson", "Hips", rot=(-8, 0, 0))
    b.ball("Tail", (0, 0.27, 0.98), (0.06, 0.05, 0.06), "GG_Fur", "Hips")

    # --- 腕: 太い腕と、ひじから先の岩の籠手 ---
    for side, s in (("Left", 1), ("Right", -1)):
        y = 1.62
        arm_pts = [
            ((s * 0.28, 0, y), 0.135),          # 肩
            ((s * 0.42, 0, y + 0.005), 0.120),  # 力こぶ
            ((s * 0.62, 0, y), 0.092),          # ひじ
            ((s * 0.74, 0, y), 0.100),          # 前腕(籠手の中)
        ]
        b.skin(f"Arm{side}", arm_pts, chain(arm_pts), "GG_Fur", region=f"{side}UpperArm")
        b.limb(f"Gauntlet{side}", (s * 0.70, 0, y), (s * 0.93, 0, y), 0.125, 0.14, "GG_Rock", f"{side}LowerArm",
               smooth=False, seg=7)
        b.ring(f"GauntletBand{side}", (s * 0.735, 0, y), 0.13, 0.016, "GG_Crimson", f"{side}LowerArm", rot=(0, 90, 0))
        b.box(f"Fist{side}", (s * 1.02, -0.005, y), (0.17, 0.17, 0.16), "GG_Rock", f"{side}Hand")
        for i, dy in enumerate((-0.055, 0.0, 0.055)):  # こぶしの爪
            b.box(f"Claw{side}{i}", (s * 1.11, dy, y + 0.05), (0.04, 0.025, 0.03), "GG_Dark", f"{side}Hand")
    # 左肩だけの岩の肩当て(2枚重ね)
    b.box("ShoulderRock", (0.38, 0, 1.76), (0.30, 0.30, 0.12), "GG_Rock", "LeftUpperArm", rot=(0, -18, 0))
    b.box("ShoulderRock2", (0.48, 0, 1.70), (0.22, 0.26, 0.09), "GG_Rock", "LeftUpperArm", rot=(0, -35, 8))

    # --- 脚: 太く短い脚、すねの布、大きな足 ---
    for side, s in (("Left", 1), ("Right", -1)):
        x = s * 0.20
        leg = [
            ((s * 0.17, 0, 1.00), 0.15),        # 付け根
            ((x, 0.0, 0.76), 0.145),            # 太もも
            ((x, 0.01, 0.50), 0.110),           # ひざ
            ((x, 0.02, 0.36), 0.118),           # ふくらはぎ
            ((x, 0.03, 0.10), 0.090),           # 足首
            ((x, -0.06, 0.05), (0.10, 0.065)),  # 足の甲
            ((x, -0.19, 0.04), (0.095, 0.045)), # つま先
        ]
        b.skin(f"Leg{side}", leg, chain(leg), "GG_Fur", region=f"{side}UpperLeg")
        b.limb(f"ShinWrap{side}", (x, 0.02, 0.36), (x, 0.03, 0.12), 0.13, 0.12, "GG_Rope", f"{side}LowerLeg",
               smooth=False, seg=10)
        for i, dx in enumerate((-0.06, 0.0, 0.06)):  # 足の爪
            b.box(f"ToeClaw{side}{i}", (x + dx, -0.225, 0.05), (0.03, 0.03, 0.025), "GG_Dark", f"{side}Toes")


def main():
    """ゴウガを作って、FBX・blend・確認画像を書き出す。"""
    b = cb.Builder(COLORS, warp=anime_warp)
    arm = b.armature(BONES, "GougaRig")
    build_body(b)
    b.join("body", "Gouga_Body")
    cb.build_actions(arm, gouga_anims.ANIMS)
    b.export_fbx(FBX_PATH)
    b.toon_preview(outline=0.006)  # 確認画像だけトゥーン調にする(FBX は書き出し済み)
    cam = b.render_previews(PREVIEW_PREFIX, HEIGHT)
    cb.render_contact_sheet(arm, gouga_anims.ANIMS, cam, PREVIEW_PREFIX + "anims.png", HEIGHT)
    import bpy
    bpy.data.objects.remove(cam)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("GOUGA_DONE", FBX_PATH)


main()
