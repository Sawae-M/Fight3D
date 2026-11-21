# -*- coding: utf-8 -*-
"""
カゲツのアニメーション(Blender のアクション)を作るスクリプト。build_kagetsu.py から呼ばれる。

ルール:
  - 60fps。技の長さは Assets/Scripts/Tuning.cs の フレーム数(出るまで+当たっている+終わりの隙)に合わせる。
  - その場で動く(位置の移動はゲームのプログラムがする)。上下の動きだけアニメに入れる。
  - ポーズは「骨の回転(度)」で書く。軸はキャラの T ポーズを基準にした向き:
        X 軸 … 体の左右の軸。+ で前に倒れる(背骨)/ 脚を後ろへ(太もも)/ ひざを曲げる(すね)
        Y 軸 … 体の前後の軸。左腕は + で下がる、右腕は - で下がる
        Z 軸 … 上下の軸。+ で体が左へ向く。左腕は - で前へ、右腕は + で前へ
  - 動きのテンポは「軽くて速い剣士」の感覚だけを参考にし、ポーズそのものはオリジナル。
"""

import math

import bpy
from mathutils import Euler, Vector

FPS = 60

# ---------------------------------------------------------------
# 構え(すべてのアニメの基本になるポーズ)
# ---------------------------------------------------------------
STANCE = {
    "HipsLoc": (0, 0, -0.065),            # 腰を落とす(m)
    "Hips": (0, 0, -20),                  # 左肩を前に、半身に構える
    "Spine": (8, 0, 0),
    "Chest": (4, 0, 8),
    "Neck": (0, 0, 4),
    "Head": (-8, 0, 8),                   # 顔は相手(正面)に向ける
    "LeftUpperLeg": (-30, -6, 0), "LeftLowerLeg": (40, 0, 0), "LeftFoot": (-10, 0, 10),
    "RightUpperLeg": (10, 8, 0), "RightLowerLeg": (20, 0, 0), "RightFoot": (-30, 0, 15),
    # 右手: 剣を低く前に向ける
    "RightShoulder": (0, 0, 0), "RightUpperArm": (0, -62, 35), "RightLowerArm": (0, 0, 45), "RightHand": (0, 25, -55),
    # 左手: 開いた手を前に出して間合いを測る
    "LeftShoulder": (0, 0, 0), "LeftUpperArm": (0, 60, -35), "LeftLowerArm": (0, 0, -70), "LeftHand": (0, -10, 0),
}


def P(base=None, **over):
    """base(なければ構え)をコピーして、指定した骨だけ上書きしたポーズを返す。"""
    d = dict(STANCE if base is None else base)
    d.update(over)
    return d


def hips(z=0.0, base=None, **over):
    """腰の高さを構えから z(m)だけ変えたポーズ。"""
    p = P(base, **over)
    x, y, zz = p["HipsLoc"]
    p["HipsLoc"] = (x, y, zz + z)
    return p


# ---------------------------------------------------------------
# よく使う腕のポーズ
# ---------------------------------------------------------------
ARM_THRUST = dict(RightUpperArm=(0, -8, 85), RightLowerArm=(0, 0, 5), RightHand=(0, 0, -5))      # 右腕をまっすぐ前へ
ARM_BACK = dict(RightUpperArm=(0, -40, -25), RightLowerArm=(0, 0, 70), RightHand=(0, 20, -40))   # 剣を後ろへ引く
ARM_RIGHTSIDE = dict(RightUpperArm=(0, -15, 10), RightLowerArm=(0, 0, 25), RightHand=(0, 0, -20)) # 剣を体の右へ
ARM_ACROSS = dict(RightUpperArm=(0, -15, 140), RightLowerArm=(0, 0, 10), RightHand=(0, 0, 0))    # 振り抜いて左へ
ARM_HIGH = dict(RightUpperArm=(0, 70, 40), RightLowerArm=(0, 0, 40), RightHand=(0, 60, 0))       # 剣を頭上へ
ARM_LOWSTRIKE = dict(RightUpperArm=(0, -35, 80), RightLowerArm=(0, 0, 5), RightHand=(0, -20, 0)) # 振り下ろし終わり
LEFT_TUCK = dict(LeftUpperArm=(0, 70, -10), LeftLowerArm=(0, 0, -100), LeftHand=(0, 0, 0))       # 左手を体に寄せる
LEFT_OUT = dict(LeftUpperArm=(0, 20, 30), LeftLowerArm=(0, 0, -10), LeftHand=(0, 0, 0))          # 左手を後ろへ流す
LUNGE_LEGS = dict(LeftUpperLeg=(-60, -5, 0), LeftLowerLeg=(70, 0, 0), LeftFoot=(-10, 0, 10),
                  RightUpperLeg=(35, 8, 0), RightLowerLeg=(10, 0, 0), RightFoot=(-20, 0, 15))     # 大きく踏み込んだ脚

