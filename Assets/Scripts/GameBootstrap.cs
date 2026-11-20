using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// ▶(再生)ボタンを押すと自動で動き、トレーニング用の場面を組み立てる。
    /// シーンに何も置かなくても、ステージ・カゲツ・サンドバッグ・カメラ・表示が全部できる。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnGameStart()
        {
            // すでに組み立て済みなら何もしない
            if (Object.FindAnyObjectByType<FighterController>() != null) return;
            BuildTraining();
        }

        public static void BuildTraining()
        {
            // ゲームの進行を 1秒に60回 にする(仕様書: 60fps 固定)
            Time.fixedDeltaTime = 1f / 60f;
            Application.targetFrameRate = 60;

            BuildStage();
            EnsureLight();

            // サンドバッグ: 円の中心から 3m(仕様書「11.」)
            var bagGo = DollBuilder.BuildSandbag(out Transform bagPivot);
            bagGo.transform.position = new Vector3(0f, 0f, 3f);
            var sandbag = bagGo.AddComponent<Sandbag>();
            sandbag.Bag = bagPivot;
            sandbag.StartPosition = bagGo.transform.position;

            // カゲツ: サンドバッグと向き合う位置
            var kagetsuGo = DollBuilder.BuildKagetsu(out Transform swordPivot);
            kagetsuGo.transform.position = new Vector3(0f, 0f, -3f);
            var fighter = kagetsuGo.AddComponent<FighterController>();
            fighter.Target = bagGo.transform;
            fighter.TargetSandbag = sandbag;
            fighter.SwordPivot = swordPivot;
            fighter.StartPosition = kagetsuGo.transform.position;
            kagetsuGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);

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
            var follow = cam.gameObject.GetComponent<FollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<FollowCamera>();
            follow.Player = kagetsuGo.transform;
            follow.Target = bagGo.transform;
            follow.SnapNow();

            // 画面表示
            var hud = new GameObject("TrainingHUD").AddComponent<TrainingHUD>();
            hud.Sandbag = sandbag;
            hud.Fighter = fighter;
        }

        static void BuildStage()
        {
            var stage = new GameObject("Stage").transform;

            // 円形の床(Cylinder は直径1なので、横を 20 倍で半径 10m)
            float d = Arena.Radius * 2f;
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
        }

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
