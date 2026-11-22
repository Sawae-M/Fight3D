# -*- coding: utf-8 -*-
"""
ゴウガのアニメーション(ポーズの表)。build_gouga.py から使う。

ルール:
  - 60fps。技の長さは Assets/Scripts/Tuning.cs のゴウガの フレーム数(出るまで+当たっている+終わりの隙)に合わせる。
    ゴウガの通常技は基本形より出るまで +2F(弱 20F、中 33F、強 50F)。
  - その場で動く(位置の移動はゲームのプログラムがする)。上下の動きだけアニメに入れる。
  - 歩く速さは 0.85倍なので、足踏みもカゲツよりゆっくり。
  - 動きのテンポは「重くて粘り強い力士・格闘家」の感覚だけを参考にし、ポーズそのものはオリジナル。
  - 軸の向きは char_builder.py の説明を見る。左腕は mirror() で右腕の回転を反転して作る。
"""

from char_builder import mirror


def arms(right_upper, right_lower, left_upper=None, left_lower=None, right_hand=(0, 0, 0), left_hand=(0, 0, 0)):
    """両腕のポーズ。左を省略すると右の左右反転になる。"""
    lu = left_upper if left_upper is not None else mirror(right_upper)
    ll = left_lower if left_lower is not None else mirror(right_lower)
    return {"RightUpperArm": right_upper, "RightLowerArm": right_lower, "RightHand": right_hand,
            "LeftUpperArm": lu, "LeftLowerArm": ll, "LeftHand": left_hand}


# ---------------------------------------------------------------
# 構え: 足を広く開いて前かがみ、両こぶしを胸の前に構える
# ---------------------------------------------------------------
STANCE = {
    "HipsLoc": (0, 0, -0.06),
    "Hips": (0, 0, -10),
    "Spine": (14, 0, 0),
    "Chest": (8, 0, 6),
    "Neck": (-6, 0, 0),
    "Head": (-14, 0, 4),
    "LeftUpperLeg": (-25, -12, 0), "LeftLowerLeg": (40, 0, 0), "LeftFoot": (-15, 0, 8),
    "RightUpperLeg": (5, 12, 0), "RightLowerLeg": (25, 0, 0), "RightFoot": (-30, 0, -8),
    **arms((0, -55, 45), (0, 65, 35)),
}


def P(base=None, **over):
    """base(なければ構え)をコピーして、指定した骨だけ上書きしたポーズ。"""
    d = dict(STANCE if base is None else base)
    d.update(over)
    return d


def hips(z=0.0, base=None, **over):
    """腰の高さを構えから z(m)だけ変えたポーズ。"""
    p = P(base, **over)
    x, y, zz = p["HipsLoc"]
    p["HipsLoc"] = (x, y, zz + z)
    return p


def A(*args, **kw):
    """arms() の短い名前(ポーズの上書きに ** で混ぜる)。"""
    return arms(*args, **kw)


# よく使う形
GUARD_ARMS = A((0, -20, 70), (0, 80, 20))                         # 前腕を立てて顔を守る
JAB_R = dict(RightUpperArm=(0, -5, 85), RightLowerArm=(0, 0, 5))   # 右ストレート(伸ばしきり)
JAB_L = dict(LeftUpperArm=(0, 5, -85), LeftLowerArm=(0, 0, -5))    # 左ストレート
PULL_R = dict(RightUpperArm=(0, -50, 10), RightLowerArm=(0, 60, 80))  # 右こぶしを引く
PULL_L = dict(LeftUpperArm=(0, 50, -10), LeftLowerArm=(0, -60, -80))  # 左こぶしを引く
WIDE_LEGS = dict(LeftUpperLeg=(-40, -14, 0), LeftLowerLeg=(70, 0, 0), LeftFoot=(-20, 0, 8),
                 RightUpperLeg=(25, 14, 0), RightLowerLeg=(40, 0, 0), RightFoot=(-35, 0, -8))  # 深く踏み込む
