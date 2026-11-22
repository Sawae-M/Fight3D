# -*- coding: utf-8 -*-
"""
キャラのモデルを Blender で作るときの共通の道具(ゴウガから使用。カゲツは build_kagetsu.py に同じ物が入っている)。

  - 部品(箱・球・筒・輪・円盤)を作り、1つの骨に100%で付ける
  - Humanoid 用の骨組みを作る
  - 部品をまとめて1つのメッシュにする
  - Unity 用の FBX を書き出す
  - ポーズ(T ポーズ基準の回転・度)からアニメを作る
  - 確認画像・ポーズ一覧を出す

ポーズの軸(T ポーズを基準にした向き。キャラは -Y を向き、左手は +X):
    X 軸 … + で前に倒れる(背骨)/ 脚を後ろへ(太もも)/ ひざを曲げる(すね)
    Y 軸 … 左腕は + で下がる、右腕は - で下がる
    Z 軸 … + で体が左へ向く。左腕は - で前へ、右腕は + で前へ
"""

import math
import os

import bpy
from mathutils import Euler, Vector


def srgb_to_linear(c):
    """sRGB の色を Blender 内部(リニア)の色に直す。"""
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


def mirror(rot):
    """右腕の回転を左腕用に左右反転する(Y と Z の向きが逆になる)。"""
    x, y, z = rot
    return (x, -y, -z)


