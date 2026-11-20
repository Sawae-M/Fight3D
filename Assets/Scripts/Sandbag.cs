using LitMotion;
using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// トレーニング用のサンドバッグ(仕様書「11. サンドバッグ」)。
    /// - 体力は減るが 0 になっても倒れない
    /// - 2秒(120F)攻撃されないと全回復する
    /// - 直前のダメージ / 連続技の合計ダメージ / ヒット数 を記録する
    /// 殴られたときの揺れ・光り方は LitMotion で動かしている。
    /// </summary>
    public class Sandbag : MonoBehaviour
    {
        public const float BodyRadius = 0.4f;
        const int RecoverFrames = 120;   // 2秒
        const int ComboWindow = 40;      // 前のヒットから40F以内なら連続技が続く

        public int MaxHp = 1000;
        public int Hp { get; private set; }
        public int LastDamage { get; private set; }
        public int ComboDamage { get; private set; }
        public int ComboHits { get; private set; }

        /// <summary>のけぞり中(動けない時間が残っている)か</summary>
        public bool InHitstun => hitstunLeft > 0;

        public Transform Bag;          // 揺らす部分
        public Vector3 StartPosition;

        int hitstunLeft;
        int framesSinceHit = 9999;
        int hitstopLeft;

        Vector3 wobbleAxis = Vector3.right;
        Vector3 bagBaseScale = Vector3.one;
        Renderer[] renderers;
        Color[] baseColors;
        MotionHandle wobbleHandle, squashHandle, flashHandle;

        void Awake()
        {
            Hp = MaxHp;
        }

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].material.color;
            if (Bag != null) bagBaseScale = Bag.localScale;
        }

        /// <summary>次に当たる攻撃が「連続技の何ヒット目」になるか</summary>
        public int NextComboHitNumber()
        {
            bool comboContinues = InHitstun && framesSinceHit <= ComboWindow;
            return comboContinues ? ComboHits + 1 : 1;
        }

        /// <summary>攻撃を受ける</summary>
        public void TakeHit(int damage, int hitstun, int hitstop, Vector3 attackerPosition)
        {
            int hitNumber = NextComboHitNumber();
            if (hitNumber == 1)
            {
                ComboHits = 0;
                ComboDamage = 0;
            }
            ComboHits = hitNumber;
            ComboDamage += damage;
            LastDamage = damage;
            Hp = Mathf.Max(0, Hp - damage);   // 0 になっても倒れない

            hitstunLeft = hitstun;
            hitstopLeft = hitstop;
            framesSinceHit = 0;

            PlayHitEffect(damage, attackerPosition);
            TrainingHUD.AddPopup(damage, hitNumber, Bag != null ? Bag.position + Vector3.up * 1.9f : transform.position + Vector3.up * 2f);
        }

        /// <summary>殴られた見た目の動き(LitMotion)</summary>
        void PlayHitEffect(int damage, Vector3 attackerPosition)
        {
            if (Bag != null)
            {
                // 殴られた方向に倒れるように揺らす
                Vector3 away = transform.position - attackerPosition;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f) wobbleAxis = Vector3.Cross(Vector3.up, away.normalized);

                float strength = Mathf.Clamp(damage * 0.25f, 6f, 30f);
                wobbleHandle.TryCancel();
                wobbleHandle = LMotion.Punch.Create(0f, strength, 0.6f)
                    .Bind(this, (angle, self) => self.Bag.localRotation = Quaternion.AngleAxis(angle, self.wobbleAxis))
                    .AddTo(gameObject);

                // ぐにゃっと少しつぶれる
                squashHandle.TryCancel();
                Vector3 squash = new Vector3(0.12f, -0.1f, 0.12f) * Mathf.Clamp01(damage / 100f);
                squashHandle = LMotion.Punch.Create(bagBaseScale, squash, 0.3f)
                    .Bind(Bag, (scale, bag) => bag.localScale = scale)
                    .AddTo(gameObject);
            }

            // 白く光って元の色に戻る
            if (renderers != null)
            {
                flashHandle.TryCancel();
                flashHandle = LMotion.Create(1f, 0f, 0.15f)
                    .WithEase(Ease.OutQuad)
                    .Bind(this, (t, self) => self.SetFlash(t))
                    .AddTo(gameObject);
            }
        }

        void SetFlash(float t)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].material.color = Color.Lerp(baseColors[i], Color.white, t);
            }
        }

        /// <summary>トレーニングのリセット(Rキー)</summary>
        public void ResetState()
        {
            Hp = MaxHp;
            LastDamage = 0;
            ComboDamage = 0;
            ComboHits = 0;
            hitstunLeft = 0;
            framesSinceHit = 9999;
            transform.position = StartPosition;
        }

        // ゲームの進行は FixedUpdate(1秒に60回)で数える
        void FixedUpdate()
        {
            if (hitstopLeft > 0) { hitstopLeft--; return; } // ヒットストップ中は時間が止まる

            if (hitstunLeft > 0) hitstunLeft--;
            if (framesSinceHit < 9999) framesSinceHit++;

            if (framesSinceHit == RecoverFrames)
            {
                Hp = MaxHp; // 2秒攻撃されないと全回復
            }
        }
    }
}
