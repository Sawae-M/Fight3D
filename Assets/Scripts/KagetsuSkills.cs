using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// カゲツのスキルの状態(使える回数・使えない時間・仮面解放の効果など)。
    /// 数値は Tuning.cs(仕様書「9. カゲツ」の値)。
    /// </summary>
    public class KagetsuSkills
    {
        // 数値(回数・時間・倍率)は Tuning.cs の Tuning.Kagetsu にある

        public int KageCount { get; private set; }   // 今の連続で何回突いたか
        int kageGap = 999;                           // 前の突きが終わってからのフレーム数
        bool kageInProgress;  // 影穿ちを出している最中か

        public int MikazukiCooldownLeft { get; private set; }  // 三日月払いが使えるまでの残り(F)
        public int KamenCooldownLeft { get; private set; }  // 仮面解放が使えるまでの残り(F)
        public int KamenLeft { get; private set; }  // 仮面解放の効果の残り(F)
        public int CurseMarks { get; private set; }  // 今付いている呪印の数

        public bool KamenActive => KamenLeft > 0;  // 仮面解放の効果中か
        public float SpeedScale => KamenActive ? Tuning.Kagetsu.KamenSpeedScale : 1f;  // 今の移動速度の倍率

        /// <summary>影穿ちをあと何回出せるか(画面表示用)</summary>
        public int KageRemaining
        {
            get
            {
                if (!kageInProgress && kageGap > Tuning.Kagetsu.KageChainWindow) return Tuning.Kagetsu.KageMaxChain;
                return Mathf.Max(0, Tuning.Kagetsu.KageMaxChain - KageCount);
            }
        }

        /// <summary>仮面解放中は技の出が 0.8倍</summary>
        public int ScaleStartup(int startup)
        {
            if (!KamenActive || startup <= 0) return startup;
            return Mathf.Max(1, Mathf.RoundToInt(startup * Tuning.Kagetsu.KamenStartupScale));
        }

        /// <summary>そのスキルを今使えるか(回数・使えない時間・効果中か)</summary>
        public bool CanUse(Command c)
        {
            switch (c)
            {
                case Command.Skill1: return KageRemaining > 0;
                case Command.Skill2: return MikazukiCooldownLeft <= 0;
                case Command.Skill3: return !KamenActive && KamenCooldownLeft <= 0;
                default: return true;
            }
        }

        /// <summary>スキル1 で次に出る技(3回目だけ強い突き)</summary>
        public MoveData NextKageUgachi()
        {
            if (!kageInProgress && kageGap > Tuning.Kagetsu.KageChainWindow) KageCount = 0;
            return KageCount == Tuning.Kagetsu.KageMaxChain - 1 ? KagetsuMoves.KageUgachi3 : KagetsuMoves.KageUgachi;
        }

        /// <summary>技を出し始めたときに呼ぶ(回数を数える・使えない時間を始める・仮面解放を発動)</summary>
        public void OnMoveStarted(Command c)
        {
            switch (c)
            {
                case Command.Skill1:
                    if (!kageInProgress && kageGap > Tuning.Kagetsu.KageChainWindow) KageCount = 0;
                    KageCount++;
                    kageInProgress = true;
                    break;
                case Command.Skill2:
                    MikazukiCooldownLeft = Tuning.Kagetsu.MikazukiCooldown;
                    break;
                case Command.Skill3:
                    KamenLeft = Tuning.Kagetsu.KamenDuration;
                    KamenCooldownLeft = Tuning.Kagetsu.KamenCooldown;
                    CurseMarks = 0;
                    break;
            }
        }

        /// <summary>技が終わったときに呼ぶ(影穿ちの連続の猶予を数え始める)</summary>
        public void OnMoveEnded(Command c)
        {
            if (c == Command.Skill1)
            {
                kageInProgress = false;
                kageGap = 0;
            }
        }

        /// <summary>攻撃が当たったとき(仮面解放中なら呪印が付く)</summary>
        public void OnHit()
        {
            if (KamenActive) CurseMarks = Mathf.Min(Tuning.Kagetsu.MaxCurseMarks, CurseMarks + 1);
        }

        /// <summary>
        /// 1F 進める。仮面解放が終わった瞬間は true を返し、付いていた呪印の数を渡す。
        /// </summary>
        public bool Tick(out int curseMarksToBurst)
        {
            curseMarksToBurst = 0;
            if (!kageInProgress && kageGap < 999) kageGap++;
            if (MikazukiCooldownLeft > 0) MikazukiCooldownLeft--;
            if (KamenCooldownLeft > 0) KamenCooldownLeft--;
            if (KamenLeft > 0)
            {
                KamenLeft--;
                if (KamenLeft == 0)
                {
                    curseMarksToBurst = CurseMarks;
                    CurseMarks = 0;
                    return true;
                }
            }
            return false;
        }

        /// <summary>スキルの状態をすべて最初に戻す</summary>
        public void ResetAll()
        {
            KageCount = 0;
            kageGap = 999;
            kageInProgress = false;
            MikazukiCooldownLeft = 0;
            KamenCooldownLeft = 0;
            KamenLeft = 0;
            CurseMarks = 0;
        }
    }
}