class Builder:
    """1体のキャラを作るための道具一式。"""

    def __init__(self, colors, segments=14, warp=None):
        """colors: {マテリアル名: ((r,g,b) sRGB, 金属っぽさ, ざらざら具合)}。segments: 筒の角の数。

        warp: 体つきを変える関数 warp(位置, 骨の名前) → (新しい位置, 太さの倍率)。
              作った部品・骨組み・肉付けの点すべてに使う(例: 頭を大きく、脚を長く)。None なら何もしない。
        """
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.segments = segments
        self.warp = warp
        self.parts = []  # (オブジェクト, グループ名)
        self.mats = {}
        for name, (rgb, metal, rough) in colors.items():
            m = bpy.data.materials.new(name)
            m.use_nodes = True
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            lin = srgb_to_linear(rgb) + (1.0,)
            bsdf.inputs["Base Color"].default_value = lin
            bsdf.inputs["Metallic"].default_value = metal
            bsdf.inputs["Roughness"].default_value = rough
            m.diffuse_color = lin  # 確認画像(Workbench)用の色
            self.mats[name] = m

    # ---------------- 部品 ----------------
    def _finish(self, obj, mat, bone, smooth, group):
        """作った部品に色と骨を付け、変形を確定して一覧に入れる。"""
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        if self.warp:
            for v in obj.data.vertices:
                v.co = self.warp(Vector(v.co), bone)[0]
        obj.data.materials.append(self.mats[mat])
        vg = obj.vertex_groups.new(name=bone)
        vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
        for p in obj.data.polygons:
            p.use_smooth = smooth
        obj.select_set(False)
        self.parts.append((obj, group))
        return obj

    @staticmethod
    def _rad(rot):
        """度 → ラジアン。"""
        return [math.radians(r) for r in rot]

    def box(self, name, loc, size, mat, bone, rot=(0, 0, 0), group="body"):
        """箱。size は (幅X, 奥行Y, 高さZ) メートル、rot は度。"""
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=self._rad(rot))
        o = bpy.context.active_object
        o.name = name
        o.scale = size
        return self._finish(o, mat, bone, False, group)

    def ball(self, name, loc, size, mat, bone, rot=(0, 0, 0), group="body", seg=16):
        """球。size は X/Y/Z 方向の半径。"""
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=1,
                                             location=loc, rotation=self._rad(rot))
        o = bpy.context.active_object
        o.name = name
        o.scale = size
        return self._finish(o, mat, bone, True, group)

    def limb(self, name, p0, p1, r0, r1, mat, bone, squash=1.0, smooth=True, group="body", seg=None):
        """p0 から p1 へ伸びる先細りの筒。squash は前後(Y)のつぶし具合。"""
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        bpy.ops.mesh.primitive_cone_add(vertices=seg or self.segments, radius1=r0, radius2=r1,
                                        depth=d.length, location=(p0 + p1) / 2)
        o = bpy.context.active_object
        o.name = name
        o.rotation_mode = "QUATERNION"
        o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        o.select_set(False)
        if squash != 1.0:
            for v in o.data.vertices:
                v.co.y *= squash
        return self._finish(o, mat, bone, smooth, group)

    def ring(self, name, loc, major, minor, mat, bone, rot=(0, 0, 0), group="body"):
        """輪っか。"""
        bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=24,
                                         minor_segments=8, location=loc, rotation=self._rad(rot))
        o = bpy.context.active_object
        o.name = name
        return self._finish(o, mat, bone, True, group)

    def disc(self, name, loc, radius, depth, mat, bone, rot=(0, 0, 0), group="body", seg=24):
        """平たい円盤。"""
        bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=radius, depth=depth, location=loc,
                                            rotation=self._rad(rot))
        o = bpy.context.active_object
        o.name = name
        return self._finish(o, mat, bone, False, group)

    def skin(self, name, points, edges, mat, subdiv=2, group="body", region="Hips", rigid_bone=None):
        """骨組み(点と線)に肉付けして、なめらかな体の部品を作る(スキンモディファイア + 細分化)。

        points: [(位置, 半径), ...]。半径は数字1つ(丸)か (左右, 前後) の2つ。最初の点が根元になる。
        edges:  [(点の番号, 点の番号), ...] でつなぐ点の組。
        骨には「自動の重み」で付ける(関節のまわりがなめらかに曲がる)。先に armature() を呼んでおくこと。
        region: warp で体つきを変えるときに、この部品をどの骨の場所として扱うか。
        rigid_bone: 指定すると、自動の重みではなく、その骨に100%で付ける(頭など、曲がらない部分用)。
        """
        if self.warp:
            moved = []
            for p, r in points:
                np_, k = self.warp(Vector(p), region)
                moved.append((tuple(np_), r * k if isinstance(r, (int, float)) else (r[0] * k, r[1] * k)))
            points = moved
        me = bpy.data.meshes.new(name)
        me.from_pydata([p for p, _ in points], edges, [])
        me.update()
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        mod = o.modifiers.new("Skin", "SKIN")
        mod.branch_smoothing = 0.5  # 枝分かれ(肩・股)の所をなめらかに
        sv = me.skin_vertices[0].data
        for i, (_, r) in enumerate(points):
            sv[i].radius = (r, r) if isinstance(r, (int, float)) else r
        sv[0].use_root = True
        sub = o.modifiers.new("Subdiv", "SUBSURF")
        sub.levels = sub.render_levels = subdiv
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.modifier_apply(modifier="Skin")
        bpy.ops.object.modifier_apply(modifier="Subdiv")
        o.data.materials.append(self.mats[mat])
        for p in o.data.polygons:
            p.use_smooth = True
        if rigid_bone:
            vg = o.vertex_groups.new(name=rigid_bone)
            vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
            bpy.ops.object.select_all(action="DESELECT")
            self.parts.append((o, group))
            return o
        # 自動の重み: 骨からの距離で、どの骨にどれだけ付くかを決める
        self.arm.select_set(True)
        bpy.context.view_layer.objects.active = self.arm
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        # 重み(頂点グループ)だけ残し、親子と骨のモディファイアは join() でまとめて付け直す
        for m in list(o.modifiers):
            o.modifiers.remove(m)
        o.parent = None
        bpy.ops.object.select_all(action="DESELECT")
        self.parts.append((o, group))
        return o

    # ---------------- 骨・まとめ ----------------
    def armature(self, bones, data_name):
        """bones: {名前: (頭, しっぽ, 親)} から骨組みを作る。オブジェクト名は Unity で分かりやすい Armature。"""
        arm_data = bpy.data.armatures.new(data_name)
        arm = bpy.data.objects.new("Armature", arm_data)
        bpy.context.collection.objects.link(arm)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode="EDIT")
        eb = arm_data.edit_bones
        for name, (h, t, parent) in bones.items():
            b = eb.new(name)
            if self.warp:
                h, t = self.warp(Vector(h), name)[0], self.warp(Vector(t), name)[0]
            b.head, b.tail = h, t
            if parent:
                b.parent = eb[parent]
                if (Vector(eb[parent].tail) - Vector(h)).length < 1e-4:
                    b.use_connect = True  # 親のしっぽと同じ位置なら「つながった骨」
        bpy.ops.armature.select_all(action="SELECT")
        bpy.ops.armature.calculate_roll(type="GLOBAL_POS_Z")
        bpy.ops.object.mode_set(mode="OBJECT")
        self.arm = arm
        return arm

    def join(self, group, name):
        """同じグループの部品を1つのメッシュにまとめ、骨で動くようにする。"""
        objs = [o for o, g in self.parts if g == group]
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.join()
        o = bpy.context.active_object
        o.name = o.data.name = name
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")  # 原点を足元に
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = self.arm
        o.parent = self.arm
        o.select_set(False)
        return o

    # ---------------- 書き出し ----------------
    @staticmethod
    def export_fbx(path):
        """Unity 用の FBX を書き出す(拡大率 1、Y 上、アクション1つ = クリップ1つ)。"""
        os.makedirs(os.path.dirname(path), exist_ok=True)
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
            axis_forward="-Z", axis_up="Y", add_leaf_bones=False, primary_bone_axis="Y",
            secondary_bone_axis="X", use_armature_deform_only=True,
            bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
            bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
            mesh_smooth_type="FACE",
        )

    def toon_preview(self, outline=0.004, shadow_tint=(0.55, 0.5, 0.7), threshold=0.45):
        """確認画像をトゥーン調(Unity の Fight3D/Toon シェーダーに近い見た目)にする。

        FBX を書き出した「後」に呼ぶこと(マテリアルの中身を作り替えるので、先に呼ぶと FBX の色が取れなくなる)。
        - 影が2段に分かれる色(Shader to RGB → 色の段差)
        - 輪郭線(少し大きい裏返しの殻を暗い色で描く)
        """
        for m in self.mats.values():
            base = tuple(m.diffuse_color)
            nt = m.node_tree
            nt.nodes.clear()
            diffuse = nt.nodes.new("ShaderNodeBsdfDiffuse")
            diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
            to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
            ramp = nt.nodes.new("ShaderNodeValToRGB")
            ramp.color_ramp.interpolation = "CONSTANT"
            shadow = tuple(base[i] * shadow_tint[i] for i in range(3)) + (1,)
            ramp.color_ramp.elements[0].position = 0.0
            ramp.color_ramp.elements[0].color = shadow
            ramp.color_ramp.elements[1].position = threshold
            ramp.color_ramp.elements[1].color = base
            emit = nt.nodes.new("ShaderNodeEmission")
            out = nt.nodes.new("ShaderNodeOutputMaterial")
            nt.links.new(diffuse.outputs[0], to_rgb.inputs[0])
            nt.links.new(to_rgb.outputs["Color"], ramp.inputs["Fac"])
            nt.links.new(ramp.outputs["Color"], emit.inputs["Color"])
            nt.links.new(emit.outputs[0], out.inputs["Surface"])
        # 輪郭線用のマテリアル(裏側だけ見える)
        line = bpy.data.materials.new("Outline")
        line.use_nodes = True
        line.use_backface_culling = True
        nt = line.node_tree
        nt.nodes.clear()
        emit = nt.nodes.new("ShaderNodeEmission")
        emit.inputs["Color"].default_value = (0.01, 0.008, 0.012, 1)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(emit.outputs[0], out.inputs["Surface"])
        for o in bpy.data.objects:
            if o.type != "MESH":
                continue
            o.data.materials.append(line)
            mod = o.modifiers.new("Outline", "SOLIDIFY")
            mod.thickness = outline
            mod.offset = 1.0
            mod.use_flip_normals = True
            mod.material_offset = len(o.data.materials) - 1
        self.toon = True

    def setup_render(self, res=(900, 1200)):
        """確認画像用のレンダー設定と、正射影カメラを用意して返す。toon_preview() 済みなら EEVEE、まだなら Workbench。"""
        scn = bpy.context.scene
        if getattr(self, "toon", False):
            try:
                scn.render.engine = "BLENDER_EEVEE"
            except TypeError:
                scn.render.engine = "BLENDER_EEVEE_NEXT"
            scn.view_settings.view_transform = "Standard"  # 色を薄めない
            sun_data = bpy.data.lights.new("PreviewSun", "SUN")
            sun_data.energy = 3.0
            sun = bpy.data.objects.new("PreviewSun", sun_data)
            scn.collection.objects.link(sun)
            sun.rotation_euler = (math.radians(50), 0, math.radians(-35))  # 左前の上から照らす
        else:
            scn.render.engine = "BLENDER_WORKBENCH"
            scn.display.shading.light = "STUDIO"
            scn.display.shading.color_type = "MATERIAL"
            scn.display.shading.show_cavity = True
        scn.render.resolution_x, scn.render.resolution_y = res
        world = bpy.data.worlds.new("PreviewWorld")
        world.color = (0.25, 0.27, 0.32)
        scn.world = world
        cd = bpy.data.cameras.new("PreviewCam")
        cd.type = "ORTHO"
        cam = bpy.data.objects.new("PreviewCam", cd)
        scn.collection.objects.link(cam)
        scn.camera = cam
        return cam

    @staticmethod
    def aim(cam, pos, target, ortho):
        """カメラを pos に置き、target を向かせる。ortho は写る範囲の幅(m)。"""
        cam.location = pos
        cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = ortho

    def render_previews(self, prefix, height, face=None):
        """正面・斜め・後ろの確認画像を出す。face=(顔の中心の位置, 写す幅) なら顔のアップ(正面・斜め)も出す。カメラを返す。"""
        cam = self.setup_render()
        mid = height / 2
        views = {"front": (0, -6, mid), "angle": (-4.2, -4.2, mid + 0.6), "back": (2.5, 5.5, mid + 0.3)}
        for key, pos in views.items():
            self.aim(cam, pos, (0, 0, mid), height * 1.45)  # 縦長の画像なので、横に広げた腕が入る幅にする
            bpy.context.scene.render.filepath = prefix + key + ".png"
            bpy.ops.render.render(write_still=True)
        if face:
            center, size = Vector(face[0]), face[1]
            scn = bpy.context.scene
            res = scn.render.resolution_x, scn.render.resolution_y
            scn.render.resolution_x = scn.render.resolution_y = 900
            for key, offset in {"face": (0, -3, 0.15), "face_angle": (-2.2, -2.2, 0.3)}.items():
                self.aim(cam, center + Vector(offset), center, size)
                scn.render.filepath = prefix + key + ".png"
                bpy.ops.render.render(write_still=True)
            scn.render.resolution_x, scn.render.resolution_y = res
        return cam

    def mesh_part(self, name, verts, faces, mat, bone, smooth=True, group="body", thickness=0.0):
        """頂点と面のリストから部品を作る(仮面など、形を細かく作りたい物用)。thickness で厚みを付ける。"""
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], faces)
        me.update()
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        if thickness:
            bpy.ops.object.select_all(action="DESELECT")
            bpy.context.view_layer.objects.active = o
            o.select_set(True)
            mod = o.modifiers.new("Solidify", "SOLIDIFY")
            mod.thickness = thickness
            bpy.ops.object.modifier_apply(modifier="Solidify")
        return self._finish(o, mat, bone, smooth, group)


