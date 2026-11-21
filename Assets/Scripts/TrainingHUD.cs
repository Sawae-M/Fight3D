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
    /// - 下: カゲツのウルトゲージとスキルの状態
    /// - トレーニング設定: F1 = サンドバッグのガード ON/OFF、F2 = ゲージ満タン
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

        GUIStyle boxStyle, labelStyle, popupStyle, shadowStyle;  // 文字や枠の見た目の設定

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

        /// <summary>画面の更新ごとに呼ばれる。トレーニング設定のキー(F1 / F2)を受け付ける</summary>
        void Update()
        {
            // トレーニング設定(キーボードの F1 / F2)
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame && Sandbag != null) Sandbag.GuardEnabled = !Sandbag.GuardEnabled;
            if (kb.f2Key.wasPressedThisFrame && Fighter != null) Fighter.AddGauge(Tuning.Fighter.MaxGauge);
        }

        /// <summary>文字の大きさや色の設定を作る(画面の高さに合わせて大きさを変える)</summary>
        void MakeStyles()
        {
            float s = Screen.height / 1080f;
            boxStyle = new GUIStyle(GUI.skin.box);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), richText = true };
            labelStyle.normal.textColor = Color.white;
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

        /// <summary>ダメージ数字・文字を描く(s: 画面の大きさに合わせる倍率)</summary>
        void DrawPopups(float s)
        {
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
            GUI.Box(new Rect(x, y, w, 330 * s), GUIContent.none, boxStyle);
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
            GUI.Label(new Rect(lx, ly, w, lh), $"連刃ボーナス: +{bonus}%", labelStyle); ly += lh;
            GUI.Label(new Rect(lx, ly, w, lh), $"状態: {Sandbag.StateText}", labelStyle); ly += lh;
            string guard = Sandbag.GuardEnabled ? "<color=#7fd0ff>ON</color>" : "OFF";
            GUI.Label(new Rect(lx, ly, w, lh), $"[F1] ガードする: {guard}", labelStyle);
        }

        /// <summary>左上の操作説明を描く</summary>
        void DrawHelpPanel(float s)
        {
            float w = 440 * s, x = 20 * s, y = 20 * s;
            string[] lines =
            {
                "<b>トレーニング(カゲツ)</b>",
                "移動: WASD / 左スティック",
                "弱攻撃: U / X",
                "中攻撃: I / Y",
                "強攻撃: O / B",
                "影穿ち: P",
                "三日月払い: W+P",
                "仮面解放: S+P",
                "冥月斬: O+P",
                "ガード: J",
                "回避: K(+方向)",
                "リセット: R",
                "[F2] ゲージ満タン",
            };
            float lh = 34 * s;
            GUI.Box(new Rect(x, y, w, lh * lines.Length + 24 * s), GUIContent.none, boxStyle);
            float ly = y + 12 * s;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(x + 20 * s, ly, w, lh), line, labelStyle);
                ly += lh;
            }
        }

        /// <summary>下のカゲツのゲージとスキル状態を描く</summary>
        void DrawFighterPanel(float s)
        {
            if (Fighter == null) return;
            var sk = Fighter.Skills;
            float w = 900 * s, h = 200 * s, x = (Screen.width - w) / 2f, y = Screen.height - h - 20 * s;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, boxStyle);
            float lx = x + 20 * s, ly = y + 10 * s, lh = 34 * s;

            // ウルトゲージ
            float g = Fighter.Gauge / Tuning.Fighter.MaxGauge;
            bool full = g >= 1f;
            GUI.Label(new Rect(lx, ly, 200 * s, lh), full ? "<b><color=#ffd34d>ウルト OK!</color></b>" : "ウルト", labelStyle);
            DrawBar(new Rect(lx + 170 * s, ly + 8 * s, w - 210 * s, 20 * s), g, full ? new Color(1f, 0.82f, 0.3f) : new Color(0.6f, 0.4f, 0.9f));
            ly += lh + 6 * s;

            // スキル
            string kage = $"影穿ち: 残り{sk.KageRemaining}回";
            string mika = sk.MikazukiCooldownLeft > 0 ? $"三日月払い: {sk.MikazukiCooldownLeft / 60f:0.0}秒" : "三日月払い: OK";
            string kamen = sk.KamenActive
                ? $"<color=#ff6070>仮面解放中 {sk.KamenLeft / 60f:0.0}秒 呪印{sk.CurseMarks}</color>"
                : (sk.KamenCooldownLeft > 0 ? $"仮面解放: {sk.KamenCooldownLeft / 60f:0.0}秒" : "仮面解放: OK");
            // 見やすいように 1行に1つずつ
            GUI.Label(new Rect(lx, ly, w, lh), kage, labelStyle); ly += lh;
            GUI.Label(new Rect(lx, ly, w, lh), mika, labelStyle); ly += lh;
            GUI.Label(new Rect(lx, ly, w, lh), kamen, labelStyle); ly += lh;

            string state = Fighter.CurrentMoveName;
            if (Fighter.IsInvincible) state += " (無敵)";
            GUI.Label(new Rect(lx, ly, w, lh), string.IsNullOrEmpty(state) ? " " : $"動作: {state}", labelStyle);
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
