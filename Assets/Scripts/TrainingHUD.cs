using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fight3D
{
    /// <summary>
    /// トレーニング画面の表示と設定。
    /// - サンドバッグの上に出るダメージ数字・文字(GUARD など)
    /// - 右上: サンドバッグの情報(体力・直前のダメージ・連続技・状態)
    /// - 左上: 操作説明
    /// - 下: 自分のキャラの体力・ウルトゲージ・スキルの状態
    /// - トレーニング設定: F1 = サンドバッグのガード ON/OFF、F2 = ゲージ満タン、F3 = サンドバッグが殴ってくる ON/OFF、
    ///   F4 = 自分の体力を 100% → 50% → 25% → 10% と切り替え、F5 = キャラ切り替え(カゲツ ⇔ ゴウガ)、F6 = CPU対戦へ
    /// (仮の表示なので Unity の古い GUI 機能 OnGUI で簡単に描いている。数字の動きは LitMotion)
    /// </summary>
    public class TrainingHUD : MonoBehaviour
    {
        /// <summary>画面に出す数字・文字1つ分の情報</summary>
        class Popup
        {
            public string Text;
            public Vector3 WorldPos;
            public float Rise;    // 上に浮かぶ量(m)
            public float Scale;   // 出た瞬間だけ大きく
            public float Alpha;   // 透明度
            public float Size;    // 文字の大きさ(倍率)
            public Color Color;
        }

        static readonly List<Popup> popups = new List<Popup>();  // 画面に出ている数字・文字

        public Sandbag Sandbag;  // 表示するサンドバッグ
        public FighterController Fighter;  // 表示するキャラ

        GUIStyle boxStyle, labelStyle;  // 枠と文字の見た目の設定
        static GUIStyle popupStyle, shadowStyle;  // ダメージ数字の見た目の設定(対戦画面でも使う)

        /// <summary>ダメージ数字(ヒット数が多いほど黄色→赤っぽく)</summary>
        public static void AddPopup(int damage, int hitNumber, Vector3 worldPos)
        {
            float t = Mathf.Clamp01((hitNumber - 1) / 8f);
            var color = Color.Lerp(new Color(1f, 0.95f, 0.4f), new Color(1f, 0.35f, 0.25f), t);
            Spawn(damage.ToString(), color, worldPos, 1f);
        }

        /// <summary>文字のポップアップ(GUARD・呪印 など)</summary>
        public static void AddTextPopup(string text, Color color, Vector3 worldPos)
        {
            Spawn(text, color, worldPos, 0.7f);
        }

        /// <summary>数字・文字を1つ出して、LitMotion で動かす</summary>
        static void Spawn(string text, Color color, Vector3 worldPos, float size)
        {
            // 同じ場所に重ならないよう少しずらす
            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.2f), 0f);
            var p = new Popup { Text = text, WorldPos = worldPos + jitter, Scale = 1f, Alpha = 1f, Size = size, Color = color };
            popups.Add(p);

            // ポンと大きく出て → 上に浮かびながら → 消える
            LMotion.Create(1.6f, 1f, 0.18f).WithEase(Ease.OutBack).Bind(p, (v, x) => x.Scale = v);
            LMotion.Create(0f, 1.0f, 0.9f).WithEase(Ease.OutCubic).Bind(p, (v, x) => x.Rise = v);
            LMotion.Create(1f, 0f, 0.35f).WithDelay(0.55f)
                .WithOnComplete(() => popups.Remove(p))
                .Bind(p, (v, x) => x.Alpha = v);
        }

        /// <summary>消えるときに呼ばれる。出ている数字・文字を消す</summary>
        void OnDestroy() { popups.Clear(); }

        /// <summary>画面の更新ごとに呼ばれる。トレーニング設定の操作(F1〜F6 / SELECT + ボタン)を受け付ける</summary>
        void Update()
        {
            // トレーニング設定(キーボードの F1〜F6 / コントローラーは SELECT + ボタン。ShortcutInput を見る)
            if (ShortcutInput.ToggleSandbagGuard && Sandbag != null) Sandbag.GuardEnabled = !Sandbag.GuardEnabled;
            if (ShortcutInput.FillGauge && Fighter != null) Fighter.AddGauge(Tuning.Fighter.MaxGauge);
            if (ShortcutInput.ToggleSandbagAttack && Sandbag != null) Sandbag.AttackEnabled = !Sandbag.AttackEnabled;
            if (ShortcutInput.CycleHp && Fighter != null) Fighter.SetHpRatio(NextHpStep());
            if (ShortcutInput.SwitchCharacter) GameBootstrap.SwitchCharacter();
            if (ShortcutInput.SwitchMode) GameBootstrap.SwitchMode();
        }

        /// <summary>F4 で次に選ぶ体力の割合: 今の体力より少ない段階のうち一番近いもの。一番下まで来たら 100% に戻る</summary>
        float NextHpStep()
        {
            float now = Fighter.MaxHp > 0 ? (float)Fighter.Hp / Fighter.MaxHp : 1f;  // 今の体力の割合
            foreach (float step in Tuning.Training.HpSteps)
            {
                if (step < now - 0.001f) return step;  // 0.001 は計算の誤差のぶん
            }
            return Tuning.Training.HpSteps[0];
        }

        /// <summary>文字の大きさや色の設定を作る(画面の高さに合わせて大きさを変える)</summary>
        void MakeStyles()
        {
            float s = Screen.height / 1080f;
            boxStyle = new GUIStyle(GUI.skin.box);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), richText = true };
            labelStyle.normal.textColor = Color.white;
        }

        /// <summary>ダメージ数字の文字の設定を作る(GUI.skin は OnGUI の中でしか使えないので、描く直前に作る)</summary>
        static void MakePopupStyles(float s)
        {
            popupStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(56 * s),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            shadowStyle = new GUIStyle(popupStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        /// <summary>画面に文字や枠を描くときに Unity が呼ぶ</summary>
        void OnGUI()
        {
            MakeStyles();
            float s = Screen.height / 1080f;

            DrawPopups(s);
            DrawSandbagPanel(s);
            DrawHelpPanel(s);
            DrawFighterPanel(s);
        }

        /// <summary>ダメージ数字・文字を描く(s: 画面の大きさに合わせる倍率)。対戦画面(BattleHUD)からも呼ぶ</summary>
        public static void DrawPopups(float s)
        {
            MakePopupStyles(s);
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var p in popups)
            {
                Vector3 sp = cam.WorldToScreenPoint(p.WorldPos + Vector3.up * p.Rise);
                if (sp.z < 0f) continue; // カメラの後ろ
                float pop = p.Scale * p.Size;
                popupStyle.fontSize = shadowStyle.fontSize = Mathf.RoundToInt(56 * s * pop);
                var rect = new Rect(sp.x - 250 * s, Screen.height - sp.y - 50 * s * pop, 500 * s, 100 * s * pop);
                shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * p.Alpha);
                GUI.Label(new Rect(rect.x + 3 * s, rect.y + 3 * s, rect.width, rect.height), p.Text, shadowStyle);
                popupStyle.normal.textColor = new Color(p.Color.r, p.Color.g, p.Color.b, p.Alpha);
                GUI.Label(rect, p.Text, popupStyle);
            }
        }

        /// <summary>右上のサンドバッグ情報を描く</summary>
        void DrawSandbagPanel(float s)
        {
            if (Sandbag == null) return;
            float w = 440 * s, x = Screen.width - w - 20 * s, y = 20 * s;
            GUI.Box(new Rect(x, y, w, 366 * s), GUIContent.none, boxStyle);
            float lx = x + 20 * s, ly = y + 12 * s, lh = 36 * s;

            GUI.Label(new Rect(lx, ly, w, lh), "<b>サンドバッグ</b>", labelStyle);
            ly += lh;
            float barW = w - 40 * s;
            DrawBar(new Rect(lx, ly + 6 * s, barW, 20 * s), (float)Sandbag.Hp / Sandbag.MaxHp, new Color(0.3f, 0.85f, 0.4f));
            GUI.Label(new Rect(lx, ly + 26 * s, w, lh), $"体力 {Sandbag.Hp} / {Sandbag.MaxHp}", labelStyle);
            ly += lh + 26 * s;
            GUI.Label(new Rect(lx, ly, w, lh), $"直前のダメージ: <b>{Sandbag.LastDamage}</b>", labelStyle); ly += lh;
            GUI.Label(new Rect(lx, ly, w, lh), $"連続技の合計: <b>{Sandbag.ComboDamage}</b>  ({Sandbag.ComboHits} ヒット)", labelStyle); ly += lh;
            int bonus = Fighter != null ? Fighter.LastComboBonusPercent : 0;
            string bonusLabel = Fighter != null ? Fighter.Kit.BonusLabel : "ボーナス";
            GUI.Label(new Rect(lx, ly, w, lh), $"{bonusLabel}: +{bonus}%", labelStyle); ly += lh;
            GUI.Label(new Rect(lx, ly, w, lh), $"状態: {Sandbag.StateText}", labelStyle); ly += lh;
            string guard = Sandbag.GuardEnabled ? "<color=#7fd0ff>ON</color>" : "OFF";
            GUI.Label(new Rect(lx, ly, w, lh), $"[F1 / SELECT+X] ガードする: {guard}", labelStyle); ly += lh;
            string attack = Sandbag.AttackEnabled ? "<color=#ff8080>ON</color>" : "OFF";
            GUI.Label(new Rect(lx, ly, w, lh), $"[F3 / SELECT+B] 殴ってくる: {attack}", labelStyle);
        }

        /// <summary>左上の操作説明を描く</summary>
        void DrawHelpPanel(float s)
        {
            float w = 440 * s, x = 20 * s, y = 20 * s;
            var lines = new List<string>
            {
                $"<b>トレーニング({(Fighter != null ? Fighter.Kit.CharacterName : "")})</b>",
                "移動: WASD / 左スティック",
                "弱攻撃: U / X",
                "中攻撃: I / Y",
                "強攻撃: O / B",
            };
            if (Fighter != null) lines.AddRange(Fighter.Kit.MoveHelpLines);  // スキル1〜3 とウルト(キャラごと)
            lines.AddRange(new[]
            {
                "ガード: J / LB・LT",
                "回避: K / A(+方向)",
                "リセット: R / START",
                "[F2 / SELECT+Y] ゲージ満タン",
                "[F4 / SELECT+A] 自分の体力を減らす",
                "[F5 / SELECT+RB] キャラ切り替え",
                "[F6 / SELECT+LB] CPU対戦へ",
            });
            float lh = 34 * s;
            GUI.Box(new Rect(x, y, w, lh * lines.Count + 24 * s), GUIContent.none, boxStyle);
            float ly = y + 12 * s;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(x + 20 * s, ly, w, lh), line, labelStyle);
                ly += lh;
            }
        }

        /// <summary>下の自分のキャラの体力・ゲージ・スキル状態を描く</summary>
        void DrawFighterPanel(float s)
        {
            if (Fighter == null) return;
            // カメラを高くしてキャラが画面の下の方に来たので、足に重ならないよう小さめの文字で詰めて描く
            var small = new GUIStyle(labelStyle)
            {
                fontSize = Mathf.RoundToInt(20 * s),    // このパネルだけ少し小さい文字(他のパネルは 24)
                padding = new RectOffset(),             // 余白なし(小さい画面で文字の下が切れないように)
                alignment = TextAnchor.MiddleLeft,      // 行の真ん中にそろえる
                clipping = TextClipping.Overflow,       // はみ出しても切らない
            };
            string[] status = Fighter.Kit.StatusLines();  // スキルの状態(1行に1つ)
            float lh = 26 * s;    // 1行の高さ(前は 34)
            float barH = 16 * s;  // バーの太さ(前は 20)
            float pad = 8 * s;    // パネルの上下の余白
            int lines = 2 + status.Length + 1;  // 体力・ウルト + スキルの行 + 動作
            float w = 900 * s, h = pad * 2 + lh * lines + 4 * s;
            float x = (Screen.width - w) / 2f, y = Screen.height - h - 12 * s;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, boxStyle);
            float lx = x + 20 * s, ly = y + pad;

            // 体力
            float hp = Fighter.MaxHp > 0 ? (float)Fighter.Hp / Fighter.MaxHp : 0f;
            GUI.Label(new Rect(lx, ly, 200 * s, lh), "体力", small);
            DrawBar(new Rect(lx + 170 * s, ly + (lh - barH) / 2f, w - 400 * s, barH), hp, new Color(0.3f, 0.85f, 0.4f));
            GUI.Label(new Rect(x + w - 210 * s, ly, 200 * s, lh), $"{Fighter.Hp} / {Fighter.MaxHp}", small);
            ly += lh + 2 * s;

            // ウルトゲージ
            float g = Fighter.Gauge / Tuning.Fighter.MaxGauge;
            bool full = g >= 1f;
            GUI.Label(new Rect(lx, ly, 200 * s, lh), full ? "<b><color=#ffd34d>ウルト OK!</color></b>" : "ウルト", small);
            DrawBar(new Rect(lx + 170 * s, ly + (lh - barH) / 2f, w - 210 * s, barH), g, full ? new Color(1f, 0.82f, 0.3f) : new Color(0.6f, 0.4f, 0.9f));
            ly += lh + 2 * s;

            // スキル(見やすいように 1行に1つずつ。中身はキャラごと)
            foreach (var line in status)
            {
                GUI.Label(new Rect(lx, ly, w, lh), line, small);
                ly += lh;
            }

            string state = Fighter.CurrentMoveName;
            if (Fighter.IsInvincible) state += " (無敵)";
            GUI.Label(new Rect(lx, ly, w, lh), string.IsNullOrEmpty(state) ? " " : $"動作: {state}", small);
        }

        /// <summary>横長のバー(体力・ゲージ)を描く(ratio: 0〜1 の割合)</summary>
        static void DrawBar(Rect r, float ratio, Color c)
        {
            DrawRect(r, new Color(0.15f, 0.15f, 0.15f));
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height), c);
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
