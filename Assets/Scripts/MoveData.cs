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
}
