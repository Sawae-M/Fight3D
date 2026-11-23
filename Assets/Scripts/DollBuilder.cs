using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// キャラの見た目を作る。
    /// - 本番の3Dモデル(Blender で作った FBX。Assets/Models/Characters/Resources/Fighters)があれば、それを使う(TryBuildModel)
    /// - 無いとき・読み込めないときは、仮モデル(人形)を Unity の基本図形(箱・球・カプセル)の組み合わせで作る
    /// </summary>
    public static class DollBuilder
    {
        /// <summary>本番モデルを置く Resources の中のフォルダ</summary>
        const string ModelFolder = "Fighters/";

        /// <summary>カゲツの T ポーズでの、刃の真ん中より少し先の位置(モデルの足元から見た m)。Blender の build_kagetsu.py の剣の位置から計算 [仮]</summary>
        static readonly Vector3 KagetsuBladePoint = new Vector3(0.765f, 1.515f, 0.7f);

        /// <summary>
        /// 本番の3Dモデルでキャラの見た目を作る。modelName は "Kagetsu" や "Gouga"(FBX のファイル名)。
        /// 見つからない・人型(Humanoid)になっていない・アニメが無いときは null を返す(呼ぶ側で人形に切り替える)。
        /// 作りは人形と同じで、ルート → Visual → モデル。swordPivot は人形用の剣の支点の代わりの空の物
        /// (本番モデルの剣・腕はアニメで動くので、FighterController が回しても見た目は変わらない)。
        /// </summary>
        public static GameObject TryBuildModel(string modelName, out Transform swordPivot, out Animator animator, out AnimationClip[] clips)
        {
            swordPivot = null;
            animator = null;
            clips = null;
            var prefab = Resources.Load<GameObject>(ModelFolder + modelName);
            if (prefab == null) return null;
            var loadedClips = Resources.LoadAll<AnimationClip>(ModelFolder + modelName);
            if (loadedClips == null || loadedClips.Length == 0) return null;

            var root = new GameObject(modelName);
            var visual = new GameObject("Visual").transform;  // 浮きで持ち上げる・人形と同じ作りにするためのまとめ役
            visual.SetParent(root.transform, false);
            var model = Object.Instantiate(prefab, visual, false);
            model.name = "Model";
            var anim = model.GetComponent<Animator>();
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
            {
                Object.Destroy(root);  // 人型の設定になっていないモデルはアニメを流せないので使わない
                return null;
            }
            anim.runtimeAnimatorController = null;  // アニメは CharacterAnimator が直接流す

            // カゲツは剣の軌跡を付ける(モデルは作った直後は T ポーズなので、その姿勢で刃の位置を決める)
            if (modelName == "Kagetsu")
                HitFx.AddWeaponTrail(anim.GetBoneTransform(HumanBodyBones.RightHand), model.transform.TransformPoint(KagetsuBladePoint));

            var pivot = new GameObject("SwordPivot").transform;  // 人形用の剣の支点の代わり(空)
            pivot.SetParent(visual, false);
            swordPivot = pivot;
            animator = anim;
            clips = loadedClips;
            return root;
        }
        /// <summary>基本図形を作り、色をつけ、当たり判定(コライダー)を外す</summary>
        public static GameObject Part(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 攻撃の当たりは距離で計算するのでコライダーは不要
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Paint(go, color);
            return go;
        }

        // 基本図形の既定マテリアルはビルドに URP/Lit が含まれずマゼンタになるため、
        // Resources/DollLit.mat(URP/Lit)を元にして色を付ける
        static Material baseMat;       // 読み込んだ DollLit マテリアル(無ければ null)
        static bool baseMatLoaded;     // もう読み込みを試したか(何度も読み込まないため)

        /// <summary>色を付ける元になるマテリアル(最初に使うときに1回だけ読み込む)</summary>
        static Material BaseMat
        {
            get
            {
                if (!baseMatLoaded)
                {
                    baseMat = Resources.Load<Material>("DollLit");
                    baseMatLoaded = true;
                }
                return baseMat;
            }
        }

        /// <summary>図形に色を付ける(DollLit を複製して色を変える)</summary>
        public static void Paint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (BaseMat != null) r.material = new Material(BaseMat) { color = color };
            else r.material.color = color; // 見つからないときは今まで通り
        }

        /// <summary>
        /// カゲツの人形。黒っぽい体に白い仮面、右手に剣。
        /// 戻り値の swordPivot を回すと剣が振られる。
        /// </summary>
        public static GameObject BuildKagetsu(out Transform swordPivot)
        {
            var root = new GameObject("Kagetsu");
            // 見た目の部品は「Visual」の下にまとめる(呼吸の上下などで Visual だけ動かすため)
            var t = new GameObject("Visual").transform;
            t.SetParent(root.transform, false);

            Color body = new Color(0.12f, 0.10f, 0.20f);   // 濃い紫紺
            Color cloth = new Color(0.30f, 0.08f, 0.12f);  // 暗い赤
            Color mask = new Color(0.92f, 0.90f, 0.85f);   // 骨のような白
            Color blade = new Color(0.75f, 0.80f, 0.90f);  // 刃

            Part(PrimitiveType.Capsule, "LegL", t, new Vector3(-0.14f, 0.38f, 0f), new Vector3(0.2f, 0.38f, 0.2f), body);
            Part(PrimitiveType.Capsule, "LegR", t, new Vector3(0.14f, 0.38f, 0f), new Vector3(0.2f, 0.38f, 0.2f), body);
            Part(PrimitiveType.Capsule, "Body", t, new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.45f, 0.38f), body);
            Part(PrimitiveType.Cube, "Sash", t, new Vector3(0f, 0.95f, 0f), new Vector3(0.58f, 0.12f, 0.42f), cloth);
            Part(PrimitiveType.Sphere, "Head", t, new Vector3(0f, 1.82f, 0f), new Vector3(0.36f, 0.38f, 0.36f), body);
            // 仮面(顔の前に付ける)。前 = +Z 方向
            Part(PrimitiveType.Cube, "Mask", t, new Vector3(0f, 1.83f, 0.17f), new Vector3(0.28f, 0.32f, 0.05f), mask);
            Part(PrimitiveType.Cube, "MaskEyeL", t, new Vector3(-0.065f, 1.87f, 0.2f), new Vector3(0.07f, 0.03f, 0.02f), Color.black);
            Part(PrimitiveType.Cube, "MaskEyeR", t, new Vector3(0.065f, 1.87f, 0.2f), new Vector3(0.07f, 0.03f, 0.02f), Color.black);
            Part(PrimitiveType.Capsule, "ArmL", t, new Vector3(-0.38f, 1.2f, 0.05f), new Vector3(0.14f, 0.3f, 0.14f), body);

            // 剣: 右肩あたりを支点にして回す
            var pivot = new GameObject("SwordPivot").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(0.38f, 1.3f, 0.05f);
            Part(PrimitiveType.Capsule, "ArmR", pivot, new Vector3(0f, 0f, 0.2f), new Vector3(0.14f, 0.25f, 0.14f), body)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cube, "Hilt", pivot, new Vector3(0f, 0f, 0.45f), new Vector3(0.18f, 0.05f, 0.05f), cloth);
            Part(PrimitiveType.Cube, "Blade", pivot, new Vector3(0f, 0f, 1.05f), new Vector3(0.04f, 0.09f, 1.1f), blade);

            swordPivot = pivot;
            return root;
        }

        /// <summary>
        /// ゴウガの人形。大きな岩熊の獣人(茶色の体・丸い耳・鼻先)、右手に大きな拳。
        /// 戻り値の armPivot を回すと右腕が振られる。
        /// </summary>
        public static GameObject BuildGouga(out Transform armPivot)
        {
            var root = new GameObject("Gouga");
            // 見た目の部品は「Visual」の下にまとめる(呼吸の上下などで Visual だけ動かすため)
            var t = new GameObject("Visual").transform;
            t.SetParent(root.transform, false);

            Color fur = new Color(0.36f, 0.24f, 0.15f);     // こげ茶の毛
            Color belly = new Color(0.55f, 0.42f, 0.30f);   // お腹と鼻先の明るい茶色
            Color rock = new Color(0.42f, 0.42f, 0.45f);    // 岩のような籠手(こて)と肩当て
            Color dark = new Color(0.08f, 0.06f, 0.05f);    // 目と鼻

            Part(PrimitiveType.Capsule, "LegL", t, new Vector3(-0.2f, 0.38f, 0f), new Vector3(0.3f, 0.38f, 0.3f), fur);
            Part(PrimitiveType.Capsule, "LegR", t, new Vector3(0.2f, 0.38f, 0f), new Vector3(0.3f, 0.38f, 0.3f), fur);
            Part(PrimitiveType.Capsule, "Body", t, new Vector3(0f, 1.2f, 0f), new Vector3(0.8f, 0.55f, 0.6f), fur);
            Part(PrimitiveType.Sphere, "Belly", t, new Vector3(0f, 1.1f, 0.18f), new Vector3(0.55f, 0.6f, 0.35f), belly);
            Part(PrimitiveType.Sphere, "Head", t, new Vector3(0f, 1.95f, 0.02f), new Vector3(0.46f, 0.44f, 0.44f), fur);
            Part(PrimitiveType.Sphere, "EarL", t, new Vector3(-0.17f, 2.15f, 0f), new Vector3(0.14f, 0.14f, 0.08f), fur);
            Part(PrimitiveType.Sphere, "EarR", t, new Vector3(0.17f, 2.15f, 0f), new Vector3(0.14f, 0.14f, 0.08f), fur);
            // 鼻先(顔の前)。前 = +Z 方向
            Part(PrimitiveType.Sphere, "Muzzle", t, new Vector3(0f, 1.9f, 0.2f), new Vector3(0.2f, 0.15f, 0.18f), belly);
            Part(PrimitiveType.Sphere, "Nose", t, new Vector3(0f, 1.93f, 0.29f), new Vector3(0.07f, 0.05f, 0.05f), dark);
            Part(PrimitiveType.Sphere, "EyeL", t, new Vector3(-0.09f, 2.0f, 0.2f), new Vector3(0.05f, 0.05f, 0.03f), dark);
            Part(PrimitiveType.Sphere, "EyeR", t, new Vector3(0.09f, 2.0f, 0.2f), new Vector3(0.05f, 0.05f, 0.03f), dark);
            Part(PrimitiveType.Cube, "ShoulderPlate", t, new Vector3(-0.45f, 1.62f, 0f), new Vector3(0.3f, 0.12f, 0.3f), rock);
            Part(PrimitiveType.Capsule, "ArmL", t, new Vector3(-0.52f, 1.25f, 0.08f), new Vector3(0.22f, 0.35f, 0.22f), fur);
            Part(PrimitiveType.Sphere, "FistL", t, new Vector3(-0.52f, 0.88f, 0.12f), new Vector3(0.26f, 0.26f, 0.26f), rock);

            // 右腕: 右肩を支点にして回す。腕は前(+Z)に伸びている
            var pivot = new GameObject("ArmPivot").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(0.5f, 1.45f, 0.1f);
            Part(PrimitiveType.Capsule, "ArmR", pivot, new Vector3(0f, 0f, 0.28f), new Vector3(0.22f, 0.3f, 0.22f), fur)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Sphere, "FistR", pivot, new Vector3(0f, 0f, 0.65f), new Vector3(0.34f, 0.34f, 0.34f), rock);

            armPivot = pivot;
            return root;
        }

        /// <summary>
        /// サンドバッグ。台の上に袋が立っている。
        /// 戻り値の bag を揺らすと殴られたように見える。
        /// </summary>
        public static GameObject BuildSandbag(out Transform bag)
        {
            var root = new GameObject("Sandbag");
            var t = root.transform;

            Color baseColor = new Color(0.25f, 0.25f, 0.28f);  // 台と支柱(暗い灰色)
            Color bagColor = new Color(0.72f, 0.60f, 0.42f);   // 袋(砂色)

            Part(PrimitiveType.Cylinder, "Base", t, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.05f, 0.9f), baseColor);

            // 根元を支点にして揺れるようにする
            var pivot = new GameObject("BagPivot").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(0f, 0.1f, 0f);
            Part(PrimitiveType.Cylinder, "Pole", pivot, new Vector3(0f, 0.25f, 0f), new Vector3(0.12f, 0.25f, 0.12f), baseColor);
            Part(PrimitiveType.Capsule, "Bag", pivot, new Vector3(0f, 1.15f, 0f), new Vector3(0.7f, 0.65f, 0.7f), bagColor);
            Part(PrimitiveType.Cube, "Band", pivot, new Vector3(0f, 1.15f, 0f), new Vector3(0.72f, 0.08f, 0.72f), new Color(0.45f, 0.30f, 0.20f));

            bag = pivot;
            return root;
        }
    }
}
