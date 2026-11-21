using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// ゲームの調整用の数値をすべてここに集めたファイル(規則7「調整数値は1か所に集める」)。
    /// 遊んでみて変えたい数値は、まずここを探す。
    ///
    /// コメントの見方:
    /// - [仕様書] … Notion の仕様書に書いてある値(変えるときは仕様書にも追記する)
    /// - [仮]     … 仕様書に無いので仮で決めた値
    /// 時間の単位 F(フレーム)は 1/60 秒(60F = 1秒)。
    /// 見た目だけの数値(色・剣の振り方・揺れの大きさなど)は、それぞれのファイルに置いている。
    /// </summary>
    public static class Tuning
    {
        /// <summary>ゲーム全体</summary>
        public static class Game
        {
            public const int FramesPerSecond = 60;  // [仕様書] 60fps 固定
        }

        /// <summary>ステージ(仕様書「3. ステージ」)</summary>
        public static class Stage
        {
            public const float Radius = 10f;  // [仕様書] 円形の床の半径(m)
            public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -3f);  // [仮] トレーニングでのカゲツの開始位置
            public static readonly Vector3 SandbagStart = new Vector3(0f, 0f, 3f);  // [仕様書] サンドバッグは円の中心から 3m
        }

        /// <summary>カメラ(仕様書「4. カメラ」)</summary>
        public static class Camera
        {
            public const float Distance = 4f;          // [仕様書] キャラの後方 4m
            public const float Height = 2.5f;          // [仕様書] 高さ 2.5m
            public const float LookHeight = 1.0f;      // [仮] 見る点の高さ(m)
            public const float LookTowardTarget = 0.7f;// [仮] 見る点を自分と相手の間のどこにするか(0 = 自分、1 = 相手)
            public const float FieldOfView = 50f;      // [仮] 視野角(度)
            public const float PositionDamping = 0.15f;// [仮] 追いかけるときのなめらかさ(大きいほどゆっくり)
            public const float RotationDamping = 0.2f; // [仮] 向きを変えるときのなめらかさ
        }

        /// <summary>キャラ共通の動き(仕様書「5. 操作と入力」「6. 共通の動き」「7. ウルトゲージ」)</summary>
        public static class Fighter
        {
            public const float BodyRadius = 0.35f;     // [仮] 体の太さ(半径 m)。壁やめり込みの判定に使う
            public const float ForwardSpeed = 4.0f;    // [仕様書] 前歩き(m/秒)
            public const float OtherSpeed = 3.0f;      // [仕様書] 後ろ・横歩き(m/秒)
            public const float StickDeadZone = 0.2f;   // [仮] スティックをこれ以上倒したら動く(0〜1)
            public const float SkillDirectionThreshold = 0.5f; // [仮] W+P / S+P とみなすスティックの倒し具合
            public const int BufferFrames = 8;         // [仕様書] 先行入力: 技の終わり際 8F 以内のボタンは受け付ける
            public const int HitstopFrames = 4;        // [仮] 当たった瞬間に少し止まる時間(F)
            public const int RushHitstopFrames = 3;    // [仮] ウルトの連続斬り1回ごとのヒットストップ(F)
            public const int UltSimultaneousFrames = 3;// [仕様書] O+P を同時押しとみなす猶予(F)
            public const int UltEarlyConvertFrames = 3;// [仮] 強・スキルの出始めこのF以内なら、O+P でウルトに切り替える
            public const float ContactGap = 0.05f;     // [仮] 踏み込みが止まる、相手との隙間(m)

            // ウルトゲージ(仕様書「7.」)
            public const float MaxGauge = 100f;        // [仕様書] 最大 100
            public const float GaugeOnHitRate = 0.10f; // [仕様書] 当てたダメージの 10% たまる
            public const float GaugeOnGuarded = 3f;    // [仕様書] ガードされたら 3 たまる
            public const bool UltGainsGauge = false;   // [仕様書] ウルトの攻撃ではゲージがたまらない(2026-11-21 に追加)

            // 回避ステップ(仕様書「6.」)
            public const float DodgeDistance = 2.5f;   // [仕様書] 移動距離(m)
            public const int DodgeFrames = 24;         // [仕様書] 全体の時間(F)
            public const int DodgeInvulnStart = 4;     // [仕様書] 無敵の始まり(4F目)
            public const int DodgeInvulnEnd = 12;      // [仕様書] 無敵の終わり(12F目)
            public const int DodgeCooldown = 20;       // [仕様書] 終わってから 20F は使えない
        }

        /// <summary>カゲツのスキル(仕様書「9. カゲツ」)</summary>
        public static class Kagetsu
        {
            // パッシブ「連刃」
            public const int RenjinBonusPerHit = 3;    // [仕様書] 2ヒット目から1ヒットごとに +3%
            public const int RenjinMaxBonus = 24;      // [仕様書] 最大 +24%

            // スキル1「影穿ち」
            public const int KageChainWindow = 40;     // [仕様書] 前の突きから 40F 以内なら連続で出せる
            public const int KageMaxChain = 3;         // [仕様書] 3回まで

            // スキル2「三日月払い」
            public const int MikazukiCooldown = 180;   // [仕様書] 使用後 3秒 使えない

            // スキル3「仮面解放」
            public const int KamenDuration = 240;      // [仕様書] 効果 4秒
            public const int KamenCooldown = 900;      // [仕様書] 使用後 15秒 使えない
            public const float KamenSpeedScale = 1.3f; // [仕様書] 移動速度 1.3倍
            public const float KamenStartupScale = 0.8f; // [仕様書] 技の出るまでの時間 0.8倍
            public const int MaxCurseMarks = 8;        // [仕様書] 呪印は最大 8
            public const int CurseDamagePerMark = 15;  // [仕様書] 呪印1つにつき 15 ダメージ
            public const int CurseBurstHitstun = 12;   // [仮] 呪印の爆発ののけぞり(F)
            public const int CurseBurstHitstop = 6;    // [仮] 呪印の爆発のヒットストップ(F)
        }

        /// <summary>サンドバッグとやられ状態(仕様書「6. やられ状態」「11. サンドバッグ」)</summary>
        public static class Sandbag
        {
            public const float BodyRadius = 0.4f;      // [仮] 体の太さ(半径 m)
            public const int MaxHp = 1000;             // [仮] 最大体力
            public const int RecoverFrames = 120;      // [仕様書] 2秒攻撃しないと全回復
            public const int ComboWindow = 40;         // [仕様書] 前のヒットから 40F 以内なら連続技が続く
            public const int StaggerFrames = 40;       // [仕様書] よろけ: 40F 動けない
            public const int AirFrames = 36;           // [仮] 浮いている時間(F)
            public const int LandingFrames = 30;       // [仕様書] 着地後 30F 動けない
            public const float JuggleScale = 0.7f;     // [仕様書] 浮いている相手への追い打ちは 0.7倍
            public const int GuardCrushFrames = 50;    // [仕様書] ガード崩し: 50F 動けない(2026-11-21 に 30F から変更。崩した後に追撃できるように)
            public const float BlockstunScale = 0.6f;  // [仮] ガードしたときの硬直 = のけぞり時間 × 0.6
            public const int MinBlockstun = 6;         // [仮] ガード硬直の最短(F)
        }
    }

    /// <summary>
    /// カゲツの技一覧(仕様書「8. 通常攻撃」「9. カゲツ」)。技のダメージやフレーム数はここで調整する。
    /// 仕様書に無い値: のけぞり時間(Hitstun)・当たっている時間(Active)・スキルの終わりの隙・剣の振り方 は [仮]。
    /// </summary>
    public static class KagetsuMoves
    {
        // ---- 剣の振り方(見た目だけ) ----
        static readonly SwordPose ThrustPose = new SwordPose(   // 突き
            new Vector3(10f, 5f, 0f), new Vector3(5f, 0f, 0f),
            new Vector3(0f, 0f, -0.25f), new Vector3(-0.15f, 0f, 0.45f));
        static readonly SwordPose SlashPose = new SwordPose(new Vector3(10f, 85f, 0f), new Vector3(10f, -75f, 0f));  // 横なぎ(右から左へ)
        static readonly SwordPose OverheadPose = new SwordPose(new Vector3(-110f, 0f, 0f), new Vector3(55f, 0f, 0f));  // 振り下ろし(頭上から前へ)

        // ---- 通常攻撃 [仕様書] ダメージ・出るまで・当たっている時間・終わりの隙・リーチ / [仮] のけぞり ----
        /// <summary>弱(U)</summary>
        public static readonly MoveData Light = new MoveData
        { Name = "弱", Damage = 30, Startup = 5, Active = 3, Recovery = 10, Reach = 1.6f, Hitstun = 16, ComboRank = 0, Pose = ThrustPose };

        /// <summary>中(I)</summary>
        public static readonly MoveData Medium = new MoveData
        { Name = "中", Damage = 60, Startup = 9, Active = 4, Recovery = 18, Reach = 2.0f, Hitstun = 22, ComboRank = 1, Pose = SlashPose };

        /// <summary>強(O)</summary>
        public static readonly MoveData Heavy = new MoveData
        { Name = "強", Damage = 100, Startup = 15, Active = 5, Recovery = 28, Reach = 2.4f, Hitstun = 28, ComboRank = 2, Pose = OverheadPose };

        // ---- スキル1「影穿ち」(P) ----
        // [仕様書] ダメージ 50(3回目 70)、出るまで 8F、リーチ 3.5m、3回目は 1.5m 踏み込み + よろけ
        // [仮] 当たっている時間 3F、終わりの隙 16F(3回目 22F)、3回目の出るまで 10F、当たる角度 30度
        /// <summary>影穿ち 1・2回目</summary>
        public static readonly MoveData KageUgachi = new MoveData
        { Name = "影穿ち", Damage = 50, Startup = 8, Active = 3, Recovery = 16, Reach = 3.5f, Arc = 30f, Hitstun = 20, Pose = ThrustPose };

        /// <summary>影穿ち 3回目(踏み込み + よろけ)</summary>
        public static readonly MoveData KageUgachi3 = new MoveData
        {
            Name = "影穿ち(3)", Damage = 70, Startup = 10, Active = 3, Recovery = 22, Reach = 3.5f, Arc = 30f, Hitstun = 40,
            Effect = HitEffect.Stagger, LungeDistance = 1.5f, LungeStart = 3, LungeEnd = 10, Pose = ThrustPose
        };

        // ---- スキル2「三日月払い」(W+P) ----
        // [仕様書] 前方 120度・半径 2.8m、ダメージ 90、出るまで 18F、終わりの隙 26F、ガード崩し / [仮] 当たっている時間 4F
        /// <summary>三日月払い</summary>
        public static readonly MoveData MikazukiBarai = new MoveData
        {
            Name = "三日月払い", Damage = 90, Startup = 18, Active = 4, Recovery = 26, Reach = 2.8f - Tuning.Fighter.BodyRadius,
            Arc = 120f, Hitstun = 26, GuardBreak = true,
            Pose = new SwordPose(new Vector3(0f, 110f, 0f), new Vector3(0f, -110f, 0f))
        };

        // ---- スキル3「仮面解放」(S+P) ----
        // [仮] 発動の構え 20F(攻撃判定なし)。効果の数値は Tuning.Kagetsu
        /// <summary>仮面解放の発動の構え</summary>
        public static readonly MoveData KamenKaihou = new MoveData
        {
            Name = "仮面解放", Damage = 0, Startup = 0, Active = 0, Recovery = 20, Reach = 0f,
            Pose = new SwordPose(new Vector3(-150f, 0f, 0f), new Vector3(-150f, 0f, 0f), new Vector3(-0.2f, 0.4f, 0f), new Vector3(-0.2f, 0.4f, 0f))
        };

        // ---- ウルト「冥月斬」(O+P) ----
        // [仕様書] 5m 踏み込み、出るまで 10F、合計 300 ダメージ、最後に浮き、外すと隙 60F
        // [仮] 最初の一撃 40 + 連斬 40×5 + 締め 60 の配分、連斬のタイミング、当たったときの隙 24F
        /// <summary>冥月斬(踏み込み斬り)</summary>
        public static readonly MoveData MeigetsuZan = new MoveData
        {
            Name = "冥月斬", Damage = 40, Startup = 10, Active = 4, Recovery = 10, WhiffRecovery = 60, Reach = 1.4f, Hitstun = 60,
            LungeDistance = 5f, LungeStart = 2, LungeEnd = 9,
            Pose = new SwordPose(new Vector3(-20f, 120f, 0f), new Vector3(10f, -80f, 0f))
        };

        /// <summary>冥月斬が当たった後の連続斬り演出</summary>
        public static readonly MoveData MeigetsuZanRush = new MoveData
        {
            Name = "冥月斬・連斬", Recovery = 24,
            ScriptedHits = new[]
            {
                new ScriptedHit(6, 40, HitEffect.Normal, 60),   // 何F目, ダメージ, 効果, のけぞり
                new ScriptedHit(12, 40, HitEffect.Normal, 60),
                new ScriptedHit(18, 40, HitEffect.Normal, 60),
                new ScriptedHit(24, 40, HitEffect.Normal, 60),
                new ScriptedHit(30, 40, HitEffect.Normal, 60),
                new ScriptedHit(44, 60, HitEffect.Launch, 0),   // 締め: 浮き
            },
            Pose = SlashPose
        };

        /// <summary>弱・中・強のボタンに対応する通常技を返す</summary>
        public static MoveData Normal(Command c)
        {
            switch (c)
            {
                case Command.Light: return Light;
                case Command.Medium: return Medium;
                default: return Heavy;
            }
        }
    }
}
