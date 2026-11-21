using LitMotion;
using UnityEngine;

namespace Fight3D
{
    /// <summary>攻撃1回分の情報(攻撃する側 → 受ける側)</summary>
    public struct HitInfo
    {
        public int Damage;  // ダメージ(連刃ボーナス込み)
        public int Hitstun;  // のけぞり時間(F)
        public int Hitstop;  // ヒットストップ(F)
        public HitEffect Effect;  // のけぞり / よろけ / 浮き
        public bool GuardBreak;  // ガード崩しか
        public bool Unblockable;  // ガードできない攻撃か(ウルトの連続斬り・呪印・つかみ技など)
        public bool IsGrab;  // つかみ技か(受け止めで反撃できない)
        public bool IsUlt;  // ウルトの攻撃か(受け止めで反撃できない)
        public Vector3 AttackerPosition;  // 攻撃した人の位置(揺れる向きに使う)
    }

    /// <summary>攻撃がどうなったか(ヒット / ガードされた / ガードを崩した / よけられた / 受け止められた)</summary>
    public enum HitOutcome { Hit, Guarded, GuardCrushed, Dodged, Countered }

    /// <summary>攻撃の結果(受ける側 → 攻撃する側)</summary>
    public struct HitResult
    {
        public HitOutcome Outcome;  // ヒット / ガード / ガード崩し
        public int DamageDealt;   // 実際に与えたダメージ(浮き中の追い打ちは 0.7倍)
        public int ComboHit;      // 連続技の何ヒット目か
    }

    /// <summary>
    /// トレーニング用のサンドバッグ(仕様書「11. サンドバッグ」)。
    /// - 体力は減るが 0 になっても倒れない。2秒(120F)攻撃されないと全回復
    /// - 直前のダメージ / 連続技の合計ダメージ / ヒット数 を記録
    /// - よろけ・浮き に対応。浮いている間の追い打ちはダメージ 0.7倍
    /// - トレーニング設定で「ガードする」をON にできる(F1キー)
    /// - トレーニング設定で「殴ってくる」をON にできる(F3キー)。近づくと赤く光って殴ってくる(受け止め・ガード・回避の練習用)
    /// - 剛腕引きでつかまれると、引き寄せられる
    /// 殴られたときの揺れ・光り・浮きの見た目は LitMotion で動かしている。
    /// </summary>
    public class Sandbag : MonoBehaviour, IFightTarget
    {
        // 数値(よろけ・浮きの時間など)は Tuning.cs の Tuning.Sandbag にある

        public int MaxHp = Tuning.Sandbag.MaxHp;  // 最大体力(値は Tuning.cs)
        public int Hp { get; private set; }  // 今の体力
        public int LastDamage { get; private set; }  // 直前のダメージ
        public int ComboDamage { get; private set; }  // 今の連続技の合計ダメージ
        public int ComboHits { get; private set; }  // 今の連続技のヒット数
        public float BodyRadius => Tuning.Sandbag.BodyRadius;  // 体の太さ(半径 m。値は Tuning.cs)

        /// <summary>トレーニング設定: ガードする</summary>
        public bool GuardEnabled;

        /// <summary>トレーニング設定: 近づくと殴ってくる</summary>
        public bool AttackEnabled;

        /// <summary>殴る相手(自分のキャラ)</summary>
        public FighterController Opponent;

        /// <summary>動けない時間が残っているか(のけぞり・よろけ・浮き・ガード崩し)</summary>
        public bool InHitstun => hitstunLeft > 0;
        public bool IsAirborne => airLeft > 0;  // 浮いているか
        public bool IsGuarding => GuardEnabled && hitstunLeft <= 0; // 動けるときはガードしている
        public string StateText
        {
            get
            {
                if (airLeft > 0) return "浮き";
                if (crushed) return "ガード崩れ";
                if (hitstunLeft > 0) return staggered ? "よろけ" : "のけぞり";
                if (blockstunLeft > 0) return "ガード中";
                if (windupLeft > 0) return "<color=#ff7070>攻撃の構え</color>";
                return GuardEnabled ? "ガード待ち" : "待機";
            }
        }

        public Transform Bag;          // 揺らす部分
        public Vector3 StartPosition;  // リセットで戻る位置

        int hitstunLeft;  // 動けない時間の残り(F)
        int blockstunLeft;  // ガード硬直の残り(F)
        int airLeft;  // 浮いている時間の残り(F)
        bool staggered, crushed;  // よろけ中か / ガード崩れ中か(表示用)
        int framesSinceHit = 9999;  // 最後に攻撃されてからのフレーム数(9999 = ずっと前)
        int hitstopLeft;  // ヒットストップの残り(F)
        int windupLeft;  // 殴るまでの残り(F)。0 なら構えていない
        int attackCooldownLeft;  // 次に殴れるまでの残り(F)
        int pullLeft;  // 引き寄せの残り(F)
        Vector3 pullFrom, pullTo;  // 引き寄せの始まりと終わりの位置
        int pullTotal;  // 引き寄せ全体のフレーム数

