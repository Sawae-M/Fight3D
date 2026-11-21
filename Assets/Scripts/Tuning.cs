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
            public static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -3f);  // [仮] トレーニングでの自分のキャラの開始位置
            public static readonly Vector3 SandbagStart = new Vector3(0f, 0f, 3f);  // [仕様書] サンドバッグは円の中心から 3m
            public static readonly Vector3 CpuStart = new Vector3(0f, 0f, 3f);      // [仕様書] 対戦のラウンド開始: 円の中心をはさんで 6m 離れて向き合う(自分は PlayerStart)
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
            public const int StaggerFrames = 40;       // [仕様書] 自分が「よろけ」になったとき 40F 動けない
            public const int LaunchFrames = 66;        // [仮] 自分が「浮き」になったときに動けない時間(空中 36F + 着地後 30F)
            public const int WallHitstunBonus = 6;     // [仮] 壁に押し付けられた状態で攻撃を受けると、のけぞりがこのF だけ長くなる(仕様書「3.」の「少し長く」)

            // ウルトゲージ(仕様書「7.」)
            public const float MaxGauge = 100f;        // [仕様書] 最大 100
            public const float GaugeOnHitRate = 0.10f; // [仕様書] 当てたダメージの 10% たまる
            public const float GaugeOnGuarded = 3f;    // [仕様書] ガードされた / ガードしたら 3 たまる
            public const float GaugeOnTakenRate = 0.08f; // [仕様書] 攻撃を受けたら、受けたダメージの 8% たまる
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
            public const int MaxHp = 950;              // [仕様書] カゲツの体力

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

        /// <summary>ゴウガ(仕様書「6. 移動」「8. 通常攻撃」「10. ゴウガ」)</summary>
        public static class Gouga
        {
            public const int MaxHp = 1100;             // [仕様書] ゴウガの体力
            public const float SpeedScale = 0.85f;     // [仕様書] 歩く速さはすべて 0.85倍
            public const float NormalDamageScale = 1.15f; // [仕様書] 通常攻撃のダメージ 1.15倍
            public const int NormalStartupAdd = 2;     // [仕様書] 通常攻撃の出るまでの時間 +2F

            // パッシブ「不屈」
            public const float FukutsuHalfHp = 0.5f;   // [仕様書] 体力がこの割合以下で攻撃力アップ(50%)
            public const float FukutsuHalfScale = 1.1f;   // [仕様書] 体力 50%以下で 1.1倍
            public const float FukutsuQuarterHp = 0.25f;  // [仕様書] 体力がこの割合以下でさらにアップ(25%)
            public const float FukutsuQuarterScale = 1.2f; // [仕様書] 体力 25%以下で 1.2倍

            // スキル1「岩砕き」
            public const int GansaiCooldown = 240;     // [仕様書] 使用後 4秒 使えない
            public const float ShockwaveRange = 5f;    // [仕様書] 衝撃波はまっすぐ 5m 進む
            public const float ShockwaveSpeed = 15f;   // [仮] 衝撃波の速さ(m/秒)。5m を約 20F で進む
            public const float ShockwaveHalfWidth = 0.5f; // [仮] 衝撃波の幅の半分(m)

            // スキル2「剛腕引き」
            public const int GouwanCooldown = 360;     // [仕様書] 使用後 6秒 使えない
            public const float PullDistance = 1.0f;    // [仕様書] 相手を目の前 1m まで引き寄せる(体の中心どうしの距離)
            public const int PullFrames = 8;           // [仮] 引き寄せにかかる時間(F)

            // スキル3「受け止め」
            public const int UketomeCooldown = 300;    // [仕様書] 使用後 5秒 使えない
            public const int UketomeWindow = 30;       // [仕様書] 構える時間 30F(この間に攻撃を受けると反撃)

            // ウルト「大地崩し」
            public const float UltMaxScale = 1.5f;     // [仕様書] 自分の体力が少ないほど威力アップ。最大 1.5倍
                                                       // 倍率 = 1 + 0.5 × 失った体力の割合(体力満タンで 1.0倍、残りわずかで約 1.5倍)。2026-11-21 M が確認して仕様書に追記
        }

        /// <summary>対戦の試合ルール(仕様書「2. 試合のルール」)</summary>
        public static class Match
        {
            public const int RoundSeconds = 99;        // [仕様書] 1ラウンド 99秒
            public const int RoundsToWin = 2;          // [仕様書] 2ラウンド先取
            public const int RoundCallFrames = 60;     // [仮] 「ROUND 1」と出ている時間(F)。この間は動けない
            public const int FightCallFrames = 40;     // [仮] 「FIGHT!」と出ている時間(F)。出た瞬間から動ける
            public const int RoundEndFrames = 150;     // [仮] K.O. / 時間切れ から次のラウンドまで(F)
        }

        /// <summary>CPU の強さと動き方(仕様書「13. これから決めること」なので、すべて [仮])</summary>
        public static class Cpu
        {
            public const int ThinkInterval = 18;       // [仮] 次の行動を考えるまでの間(F)。小さいほど手数が多い
            public const int ThinkJitter = 24;         // [仮] 考える間のばらつき(0〜このF をランダムに足す)
            public const float AttackRange = 1.9f;     // [仮] この距離(m、体の中心どうし)以内なら通常攻撃を始める
            public const float KeepRange = 1.5f;       // [仮] これより近いと、たまに少し下がる
            public const float ReactRange = 3.2f;      // [仮] 相手の技の出始めに反応する距離(m)
            public const int ReactDelay = 3;           // [仮] 相手の技が出始めてから反応するまで(F)。人間らしい遅れ
            public const float GuardChance = 0.4f;     // [仮] 相手の技にガードで反応する確率
            public const float DodgeChance = 0.12f;    // [仮] 相手の技に回避で反応する確率
            public const float CounterChance = 0.35f;  // [仮] ゴウガが受け止めで反応する確率(使えるとき)
            public const int GuardHoldFrames = 28;     // [仮] ガードを続ける時間(F)
            public const float ComboChance = 0.75f;    // [仮] 当たったときに次の技へつなげる確率
            public const float SkillFinishChance = 0.5f; // [仮] 連続技の最後をスキルにする確率(使えるとき)
            public const float RangedSkillChance = 0.3f; // [仮] 離れているときに遠くまで届くスキル(影穿ち・岩砕き・剛腕引き)を出す確率
            public const float BuffChance = 0.25f;     // [仮] カゲツが仮面解放を使う確率(使えるとき、考えるたび)
            public const float UltRange = 2.3f;        // [仮] ゲージ満タンのとき、この距離以内ならウルトを狙う
            public const float StrafeChance = 0.35f;   // [仮] 考えるたびに、相手の周りを回り始める確率
            public const int Seed = 12345;             // [仮] CPU の乱数の種(同じ種なら毎回同じ動きになる)
        }

        /// <summary>トレーニング用: サンドバッグが殴ってくる設定(F3)。受け止め・ガード・回避の練習用。すべて [仮]</summary>
        public static class SandbagAttack
        {
            public const float Range = 2.0f;           // [仮] この距離(m、体の中心どうし)まで近づくと殴ってくる
            public const int Windup = 30;              // [仮] 振りかぶってから当たるまで(F)。赤く光るのが合図
            public const int Interval = 90;            // [仮] 殴ってから次に殴るまでの間(F)
            public const int Damage = 60;              // [仮] ダメージ
            public const int Hitstun = 20;             // [仮] 当たったときのけぞる時間(F)
            public const int Hitstop = 4;              // [仮] 当たった瞬間に止まる時間(F)
        }

        /// <summary>トレーニングのその他の設定(すべて [仮])</summary>
        public static class Training
        {
            public static readonly float[] HpSteps = { 1f, 0.5f, 0.25f, 0.1f };  // [仮] F4 で自分の体力を 100% → 50% → 25% → 10% と切り替える(不屈・大地崩しの確認用)
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
        { Name = "弱攻撃", Damage = 30, Startup = 5, Active = 3, Recovery = 10, Reach = 1.6f, Hitstun = 16, ComboRank = 0, Pose = ThrustPose };

        /// <summary>中(I)</summary>
        public static readonly MoveData Medium = new MoveData
        { Name = "中攻撃", Damage = 60, Startup = 9, Active = 4, Recovery = 18, Reach = 2.0f, Hitstun = 22, ComboRank = 1, Pose = SlashPose };

        /// <summary>強(O)</summary>
        public static readonly MoveData Heavy = new MoveData
        { Name = "強攻撃", Damage = 100, Startup = 15, Active = 5, Recovery = 28, Reach = 2.4f, Hitstun = 28, ComboRank = 2, Pose = OverheadPose };

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

    /// <summary>
    /// ゴウガの技一覧(仕様書「8. 通常攻撃」「10. ゴウガ」)。技のダメージやフレーム数はここで調整する。
    /// 仕様書に無い値: のけぞり時間(Hitstun)・当たっている時間(Active)・スキルの終わりの隙・腕の振り方 は [仮]。
    /// </summary>
    public static class GougaMoves
    {
        /// <summary>
        /// 通常攻撃のダメージ: 基本形 × 1.15(四捨五入)。
        /// float のままだと 30 × 1.15 が 34.4999… になり 34 になってしまうので、
        /// 倍率を小数第3位までの10進数(decimal)に直してから計算する
        /// </summary>
        static int NormalDamage(int baseDamage)
        {
            decimal scale = System.Math.Round((decimal)Tuning.Gouga.NormalDamageScale, 3);  // 1.15f → 1.150(float の誤差を消す)
            return (int)System.Math.Round(baseDamage * scale, System.MidpointRounding.AwayFromZero);  // 30→35、60→69、100→115
        }

        /// <summary>通常攻撃の出るまでの時間: 基本形 + 2F</summary>
        static int NormalStartup(int baseStartup) => baseStartup + Tuning.Gouga.NormalStartupAdd;

        // ---- 腕の振り方(見た目だけ)。右肩を支点に、腕は前(+Z)に伸びている ----
        static readonly SwordPose JabPose = new SwordPose(   // まっすぐ突くパンチ
            new Vector3(10f, 10f, 0f), new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, -0.3f), new Vector3(-0.1f, 0f, 0.45f));
        static readonly SwordPose HookPose = new SwordPose(new Vector3(0f, 75f, 0f), new Vector3(0f, -35f, 0f));  // 横から振るフック
        static readonly SwordPose SlamPose = new SwordPose(new Vector3(-110f, 0f, 0f), new Vector3(55f, 0f, 0f));  // 頭上から叩きつけ
        static readonly SwordPose GroundPose = new SwordPose(new Vector3(-130f, 0f, 0f), new Vector3(85f, 0f, 0f)); // 地面を殴る
        static readonly SwordPose UpperPose = new SwordPose(new Vector3(70f, 0f, 0f), new Vector3(-80f, 0f, 0f));   // アッパー

        // ---- 通常攻撃 [仕様書] 基本形(仕様書「8.」)のダメージ × 1.15、出るまで +2F / [仮] のけぞり ----
        /// <summary>弱(U)</summary>
        public static readonly MoveData Light = new MoveData
        { Name = "弱攻撃", Damage = NormalDamage(30), Startup = NormalStartup(5), Active = 3, Recovery = 10, Reach = 1.6f, Hitstun = 17, ComboRank = 0, Pose = JabPose };

        /// <summary>中(I)</summary>
        public static readonly MoveData Medium = new MoveData
        { Name = "中攻撃", Damage = NormalDamage(60), Startup = NormalStartup(9), Active = 4, Recovery = 18, Reach = 2.0f, Hitstun = 24, ComboRank = 1, Pose = HookPose };

        /// <summary>強(O)</summary>
        public static readonly MoveData Heavy = new MoveData
        { Name = "強攻撃", Damage = NormalDamage(100), Startup = NormalStartup(15), Active = 5, Recovery = 28, Reach = 2.4f, Hitstun = 30, ComboRank = 2, Pose = SlamPose };

        // ---- スキル1「岩砕き」(P) ----
        // [仕様書] 衝撃波がまっすぐ 5m 進む、ダメージ 80、出るまで 20F、当たると浮き
        // [仮] 終わりの隙 20F(衝撃波の速さ・幅は Tuning.Gouga)
        /// <summary>岩砕き(地面を殴って衝撃波を出す)</summary>
        public static readonly MoveData Gansai = new MoveData
        {
            Name = "岩砕き", Damage = 80, Startup = 20, Active = 1, Recovery = 20, Reach = 0f, Hitstun = 0,
            Effect = HitEffect.Launch, SpawnsShockwave = true, Pose = GroundPose
        };

        // ---- スキル2「剛腕引き」(W+P) ----
        // [仕様書] 前方 4m までつかむ、ガードできない、目の前 1m まで引き寄せ、ダメージ 40、相手は 25F 動けない、外すと隙 30F
        // [仮] 出るまで 12F、当たっている時間 4F、つかんだときの隙 10F(相手が動けない間に攻撃できるように短め)、つかめる角度 40度
        /// <summary>剛腕引き(つかんで引き寄せる)</summary>
        public static readonly MoveData Gouwan = new MoveData
        {
            Name = "剛腕引き", Damage = 40, Startup = 12, Active = 4, Recovery = 10, WhiffRecovery = 30,
            Reach = 4f - Tuning.Fighter.BodyRadius, Arc = 40f, Hitstun = 25, IsGrab = true,
            Pose = new SwordPose(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -0.2f), new Vector3(0f, 0f, 0.9f))
        };

        // ---- スキル3「受け止め」(S+P) ----
        // [仕様書] 構える時間 30F、何も来なければ終わりの隙 20F
        /// <summary>受け止めの構え(この間に攻撃を受けると反撃に変わる)</summary>
        public static readonly MoveData Uketome = new MoveData
        {
            Name = "受け止め", Damage = 0, Startup = 0, Active = Tuning.Gouga.UketomeWindow, Recovery = 20, Reach = 0f, IsCounterStance = true,
            Pose = new SwordPose(new Vector3(-20f, -70f, 0f), new Vector3(-20f, -70f, 0f), new Vector3(-0.1f, 0.15f, 0.2f), new Vector3(-0.1f, 0.15f, 0.2f))
        };

        // [仕様書] 反撃ダメージ 120、相手はよろけ / [仮] 反撃が当たるのは 4F目、反撃後の隙 20F
        /// <summary>受け止めからの反撃(必ず当たる)</summary>
        public static readonly MoveData UketomeCounter = new MoveData
        {
            Name = "受け止め・反撃", Recovery = 20,
            ScriptedHits = new[] { new ScriptedHit(4, 120, HitEffect.Stagger, 40) },  // 何F目, ダメージ, 効果, のけぞり
            Pose = UpperPose
        };

        // ---- ウルト「大地崩し」(O+P) ----
        // [仕様書] 前方 2.5m のパンチ、出るまで 12F、合計(基本)ダメージ 280、アッパーで締める、外すと隙 50F
        // [仮] 最初の一撃 30 + 連打 30×5 + アッパー 100 の配分、連打のタイミング、当たったときの隙 24F
        /// <summary>大地崩し(最初のパンチ)</summary>
        public static readonly MoveData DaichiKuzushi = new MoveData
        {
            Name = "大地崩し", Damage = 30, Startup = 12, Active = 4, Recovery = 10, WhiffRecovery = 50, Reach = 2.5f, Hitstun = 60,
            Pose = JabPose
        };

        /// <summary>大地崩しが当たった後の連続パンチ → アッパー</summary>
        public static readonly MoveData DaichiKuzushiRush = new MoveData
        {
            Name = "大地崩し・連打", Recovery = 24,
            ScriptedHits = new[]
            {
                new ScriptedHit(6, 30, HitEffect.Normal, 60),   // 何F目, ダメージ, 効果, のけぞり
                new ScriptedHit(10, 30, HitEffect.Normal, 60),
                new ScriptedHit(14, 30, HitEffect.Normal, 60),
                new ScriptedHit(18, 30, HitEffect.Normal, 60),
                new ScriptedHit(22, 30, HitEffect.Normal, 60),
                new ScriptedHit(38, 100, HitEffect.Launch, 0),  // 締めのアッパー: 浮き
            },
            Pose = JabPose
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
