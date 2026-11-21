using UnityEngine;

namespace Fight3D
{
    /// <summary>当たった相手がどうなるか</summary>
    public enum HitEffect
    {
        Normal,   // のけぞり(技ごとの時間)
        Stagger,  // よろけ(40F 動けない)
        Launch,   // 浮き(空中にいる間 + 着地後 30F 動けない)
    }

    /// <summary>技の種類(どのボタンで出したか)</summary>
    public enum Command { Light, Medium, Heavy, Skill1, Skill2, Skill3, Ult }

    /// <summary>決まったフレームに必ず当たる攻撃(ウルトの連続斬りなど)</summary>
    public struct ScriptedHit
    {
        public int Frame;  // 技の何F目に当たるか
        public int Damage;  // ダメージ
        public HitEffect Effect;  // 当たった相手がどうなるか
        public int Hitstun;  // のけぞり時間(F)
        /// <summary>決まったフレームの攻撃を作る</summary>
        public ScriptedHit(int frame, int damage, HitEffect effect, int hitstun)
        { Frame = frame; Damage = damage; Effect = effect; Hitstun = hitstun; }
    }

    /// <summary>剣の振り方(見た目だけ)</summary>
    public struct SwordPose
    {
        public Vector3 WindEuler, EndEuler;     // 構え → 振り終わり の向き
        public Vector3 WindOffset, EndOffset;   // 剣の支点の位置ずれ
        /// <summary>剣の振り方を作る(向きと位置のずれを指定)</summary>
        public SwordPose(Vector3 wind, Vector3 end, Vector3 windOffset, Vector3 endOffset)
        { WindEuler = wind; EndEuler = end; WindOffset = windOffset; EndOffset = endOffset; }
        /// <summary>剣の振り方を作る(向きだけ指定)</summary>
        public SwordPose(Vector3 wind, Vector3 end) : this(wind, end, Vector3.zero, Vector3.zero) { }
    }

    /// <summary>
    /// 1つの技のデータ。時間はすべてフレーム(1F = 1/60秒)。数値は仕様書の仮の値。
    /// 仕様書に書いていない値(のけぞり時間・当たっている時間など)は仮で決めている。
    /// </summary>
    public class MoveData
    {
        public string Name;  // 技の名前(画面表示用)
        public int Damage;  // ダメージ
        public int Startup;          // 出るまで
        public int Active;           // 当たっている時間
        public int Recovery;         // 終わりの隙(当たったとき・外したとき共通)
        public int WhiffRecovery = -1; // 外したときだけ隙が違う技(ウルト)。-1 なら Recovery と同じ
        public float Reach;          // リーチ(m)
        public float Arc = 60f;      // 当たる角度(前方の扇形の全体の角度)
        public int Hitstun;          // のけぞり時間
        public HitEffect Effect = HitEffect.Normal;  // 当たった相手がどうなるか
        public bool GuardBreak;      // ガード崩し
        public int ComboRank = -1;   // 通常技の強さ(弱0/中1/強2)。スキルは -1

        // 踏み込み: LungeStart〜LungeEnd フレームの間に LungeDistance m 前へ進む(相手に当たる距離で止まる)
        public float LungeDistance;
        public int LungeStart = 1, LungeEnd;  // 踏み込む期間(何F目から何F目まで)

        // 決まったフレームに必ず当たる攻撃(ある場合は Startup/Active の判定は使わない)
        public ScriptedHit[] ScriptedHits;

        public SwordPose Pose;  // 剣の振り方(見た目)

        public bool IsNormal => ComboRank >= 0;  // 通常技(弱・中・強)か

        /// <summary>技の全体のフレーム数(hit = 当たったか)</summary>
        public int TotalFrames(bool hit)
        {
            int recovery = (!hit && WhiffRecovery >= 0) ? WhiffRecovery : Recovery;
            if (ScriptedHits != null && ScriptedHits.Length > 0)
                return ScriptedHits[ScriptedHits.Length - 1].Frame + recovery;
            return Startup + Active + recovery;
        }
    }

