using LitMotion;
using UnityEngine;

namespace Fight3D
{
    /// <summary>攻撃1回分の情報(攻撃する側 → 受ける側)</summary>
    public struct HitInfo
    {
        public int Damage;
        public int Hitstun;
        public int Hitstop;
        public HitEffect Effect;
        public bool GuardBreak;
        public bool Unblockable;
        public Vector3 AttackerPosition;
    }

    public enum HitOutcome { Hit, Guarded, GuardCrushed }

    /// <summary>攻撃の結果(受ける側 → 攻撃する側)</summary>
    public struct HitResult
    {
        public HitOutcome Outcome;
        public int DamageDealt;   // 実際に与えたダメージ(浮き中の追い打ちは 0.7倍)
        public int ComboHit;      // 連続技の何ヒット目か
    }

    /// <summary>
    /// トレーニング用のサンドバッグ(仕様書「11. サンドバッグ」)。
    /// - 体力は減るが 0 になっても倒れない。2秒(120F)攻撃されないと全回復
    /// - 直前のダメージ / 連続技の合計ダメージ / ヒット数 を記録
    /// - よろけ・浮き に対応。浮いている間の追い打ちはダメージ 0.7倍
    /// - トレーニング設定で「ガードする」をON にできる(F1キー)
    /// 殴られたときの揺れ・光り・浮きの見た目は LitMotion で動かしている。
    /// </summary>
    public class Sandbag : MonoBehaviour
    {
        public const float BodyRadius = 0.4f;
        const int RecoverFrames = 120;   // 2秒
        const int ComboWindow = 40;      // 前のヒットから40F以内なら連続技が続く
        const int StaggerFrames = 40;    // よろけ
        const int AirFrames = 36;        // 浮いている時間(仮)
        const int LandingFrames = 30;    // 着地後に動けない時間
        const float JuggleScale = 0.7f;  // 浮き中の追い打ち
        const int GuardCrushFrames = 30; // ガード崩し
        const float BlockstunScale = 0.6f;

        public int MaxHp = 1000;
        public int Hp { get; private set; }
        public int LastDamage { get; private set; }
        public int ComboDamage { get; private set; }
        public int ComboHits { get; private set; }

        /// <summary>トレーニング設定: ガードする</summary>
        public bool GuardEnabled;

        /// <summary>動けない時間が残っているか(のけぞり・よろけ・浮き・ガード崩し)</summary>
        public bool InHitstun => hitstunLeft > 0;
        public bool IsAirborne => airLeft > 0;
        public bool IsGuarding => GuardEnabled && hitstunLeft <= 0; // 動けるときはガードしている
        public string StateText
        {
            get
            {
                if (airLeft > 0) return "浮き";
                if (crushed) return "ガード崩れ";
                if (hitstunLeft > 0) return staggered ? "よろけ" : "のけぞり";
                if (blockstunLeft > 0) return "ガード中";
                return GuardEnabled ? "ガード待ち" : "待機";
            }
        }

        public Transform Bag;          // 揺らす部分
        public Vector3 StartPosition;

        int hitstunLeft;
        int blockstunLeft;
        int airLeft;
        bool staggered, crushed;
        int framesSinceHit = 9999;
        int hitstopLeft;

        Vector3 wobbleAxis = Vector3.right;
        Vector3 bagBaseScale = Vector3.one;
        Vector3 bagBasePos;
        float wobbleAngle;
        float liftHeight;
        Renderer[] renderers;
        Color[] baseColors;
        MotionHandle wobbleHandle, squashHandle, flashHandle, liftHandle;

