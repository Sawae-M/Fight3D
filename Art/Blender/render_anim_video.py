# -*- coding: utf-8 -*-
"""
Kagetsu.blend のアニメを全部つなげて、確認用の動画(MP4)を書き出すスクリプト。

使い方(PowerShell):
  & "D:\steam\steamapps\common\Blender\blender.exe" --background Art\Blender\Kagetsu.blend --python Art\Blender\render_anim_video.py
出力: Art/Blender/Kagetsu_anims.mp4
"""

import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kagetsu_anims  # noqa: E402  アニメの順番を使う

GAP = 20  # アニメとアニメの間に入れる、構えで止まる時間(F)。仮

scn = bpy.context.scene
arm = bpy.data.objects["Armature"]
arm.animation_data.action = None

# NLA にアニメを順番に並べる
track = arm.animation_data.nla_tracks.new()
start = 1
for name in kagetsu_anims.ANIMS:
    act = bpy.data.actions[name]
    length = int(act.frame_end - act.frame_start)
    # ループするアニメは2回くり返して見せる
    repeat = 2 if kagetsu_anims.ANIMS[name][0] else 1
    strip = track.strips.new(name, start, act)
    strip.repeat = repeat
    strip.extrapolation = "HOLD_FORWARD"  # 次のアニメまで最後のポーズ(構え)で止める
    start += length * repeat + GAP
scn.frame_start, scn.frame_end = 1, start
scn.render.fps = 60

# カメラ(斜め前から)と見た目
cd = bpy.data.cameras.new("VideoCam")
cd.type = "ORTHO"
cd.ortho_scale = 2.8
cam = bpy.data.objects.new("VideoCam", cd)
scn.collection.objects.link(cam)
scn.camera = cam
cam.location = (-4.2, -4.6, 1.6)
cam.rotation_euler = (Vector((0, 0, 0.9)) - cam.location).to_track_quat("-Z", "Y").to_euler()
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
scn.render.filepath = os.path.join(HERE, "Kagetsu_anims.mp4")
bpy.ops.render.render(animation=True)
print("VIDEO_DONE", scn.render.filepath, "frames", start)