# ---------------------------------------------------------------
# アニメーションの一覧: 名前 → (ループするか, [(フレーム, ポーズ), ...])
# ---------------------------------------------------------------
def walk_keys(length, back=False):
    """その場で足踏みする歩き。back=True なら逆再生(後ろ歩き)。"""
    a = P(LeftUpperLeg=(-35, -6, 0), LeftLowerLeg=(35, 0, 0), RightUpperLeg=(15, 8, 0), RightLowerLeg=(25, 0, 0))
    b = hips(0.015, LeftUpperLeg=(-20, -6, 0), LeftLowerLeg=(30, 0, 0), RightUpperLeg=(-25, 8, 0), RightLowerLeg=(70, 0, 0),
             RightFoot=(-10, 0, 15))
    c = P(LeftUpperLeg=(-15, -6, 0), LeftLowerLeg=(20, 0, 0), RightUpperLeg=(-5, 8, 0), RightLowerLeg=(30, 0, 0))
    d = hips(0.015, LeftUpperLeg=(-45, -6, 0), LeftLowerLeg=(75, 0, 0), RightUpperLeg=(15, 8, 0), RightLowerLeg=(25, 0, 0))
    q = length // 4
    keys = [(0, a), (q, b), (2 * q, c), (3 * q, d), (length, a)]
    if back:
        keys = [(length - f, p) for f, p in reversed(keys)]
    return keys


def strafe_keys(length, left=True):
    """横歩き。足を開く → そろえる を繰り返す。"""
    s = -1 if left else 1  # 左へ動くときは左足から開く
    wide = P(LeftUpperLeg=(-25, -18, 0), RightUpperLeg=(8, 18, 0))
    lead = hips(0.02, **{("LeftUpperLeg" if left else "RightUpperLeg"): (-30, -25 if left else 25, 0),
                         ("LeftLowerLeg" if left else "RightLowerLeg"): (55, 0, 0)})
    close = hips(-0.01, LeftUpperLeg=(-25, 0, 0), RightUpperLeg=(8, 2, 0))
    trail = hips(0.02, **{("RightUpperLeg" if left else "LeftUpperLeg"): (-10, 0, 0),
                          ("RightLowerLeg" if left else "LeftLowerLeg"): (60, 0, 0)})
    q = length // 4
    return [(0, wide), (q, lead), (2 * q, wide), (3 * q, trail), (length, wide)] if s else []


