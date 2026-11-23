using LitMotion;
using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// 攻撃の演出(見た目だけ)。プレイの邪魔にならないよう、小さく・短くしている。
    /// - Spark: 攻撃が当たった / ガードされたときの小さな光と火花
    /// - Dust: ゴウガの衝撃波が地面を走るときの土ぼこり
    /// - AddWeaponTrail: カゲツの剣の軌跡(技を出している間だけ CharacterAnimator が出す)
    /// 光る物は Resources/FX_Additive(Fight3D/Effect シェーダー、加算)を使う。見つからないときは何も出さない。
    /// 見た目だけの数値なので、Tuning.cs ではなくこのファイルの上にまとめている。すべて [仮]。
    /// </summary>
    public static class HitFx
    {
        // ---- 色 ----
        public static readonly Color HitColor = new Color(1f, 0.75f, 0.35f, 0.9f);     // 当たった: 暖かい黄色
        public static readonly Color GuardColor = new Color(0.45f, 0.75f, 1f, 0.8f);   // ガードされた: 青
        public static readonly Color CounterColor = new Color(1f, 1f, 1f, 0.9f);       // 受け止められた: 白
        static readonly Color TrailColor = new Color(0.75f, 0.8f, 1f, 0.45f);          // 剣の軌跡: 薄い青白
        static readonly Color DustColor = new Color(0.55f, 0.45f, 0.35f);              // 土ぼこり: 土の色

        // ---- 大きさ・時間 ----
        const float FlashSize = 0.32f;      // 光の玉の一番大きいときの直径(m)
        const float FlashTime = 0.09f;      // 光の玉が消えるまで(秒)
        const int SparkCount = 6;           // 火花の数
        const float SparkLength = 0.12f;    // 火花1本の長さ(m)
        const float SparkDistance = 0.35f;  // 火花が飛ぶ距離(m)
        const float SparkTime = 0.16f;      // 火花が消えるまで(秒)
        const float DustSize = 0.35f;       // 土ぼこりの一番大きいときの直径(m)
        const float DustTime = 0.35f;       // 土ぼこりが消えるまで(秒)
        const float TrailTime = 0.1f;       // 剣の軌跡が残る時間(秒)
        const float TrailWidth = 0.14f;     // 剣の軌跡の太さ(m)

        static Material fxMat;       // 加算のマテリアル(最初に使うときに1回だけ読み込む)
        static bool fxMatLoaded;

        /// <summary>光る演出用のマテリアル(無ければ null)</summary>
        static Material FxMat
        {
            get
            {
                if (!fxMatLoaded)
                {
                    fxMat = Resources.Load<Material>("FX_Additive");
                    fxMatLoaded = true;
                }
                return fxMat;
            }
        }

        /// <summary>当たった場所に、小さな光の玉と、外へ飛び散る火花を出す。scale で大きさを変えられる</summary>
        public static void Spark(Vector3 position, Color color, float scale = 1f)
        {
            if (FxMat == null) return;
            var root = new GameObject("HitSpark");
            root.transform.position = position;
            var mat = new Material(FxMat) { color = color };  // この演出だけの色(薄くしていくため)

            // 光の玉: ぱっと広がって消える
            var flash = MakePart(PrimitiveType.Sphere, root.transform, mat);
            LMotion.Create(0f, 1f, FlashTime)
                .WithEase(Ease.OutQuad)
                .Bind(flash.transform, (k, t) => t.localScale = Vector3.one * (FlashSize * scale * (0.3f + 0.7f * k)))
                .AddTo(root);

            // 火花: 細い棒が四方へ飛び、縮んで消える
            var sparks = new Transform[SparkCount];
            var dirs = new Vector3[SparkCount];
            for (int i = 0; i < SparkCount; i++)
            {
                dirs[i] = Random.onUnitSphere;
                dirs[i].y = Mathf.Abs(dirs[i].y) * 0.6f + 0.1f;  // 少し上向きに飛ばす
                dirs[i].Normalize();
                sparks[i] = MakePart(PrimitiveType.Cube, root.transform, mat).transform;
                sparks[i].rotation = Quaternion.LookRotation(dirs[i]);
            }
            LMotion.Create(0f, 1f, SparkTime)
                .WithEase(Ease.OutCubic)
                .WithOnComplete(() => { Object.Destroy(root); Object.Destroy(mat); })
                .Bind(root, (k, r) =>
                {
                    if (r == null) return;
                    float len = SparkLength * scale * (1f - k);
                    for (int i = 0; i < sparks.Length; i++)
                    {
                        sparks[i].localPosition = dirs[i] * (SparkDistance * scale * k);
                        sparks[i].localScale = new Vector3(0.015f * scale, 0.015f * scale, Mathf.Max(0.001f, len));
                    }
                    mat.color = new Color(color.r, color.g, color.b, color.a * (1f - k));  // だんだん薄く
                })
                .AddTo(root);
        }

        /// <summary>地面に小さな土ぼこりを出す(ゴウガの衝撃波の通り道)。少し浮き上がりながら小さくなって消える</summary>
        public static void Dust(Vector3 position)
        {
            var puff = DollBuilder.Part(PrimitiveType.Sphere, "Dust", null, position, Vector3.one * 0.05f, DustColor);
            float tilt = Random.Range(0.8f, 1.2f);  // 大きさを少しばらつかせる
            LMotion.Create(0f, 1f, DustTime)
                .WithEase(Ease.OutQuad)
                .WithOnComplete(() => Object.Destroy(puff))
                .Bind(puff.transform, (k, t) =>
                {
                    if (t == null) return;
                    float size = DustSize * tilt * Mathf.Sin(k * Mathf.PI);  // ふくらんでからしぼむ
                    t.localScale = new Vector3(size, size * 0.6f, size);
                    t.position = position + Vector3.up * (0.25f * k);
                })
                .AddTo(puff);
        }

        /// <summary>
        /// 剣の軌跡を付ける。hand(手の骨)の子に、localTipPoint(手から見た刃の位置)で TrailRenderer を置く。
        /// 最初は出さない(emitting = false)。技を出している間だけ CharacterAnimator が出す。
        /// </summary>
        public static TrailRenderer AddWeaponTrail(Transform hand, Vector3 worldPoint)
        {
            if (FxMat == null || hand == null) return null;
            var go = new GameObject("WeaponTrail");
            go.transform.position = worldPoint;
            go.transform.SetParent(hand, true);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = FxMat;
            trail.time = TrailTime;
            trail.minVertexDistance = 0.02f;
            trail.widthCurve = AnimationCurve.Linear(0f, TrailWidth, 1f, 0f);  // 先は太く、後ろは細く
            trail.startColor = TrailColor;
            trail.endColor = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return trail;
        }

        /// <summary>演出の部品を作る(当たり判定は外し、影も落とさない)</summary>
        static GameObject MakePart(PrimitiveType type, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.zero;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
