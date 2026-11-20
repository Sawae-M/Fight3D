using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// カゲツを操作するクラス(マイルストーン1)。
    /// - WASD で移動(W=相手に近づく、S=離れる、A/D=相手の周りを回る)
    /// - U / I / O で 弱 / 中 / 強 攻撃
    /// - 弱 → 中 → 強 は当たっていればつながる(連続技)
    /// - 技の終わり際 8F 以内に押したボタンは、技が終わった瞬間に出る(先行入力)
    /// - パッシブ「連刃」: 2ヒット目から1ヒットごとに +3%(最大 +24%)
    /// </summary>
    public class FighterController : MonoBehaviour
    {
        public const float BodyRadius = 0.35f;
        const float ForwardSpeed = 4.0f;   // 前歩き m/秒
        const float OtherSpeed = 3.0f;     // 後ろ・横歩き m/秒
        const int BufferFrames = 8;        // 先行入力の猶予
        const int HitstopFrames = 4;       // 当たった瞬間に少し止まる演出

        public Transform Target;           // 相手(今はサンドバッグ)
        public Sandbag TargetSandbag;
        public Transform SwordPivot;       // 剣の支点(見た目用)
        public Vector3 StartPosition;

        // 攻撃の状態
        AttackData current;                // 今出している技(null なら技を出していない)
        int attackFrame;                   // 技が始まってから何F目か
        bool hasHit;                       // 今の技が当たったか
        AttackType? bufferedAttack;        // 先行入力された技
        int bufferLeft;
        int hitstopLeft;

        // Update で読んだ入力を FixedUpdate に渡すための入れ物
        AttackType? pressedThisFrame;

        public bool IsAttacking => current != null;
        public int LastComboBonusPercent { get; private set; }

        void Update()
        {
            // 押した瞬間の入力は Update で拾う(FixedUpdate だと取りこぼすことがある)
            if (GameInput.Pressed(GameInput.Key.U)) pressedThisFrame = AttackType.Light;
            if (GameInput.Pressed(GameInput.Key.I)) pressedThisFrame = AttackType.Medium;
            if (GameInput.Pressed(GameInput.Key.O)) pressedThisFrame = AttackType.Heavy;

            if (GameInput.Pressed(GameInput.Key.R)) ResetTraining();

            UpdateSwordPose();
        }

        // ゲームの進行は 1秒に60回(1F ごと)
        void FixedUpdate()
        {
            if (pressedThisFrame.HasValue)
            {
                bufferedAttack = pressedThisFrame;
                bufferLeft = BufferFrames;
                pressedThisFrame = null;
            }

            if (hitstopLeft > 0) { hitstopLeft--; return; }

            // 先行入力された技が出せるなら出す
            if (bufferedAttack.HasValue)
            {
                if (CanStart(bufferedAttack.Value))
                {
                    StartAttack(bufferedAttack.Value);
                    bufferedAttack = null;
                }
                else if (--bufferLeft <= 0)
                {
                    bufferedAttack = null;
                }
            }

            if (current != null) TickAttack();
            else Move();

            FaceTarget();
        }

        bool CanStart(AttackType type)
        {
            if (current == null) return true;
            // 連続技: 今の技が当たっていて、次の技が今より強ければキャンセルしてつなげる
            return hasHit && type > current.Type;
        }

        void StartAttack(AttackType type)
        {
            current = AttackData.Get(type);
            attackFrame = 0;
            hasHit = false;
        }

        void TickAttack()
        {
            attackFrame++;

            bool active = attackFrame > current.Startup && attackFrame <= current.Startup + current.Active;
            if (active && !hasHit && TargetIsInReach(current.Reach))
            {
                Hit();
            }

            if (attackFrame >= current.TotalFrames)
            {
                current = null;
            }
        }

        bool TargetIsInReach(float reach)
        {
            if (Target == null) return false;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            // 体の表面からリーチ分届くか(相手の太さも少し含める)
            if (dist > BodyRadius + reach + Sandbag.BodyRadius * 0.5f) return false;
            // 前方 60度以内にいるか
            return dist < 0.01f || Vector3.Angle(transform.forward, to) <= 60f;
        }

        void Hit()
        {
            hasHit = true;
            if (TargetSandbag == null) return;

            // パッシブ「連刃」: 2ヒット目から1ヒットごとに +3%、最大 +24%
            int hitNumber = TargetSandbag.NextComboHitNumber();
            int bonusPercent = Mathf.Min((hitNumber - 1) * 3, 24);
            LastComboBonusPercent = bonusPercent;
            int damage = Mathf.RoundToInt(current.Damage * (1f + bonusPercent / 100f));

            TargetSandbag.TakeHit(damage, current.Hitstun, HitstopFrames, transform.position);
            hitstopLeft = HitstopFrames;
        }

        void Move()
        {
            if (Target == null) return;

            float v = 0f, h = 0f;
            if (GameInput.Held(GameInput.Key.W)) v += 1f;
            if (GameInput.Held(GameInput.Key.S)) v -= 1f;
            if (GameInput.Held(GameInput.Key.D)) h += 1f;
            if (GameInput.Held(GameInput.Key.A)) h -= 1f;
            if (v == 0f && h == 0f) return;

            Vector3 toTarget = Target.position - transform.position;
            toTarget.y = 0f;
            Vector3 forward = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 dir = (forward * v + right * h).normalized;
            float speed = v > 0f ? ForwardSpeed : OtherSpeed;
            Vector3 pos = transform.position + dir * speed * Time.fixedDeltaTime;

            // 相手とめり込まないようにする
            Vector3 fromTarget = pos - Target.position;
            fromTarget.y = 0f;
            float minDist = BodyRadius + Sandbag.BodyRadius;
            if (fromTarget.magnitude < minDist)
            {
                Vector3 push = fromTarget.sqrMagnitude > 0.0001f ? fromTarget.normalized : -forward;
                pos = new Vector3(Target.position.x, pos.y, Target.position.z) + push * minDist;
            }

            // 見えない壁
            transform.position = Arena.ClampInside(pos, BodyRadius);
        }

        void FaceTarget()
        {
            if (Target == null) return;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to);
        }

        void ResetTraining()
        {
            current = null;
            bufferedAttack = null;
            transform.position = StartPosition;
            if (TargetSandbag != null) TargetSandbag.ResetState();
        }

        // ---- ここから下は見た目(剣の振り)だけ ----

        static readonly Vector3 RestPos = new Vector3(0.38f, 1.3f, 0.05f);
        static readonly Quaternion RestRot = Quaternion.Euler(45f, 15f, 0f);

        void UpdateSwordPose()
        {
            if (SwordPivot == null) return;

            if (current == null)
            {
                SwordPivot.localRotation = Quaternion.Slerp(SwordPivot.localRotation, RestRot, 1f - Mathf.Exp(-15f * Time.deltaTime));
                SwordPivot.localPosition = Vector3.Lerp(SwordPivot.localPosition, RestPos, 1f - Mathf.Exp(-15f * Time.deltaTime));
                return;
            }

            // 技ごとの「構え → 振り終わり」の向き
            Quaternion wind, end;
            Vector3 windPos = RestPos, endPos = RestPos;
            switch (current.Type)
            {
                case AttackType.Light: // 突き
                    wind = Quaternion.Euler(10f, 5f, 0f);
                    end = Quaternion.Euler(5f, 0f, 0f);
                    windPos = RestPos + new Vector3(0f, 0f, -0.25f);
                    endPos = RestPos + new Vector3(-0.15f, 0f, 0.45f);
                    break;
                case AttackType.Medium: // 横なぎ
                    wind = Quaternion.Euler(10f, 85f, 0f);
                    end = Quaternion.Euler(10f, -75f, 0f);
                    break;
                default: // 振り下ろし
                    wind = Quaternion.Euler(-110f, 0f, 0f);
                    end = Quaternion.Euler(55f, 0f, 0f);
                    break;
            }

            int f = attackFrame;
            if (f <= current.Startup)
            {
                float k = current.Startup > 0 ? (float)f / current.Startup : 1f;
                SwordPivot.localRotation = Quaternion.Slerp(RestRot, wind, k);
                SwordPivot.localPosition = Vector3.Lerp(RestPos, windPos, k);
            }
            else if (f <= current.Startup + current.Active)
            {
                float k = (float)(f - current.Startup) / current.Active;
                SwordPivot.localRotation = Quaternion.Slerp(wind, end, k);
                SwordPivot.localPosition = Vector3.Lerp(windPos, endPos, k);
            }
            else
            {
                float k = (float)(f - current.Startup - current.Active) / current.Recovery;
                SwordPivot.localRotation = Quaternion.Slerp(end, RestRot, k * k);
                SwordPivot.localPosition = Vector3.Lerp(endPos, RestPos, k * k);
            }
        }
    }
}
