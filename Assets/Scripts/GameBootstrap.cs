using UnityEngine;

namespace Fight3D
{
    /// <summary>使えるキャラ</summary>
    public enum CharacterId
    {
        Kagetsu,  // カゲツ(仮面の剣士)
        Gouga,    // ゴウガ(岩熊の獣人)
    }

    /// <summary>
    /// ▶(再生)ボタンを押すと自動で動き、トレーニング用の場面を組み立てる。
    /// シーンに何も置かなくても、ステージ・キャラ・サンドバッグ・カメラ・表示が全部できる。
    /// F5 でキャラを切り替えると、場面を作り直す。
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>トレーニングで使うキャラ(最初はカゲツ)</summary>
        public static CharacterId Selected = CharacterId.Kagetsu;

        static GameObject root;  // 作った場面のまとめ役(キャラ切り替えのときにまとめて消す)
        static int lastSwitchFrame = -1;  // 最後にキャラを切り替えた画面フレーム(1回の押下で2回切り替わらないように)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        /// <summary>▶ を押してシーンが読み込まれた直後に Unity が自動で呼ぶ</summary>
        static void OnGameStart()
        {
            // すでに組み立て済みなら何もしない
            if (Object.FindAnyObjectByType<FighterController>() != null) return;
            BuildTraining();
        }

        /// <summary>キャラを切り替えて、トレーニングの場面を作り直す(F5)</summary>
        public static void SwitchCharacter()
        {
            if (Time.frameCount == lastSwitchFrame) return;
            lastSwitchFrame = Time.frameCount;
            Selected = Selected == CharacterId.Kagetsu ? CharacterId.Gouga : CharacterId.Kagetsu;
            if (root != null) Object.Destroy(root);
            BuildTraining();
        }

        /// <summary>トレーニング用の場面(ステージ・キャラ・サンドバッグ・カメラ・表示)を全部作る</summary>
        public static void BuildTraining()
        {
            root = new GameObject("Training");

            // ゲームの進行を 1秒に60回 にする(仕様書: 60fps 固定)
            Time.fixedDeltaTime = 1f / Tuning.Game.FramesPerSecond;
            Application.targetFrameRate = Tuning.Game.FramesPerSecond;

            BuildStage().SetParent(root.transform, true);
            EnsureLight();

            // サンドバッグ: 円の中心から 3m(仕様書「11.」)
            var bagGo = DollBuilder.BuildSandbag(out Transform bagPivot);
            bagGo.transform.SetParent(root.transform, false);
            bagGo.transform.position = Tuning.Stage.SandbagStart;
            var sandbag = bagGo.AddComponent<Sandbag>();
            sandbag.Bag = bagPivot;
            sandbag.StartPosition = bagGo.transform.position;

            // 自分のキャラ: サンドバッグと向き合う位置
            Transform swordPivot;  // 剣(ゴウガは右腕)の支点
            GameObject playerGo = Selected == CharacterId.Gouga
                ? DollBuilder.BuildGouga(out swordPivot)
                : DollBuilder.BuildKagetsu(out swordPivot);
            playerGo.transform.SetParent(root.transform, false);
            playerGo.transform.position = Tuning.Stage.PlayerStart;
            var fighter = playerGo.AddComponent<FighterController>();
            fighter.Kit = Selected == CharacterId.Gouga ? new GougaSkills() : (CharacterKit)new KagetsuSkills();
            fighter.Target = bagGo.transform;
            fighter.TargetSandbag = sandbag;
            fighter.SwordPivot = swordPivot;
            fighter.StartPosition = playerGo.transform.position;
            playerGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            sandbag.Opponent = fighter;  // 「殴ってくる」設定のときに殴る相手

            // カメラ
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.04f, 0.10f); // 夜空
            // 背後カメラ(Cinemachine)
            fighter.CameraRig = CameraRig.Create(cam, playerGo.transform, bagGo.transform);
            fighter.CameraRig.transform.SetParent(root.transform, true);
            fighter.CameraRig.Snap();

            // 画面表示
            var hud = new GameObject("TrainingHUD").AddComponent<TrainingHUD>();
            hud.transform.SetParent(root.transform, false);
            hud.Sandbag = sandbag;
            hud.Fighter = fighter;
        }

        /// <summary>円形のステージと空の飾りを作る(戻り値はステージのまとめ役)</summary>
        static Transform BuildStage()
        {
            var stage = new GameObject("Stage").transform;

            // 円形の床(Cylinder は直径1なので、横を 20 倍で半径 10m)
            float d = Tuning.Stage.Radius * 2f;
            DollBuilder.Part(PrimitiveType.Cylinder, "Edge", stage, new Vector3(0f, -0.12f, 0f),
                new Vector3(d + 0.6f, 0.1f, d + 0.6f), new Color(0.35f, 0.12f, 0.15f));
            DollBuilder.Part(PrimitiveType.Cylinder, "Floor", stage, new Vector3(0f, -0.05f, 0f),
                new Vector3(d, 0.05f, d), new Color(0.32f, 0.31f, 0.36f));
            // 中央の模様(月蝕っぽい丸)
            DollBuilder.Part(PrimitiveType.Cylinder, "Emblem", stage, new Vector3(0f, 0.002f, 0f),
                new Vector3(3f, 0.002f, 3f), new Color(0.22f, 0.18f, 0.28f));

            // 空の赤い月(飾り)
            DollBuilder.Part(PrimitiveType.Sphere, "EclipseMoon", stage, new Vector3(-25f, 30f, 70f),
                new Vector3(14f, 14f, 14f), new Color(0.6f, 0.15f, 0.12f));
            return stage;
        }

        /// <summary>シーンにライトが無ければ作る</summary>
        static void EnsureLight()
        {
            if (Object.FindAnyObjectByType<Light>() != null) return;
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