ANIMS = {
    # ---- 基本の動き ----
    "Idle": (True, [
        (0, P()),
        (30, hips(-0.012, Chest=(8, 0, 8), RightHand=(0, 30, -55), LeftLowerArm=(0, 0, -75))),
        (60, P()),
    ]),
    "WalkForward": (True, walk_keys(36)),
    "WalkBack": (True, walk_keys(44, back=True)),
    "StrafeLeft": (True, strafe_keys(40, left=True)),
    "StrafeRight": (True, strafe_keys(40, left=False)),

    # ---- 通常技(弱 5+3+10=18F、中 9+4+18=31F、強 15+5+28=48F) ----
    "Light": (False, [
        (0, P()),
        (4, P(Chest=(4, 0, -10), RightUpperArm=(0, -45, 20), RightLowerArm=(0, 0, 70))),
        (6, P(Spine=(15, 0, 0), Chest=(6, 0, 25), Hips=(0, 0, -5), **ARM_THRUST)),
        (8, P(Spine=(16, 0, 0), Chest=(6, 0, 27), Hips=(0, 0, -5), **ARM_THRUST)),
        (18, P()),
    ]),
    "Medium": (False, [
        (0, P()),
        (8, P(Chest=(4, 0, -35), Hips=(0, 0, -35), **ARM_RIGHTSIDE)),
        (10, P(Chest=(8, 0, 0), Hips=(0, 0, -15), RightUpperArm=(0, -15, 70), RightLowerArm=(0, 0, 10), RightHand=(0, 0, -10))),
        (13, P(Chest=(10, 0, 35), Hips=(0, 0, 10), **ARM_ACROSS, **LEFT_OUT)),
        (20, P(Chest=(8, 0, 30), Hips=(0, 0, 5), **ARM_ACROSS, **LEFT_OUT)),
        (31, P()),
    ]),
    "Heavy": (False, [
        (0, P()),
        (13, hips(0.02, Spine=(-5, 0, 0), Chest=(-8, 0, 0), **ARM_HIGH, **LEFT_TUCK)),
        (16, hips(-0.08, Spine=(25, 0, 0), Chest=(10, 0, 10), **ARM_LOWSTRIKE, **LUNGE_LEGS, **LEFT_OUT)),
        (22, hips(-0.08, Spine=(27, 0, 0), Chest=(10, 0, 10), **ARM_LOWSTRIKE, **LUNGE_LEGS, **LEFT_OUT)),
        (48, P()),
    ]),

    # ---- スキル1 影穿ち(1・2回目 8+3+16=27F、3回目 10+3+22=35F) ----
    "KageUgachi": (False, [
        (0, P()),
        (6, hips(-0.02, Chest=(4, 0, -30), **ARM_BACK, **LEFT_TUCK)),
        (9, hips(-0.10, Spine=(25, 0, 0), Chest=(8, 0, 25), Hips=(0, 0, -5), **ARM_THRUST, **LUNGE_LEGS, **LEFT_OUT)),
        (13, hips(-0.10, Spine=(25, 0, 0), Chest=(8, 0, 25), Hips=(0, 0, -5), **ARM_THRUST, **LUNGE_LEGS, **LEFT_OUT)),
        (27, P()),
    ]),
    "KageUgachi3": (False, [
        (0, P()),
        (7, hips(-0.08, Spine=(20, 0, 0), Chest=(4, 0, -45), Hips=(0, 0, -40), **ARM_BACK, **LEFT_TUCK)),
        (11, hips(-0.14, Spine=(32, 0, 0), Chest=(8, 0, 30), Hips=(0, 0, 0), **ARM_THRUST, **LUNGE_LEGS, **LEFT_OUT)),
        (16, hips(-0.14, Spine=(32, 0, 0), Chest=(8, 0, 30), Hips=(0, 0, 0), **ARM_THRUST, **LUNGE_LEGS, **LEFT_OUT)),
        (35, P()),
    ]),

    # ---- スキル2 三日月払い(18+4+26=48F) 低く沈んで、三日月を描くように大きくなぎ払う ----
    "MikazukiBarai": (False, [
        (0, P()),
        (15, hips(-0.16, Spine=(20, 0, 0), Chest=(5, 0, -45), Hips=(0, 0, -60), **ARM_RIGHTSIDE, **LEFT_TUCK,
                  LeftUpperLeg=(-45, -15, 0), LeftLowerLeg=(80, 0, 0), RightUpperLeg=(20, 15, 0), RightLowerLeg=(60, 0, 0))),
        (19, hips(-0.12, Spine=(15, 0, 0), Chest=(5, 0, 0), Hips=(0, 0, 0), RightUpperArm=(0, -5, 60),
                  RightLowerArm=(0, 0, 0), RightHand=(0, 0, -10), **LEFT_OUT, **LUNGE_LEGS)),
        (22, hips(-0.10, Spine=(12, 0, 0), Chest=(5, 0, 40), Hips=(0, 0, 50), RightUpperArm=(0, -5, 150),
                  RightLowerArm=(0, 0, 0), RightHand=(0, 0, 10), **LEFT_OUT, **LUNGE_LEGS)),
        (30, hips(-0.10, Spine=(12, 0, 0), Chest=(5, 0, 40), Hips=(0, 0, 50), RightUpperArm=(0, -10, 150),
                  RightLowerArm=(0, 0, 5), RightHand=(0, 0, 10), **LEFT_OUT, **LUNGE_LEGS)),
        (48, P()),
    ]),

    # ---- スキル3 仮面解放(20F) 左手で仮面に触れ、気を解き放つ ----
    "KamenKaihou": (False, [
        (0, P()),
        (7, hips(0.03, Head=(10, 0, 8), LeftUpperArm=(0, 55, -80), LeftLowerArm=(0, 0, -150), LeftHand=(0, 0, -20),
                 RightUpperArm=(0, -70, 0), RightLowerArm=(0, 0, 10))),
        (12, hips(0.05, Spine=(-8, 0, 0), Head=(-25, 0, 8), LeftUpperArm=(0, -30, 10), LeftLowerArm=(0, 0, -10),
                  RightUpperArm=(0, -30, -10), RightLowerArm=(0, 0, 10), RightHand=(0, 0, 0))),
        (20, P()),
    ]),

    # ---- ウルト 冥月斬(踏み込み 10+4+10=24F、外したら隙 60F は構えのまま止まる) ----
    "MeigetsuZan": (False, [
        (0, P()),
        (5, hips(-0.14, Spine=(35, 0, 0), Chest=(5, 0, -30), Hips=(0, 0, -30), **ARM_BACK, **LEFT_OUT,
                 LeftUpperLeg=(-50, -5, 0), LeftLowerLeg=(60, 0, 0), RightUpperLeg=(30, 8, 0), RightLowerLeg=(50, 0, 0))),
        (11, hips(-0.12, Spine=(25, 0, 0), Chest=(8, 0, 30), Hips=(0, 0, 10), **ARM_ACROSS, **LUNGE_LEGS, **LEFT_OUT)),
        (15, hips(-0.12, Spine=(25, 0, 0), Chest=(8, 0, 32), Hips=(0, 0, 10), **ARM_ACROSS, **LUNGE_LEGS, **LEFT_OUT)),
        (24, P()),
    ]),
    # 当たった後の連続斬り(当たるのは 6,12,18,24,30F、締めの浮かせ斬りは 44F、隙 24F → 68F)
    "MeigetsuZanRush": (False, [
        (0, P()),
        (5, P(Chest=(8, 0, -35), Hips=(0, 0, -35), **ARM_RIGHTSIDE)),
        (6, P(Chest=(10, 0, 30), Hips=(0, 0, 5), **ARM_ACROSS)),
        (11, P(Chest=(8, 0, 25), RightUpperArm=(0, 50, 120), RightLowerArm=(0, 0, 10), RightHand=(0, 0, 0))),
        (12, P(Chest=(8, 0, -30), Hips=(0, 0, -30), **ARM_RIGHTSIDE)),
        (17, P(Chest=(8, 0, -35), Hips=(0, 0, -35), **ARM_RIGHTSIDE)),
        (18, P(Chest=(10, 0, 30), Hips=(0, 0, 5), **ARM_ACROSS)),
        (23, P(Chest=(5, 0, 0), **ARM_HIGH)),
        (24, hips(-0.06, Spine=(20, 0, 0), **ARM_LOWSTRIKE)),
        (29, P(Chest=(8, 0, -35), Hips=(0, 0, -35), **ARM_RIGHTSIDE)),
        (30, P(Chest=(10, 0, 35), Hips=(0, 0, 10), **ARM_ACROSS, **LEFT_OUT)),
        (40, hips(-0.18, Spine=(30, 0, 0), Chest=(5, 0, -20), RightUpperArm=(0, -70, 20), RightLowerArm=(0, 0, 20),
                  RightHand=(0, -40, 0), **LEFT_TUCK, LeftUpperLeg=(-45, -6, 0), LeftLowerLeg=(85, 0, 0),
                  RightUpperLeg=(15, 8, 0), RightLowerLeg=(70, 0, 0))),
        (44, hips(0.12, Spine=(-12, 0, 0), Chest=(-5, 0, 10), RightUpperArm=(0, 80, 60), RightLowerArm=(0, 0, 0),
                  RightHand=(0, 30, 0), **LEFT_OUT, LeftUpperLeg=(-10, -6, 0), LeftLowerLeg=(5, 0, 0),
                  RightUpperLeg=(20, 8, 0), RightLowerLeg=(40, 0, 0))),
        (54, hips(0.05, Spine=(-8, 0, 0), RightUpperArm=(0, 75, 60), RightLowerArm=(0, 0, 5), RightHand=(0, 30, 0),
                  **LEFT_OUT)),
        (68, P()),
    ]),

    # ---- 守り(剣を顔の横に立てて受ける) ----
    "Guard": (True, [
        (0, hips(-0.025, Spine=(12, 0, 0), Chest=(6, 0, 20), RightUpperArm=(0, -25, 70), RightLowerArm=(0, 75, 10),
                 RightHand=(-75, 0, 0), LeftUpperArm=(0, 30, -60), LeftLowerArm=(0, 0, -80))),
        (20, hips(-0.03, Spine=(13, 0, 0), Chest=(6, 0, 20), RightUpperArm=(0, -25, 70), RightLowerArm=(0, 75, 10),
                  RightHand=(-75, 0, 0), LeftUpperArm=(0, 30, -60), LeftLowerArm=(0, 0, -82))),
        (40, hips(-0.025, Spine=(12, 0, 0), Chest=(6, 0, 20), RightUpperArm=(0, -25, 70), RightLowerArm=(0, 75, 10),
                  RightHand=(-75, 0, 0), LeftUpperArm=(0, 30, -60), LeftLowerArm=(0, 0, -80))),
    ]),
    # 回避ステップ 24F(無敵 4〜12F)。低く跳んで身をかがめる
    "Dodge": (False, [
        (0, P()),
        (3, hips(-0.08, Spine=(20, 0, 0), LeftUpperLeg=(-40, -6, 0), LeftLowerLeg=(70, 0, 0), RightLowerLeg=(50, 0, 0))),
        (9, hips(0.02, Spine=(30, 0, 0), Chest=(10, 0, 0), **LEFT_TUCK, RightUpperArm=(0, -70, 10),
                 LeftUpperLeg=(-60, -10, 0), LeftLowerLeg=(100, 0, 0), RightUpperLeg=(-30, 10, 0), RightLowerLeg=(100, 0, 0))),
        (16, hips(-0.10, Spine=(22, 0, 0), LeftUpperLeg=(-45, -10, 0), LeftLowerLeg=(80, 0, 0), RightUpperLeg=(15, 12, 0),
                  RightLowerLeg=(60, 0, 0))),
        (24, P()),
    ]),

    # ---- やられ ----
    "Hit": (False, [       # のけぞり 16F 前後(技ごとの長さはゲーム側で再生速度を変える)
        (0, P()),
        (3, hips(0.01, Spine=(-15, 0, 0), Chest=(-10, 0, 15), Head=(-25, 0, 0), RightUpperArm=(0, -40, -10),
                 LeftUpperArm=(0, 20, 10), LeftLowerArm=(0, 0, -30))),
        (16, P()),
    ]),
    "Stagger": (False, [   # よろけ 40F
        (0, P()),
        (4, hips(0.02, Spine=(-25, 0, 0), Chest=(-12, 0, 20), Head=(-30, 0, 10), RightUpperArm=(0, -30, -30),
                 LeftUpperArm=(0, 0, 30), LeftLowerArm=(0, 0, -20))),
        (14, hips(-0.16, Spine=(35, 0, 0), Chest=(10, 0, -10), Head=(10, 0, 0), RightUpperArm=(0, -85, 10),
                  RightLowerArm=(0, 0, 10), LeftUpperArm=(0, 80, 0), LeftLowerArm=(0, 0, -20),
                  LeftUpperLeg=(-20, -6, 0), LeftLowerLeg=(50, 0, 0), RightUpperLeg=(-10, 8, 0), RightLowerLeg=(60, 0, 0))),
        (30, hips(-0.15, Spine=(32, 0, 0), Chest=(10, 0, -10), Head=(5, 0, 0), RightUpperArm=(0, -85, 10),
                  RightLowerArm=(0, 0, 10), LeftUpperArm=(0, 80, 0), LeftLowerArm=(0, 0, -20),
                  LeftUpperLeg=(-20, -6, 0), LeftLowerLeg=(50, 0, 0), RightUpperLeg=(-10, 8, 0), RightLowerLeg=(60, 0, 0))),
        (40, P()),
    ]),
    # 浮き 66F(空中 36F + 着地後 30F)。空中の高さはゲーム側が動かす。着地して倒れ、起き上がる
    "Launch": (False, [
        (0, P()),
        (5, hips(0.0, Spine=(-30, 0, 0), Chest=(-15, 0, 0), Head=(-30, 0, 0), RightUpperArm=(0, 30, -20),
                 LeftUpperArm=(0, -30, 20), LeftLowerArm=(0, 0, -10), LeftUpperLeg=(-35, -6, 0), LeftLowerLeg=(60, 0, 0),
                 RightUpperLeg=(-20, 8, 0), RightLowerLeg=(40, 0, 0))),
        (30, hips(0.0, Hips=(-45, 0, 0), Spine=(-10, 0, 0), RightUpperArm=(0, 10, -10), LeftUpperArm=(0, -10, 10),
                  LeftUpperLeg=(-40, -6, 0), LeftLowerLeg=(40, 0, 0), RightUpperLeg=(-30, 8, 0), RightLowerLeg=(30, 0, 0))),
        (36, hips(-0.80, Hips=(-85, 0, 0), Spine=(-5, 0, 0), Head=(10, 0, 0), RightUpperArm=(0, -10, 0),
                  LeftUpperArm=(0, 10, 0), LeftLowerArm=(0, 0, -10),
                  LeftUpperLeg=(-10, -8, 0), LeftLowerLeg=(15, 0, 0), RightUpperLeg=(-5, 8, 0), RightLowerLeg=(10, 0, 0))),
        (48, hips(-0.80, Hips=(-85, 0, 0), Spine=(-5, 0, 0), Head=(10, 0, 0), RightUpperArm=(0, -10, 0),
                  LeftUpperArm=(0, 10, 0), LeftLowerArm=(0, 0, -10),
                  LeftUpperLeg=(-10, -8, 0), LeftLowerLeg=(15, 0, 0), RightUpperLeg=(-5, 8, 0), RightLowerLeg=(10, 0, 0))),
        (56, hips(-0.55, Hips=(-35, 0, 0), Spine=(30, 0, 0), Head=(10, 0, 0), RightUpperArm=(0, -70, -20),
                  LeftUpperArm=(0, 70, 20), LeftUpperLeg=(-80, -10, 0), LeftLowerLeg=(130, 0, 0),
                  RightUpperLeg=(-70, 10, 0), RightLowerLeg=(130, 0, 0))),
        (62, hips(-0.25, Spine=(30, 0, 0), LeftUpperLeg=(-70, -8, 0), LeftLowerLeg=(110, 0, 0),
                  RightUpperLeg=(-20, 8, 0), RightLowerLeg=(100, 0, 0))),
        (66, P()),
    ]),
    # K.O.: 崩れ落ちて、倒れたまま
    "KO": (False, [
        (0, P()),
        (8, hips(-0.25, Spine=(30, 0, 0), Head=(25, 0, 0), RightUpperArm=(0, -85, 0), LeftUpperArm=(0, 85, 0),
                 LeftUpperLeg=(-50, -6, 0), LeftLowerLeg=(110, 0, 0), RightUpperLeg=(-10, 8, 0), RightLowerLeg=(110, 0, 0))),
        (24, hips(-0.86, Hips=(80, 0, 20), Spine=(5, 0, 0), Head=(0, 0, 30), RightUpperArm=(0, -40, 40),
                  LeftUpperArm=(0, 70, 0), LeftUpperLeg=(-15, -6, 0), LeftLowerLeg=(30, 0, 0),
                  RightUpperLeg=(0, 8, 0), RightLowerLeg=(10, 0, 0))),
        (60, hips(-0.87, Hips=(82, 0, 20), Spine=(5, 0, 0), Head=(0, 0, 30), RightUpperArm=(0, -40, 40),
                  LeftUpperArm=(0, 70, 0), LeftUpperLeg=(-15, -6, 0), LeftLowerLeg=(30, 0, 0),
                  RightUpperLeg=(0, 8, 0), RightLowerLeg=(10, 0, 0))),
    ]),
    # 勝ち: 剣をひと振りして下ろし、まっすぐ立って左手を仮面に添える
    "Win": (False, [
        (0, P()),
        (10, P(Chest=(8, 0, -30), Hips=(0, 0, -30), **ARM_RIGHTSIDE)),
        (16, P(Chest=(8, 0, 30), Hips=(0, 0, 0), **ARM_ACROSS)),
        (34, {**P(), "HipsLoc": (0, 0, 0), "Hips": (0, 0, -10), "Spine": (0, 0, 0), "Chest": (0, 0, 0), "Head": (5, 0, 10),
              "LeftUpperLeg": (0, -3, 0), "LeftLowerLeg": (0, 0, 0), "LeftFoot": (0, 0, 0),
              "RightUpperLeg": (0, 3, 0), "RightLowerLeg": (0, 0, 0), "RightFoot": (0, 0, 0),
              "RightUpperArm": (0, -80, -5), "RightLowerArm": (0, 0, 5), "RightHand": (0, 70, 0),
              "LeftUpperArm": (0, 55, -80), "LeftLowerArm": (0, 0, -150), "LeftHand": (0, 0, -20)}),
        (90, {**P(), "HipsLoc": (0, 0, 0), "Hips": (0, 0, -10), "Spine": (0, 0, 0), "Chest": (0, 0, 0), "Head": (5, 0, 10),
              "LeftUpperLeg": (0, -3, 0), "LeftLowerLeg": (0, 0, 0), "LeftFoot": (0, 0, 0),
              "RightUpperLeg": (0, 3, 0), "RightLowerLeg": (0, 0, 0), "RightFoot": (0, 0, 0),
              "RightUpperArm": (0, -80, -5), "RightLowerArm": (0, 0, 5), "RightHand": (0, 70, 0),
              "LeftUpperArm": (0, 55, -80), "LeftLowerArm": (0, 0, -150), "LeftHand": (0, 0, -20)}),
    ]),
}