SQUAT_LEGS = dict(LeftUpperLeg=(-55, -20, 0), LeftLowerLeg=(95, 0, 0), LeftFoot=(-30, 0, 8),
                  RightUpperLeg=(-20, 20, 0), RightLowerLeg=(85, 0, 0), RightFoot=(-55, 0, -8))  # 深く腰を落とす


def walk_keys(length, back=False):
    """その場で重く足踏みする歩き。back=True なら逆再生(後ろ歩き)。"""
    a = P(LeftUpperLeg=(-35, -12, 0), LeftLowerLeg=(40, 0, 0), RightUpperLeg=(15, 12, 0), RightLowerLeg=(30, 0, 0),
          Chest=(8, 0, 10))
    b = hips(0.02, LeftUpperLeg=(-20, -12, 0), LeftLowerLeg=(30, 0, 0), RightUpperLeg=(-30, 12, 0),
             RightLowerLeg=(70, 0, 0), Chest=(8, 0, 2))
    c = P(LeftUpperLeg=(-10, -12, 0), LeftLowerLeg=(25, 0, 0), RightUpperLeg=(-10, 12, 0), RightLowerLeg=(35, 0, 0),
          Chest=(8, 0, 0))
    d = hips(0.02, LeftUpperLeg=(-45, -12, 0), LeftLowerLeg=(80, 0, 0), RightUpperLeg=(15, 12, 0),
             RightLowerLeg=(30, 0, 0), Chest=(8, 0, 10))
    q = length // 4
    keys = [(0, a), (q, b), (2 * q, c), (3 * q, d), (length, a)]
    if back:
        keys = [(length - f, p) for f, p in reversed(keys)]
    return keys


def strafe_keys(length, left=True):
    """横歩き。足を開く → 片足を上げて寄せる をくり返す。"""
    lead, trail = ("Left", "Right") if left else ("Right", "Left")
    out = -1 if left else 1
    wide = P(LeftUpperLeg=(-25, -20, 0), RightUpperLeg=(5, 20, 0))
    step = hips(0.025, **{f"{lead}UpperLeg": (-30, 28 * out, 0), f"{lead}LowerLeg": (55, 0, 0)})
    close = hips(0.025, **{f"{trail}UpperLeg": (-15, 0, 0), f"{trail}LowerLeg": (65, 0, 0)})
    q = length // 4
    return [(0, wide), (q, step), (2 * q, wide), (3 * q, close), (length, wide)]


