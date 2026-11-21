# -*- coding: utf-8 -*-
"""
キャラの .blend のアニメを全部つなげて、確認用の動画(MP4)を書き出すスクリプト。

使い方(PowerShell):
  & "D:\steam\steamapps\common\Blender\blender.exe" --background Art\Blender\Kagetsu.blend --python Art\Blender\render_anim_video.py
  & "D:\steam\steamapps\common\Blender\blender.exe" --background Art\Blender\Gouga.blend --python Art\Blender\render_anim_video.py
出力: Art/Blender/<キャラ名>_anims.mp4(キャラ名は開いた .blend の名前)
"""

import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import importlib  # noqa: E402

# 開いた .blend の名前(Kagetsu / Gouga)から、アニメの表(kagetsu_anims / gouga_anims)を選ぶ
CHAR = os.path.splitext(os.path.basename(bpy.data.filepath))[0]
anims_module = importlib.import_module(CHAR.lower() + "_anims")
HEIGHT = 2.2 if CHAR == "Gouga" else 1.95  # 身長(m)。カメラの写す範囲に使う

GAP = 20  # アニメとアニメの間に入れる、構えで止まる時間(F)。仮

scn = bpy.context.scene
arm = bpy.data.objects["Armature"]
arm.animation_data.action = None

# NLA にアニメを順番に並べる
track = arm.animation_data.nla_tracks.new()
start = 1
for name in anims_module.ANIMS:
    act = bpy.data.actions[name]
    length = int(act.frame_end - act.frame_start)
    # ループするアニメは2回くり返して見せる
    repeat = 2 if anims_module.ANIMS[name][0] else 1
    strip = track.strips.new(name, start, act)
    strip.repeat = repeat
    strip.extrapolation = "HOLD_FORWARD"  # 次のアニメまで最後のポーズ(構え)で止める
    start += length * repeat + GAP
scn.frame_start, scn.frame_end = 1, start
scn.render.fps = 60

# カメラ(斜め前から)と見た目
cd = bpy.data.cameras.new("VideoCam")
cd.type = "ORTHO"
cd.ortho_scale = HEIGHT * 1.45
cam = bpy.data.objects.new("VideoCam", cd)
scn.collection.objects.link(cam)
scn.camera = cam
cam.location = (-4.2, -4.6, HEIGHT * 0.8)
cam.rotation_euler = (Vector((0, 0, HEIGHT * 0.46)) - cam.location).to_track_quat("-Z", "Y").to_euler()
scn.render.engine = "BLENDER_WORKBENCH"
scn.display.shading.light = "STUDIO"
scn.display.shading.color_type = "MATERIAL"
scn.display.shading.show_cavity = True
scn.render.resolution_x, scn.render.resolution_y = 540, 720

# 動画として書き出す
if hasattr(scn.render.image_settings, "media_type"):
    scn.render.image_settings.media_type = "VIDEO"
scn.render.image_settings.file_format = "FFMPEG"
scn.render.ffmpeg.format = "MPEG4"
scn.render.ffmpeg.codec = "H264"
scn.render.ffmpeg.constant_rate_factor = "MEDIUM"
scn.render.filepath = os.path.join(HERE, CHAR + "_anims.mp4")
bpy.ops.render.render(animation=True)
print("VIDEO_DONE", scn.render.filepath, "frames", start)
