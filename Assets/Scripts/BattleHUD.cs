using LitMotion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fight3D
{
    /// <summary>
    /// CPU対戦の画面表示と操作。
    /// - 上: 両方の体力バー・名前・取ったラウンド(●)・残り時間
    /// - 真ん中: ROUND 1 / FIGHT! / K.O. / YOU WIN! などの大きな文字(出るときに LitMotion で縮む)
    /// - 下: 両方のウルトゲージと、今の動作
    /// - キー: R = 試合をやり直す、F5 = キャラ切り替え、F6 = トレーニングへ戻る(コントローラーは START / SELECT + RB / SELECT + LB)
    /// (仮の表示なので OnGUI で描いている。本番の画面は後で作る)
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        public BattleManager Battle;  // 試合の進行(表示する元)

        GUIStyle labelStyle, bigStyle, bigShadowStyle, timerStyle, boxStyle;  // 文字や枠の見た目の設定
        int shownBannerSerial = -1;  // 最後にアニメを始めた真ん中の文字の番号
        float bannerScale = 1f;      // 真ん中の文字の大きさの倍率(LitMotion で動かす)
        MotionHandle bannerMotion;   // 真ん中の文字のアニメ

        /// <summary>画面の更新ごとに呼ばれる。操作(R / F5 / F6、コントローラーは START / SELECT + RB / SELECT + LB)を受け付け、真ん中の文字のアニメを始める</summary>
        void Update()
        {
            // キーボードとコントローラー(ShortcutInput を見る)
            if (ShortcutInput.RestartMatch && Battle != null) Battle.RestartMatch();  // R / START
            if (ShortcutInput.SwitchCharacter) GameBootstrap.SwitchCharacter();      // F5 / SELECT + RB
            if (ShortcutInput.SwitchMode) GameBootstrap.SwitchMode();                // F6 / SELECT + LB

            if (Battle != null && Battle.BannerSerial != shownBannerSerial)
            {
                shownBannerSerial = Battle.BannerSerial;
                bannerMotion.TryCancel();
                bannerMotion = LMotion.Create(1.8f, 1f, 0.25f)  // 大きく出て、0.25秒で元の大きさに
                    .WithEase(Ease.OutBack)
                    .Bind(this, (v, self) => self.bannerScale = v)
                    .AddTo(gameObject);
            }
        }

        /// <summary>文字の大きさや色の設定を作る(画面の高さに合わせて大きさを変える)</summary>
        void MakeStyles(float s)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), richText = true };
            labelStyle.normal.textColor = Color.white;
            timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(56 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            timerStyle.normal.textColor = Color.white;
            bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(110 * s * bannerScale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bigStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            bigShadowStyle = new GUIStyle(bigStyle);
            bigShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        /// <summary>画面に文字や枠を描くときに Unity が呼ぶ</summary>
        void OnGUI()
        {
            if (Battle == null || Battle.Player == null || Battle.Cpu == null) return;
            float s = Screen.height / 1080f;  // 画面の大きさに合わせる倍率(1080p で 1)
            MakeStyles(s);

            TrainingHUD.DrawPopups(s);
            DrawTop(s);
            DrawBottom(s);
            DrawBanner(s);
        }

        /// <summary>上: 体力バー・名前・取ったラウンド・残り時間</summary>
        void DrawTop(float s)
        {
            float margin = 30 * s, barW = Screen.width * 0.36f, barH = 30 * s, y = 30 * s;  // 余白・バーの幅と高さ・上からの位置
            DrawFighterTop(Battle.Player, "あなた", Battle.PlayerWins, new Rect(margin, y, barW, barH), false, s);
            DrawFighterTop(Battle.Cpu, "CPU", Battle.CpuWins, new Rect(Screen.width - margin - barW, y, barW, barH), true, s);

            // 残り時間(真ん中)
            GUI.Label(new Rect(Screen.width / 2f - 80 * s, y - 14 * s, 160 * s, 70 * s), Battle.SecondsLeft.ToString(), timerStyle);
            GUI.Label(new Rect(Screen.width / 2f - 80 * s, y + 46 * s, 160 * s, 40 * s), $"<size={Mathf.RoundToInt(22 * s)}>ROUND {Battle.Round}</size>",
                new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter });
        }

        /// <summary>1人分の体力バーと名前・取ったラウンド(right = 右側のバーは右から減る)</summary>
        void DrawFighterTop(FighterController f, string who, int wins, Rect bar, bool right, float s)
        {
            float ratio = f.MaxHp > 0 ? (float)f.Hp / f.MaxHp : 0f;  // 残り体力の割合
            DrawRect(bar, new Color(0.15f, 0.15f, 0.15f));
            float w = bar.width * Mathf.Clamp01(ratio);
            Color hpColor = ratio > 0.25f ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.95f, 0.35f, 0.25f);  // 残り 25% 以下は赤
            DrawRect(right ? new Rect(bar.xMax - w, bar.y, w, bar.height) : new Rect(bar.x, bar.y, w, bar.height), hpColor);

            // 名前と取ったラウンド(● = 取った、○ = まだ)
            string pips = "";
            for (int i = 0; i < Tuning.Match.RoundsToWin; i++) pips += i < wins ? "●" : "○";
            string text = $"<b>{f.Kit.CharacterName}</b>({who})  {pips}";
            var style = new GUIStyle(labelStyle) { alignment = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
            GUI.Label(new Rect(bar.x, bar.yMax + 4 * s, bar.width, 40 * s), text, style);
            GUI.Label(new Rect(bar.x, bar.yMax + 40 * s, bar.width, 40 * s), $"{f.Hp} / {f.MaxHp}", style);
        }

        /// <summary>下: 両方のウルトゲージと今の動作、キーの説明</summary>
        void DrawBottom(float s)
        {
            float margin = 30 * s, w = Screen.width * 0.3f, h = 22 * s, y = Screen.height - 110 * s;  // 余白・ゲージの幅と高さ・上からの位置
            DrawGauge(Battle.Player, new Rect(margin, y, w, h), false, s);
            DrawGauge(Battle.Cpu, new Rect(Screen.width - margin - w, y, w, h), true, s);

            var help = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(22 * s) };
            GUI.Label(new Rect(0, Screen.height - 46 * s, Screen.width, 40 * s), "R / START: やり直す   F5 / SELECT+RB: キャラ切り替え   F6 / SELECT+LB: トレーニングへ", help);
        }

        /// <summary>1人分のウルトゲージと今の動作</summary>
        void DrawGauge(FighterController f, Rect bar, bool right, float s)
        {
            float g = f.Gauge / Tuning.Fighter.MaxGauge;  // ゲージの割合(0〜1)
            bool full = g >= 1f;
            DrawRect(bar, new Color(0.15f, 0.15f, 0.15f));
            float w = bar.width * Mathf.Clamp01(g);
            Color c = full ? new Color(1f, 0.82f, 0.3f) : new Color(0.6f, 0.4f, 0.9f);
            DrawRect(right ? new Rect(bar.xMax - w, bar.y, w, bar.height) : new Rect(bar.x, bar.y, w, bar.height), c);

            var style = new GUIStyle(labelStyle) { alignment = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
            string label = full ? "<color=#ffd34d><b>ウルト OK!</b></color>" : "ウルト";
            string move = f.CurrentMoveName;
            GUI.Label(new Rect(bar.x, bar.y - 40 * s, bar.width, 40 * s), string.IsNullOrEmpty(move) ? label : $"{label}  /  {move}", style);
        }

        /// <summary>真ん中の大きな文字</summary>
        void DrawBanner(float s)
        {
            if (string.IsNullOrEmpty(Battle.Banner)) return;
            var rect = new Rect(0, Screen.height * 0.3f, Screen.width, 200 * s);
            GUI.Label(new Rect(rect.x + 5 * s, rect.y + 5 * s, rect.width, rect.height), Battle.Banner, bigShadowStyle);
            GUI.Label(rect, Battle.Banner, bigStyle);
            if (Battle.Phase == BattlePhase.MatchEnd)
            {
                var sub = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(34 * s) };
                GUI.Label(new Rect(0, rect.yMax, Screen.width, 60 * s), "R / START でもう一度   F6 / SELECT+LB でトレーニングへ", sub);
            }
        }

        /// <summary>指定した色の四角を描く</summary>
        static void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