# ---------------------------------------------------------------
# アニメ
# ---------------------------------------------------------------
def apply_pose(arm, pose):
    """ポーズ(T ポーズ基準の回転・度)を骨に入れる。指定のない骨は回転なし。HipsLoc は腰の移動(m)。"""
    for pb in arm.pose.bones:
        rest = pb.bone.matrix_local.to_3x3()
        pb.rotation_mode = "QUATERNION"
        r = Euler([math.radians(v) for v in pose.get(pb.name, (0, 0, 0))], "XYZ").to_matrix()
        pb.rotation_quaternion = (rest.inverted() @ r @ rest).to_quaternion()
        if pb.parent is None:
            pb.location = rest.inverted() @ Vector(pose.get("HipsLoc", (0, 0, 0)))


def build_actions(arm, anims, fps=60):
    """anims: {名前: (ループするか, [(フレーム, ポーズ), ...])} を全部アクションにする。"""
    bpy.context.scene.render.fps = fps
    arm.animation_data_create()
    for name, (loop, keys) in anims.items():
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
        act.frame_start, act.frame_end = keys[0][0], keys[-1][0]
        act.use_cyclic = loop
    arm.animation_data.action = None
    apply_pose(arm, {})


def render_contact_sheet(arm, anims, cam, path, height, cols=4, cell=(300, 400)):
    """各アニメのキーのポーズを並べた確認画像(縦 = アニメ、横 = 時間)。"""
    import numpy as np
    scn = bpy.context.scene
    scn.render.resolution_x, scn.render.resolution_y = cell
    Builder.aim(cam, (-4.2, -4.6, height * 0.75), (0, 0, height * 0.48), height * 1.3)
    w, h = cell
    names = list(anims.keys())
    sheet = np.zeros((h * len(names), w * cols, 4), dtype=np.float32)
    tmp = os.path.join(os.path.dirname(path), "_sheet_tmp.png")
    for row, name in enumerate(names):
        arm.animation_data.action = bpy.data.actions[name]
        frames = [k[0] for k in anims[name][1]]
        picks = frames[:-1] if len(frames) - 1 >= cols else frames
        picks = (picks + [frames[-1]] * cols)[:cols]
        for col, f in enumerate(picks):
            scn.frame_set(f)
            scn.render.filepath = tmp
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(tmp)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
            bpy.data.images.remove(img)
            y0 = (len(names) - 1 - row) * h  # 画像は下の行から詰まっている
            sheet[y0:y0 + h, col * w:(col + 1) * w] = px
    os.remove(tmp)
    out = bpy.data.images.new("sheet", w * cols, h * len(names), alpha=True)
    out.pixels = sheet.ravel()
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    arm.animation_data.action = None
    scn.frame_set(0)
    apply_pose(arm, {})
