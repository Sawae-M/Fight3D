using UnityEngine;

namespace Fight3D
{
    /// <summary>試合の進み具合</summary>
    public enum BattlePhase
    {
        RoundCall,  // 「ROUND 1」の表示中(動けない)
        Fight,      // 戦っている(「FIGHT!」の表示もこの中)
        RoundEnd,   // K.O. / 時間切れ の後、次のラウンドを待っている
        MatchEnd,   // 試合が終わった(R でもう一度)
    }

    /// <summary>
    /// CPU対戦の試合の進行(仕様書「2. 試合のルール」)。
    /// - 1ラウンド 99秒、2ラウンド先取
    /// - 時間切れは体力の割合が多い方の勝ち。同じなら両者1本
    /// - ラウンドの始めは円の中心をはさんで 6m 離れて向き合う。体力は全回復、ウルトゲージは持ち越し
    /// 進行は FixedUpdate(1秒に60回)で数える。数値は Tuning.cs の Tuning.Match。
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public FighterController Player;  // 自分のキャラ
        public FighterController Cpu;     // CPU のキャラ

        public BattlePhase Phase { get; private set; }  // 今の進み具合
        public int Round { get; private set; } = 1;     // 今何ラウンド目か
        public int PlayerWins { get; private set; }     // 自分が取ったラウンド数
        public int CpuWins { get; private set; }        // CPU が取ったラウンド数
        public int FramesLeft { get; private set; }     // ラウンドの残り時間(F)
        public int SecondsLeft => Mathf.Max(0, (FramesLeft + Tuning.Game.FramesPerSecond - 1) / Tuning.Game.FramesPerSecond);  // 残り時間(秒、切り上げ)
        public string Banner { get; private set; } = "";  // 画面の真ん中に出す文字(ROUND 1 / FIGHT! / K.O. など)
        public int BannerSerial { get; private set; }      // 文字が変わるたびに 1 増える(表示のアニメを始める合図)

        int phaseFrame;  // 今の進み具合になってから何F たったか

        /// <summary>最初のフレームの前に1回だけ呼ばれる。1ラウンド目を始める</summary>
        void Start()
        {
            StartRound();
        }

        /// <summary>試合を最初からやり直す(R キー)。ウルトゲージも 0 に戻す(持ち越すのはラウンドの間だけ)</summary>
        public void RestartMatch()
        {
            Player.ClearGauge();
            Cpu.ClearGauge();
            Round = 1;
            PlayerWins = 0;
            CpuWins = 0;
            StartRound();
        }

        /// <summary>ラウンドを始める: 位置・体力を戻して「ROUND n」を出す</summary>
        void StartRound()
        {
            Player.ResetState(Tuning.Stage.PlayerStart);
            Cpu.ResetState(Tuning.Stage.CpuStart);
            if (Player.CameraRig != null) Player.CameraRig.Snap();
            FramesLeft = Tuning.Match.RoundSeconds * Tuning.Game.FramesPerSecond;
            SetFrozen(true);
            SetPhase(BattlePhase.RoundCall, $"ROUND {Round}");
        }

        /// <summary>1秒に60回(1F ごと)呼ばれる。試合を進める</summary>
        void FixedUpdate()
        {
            phaseFrame++;
            switch (Phase)
            {
                case BattlePhase.RoundCall:
                    if (phaseFrame >= Tuning.Match.RoundCallFrames)
                    {
                        SetFrozen(false);
                        SetPhase(BattlePhase.Fight, "FIGHT!");
                    }
                    break;

                case BattlePhase.Fight:
                    if (phaseFrame == Tuning.Match.FightCallFrames) SetBanner("");
                    FramesLeft--;
                    if (Player.Hp <= 0 || Cpu.Hp <= 0) EndRoundByKo();
                    else if (FramesLeft <= 0) EndRoundByTime();
                    break;

                case BattlePhase.RoundEnd:
                    if (phaseFrame >= Tuning.Match.RoundEndFrames)
                    {
                        if (PlayerWins >= Tuning.Match.RoundsToWin || CpuWins >= Tuning.Match.RoundsToWin) EndMatch();
                        else
                        {
                            Round++;
                            StartRound();
                        }
                    }
                    break;
            }
        }

        /// <summary>K.O. でラウンドが終わる(体力が 0 になった方が負け。両方 0 なら両者1本)</summary>
        void EndRoundByKo()
        {
            bool playerDown = Player.Hp <= 0, cpuDown = Cpu.Hp <= 0;
            Player.KnockedDown = playerDown;
            Cpu.KnockedDown = cpuDown;
            if (!playerDown) CpuLost();
            else if (!cpuDown) PlayerLost();
            else { PlayerWins++; CpuWins++; }
            SetFrozen(true);
            SetPhase(BattlePhase.RoundEnd, playerDown && cpuDown ? "DOUBLE K.O." : "K.O.");
        }

        /// <summary>時間切れでラウンドが終わる(体力の割合が多い方の勝ち。同じなら両者1本)</summary>
        void EndRoundByTime()
        {
            float p = (float)Player.Hp / Player.MaxHp, c = (float)Cpu.Hp / Cpu.MaxHp;  // 残り体力の割合
            if (Mathf.Approximately(p, c)) { PlayerWins++; CpuWins++; }
            else if (p > c) CpuLost();
            else PlayerLost();
            SetFrozen(true);
            SetPhase(BattlePhase.RoundEnd, "TIME UP");
        }

        /// <summary>自分がラウンドを取った</summary>
        void CpuLost() { PlayerWins++; }

        /// <summary>CPU がラウンドを取った</summary>
        void PlayerLost() { CpuWins++; }

        /// <summary>試合が終わる: 勝ち・負け・引き分けを出す</summary>
        void EndMatch()
        {
            string result;
            if (PlayerWins > CpuWins) result = "YOU WIN!";
            else if (CpuWins > PlayerWins) result = "YOU LOSE";
            else result = "DRAW";
            SetPhase(BattlePhase.MatchEnd, result);
        }

        /// <summary>両方のキャラを止める / 動かす</summary>
        void SetFrozen(bool frozen)
        {
            Player.Frozen = frozen;
            Cpu.Frozen = frozen;
        }

        /// <summary>進み具合を変えて、真ん中の文字を出す</summary>
        void SetPhase(BattlePhase phase, string banner)
        {
            Phase = phase;
            phaseFrame = 0;
            SetBanner(banner);
        }

        /// <summary>真ん中の文字を変える</summary>
        void SetBanner(string text)
        {
            Banner = text;
            BannerSerial++;
        }
    }
}
