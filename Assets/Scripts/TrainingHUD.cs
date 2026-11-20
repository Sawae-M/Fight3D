using System.Collections.Generic;
using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// トレーニング画面の表示。
    /// - サンドバッグの上に出るダメージ数字
    /// - 右上: 直前のダメージ / 連続技の合計 / ヒット数 / 連刃ボーナス
    /// - 左上: 操作説明
    /// (仮の表示なので Unity の古い GUI 機能 OnGUI で簡単に描いている)
    /// </summary>
    public class TrainingHUD : MonoBehaviour
    {
        class Popup
        {
            public string Text;
            public Vector3 WorldPos;
            public float Age;
            public Color Color;
        }

        const float PopupLife = 0.9f;
        static readonly List<Popup> popups = new List<Popup>();

        public Sandbag Sandbag;
        public FighterController Fighter;

        GUIStyle boxStyle, labelStyle, popupStyle, shadowStyle;

        public static void AddPopup(int damage, int hitNumber, Vector3 worldPos)
        {
            // ヒット数が多いほど黄色→赤っぽく
            float t = Mathf.Clamp01((hitNumber - 1) / 8f);
            var color = Color.Lerp(new Color(1f, 0.95f, 0.4f), new Color(1f, 0.35f, 0.25f), t);
            // 同じ場所に重ならないよう少しずらす
            Vector3 jitter = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.2f), 0f);
            popups.Add(new Popup { Text = damage.ToString(), WorldPos = worldPos + jitter, Age = 0f, Color = color });
        }

        void OnDestroy() { popups.Clear(); }

        void Update()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                popups[i].Age += Time.deltaTime;
                popups[i].WorldPos += Vector3.up * 1.2f * Time.deltaTime;
                if (popups[i].Age > PopupLife) popups.RemoveAt(i);
            }
        }

        void MakeStyles()
        {
            float s = Screen.height / 1080f;
            boxStyle = new GUIStyle(GUI.skin.box);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), richText = true };
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

        void OnGUI()
        {
            MakeStyles();
            float s = Screen.height / 1080f;

            // ---- ダメージ数字 ----
            var cam = Camera.main;
            if (cam != null)
            {
                foreach (var p in popups)
                {
                    Vector3 sp = cam.WorldToScreenPoint(p.WorldPos);
                    if (sp.z < 0f) continue; // カメラの後ろ
                    float alpha = 1f - Mathf.Clamp01((p.Age - PopupLife * 0.5f) / (PopupLife * 0.5f));
                    float pop = 1f + Mathf.Max(0f, 0.15f - p.Age) * 3f; // 出た瞬間だけ少し大きく
                    var rect = new Rect(sp.x - 150 * s, Screen.height - sp.y - 50 * s * pop, 300 * s, 100 * s * pop);
                    shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
                    GUI.Label(new Rect(rect.x + 3 * s, rect.y + 3 * s, rect.width, rect.height), p.Text, shadowStyle);
                    popupStyle.normal.textColor = new Color(p.Color.r, p.Color.g, p.Color.b, alpha);
                    GUI.Label(rect, p.Text, popupStyle);
                }
            }

            // ---- 右上: サンドバッグの情報 ----
            if (Sandbag != null)
            {
                float w = 420 * s, x = Screen.width - w - 20 * s, y = 20 * s;
                GUI.Box(new Rect(x, y, w, 250 * s), GUIContent.none, boxStyle);
                float lx = x + 20 * s, ly = y + 12 * s, lh = 38 * s;

                GUI.Label(new Rect(lx, ly, w, lh), "<b>サンドバッグ</b>", labelStyle);
                ly += lh;
                // 体力バー
                float barW = w - 40 * s;
                float ratio = (float)Sandbag.Hp / Sandbag.MaxHp;
                DrawRect(new Rect(lx, ly + 6 * s, barW, 20 * s), new Color(0.15f, 0.15f, 0.15f));
                DrawRect(new Rect(lx, ly + 6 * s, barW * ratio, 20 * s), new Color(0.3f, 0.85f, 0.4f));
                GUI.Label(new Rect(lx, ly + 26 * s, w, lh), $"体力 {Sandbag.Hp} / {Sandbag.MaxHp}", labelStyle);
                ly += lh + 26 * s;
                GUI.Label(new Rect(lx, ly, w, lh), $"直前のダメージ: <b>{Sandbag.LastDamage}</b>", labelStyle);
                ly += lh;
                GUI.Label(new Rect(lx, ly, w, lh), $"連続技の合計: <b>{Sandbag.ComboDamage}</b>  ({Sandbag.ComboHits} ヒット)", labelStyle);
                ly += lh;
                int bonus = Fighter != null ? Fighter.LastComboBonusPercent : 0;
                GUI.Label(new Rect(lx, ly, w, lh), $"連刃ボーナス: +{bonus}%", labelStyle);
            }

            // ---- 左上: 操作説明 ----
            {
                float w = 420 * s, x = 20 * s, y = 20 * s;
                GUI.Box(new Rect(x, y, w, 210 * s), GUIContent.none, boxStyle);
                float lx = x + 20 * s, ly = y + 12 * s, lh = 36 * s;
                GUI.Label(new Rect(lx, ly, w, lh), "<b>トレーニング(カゲツ)</b>", labelStyle); ly += lh;
                GUI.Label(new Rect(lx, ly, w, lh), "W A S D : 移動", labelStyle); ly += lh;
                GUI.Label(new Rect(lx, ly, w, lh), "U / I / O : 弱 / 中 / 強", labelStyle); ly += lh;
                GUI.Label(new Rect(lx, ly, w, lh), "弱→中→強 で連続技", labelStyle); ly += lh;
                GUI.Label(new Rect(lx, ly, w, lh), "R : 位置をリセット", labelStyle);
            }
        }

        static void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