    /// <summary>カゲツの技一覧(仕様書「8. 通常攻撃」「9. カゲツ」)</summary>
    public static class KagetsuMoves
    {
        static readonly SwordPose ThrustPose = new SwordPose(
            new Vector3(10f, 5f, 0f), new Vector3(5f, 0f, 0f),
            new Vector3(0f, 0f, -0.25f), new Vector3(-0.15f, 0f, 0.45f));
        static readonly SwordPose SlashPose = new SwordPose(new Vector3(10f, 85f, 0f), new Vector3(10f, -75f, 0f));  // 横なぎ(右から左へ)
        static readonly SwordPose OverheadPose = new SwordPose(new Vector3(-110f, 0f, 0f), new Vector3(55f, 0f, 0f));  // 振り下ろし(頭上から前へ)

        // ---- 通常攻撃 ----
        public static readonly MoveData Light = new MoveData
        { Name = "弱", Damage = 30, Startup = 5, Active = 3, Recovery = 10, Reach = 1.6f, Hitstun = 16, ComboRank = 0, Pose = ThrustPose };

        public static readonly MoveData Medium = new MoveData
        { Name = "中", Damage = 60, Startup = 9, Active = 4, Recovery = 18, Reach = 2.0f, Hitstun = 22, ComboRank = 1, Pose = SlashPose };

        public static readonly MoveData Heavy = new MoveData
        { Name = "強", Damage = 100, Startup = 15, Active = 5, Recovery = 28, Reach = 2.4f, Hitstun = 28, ComboRank = 2, Pose = OverheadPose };

        // ---- スキル1「影穿ち」: リーチの長い突き。3回目は踏み込み + よろけ ----
        public static readonly MoveData KageUgachi = new MoveData
        { Name = "影穿ち", Damage = 50, Startup = 8, Active = 3, Recovery = 16, Reach = 3.5f, Arc = 30f, Hitstun = 20, Pose = ThrustPose };

        public static readonly MoveData KageUgachi3 = new MoveData
        {
            Name = "影穿ち(3)", Damage = 70, Startup = 10, Active = 3, Recovery = 22, Reach = 3.5f, Arc = 30f, Hitstun = 40,
            Effect = HitEffect.Stagger, LungeDistance = 1.5f, LungeStart = 3, LungeEnd = 10, Pose = ThrustPose
        };

        // ---- スキル2「三日月払い」: 前方120度のなぎ払い。ガード崩し ----
        public static readonly MoveData MikazukiBarai = new MoveData
        {
            Name = "三日月払い", Damage = 90, Startup = 18, Active = 4, Recovery = 26, Reach = 2.8f - FighterController.BodyRadius,
            Arc = 120f, Hitstun = 26, GuardBreak = true,
            Pose = new SwordPose(new Vector3(0f, 110f, 0f), new Vector3(0f, -110f, 0f))
        };

        // ---- スキル3「仮面解放」: 発動の構え(攻撃判定なし) ----
        public static readonly MoveData KamenKaihou = new MoveData
        {
            Name = "仮面解放", Damage = 0, Startup = 0, Active = 0, Recovery = 20, Reach = 0f,
            Pose = new SwordPose(new Vector3(-150f, 0f, 0f), new Vector3(-150f, 0f, 0f), new Vector3(-0.2f, 0.4f, 0f), new Vector3(-0.2f, 0.4f, 0f))
        };

        // ---- ウルト「冥月斬」: 5m を一瞬で踏み込んで斬る。外すと隙 60F ----
        public static readonly MoveData MeigetsuZan = new MoveData
        {
            Name = "冥月斬", Damage = 40, Startup = 10, Active = 4, Recovery = 10, WhiffRecovery = 60, Reach = 1.4f, Hitstun = 60,
            LungeDistance = 5f, LungeStart = 2, LungeEnd = 9,
            Pose = new SwordPose(new Vector3(-20f, 120f, 0f), new Vector3(10f, -80f, 0f))
        };

        // 当たった後の連続斬り演出(最初の 40 と合わせて合計 300。最後に浮き)
        public static readonly MoveData MeigetsuZanRush = new MoveData
        {
            Name = "冥月斬・連斬", Recovery = 24,
            ScriptedHits = new[]
            {
                new ScriptedHit(6, 40, HitEffect.Normal, 60),
                new ScriptedHit(12, 40, HitEffect.Normal, 60),
                new ScriptedHit(18, 40, HitEffect.Normal, 60),
                new ScriptedHit(24, 40, HitEffect.Normal, 60),
                new ScriptedHit(30, 40, HitEffect.Normal, 60),
                new ScriptedHit(44, 60, HitEffect.Launch, 0),
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