        Vector3 wobbleAxis = Vector3.right;  // 揺れる回転軸
        Vector3 bagBaseScale = Vector3.one;  // 袋の元の大きさ
        Vector3 bagBasePos;  // 袋の元の位置
        float wobbleAngle;  // 今の揺れの角度(度)
        float liftHeight;  // 今の浮きの高さ(m)
        Renderer[] renderers;  // 色を変える部品
        Color[] baseColors;  // 各部品の元の色
        MotionHandle wobbleHandle, squashHandle, flashHandle, liftHandle;  // LitMotion のアニメ(揺れ・つぶれ・光り・浮き)。新しく殴られたら止めてやり直す

        /// <summary>作られた瞬間に1回だけ呼ばれる。体力を満タンにする</summary>
        void Awake()
        {
            Hp = MaxHp;
        }

        /// <summary>最初のフレームの前に1回だけ呼ばれる。元の色や大きさを覚えておく</summary>
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
            bool comboContinues = InHitstun && framesSinceHit <= Tuning.Sandbag.ComboWindow;
            return comboContinues ? ComboHits + 1 : 1;
        }

        /// <summary>攻撃を受ける</summary>
        public HitResult TakeHit(HitInfo info)
        {
            var result = new HitResult();
            Vector3 popupPos = (Bag != null ? Bag.position : transform.position) + Vector3.up * 1.9f;
            framesSinceHit = 0;
            hitstopLeft = info.Hitstop;
            CancelWindup(); // 殴られたら、殴ろうとしていたのをやめる

            // ---- ガード ----
            if (IsGuarding && !info.Unblockable)
            {
                if (info.GuardBreak)
                {
                    // ガード崩し: ガードが解けてしばらく動けない(時間は Tuning.cs、ダメージは無し)
                    result.Outcome = HitOutcome.GuardCrushed;
                    blockstunLeft = 0;
                    hitstunLeft = Tuning.Sandbag.GuardCrushFrames;
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
                    blockstunLeft = Mathf.Max(Tuning.Sandbag.MinBlockstun, Mathf.RoundToInt(info.Hitstun * Tuning.Sandbag.BlockstunScale));
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
            if (airLeft > 0) damage = Mathf.RoundToInt(damage * Tuning.Sandbag.JuggleScale); // 浮き中の追い打ち

            ComboHits = hitNumber;
            ComboDamage += damage;
            LastDamage = damage;
            Hp = Mathf.Max(0, Hp - damage);   // 0 になっても倒れない
            crushed = false;
            blockstunLeft = 0;

            switch (info.Effect)
            {
                case HitEffect.Launch:
                    airLeft = Tuning.Sandbag.AirFrames;
                    hitstunLeft = Tuning.Sandbag.AirFrames + Tuning.Sandbag.LandingFrames;
                    staggered = false;
                    PlayLaunch();
                    break;
                case HitEffect.Stagger:
                    staggered = true;
                    hitstunLeft = Mathf.Max(hitstunLeft, airLeft > 0 ? airLeft + Tuning.Sandbag.LandingFrames : Tuning.Sandbag.StaggerFrames);
                    break;
                default:
                    if (airLeft > 0) hitstunLeft = Mathf.Max(hitstunLeft, airLeft + Tuning.Sandbag.LandingFrames); // 空中では落ちるまで続く
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

        /// <summary>剛腕引きでつかまれたとき: frames F かけて dest まで引き寄せられる</summary>
        public void StartPull(Vector3 dest, int frames)
        {
            pullFrom = transform.position;
            pullTo = Arena.ClampInside(new Vector3(dest.x, transform.position.y, dest.z), Tuning.Sandbag.BodyRadius);
            pullTotal = Mathf.Max(1, frames);
            pullLeft = pullTotal;
        }

        /// <summary>引き寄せを1F進める</summary>
        void TickPull()
        {
            if (pullLeft <= 0) return;
            pullLeft--;
            float t = 1f - (float)pullLeft / pullTotal;          // 進み具合(0〜1)
            float eased = 1f - (1f - t) * (1f - t);               // はじめ速く、終わりゆっくり
            transform.position = Vector3.Lerp(pullFrom, pullTo, eased);
        }

        /// <summary>「殴ってくる」設定のときの動きを1F進める(近づいたら構えて、時間がたったら殴る)</summary>
        void TickAttack()
        {
            if (!AttackEnabled || Opponent == null) { CancelWindup(); return; }
            if (hitstunLeft > 0 || airLeft > 0 || pullLeft > 0) { CancelWindup(); return; } // 動けない間は殴れない

            if (windupLeft > 0)
            {
                windupLeft--;
                if (windupLeft == 0)
                {
                    // 殴る(少し離れていても、構えた時の距離 + 0.3m までは届く)
                    if (DistanceTo(Opponent.transform) <= Tuning.SandbagAttack.Range + 0.3f)
                    {
                        Opponent.TakeHit(new HitInfo
                        {
                            Damage = Tuning.SandbagAttack.Damage,
                            Hitstun = Tuning.SandbagAttack.Hitstun,
                            Hitstop = Tuning.SandbagAttack.Hitstop,
                            Effect = HitEffect.Normal,
                            AttackerPosition = transform.position,
                        });
                    }
                    attackCooldownLeft = Tuning.SandbagAttack.Interval;
                    PlayPunch();
                }
                return;
            }

            if (attackCooldownLeft > 0) { attackCooldownLeft--; return; }
            if (DistanceTo(Opponent.transform) <= Tuning.SandbagAttack.Range)
            {
                windupLeft = Tuning.SandbagAttack.Windup;
                PlayWindup();
            }
        }

        /// <summary>殴ろうとしていたのをやめる</summary>
        void CancelWindup()
        {
            if (windupLeft <= 0) return;
            windupLeft = 0;
            attackCooldownLeft = Tuning.SandbagAttack.Interval;
            if (renderers != null) SetFlash(0f, Color.white);
        }

        /// <summary>相手との水平な距離(m)</summary>
        float DistanceTo(Transform other)
        {
            Vector3 d = other.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>殴る前の合図: だんだん赤く光る(LitMotion)</summary>
        void PlayWindup()
        {
            if (renderers == null) return;
            flashHandle.TryCancel();
            flashHandle = LMotion.Create(0f, 0.8f, Tuning.SandbagAttack.Windup / 60f)
                .WithEase(Ease.InQuad)
                .Bind(this, (t, self) => self.SetFlash(t, new Color(1f, 0.15f, 0.1f)))
                .AddTo(gameObject);
        }

        /// <summary>殴った見た目: 相手の方へ倒れ込むように揺れる(LitMotion)</summary>
        void PlayPunch()
        {
            if (renderers != null) { flashHandle.TryCancel(); SetFlash(0f, Color.white); }
            if (Bag == null || Opponent == null) return;
            Vector3 toward = Opponent.transform.position - transform.position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.0001f) wobbleAxis = Vector3.Cross(Vector3.up, toward.normalized);
            wobbleHandle.TryCancel();
            wobbleHandle = LMotion.Punch.Create(0f, 25f, 0.5f)  // 25度 倒れ込む
                .Bind(this, (angle, self) => self.wobbleAngle = angle)
                .AddTo(gameObject);
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
            float airSeconds = Tuning.Sandbag.AirFrames / 60f;
            liftHandle.TryCancel();
            liftHandle = LMotion.Create(0f, 1f, airSeconds)
                .Bind(this, (t, self) => self.liftHeight = 4f * 1.3f * t * (1f - t)) // 放物線(最高 1.3m)
                .AddTo(gameObject);
        }

        /// <summary>光り具合を反映する(t: 0 = 元の色、1 = 光る色)</summary>
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
            windupLeft = 0;
            attackCooldownLeft = 0;
            pullLeft = 0;
            if (renderers != null) { flashHandle.TryCancel(); SetFlash(0f, Color.white); }
            liftHandle.TryCancel();
            liftHeight = 0f;
            transform.position = StartPosition;
        }

        /// <summary>1秒に60回(1F ごと)呼ばれる。動けない時間などを数える</summary>
        void FixedUpdate()
        {
            if (hitstopLeft > 0) { hitstopLeft--; return; } // ヒットストップ中は時間が止まる

            TickPull();

            if (airLeft > 0) airLeft--;
            if (hitstunLeft > 0)
            {
                hitstunLeft--;
                if (hitstunLeft == 0) staggered = crushed = false;
            }
            if (blockstunLeft > 0) blockstunLeft--;
            if (framesSinceHit < 9999) framesSinceHit++;

            if (framesSinceHit == Tuning.Sandbag.RecoverFrames)
            {
                Hp = MaxHp; // 2秒攻撃されないと全回復
            }

            TickAttack();
        }

        /// <summary>画面の更新の最後に呼ばれる。揺れと浮きの見た目をまとめて反映する</summary>
        void LateUpdate()
        {
            if (Bag == null) return;
            Bag.localRotation = Quaternion.AngleAxis(wobbleAngle, wobbleAxis);
            Bag.localPosition = bagBasePos + Vector3.up * liftHeight;
        }
    }
}
