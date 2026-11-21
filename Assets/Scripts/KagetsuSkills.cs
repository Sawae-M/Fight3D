using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// カゲツのキャラ設定とスキルの状態(使える回数・使えない時間・仮面解放の効果・パッシブ「連刃」)。
    /// 数値は Tuning.cs(仕様書「9. カゲツ」の値)。
    /// </summary>
    public class KagetsuSkills : CharacterKit
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
        public override float SpeedScale => KamenActive ? Tuning.Kagetsu.KamenSpeedScale : 1f;  // 今の移動速度の倍率(仮面解放中は 1.3倍)

        /// <summary>キャラの名前</summary>
        public override string CharacterName => "カゲツ";
        /// <summary>最大体力(値は Tuning.cs)</summary>
        public override int MaxHp => Tuning.Kagetsu.MaxHp;
        /// <summary>ダメージ倍率の表示名</summary>
        public override string BonusLabel => "連刃ボーナス";

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
        public override int ScaleStartup(int startup)
        {
            if (!KamenActive || startup <= 0) return startup;
            return Mathf.Max(1, Mathf.RoundToInt(startup * Tuning.Kagetsu.KamenStartupScale));
        }

        /// <summary>そのスキルを今使えるか(回数・使えない時間・効果中か)</summary>
        public override bool CanUse(Command c)
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
        public override void OnMoveStarted(Command c)
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
        public override void OnMoveEnded(Command c)
        {
            if (c == Command.Skill1)
            {
                kageInProgress = false;
                kageGap = 0;
            }
        }

        /// <summary>攻撃が当たったとき(仮面解放中なら呪印が付く)</summary>
        public override void OnHit()
        {
            if (KamenActive) CurseMarks = Mathf.Min(Tuning.Kagetsu.MaxCurseMarks, CurseMarks + 1);
        }

        /// <summary>1F 進める。仮面解放が終わった瞬間に、付いていた呪印をまとめて爆発させる</summary>
        public override void Tick(FighterController owner)
        {
            if (TickTimers(out int marks) && marks > 0) CurseBurst(owner, marks);
        }

        /// <summary>仮面解放の終わり: 呪印 1つにつき 15 ダメージ(連刃も効く)</summary>
        void CurseBurst(FighterController owner, int marks)
        {
            owner.PopupAtTarget($"呪印 ×{marks}", new Color(0.8f, 0.3f, 1f));
            owner.BonusStrike(marks * Tuning.Kagetsu.CurseDamagePerMark, Tuning.Kagetsu.CurseBurstHitstun, Tuning.Kagetsu.CurseBurstHitstop, Command.Skill3);
        }

        /// <summary>
        /// 時間を 1F 進める。仮面解放が終わった瞬間は true を返し、付いていた呪印の数を渡す。
        /// </summary>
        bool TickTimers(out int curseMarksToBurst)
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
        public override void ResetAll()
        {
            KageCount = 0;
            kageGap = 999;
            kageInProgress = false;
            MikazukiCooldownLeft = 0;
            KamenCooldownLeft = 0;
            KamenLeft = 0;
            CurseMarks = 0;
        }

        // ================================================================
        //  技の表・つながり・ダメージ(CharacterKit の決まりごと)
        // ================================================================
        /// <summary>弱・中・強のボタンに対応する通常技</summary>
        public override MoveData Normal(Command c) => KagetsuMoves.Normal(c);

        /// <summary>スキル1〜3 のボタンで出る技</summary>
        public override MoveData SkillMove(Command c)
        {
            switch (c)
            {
                case Command.Skill1: return NextKageUgachi();
                case Command.Skill2: return KagetsuMoves.MikazukiBarai;
                default: return KagetsuMoves.KamenKaihou;
            }
        }

        /// <summary>ウルト「冥月斬」の踏み込み斬り</summary>
        public override MoveData UltMove => KagetsuMoves.MeigetsuZan;

        /// <summary>冥月斬が当たった後の連続斬り</summary>
        public override MoveData UltRush => KagetsuMoves.MeigetsuZanRush;

        /// <summary>影穿ちは影穿ち・ウルトに、三日月払いはウルトにつながる</summary>
        public override bool CanCancelSkill(Command from, Command to)
        {
            if (from == Command.Skill1) return to == Command.Skill1 || to == Command.Ult;
            if (from == Command.Skill2) return to == Command.Ult;
            return false;
        }

        /// <summary>パッシブ「連刃」: 2ヒット目から1ヒットごとに +3%、最大 +24%</summary>
        public override float DamageScale(int hitNumber, Command source, FighterController owner)
        {
            int bonusPercent = Mathf.Min((hitNumber - 1) * Tuning.Kagetsu.RenjinBonusPerHit, Tuning.Kagetsu.RenjinMaxBonus);
            return 1f + bonusPercent / 100f;
        }

        /// <summary>左上の操作説明(技の行)</summary>
        public override string[] MoveHelpLines => new[] { "影穿ち: P", "三日月払い: W+P", "仮面解放: S+P", "冥月斬: O+P" };

        /// <summary>下のパネルのスキル状態(1行に1つ)</summary>
        public override string[] StatusLines()
        {
            string kage = $"影穿ち: 残り{KageRemaining}回";
            string mika = MikazukiCooldownLeft > 0 ? $"三日月払い: {MikazukiCooldownLeft / 60f:0.0}秒" : "三日月払い: OK";
            string kamen = KamenActive
                ? $"<color=#ff6070>仮面解放中 {KamenLeft / 60f:0.0}秒 呪印{CurseMarks}</color>"
                : (KamenCooldownLeft > 0 ? $"仮面解放: {KamenCooldownLeft / 60f:0.0}秒" : "仮面解放: OK");
            return new[] { kage, mika, kamen };
        }

        // ---- 見た目: 剣の構え(見た目だけの数値) ----
        static readonly Vector3 restPos = new Vector3(0.38f, 1.3f, 0.05f);        // 構えの剣の支点の位置(右肩)
        static readonly Quaternion restRot = Quaternion.Euler(45f, 15f, 0f);       // 構えの剣の向き(前下がり)
        static readonly Vector3 guardPos = new Vector3(0.3f, 1.45f, 0.35f);        // ガードの剣の支点の位置
        static readonly Quaternion guardRot = Quaternion.Euler(-15f, -80f, 0f);    // ガードの剣の向き(体の前で横に)
        /// <summary>構えの剣の支点の位置</summary>
        public override Vector3 RestPos => restPos;
        /// <summary>構えの剣の向き</summary>
        public override Quaternion RestRot => restRot;
        /// <summary>ガードの剣の支点の位置</summary>
        public override Vector3 GuardPos => guardPos;
        /// <summary>ガードの剣の向き</summary>
        public override Quaternion GuardRot => guardRot;
    }
}