        void Awake()
        {
            Hp = MaxHp;
        }

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].material.color;
            if (Bag != null)
            {
                bagBaseScale = Bag.localScale;
                bagBasePos = Bag.localPosition;
            }
        }

        /// <summary>次に当たる攻撃が「連続技の何ヒット目」になるか</summary>
        public int NextComboHitNumber()
        {
            bool comboContinues = InHitstun && framesSinceHit <= ComboWindow;
            return comboContinues ? ComboHits + 1 : 1;
        }

        /// <summary>攻撃を受ける</summary>
        public HitResult TakeHit(HitInfo info)
        {
            var result = new HitResult();
            Vector3 popupPos = (Bag != null ? Bag.position : transform.position) + Vector3.up * 1.9f;
            framesSinceHit = 0;
            hitstopLeft = info.Hitstop;

            // ---- ガード ----
            if (IsGuarding && !info.Unblockable)
            {
                if (info.GuardBreak)
                {
                    // ガード崩し: ガードが解けて 30F 動けない(ダメージは無し)
                    result.Outcome = HitOutcome.GuardCrushed;
                    blockstunLeft = 0;
                    hitstunLeft = GuardCrushFrames;
                    crushed = true;
                    staggered = false;
                    ComboHits = 0;
                    ComboDamage = 0;
                    PlayHitEffect(40, info.AttackerPosition, new Color(1f, 0.5f, 0.2f));
                    TrainingHUD.AddTextPopup("ガードクラッシュ!", new Color(1f, 0.55f, 0.2f), popupPos);
                }
                else
                {
                    result.Outcome = HitOutcome.Guarded;
                    blockstunLeft = Mathf.Max(6, Mathf.RoundToInt(info.Hitstun * BlockstunScale));
                    PlayHitEffect(8, info.AttackerPosition, new Color(0.4f, 0.7f, 1f));
                    TrainingHUD.AddTextPopup("GUARD", new Color(0.5f, 0.8f, 1f), popupPos);
                }
                return result;
            }

            // ---- ヒット ----
            int hitNumber = NextComboHitNumber();
            if (hitNumber == 1)
            {
                ComboHits = 0;
                ComboDamage = 0;
            }

            int damage = info.Damage;
            if (airLeft > 0) damage = Mathf.RoundToInt(damage * JuggleScale); // 浮き中の追い打ち

            ComboHits = hitNumber;
            ComboDamage += damage;
            LastDamage = damage;
            Hp = Mathf.Max(0, Hp - damage);   // 0 になっても倒れない
            crushed = false;
            blockstunLeft = 0;

            switch (info.Effect)
            {
                case HitEffect.Launch:
                    airLeft = AirFrames;
                    hitstunLeft = AirFrames + LandingFrames;
                    staggered = false;
                    PlayLaunch();
                    break;
                case HitEffect.Stagger:
                    staggered = true;
                    hitstunLeft = Mathf.Max(hitstunLeft, airLeft > 0 ? airLeft + LandingFrames : StaggerFrames);
                    break;
                default:
                    if (airLeft > 0) hitstunLeft = Mathf.Max(hitstunLeft, airLeft + LandingFrames); // 空中では落ちるまで続く
                    else { hitstunLeft = info.Hitstun; staggered = false; }
                    break;
            }

            PlayHitEffect(damage, info.AttackerPosition, Color.white);
            TrainingHUD.AddPopup(damage, hitNumber, popupPos);

            result.Outcome = HitOutcome.Hit;
            result.DamageDealt = damage;
            result.ComboHit = hitNumber;
            return result;
        }

        /// <summary>殴られた見た目の動き(LitMotion)</summary>
        void PlayHitEffect(int strengthValue, Vector3 attackerPosition, Color flashColor)
        {
            if (Bag != null)
            {
                // 殴られた方向に倒れるように揺らす
                Vector3 away = transform.position - attackerPosition;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f) wobbleAxis = Vector3.Cross(Vector3.up, away.normalized);

                float strength = Mathf.Clamp(strengthValue * 0.25f, 4f, 30f);
                wobbleHandle.TryCancel();
                wobbleHandle = LMotion.Punch.Create(0f, strength, 0.6f)
                    .Bind(this, (angle, self) => self.wobbleAngle = angle)
                    .AddTo(gameObject);

                // ぐにゃっと少しつぶれる
                squashHandle.TryCancel();
                Vector3 squash = new Vector3(0.12f, -0.1f, 0.12f) * Mathf.Clamp01(strengthValue / 100f);
                squashHandle = LMotion.Punch.Create(bagBaseScale, squash, 0.3f)
                    .Bind(Bag, (scale, bag) => bag.localScale = scale)
                    .AddTo(gameObject);
            }

            // 光って元の色に戻る
            if (renderers != null)
            {
                flashHandle.TryCancel();
                flashHandle = LMotion.Create(1f, 0f, 0.15f)
                    .WithEase(Ease.OutQuad)
                    .Bind(this, (t, self) => self.SetFlash(t, flashColor))
                    .AddTo(gameObject);
            }
        }

        /// <summary>浮きの見た目: 袋が上に跳ね上がって落ちてくる</summary>
        void PlayLaunch()
        {
            if (Bag == null) return;
            float airSeconds = AirFrames / 60f;
            liftHandle.TryCancel();
            liftHandle = LMotion.Create(0f, 1f, airSeconds)
                .Bind(this, (t, self) => self.liftHeight = 4f * 1.3f * t * (1f - t)) // 放物線(最高 1.3m)
                .AddTo(gameObject);
        }

        void SetFlash(float t, Color flashColor)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].material.color = Color.Lerp(baseColors[i], flashColor, t);
            }
        }

        /// <summary>トレーニングのリセット</summary>
        public void ResetState()
        {
            Hp = MaxHp;
            LastDamage = 0;
            ComboDamage = 0;
            ComboHits = 0;
            hitstunLeft = 0;
            blockstunLeft = 0;
            airLeft = 0;
            staggered = crushed = false;
            framesSinceHit = 9999;
            liftHandle.TryCancel();
            liftHeight = 0f;
            transform.position = StartPosition;
        }

        // ゲームの進行は FixedUpdate(1秒に60回)で数える
        void FixedUpdate()
        {
            if (hitstopLeft > 0) { hitstopLeft--; return; } // ヒットストップ中は時間が止まる

            if (airLeft > 0) airLeft--;
            if (hitstunLeft > 0)
            {
                hitstunLeft--;
                if (hitstunLeft == 0) staggered = crushed = false;
            }
            if (blockstunLeft > 0) blockstunLeft--;
            if (framesSinceHit < 9999) framesSinceHit++;

            if (framesSinceHit == RecoverFrames)
            {
                Hp = MaxHp; // 2秒攻撃されないと全回復
            }
        }

        // 見た目(揺れ + 浮き)をまとめて反映
        void LateUpdate()
        {
            if (Bag == null) return;
            Bag.localRotation = Quaternion.AngleAxis(wobbleAngle, wobbleAxis);
            Bag.localPosition = bagBasePos + Vector3.up * liftHeight;
        }
    }
}
