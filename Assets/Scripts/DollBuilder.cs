using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// 仮モデル(人形)を Unity の基本図形(箱・球・カプセル)の組み合わせで作る。
    /// 本番の3Dモデルができたら、ここを差し替える。
    /// </summary>
    public static class DollBuilder
    {
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

        public static void Paint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
        }

        /// <summary>
        /// カゲツの人形。黒っぽい体に白い仮面、右手に剣。
        /// 戻り値の swordPivot を回すと剣が振られる。
        /// </summary>
        public static GameObject BuildKagetsu(out Transform swordPivot)
        {
            var root = new GameObject("Kagetsu");
            var t = root.transform;

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
        /// サンドバッグ。台の上に袋が立っている。
        /// 戻り値の bag を揺らすと殴られたように見える。
        /// </summary>
        public static GameObject BuildSandbag(out Transform bag)
        {
            var root = new GameObject("Sandbag");
            var t = root.transform;

            Color baseColor = new Color(0.25f, 0.25f, 0.28f);
            Color bagColor = new Color(0.72f, 0.60f, 0.42f);

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