ANIMS = {
    # ---- 基本の動き ----
    "Idle": (True, [   # 肩で大きく息をする
        (0, P()),
        (36, hips(-0.015, Chest=(4, 0, 6), Head=(-10, 0, 4), **A((0, -52, 45), (0, 68, 35)))),
        (72, P()),
    ]),
    "WalkForward": (True, walk_keys(48)),
    "WalkBack": (True, walk_keys(56, back=True)),
    "StrafeLeft": (True, strafe_keys(48, left=True)),
    "StrafeRight": (True, strafe_keys(48, left=False)),

    # ---- 通常技(弱 7+3+10=20F、中 11+4+18=33F、強 17+5+28=50F) ----
    "Light": (False, [   # 右のまっすぐな突き
        (0, P()),
        (5, P(Chest=(8, 0, -15), **PULL_R)),
        (8, P(Spine=(20, 0, 0), Chest=(10, 0, 25), Hips=(0, 0, 0), **JAB_R)),
        (11, P(Spine=(20, 0, 0), Chest=(10, 0, 25), Hips=(0, 0, 0), **JAB_R)),
        (20, P()),
    ]),
    "Medium": (False, [  # 横から振り回すフック
        (0, P()),
        (9, P(Chest=(8, 0, -40), Hips=(0, 0, -30), RightUpperArm=(0, -10, -15), RightLowerArm=(0, 0, 70))),
        (12, P(Chest=(10, 0, 10), Hips=(0, 0, -5), RightUpperArm=(0, -5, 60), RightLowerArm=(0, 0, 70))),
        (15, P(Chest=(10, 0, 40), Hips=(0, 0, 15), RightUpperArm=(0, -5, 115), RightLowerArm=(0, 0, 55), **PULL_L)),
        (22, P(Chest=(10, 0, 38), Hips=(0, 0, 12), RightUpperArm=(0, -10, 110), RightLowerArm=(0, 0, 55), **PULL_L)),
        (33, P()),
    ]),
    "Heavy": (False, [   # 両こぶしを組んで頭上から叩きつける
        (0, P()),
        (15, hips(0.03, Spine=(-8, 0, 0), Chest=(-5, 0, 0), Head=(-20, 0, 0), **A((0, 75, 45), (0, 0, 60)))),
        (18, hips(-0.14, Spine=(40, 0, 0), Chest=(10, 0, 0), Head=(5, 0, 0), **A((0, -45, 80), (0, 0, 10)), **WIDE_LEGS)),
        (24, hips(-0.14, Spine=(42, 0, 0), Chest=(10, 0, 0), Head=(5, 0, 0), **A((0, -47, 80), (0, 0, 10)), **WIDE_LEGS)),
        (50, P()),
    ]),

    # ---- スキル1 岩砕き(20+1+20=41F) 右こぶしを振り上げ、地面を殴って衝撃波 ----
    "Gansai": (False, [
        (0, P()),
        (16, hips(0.04, Spine=(-12, 0, 0), Chest=(-5, 0, -20), Head=(-20, 0, 0), RightUpperArm=(0, 80, 30),
                  RightLowerArm=(0, 0, 40), **PULL_L)),
        (20, hips(-0.32, Spine=(55, 0, 0), Chest=(15, 0, 10), Head=(-20, 0, 0), RightUpperArm=(0, -60, 70),
                  RightLowerArm=(0, 0, 0), LeftUpperArm=(0, 60, 20), LeftLowerArm=(0, 0, -20), **SQUAT_LEGS)),
        (28, hips(-0.32, Spine=(55, 0, 0), Chest=(15, 0, 10), Head=(-22, 0, 0), RightUpperArm=(0, -60, 70),
                  RightLowerArm=(0, 0, 0), LeftUpperArm=(0, 60, 20), LeftLowerArm=(0, 0, -20), **SQUAT_LEGS)),
        (41, P()),
    ]),

    # ---- スキル2 剛腕引き(12+4+10=26F。外したときの隙 30F はゲーム側で最後のポーズを伸ばす) ----
    "Gouwan": (False, [
        (0, P()),
        (8, P(Chest=(8, 0, -30), Hips=(0, 0, -20), **PULL_R)),
        (12, hips(-0.08, Spine=(30, 0, 0), Chest=(10, 0, 30), Hips=(0, 0, 5), RightUpperArm=(0, 0, 90),
                  RightLowerArm=(0, 0, 0), RightHand=(0, 0, -20), **PULL_L, **WIDE_LEGS)),
        (16, hips(-0.08, Spine=(30, 0, 0), Chest=(10, 0, 30), Hips=(0, 0, 5), RightUpperArm=(0, 0, 90),
                  RightLowerArm=(0, 0, 0), RightHand=(0, 0, -20), **PULL_L, **WIDE_LEGS)),
        (21, hips(-0.02, Spine=(0, 0, 0), Chest=(-5, 0, -30), Hips=(0, 0, -20), RightUpperArm=(0, -40, -10),
                  RightLowerArm=(0, 30, 100))),
        (26, P()),
    ]),

    # ---- スキル3 受け止め(構え 30F + 隙 20F = 50F) 腕を交差させて腰を落とす ----
    "Uketome": (False, [
        (0, P()),
        (6, hips(-0.12, Spine=(18, 0, 0), Head=(0, 0, 0), **A((0, -25, 80), (0, 80, 45)), **SQUAT_LEGS)),
        (30, hips(-0.13, Spine=(19, 0, 0), Head=(0, 0, 0), **A((0, -25, 80), (0, 80, 45)), **SQUAT_LEGS)),
        (50, P()),
    ]),
    # 受け止めからの反撃(4F目に当たる、隙 20F → 24F) 下からのアッパー
    "UketomeCounter": (False, [
        (0, hips(-0.13, Spine=(19, 0, 0), **A((0, -25, 80), (0, 80, 45)), **SQUAT_LEGS)),
        (2, hips(-0.16, Spine=(25, 0, 0), Chest=(10, 0, -20), RightUpperArm=(0, -70, 20), RightLowerArm=(0, 30, 40),
                 **SQUAT_LEGS)),
        (5, hips(0.06, Spine=(-15, 0, 0), Chest=(-5, 0, 25), Head=(-25, 0, 0), RightUpperArm=(0, 75, 70),
                 RightLowerArm=(0, 30, 0), **PULL_L, LeftUpperLeg=(-15, -12, 0), LeftLowerLeg=(10, 0, 0),
                 RightUpperLeg=(15, 12, 0), RightLowerLeg=(20, 0, 0))),
        (12, hips(0.03, Spine=(-12, 0, 0), Chest=(-5, 0, 25), Head=(-20, 0, 0), RightUpperArm=(0, 72, 70),
                  RightLowerArm=(0, 30, 0), **PULL_L)),
        (24, P()),
    ]),

    # ---- ウルト 大地崩し(踏み込みパンチ 12+4+10=26F。外したら隙 50F は最後のポーズを伸ばす) ----
    "DaichiKuzushi": (False, [
        (0, P()),
        (9, hips(-0.05, Chest=(8, 0, -35), Hips=(0, 0, -25), **PULL_R, **PULL_L)),
        (12, hips(-0.14, Spine=(30, 0, 0), Chest=(10, 0, 30), Hips=(0, 0, 5), **JAB_R, **PULL_L, **WIDE_LEGS)),
        (16, hips(-0.14, Spine=(30, 0, 0), Chest=(10, 0, 30), Hips=(0, 0, 5), **JAB_R, **PULL_L, **WIDE_LEGS)),
        (26, P()),
    ]),
    # 当たった後の連打(当たるのは 6,10,14,18,22F、締めのアッパーは 38F、隙 24F → 62F)
    "DaichiKuzushiRush": (False, [
        (0, P(**WIDE_LEGS)),
        (4, P(Chest=(10, 0, -20), **PULL_R, **JAB_L, **WIDE_LEGS)),
        (6, P(Chest=(10, 0, 25), **JAB_R, **PULL_L, **WIDE_LEGS)),
        (10, P(Chest=(10, 0, -25), **PULL_R, **JAB_L, **WIDE_LEGS)),
        (14, P(Chest=(10, 0, 25), **JAB_R, **PULL_L, **WIDE_LEGS)),
        (18, P(Chest=(10, 0, -25), **PULL_R, **JAB_L, **WIDE_LEGS)),
        (22, P(Chest=(12, 0, 30), Spine=(22, 0, 0), **JAB_R, **PULL_L, **WIDE_LEGS)),
        (33, hips(-0.22, Spine=(35, 0, 0), Chest=(10, 0, -25), RightUpperArm=(0, -75, 10), RightLowerArm=(0, 40, 50),
                  **PULL_L, **SQUAT_LEGS)),
        (38, hips(0.10, Spine=(-18, 0, 0), Chest=(-5, 0, 25), Head=(-30, 0, 0), RightUpperArm=(0, 85, 75),
                  RightLowerArm=(0, 20, 0), **PULL_L, LeftUpperLeg=(-10, -12, 0), LeftLowerLeg=(5, 0, 0),
                  RightUpperLeg=(10, 12, 0), RightLowerLeg=(15, 0, 0))),
        (48, hips(0.04, Spine=(-12, 0, 0), Chest=(-5, 0, 25), Head=(-25, 0, 0), RightUpperArm=(0, 82, 75),
                  RightLowerArm=(0, 20, 0), **PULL_L)),
        (62, P()),
    ]),

    # ---- 守り ----
    "Guard": (True, [
        (0, hips(-0.04, Spine=(20, 0, 0), Head=(0, 0, 0), **GUARD_ARMS)),
        (20, hips(-0.045, Spine=(21, 0, 0), Head=(0, 0, 0), **A((0, -20, 70), (0, 82, 20)))),
        (40, hips(-0.04, Spine=(20, 0, 0), Head=(0, 0, 0), **GUARD_ARMS)),
    ]),
    # 回避ステップ 24F(無敵 4〜12F)。体が大きいので低くかがんで横へ跳ぶ
    "Dodge": (False, [
        (0, P()),
        (3, hips(-0.10, Spine=(25, 0, 0), **SQUAT_LEGS)),
        (9, hips(0.0, Spine=(35, 0, 0), Chest=(10, 0, 0), **GUARD_ARMS,
                 LeftUpperLeg=(-60, -15, 0), LeftLowerLeg=(100, 0, 0), RightUpperLeg=(-35, 15, 0), RightLowerLeg=(100, 0, 0))),
        (16, hips(-0.14, Spine=(28, 0, 0), **SQUAT_LEGS)),
        (24, P()),
    ]),

    # ---- やられ ----
    "Hit": (False, [   # のけぞり 17F 前後(技ごとの長さはゲーム側で再生速度を変える)
        (0, P()),
        (3, hips(0.01, Spine=(-8, 0, 0), Chest=(-8, 0, 12), Head=(-25, 0, 0), **A((0, -40, 20), (0, 40, 30)))),
        (17, P()),
    ]),
    "Stagger": (False, [   # よろけ 40F
        (0, P()),
        (4, hips(0.02, Spine=(-20, 0, 0), Chest=(-10, 0, 15), Head=(-30, 0, 10), **A((0, -20, -20), (0, 10, 20)))),
        (14, hips(-0.14, Spine=(35, 0, 0), Chest=(10, 0, -8), Head=(5, 0, 0), **A((0, -80, 15), (0, 0, 10)),
                  LeftUpperLeg=(-20, -12, 0), LeftLowerLeg=(50, 0, 0), RightUpperLeg=(-10, 12, 0), RightLowerLeg=(60, 0, 0))),
        (30, hips(-0.13, Spine=(33, 0, 0), Chest=(10, 0, -8), Head=(0, 0, 0), **A((0, -80, 15), (0, 0, 10)),
                  LeftUpperLeg=(-20, -12, 0), LeftLowerLeg=(50, 0, 0), RightUpperLeg=(-10, 12, 0), RightLowerLeg=(60, 0, 0))),
        (40, P()),
    ]),
    # 浮き 66F(空中 36F + 着地後 30F)。空中の高さはゲーム側が動かす
    "Launch": (False, [
        (0, P()),
        (5, P(Spine=(-25, 0, 0), Chest=(-12, 0, 0), Head=(-30, 0, 0), **A((0, 30, -20), (0, 0, 10)),
              LeftUpperLeg=(-35, -12, 0), LeftLowerLeg=(60, 0, 0), RightUpperLeg=(-20, 12, 0), RightLowerLeg=(40, 0, 0))),
        (30, P(Hips=(-45, 0, 0), Spine=(-10, 0, 0), **A((0, 10, -10), (0, 0, 5)),
               LeftUpperLeg=(-40, -12, 0), LeftLowerLeg=(40, 0, 0), RightUpperLeg=(-30, 12, 0), RightLowerLeg=(30, 0, 0))),
        (36, hips(-0.64, Hips=(-85, 0, 0), Spine=(-5, 0, 0), Head=(10, 0, 0), **A((0, -10, 0), (0, 0, 5)),
                  LeftUpperLeg=(-10, -14, 0), LeftLowerLeg=(15, 0, 0), RightUpperLeg=(-5, 14, 0), RightLowerLeg=(10, 0, 0))),
        (48, hips(-0.64, Hips=(-85, 0, 0), Spine=(-5, 0, 0), Head=(10, 0, 0), **A((0, -10, 0), (0, 0, 5)),
                  LeftUpperLeg=(-10, -14, 0), LeftLowerLeg=(15, 0, 0), RightUpperLeg=(-5, 14, 0), RightLowerLeg=(10, 0, 0))),
        (56, hips(-0.50, Hips=(-35, 0, 0), Spine=(30, 0, 0), Head=(10, 0, 0), **A((0, -70, -20), (0, 0, 10)),
                  LeftUpperLeg=(-80, -14, 0), LeftLowerLeg=(130, 0, 0), RightUpperLeg=(-70, 14, 0), RightLowerLeg=(130, 0, 0))),
        (62, hips(-0.22, Spine=(30, 0, 0), **SQUAT_LEGS)),
        (66, P()),
    ]),
    # K.O.: ひざから崩れ、前のめりに倒れたまま
    "KO": (False, [
        (0, P()),
        (10, hips(-0.25, Spine=(30, 0, 0), Head=(25, 0, 0), **A((0, -85, 0), (0, 0, 0)),
                  LeftUpperLeg=(-50, -12, 0), LeftLowerLeg=(110, 0, 0), RightUpperLeg=(-10, 12, 0), RightLowerLeg=(110, 0, 0))),
        (28, hips(-0.64, Hips=(80, 0, 15), Spine=(5, 0, 0), Head=(0, 0, 30), **A((0, -5, -10), (0, 0, 10)),
                  LeftUpperLeg=(-15, -12, 0), LeftLowerLeg=(30, 0, 0), RightUpperLeg=(0, 12, 0), RightLowerLeg=(10, 0, 0))),
        (60, hips(-0.65, Hips=(82, 0, 15), Spine=(5, 0, 0), Head=(0, 0, 30), **A((0, -5, -10), (0, 0, 10)),
                  LeftUpperLeg=(-15, -12, 0), LeftLowerLeg=(30, 0, 0), RightUpperLeg=(0, 12, 0), RightLowerLeg=(10, 0, 0))),
    ]),
    # 勝ち: 胸を2回たたいてから、両腕を上げて吠える
    "Win": (False, [
        (0, P()),
        (10, hips(0.03, Spine=(0, 0, 0), RightUpperArm=(0, -50, 70), RightLowerArm=(0, 0, 120))),
        (14, hips(0.03, Spine=(0, 0, 0), RightUpperArm=(0, -30, 50), RightLowerArm=(0, 0, 70))),
        (20, hips(0.03, Spine=(0, 0, 0), LeftUpperArm=(0, 50, -70), LeftLowerArm=(0, 0, -120))),
        (24, hips(0.03, Spine=(0, 0, 0), LeftUpperArm=(0, 30, -50), LeftLowerArm=(0, 0, -70))),
        (40, hips(0.06, Spine=(-15, 0, 0), Chest=(-10, 0, 0), Head=(-30, 0, 0), **A((0, 50, 20), (0, 60, 10)),
                  LeftUpperLeg=(-15, -14, 0), LeftLowerLeg=(15, 0, 0), RightUpperLeg=(5, 14, 0), RightLowerLeg=(15, 0, 0))),
        (90, hips(0.06, Spine=(-16, 0, 0), Chest=(-10, 0, 0), Head=(-32, 0, 0), **A((0, 52, 20), (0, 62, 10)),
                  LeftUpperLeg=(-15, -14, 0), LeftLowerLeg=(15, 0, 0), RightUpperLeg=(5, 14, 0), RightLowerLeg=(15, 0, 0))),
    ]),
}
