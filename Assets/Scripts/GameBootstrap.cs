using UnityEngine;

namespace Fight3D
{
    /// <summary>使えるキャラ</summary>
    public enum CharacterId
    {
        Kagetsu,  // カゲツ(仮面の剣士)
        Gouga,    // ゴウガ(岩熊の獣人)
    }

    /// <summary>遊び方(モード)</summary>
    public enum GameMode
    {
        Training,   // トレーニング(サンドバッグ)
        CpuBattle,  // CPU対戦
    }

    /// <summary>
    /// ▶(再生)ボタンを押すと自動で動き、トレーニング用の場面を組み立てる。
    /// シーンに何も置かなくても、ステージ・キャラ・サンドバッグ・カメラ・表示が全部できる。
    /// F5 でキャラを、F6 でモード(トレーニング ⇔ CPU対戦)を切り替えると、場面を作り直す。
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>自分が使うキャラ(最初はカゲツ)。CPU対戦では、CPU はもう一方のキャラになる</summary>
        public static CharacterId Selected = CharacterId.Kagetsu;

        /// <summary>今のモード(最初はトレーニング)</summary>
        public static GameMode Mode = GameMode.Training;

        static GameObject root;  // 作った場面のまとめ役(キャラ切り替えのときにまとめて消す)
        static int lastSwitchFrame = -1;  // 最後に切り替えた画面フレーム(1回の押下で2回切り替わらないように)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        /// <summary>▶ を押してシーンが読み込まれた直後に Unity が自動で呼ぶ</summary>
        static void OnGameStart()
        {
            // すでに組み立て済みなら何もしない
            if (Object.FindAnyObjectByType<FighterController>() != null) return;
            BuildTraining();
        }

        /// <summary>キャラを切り替えて、今のモードの場面を作り直す(F5)</summary>
        public static void SwitchCharacter()
        {
            if (Time.frameCount == lastSwitchFrame) return;
            lastSwitchFrame = Time.frameCount;
            Selected = Other(Selected);
            Rebuild();
        }

        /// <summary>モード(トレーニング ⇔ CPU対戦)を切り替えて、場面を作り直す(F6)</summary>
        public static void SwitchMode()
        {
            if (Time.frameCount == lastSwitchFrame) return;
            lastSwitchFrame = Time.frameCount;
            Mode = Mode == GameMode.Training ? GameMode.CpuBattle : GameMode.Training;
            Rebuild();
        }

        /// <summary>今の場面を消して、今のモードで作り直す</summary>
        static void Rebuild()
        {
            if (root != null) Object.Destroy(root);
            if (Mode == GameMode.CpuBattle) BuildBattle();
            else BuildTraining();
        }

        /// <summary>もう一方のキャラ</summary>
        static CharacterId Other(CharacterId id) => id == CharacterId.Kagetsu ? CharacterId.Gouga : CharacterId.Kagetsu;

        /// <summary>場面のまとめ役・ゲームの速さ・ステージ・ライトを用意する(どのモードでも同じ)</summary>
        static void BeginScene(string name)
        {
            root = new GameObject(name);

            // ゲームの進行を 1秒に60回 にする(仕様書: 60fps 固定)
            Time.fixedDeltaTime = 1f / Tuning.Game.FramesPerSecond;
            Application.targetFrameRate = Tuning.Game.FramesPerSecond;

            BuildStage().SetParent(root.transform, true);
            EnsureLight();
        }

        /// <summary>キャラ(人形 + FighterController)を1人作る</summary>
        static FighterController BuildFighter(CharacterId id, Vector3 position)
        {
            Transform swordPivot;  // 剣(ゴウガは右腕)の支点
            GameObject go = id == CharacterId.Gouga
                ? DollBuilder.BuildGouga(out swordPivot)
                : DollBuilder.BuildKagetsu(out swordPivot);
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            var fighter = go.AddComponent<FighterController>();
            fighter.Kit = id == CharacterId.Gouga ? new GougaSkills() : (CharacterKit)new KagetsuSkills();
            fighter.SwordPivot = swordPivot;
            fighter.StartPosition = position;
            return fighter;
        }

        /// <summary>メインカメラを用意して、自分のキャラの背後カメラ(Cinemachine)を付ける</summary>
        static void SetupCamera(FighterController player)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.04f, 0.10f); // 夜空
            player.CameraRig = CameraRig.Create(cam, player.transform, player.Target);
            player.CameraRig.transform.SetParent(root.transform, true);
            player.CameraRig.Snap();
        }

        /// <summary>CPU対戦の場面(ステージ・自分・CPU・カメラ・試合の進行・表示)を全部作る</summary>
        public static void BuildBattle()
        {
            BeginScene("Battle");

            // 自分と CPU: 円の中心をはさんで 6m 離れて向き合う(仕様書「2.」)
            var player = BuildFighter(Selected, Tuning.Stage.PlayerStart);
            var cpu = BuildFighter(Other(Selected), Tuning.Stage.CpuStart);
            cpu.Cpu = new CpuBrain();
            player.Opponent = cpu;
            cpu.Opponent = player;
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            cpu.transform.rotation = Quaternion.LookRotation(Vector3.back);

            SetupCamera(player);

            // 試合の進行と画面表示
            var managerGo = new GameObject("BattleManager");
            managerGo.transform.SetParent(root.transform, false);
            var battle = managerGo.AddComponent<BattleManager>();
            battle.Player = player;
            battle.Cpu = cpu;
            var hud = managerGo.AddComponent<BattleHUD>();
            hud.Battle = battle;
        }

        /// <summary>トレーニング用の場面(ステージ・キャラ・サンドバッグ・カメラ・表示)を全部作る</summary>
        public static void BuildTraining()
        {
            BeginScene("Training");

            // サンドバッグ: 円の中心から 3m(仕様書「11.」)
            var bagGo = DollBuilder.BuildSandbag(out Transform bagPivot);
            bagGo.transform.SetParent(root.transform, false);
            bagGo.transform.position = Tuning.Stage.SandbagStart;
            var sandbag = bagGo.AddComponent<Sandbag>();
            sandbag.Bag = bagPivot;
            sandbag.StartPosition = bagGo.transform.position;

            // 自分のキャラ: サンドバッグと向き合う位置
            var fighter = BuildFighter(Selected, Tuning.Stage.PlayerStart);
            fighter.Opponent = sandbag;
            fighter.TrainingMode = true;  // R キーでリセットできる
            fighter.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            sandbag.Opponent = fighter;  // 「殴ってくる」設定のときに殴る相手

            // 背後カメラ(Cinemachine)
            SetupCamera(fighter);

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