# ---------------------------------------------------------------
# Blender に書き込む
# ---------------------------------------------------------------
def apply_pose(arm, pose):
    """ポーズ(T ポーズ基準の回転・度)を骨に入れる。指定のない骨は回転なし。"""
    for pb in arm.pose.bones:
        rest = pb.bone.matrix_local.to_3x3()
        pb.rotation_mode = "QUATERNION"
        e = pose.get(pb.name, (0, 0, 0))
        r = Euler([math.radians(v) for v in e], "XYZ").to_matrix()
        pb.rotation_quaternion = (rest.inverted() @ r @ rest).to_quaternion()
        if pb.parent is None:
            pb.location = rest.inverted() @ Vector(pose.get("HipsLoc", (0, 0, 0)))


def build_actions(arm):
    """ANIMS の全部をアクションとして作る。作ったアクションの一覧を返す。"""
    bpy.context.scene.render.fps = FPS
    arm.animation_data_create()
    actions = []
    for name, (loop, keys) in ANIMS.items():
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        arm.animation_data.action = act
        for frame, pose in keys:
            apply_pose(arm, pose)
            for pb in arm.pose.bones:
                pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
                if pb.parent is None:
                    pb.keyframe_insert("location", frame=frame, group=pb.name)
        act.use_frame_range = True
        act.frame_start = keys[0][0]
        act.frame_end = keys[-1][0]
        act.use_cyclic = loop
        actions.append(act)
    arm.animation_data.action = None
    apply_pose(arm, {})  # T ポーズに戻す
    return actions


def render_contact_sheet(arm, path, frames_per_anim=4, cell=(300, 400)):
    """各アニメのポーズを何枚か並べた確認画像を作る(縦 = アニメ、横 = 時間)。"""
    import numpy as np
    scn = bpy.context.scene
    scn.render.resolution_x, scn.render.resolution_y = cell
    cam = scn.camera
    cam.location = (-4.2, -4.6, 1.5)
    cam.rotation_euler = (Vector((0, 0, 0.95)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = 2.6
    names = list(ANIMS.keys())
    cols = frames_per_anim
    w, h = cell
    sheet = np.zeros((h * len(names), w * cols, 4), dtype=np.float32)
    tmp = path + ".tmp.png"
    for row, name in enumerate(names):
        act = bpy.data.actions[name]
        arm.animation_data.action = act
        f0, f1 = int(act.frame_start), int(act.frame_end)
        keyframes = [k[0] for k in ANIMS[name][1]]
        picks = keyframes[:-1] if len(keyframes) - 1 >= cols else keyframes
        picks = (picks + [f1] * cols)[:cols]
        for col, f in enumerate(picks):
            scn.frame_set(f)
            scn.render.filepath = tmp
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(tmp)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
            bpy.data.images.remove(img)
            # 画像は下から上の順なので、行は下から詰める
            y0 = (len(names) - 1 - row) * h
            sheet[y0:y0 + h, col * w:(col + 1) * w] = px
    out = bpy.data.images.new("sheet", w * cols, h * len(names), alpha=True)
    out.pixels = sheet.ravel()
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    arm.animation_data.action = None
    scn.frame_set(0)
    apply_pose(arm, {})
    return names
