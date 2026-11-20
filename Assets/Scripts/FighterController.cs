using LitMotion;
using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// カゲツを操作するクラス。
    /// - WASD / 左スティック: 移動(W=相手に近づく、S=離れる、A/D=相手の周りを回る)
    /// - U / I / O: 弱 / 中 / 強(当たっていれば 弱→中→強 とつながる。スキルにもつながる)
    /// - P: 影穿ち / W+P: 三日月払い / S+P: 仮面解放 / O+P 同時押し: ウルト「冥月斬」
    /// - J: ガード(押している間) / K: 回避ステップ
    /// - 技の終わり際 8F 以内に押したボタンは、技が終わった瞬間に出る(先行入力)
    /// - パッシブ「連刃」: 2ヒット目から1ヒットごとに +3%(最大 +24%)
    /// ゲームの進行は FixedUpdate(1秒に60回)、見た目は Update で動かす。
    /// </summary>
    public class FighterController : MonoBehaviour
    {
        public const float BodyRadius = 0.35f;
        const float ForwardSpeed = 4.0f;    // 前歩き m/秒
        const float OtherSpeed = 3.0f;      // 後ろ・横歩き m/秒
        const int BufferFrames = 8;         // 先行入力の猶予
        const int HitstopFrames = 4;        // 当たった瞬間に少し止まる演出
        const int UltSimultaneousFrames = 3;// O+P を同時とみなす猶予
        const int UltEarlyConvertFrames = 3;// 強・スキルの出始めならウルトに切り替える
        public const float MaxGauge = 100f;

        // 回避ステップ(仕様書「6. 共通の動き」)
        const float DodgeDistance = 2.5f;
        const int DodgeFrames = 24;
        const int DodgeInvulnStart = 4, DodgeInvulnEnd = 12;
        const int DodgeCooldown = 20;

        public Transform Target;            // 相手(今はサンドバッグ)
        public Sandbag TargetSandbag;
        public Transform SwordPivot;        // 剣の支点(見た目用)
        public Vector3 StartPosition;
        public CameraRig CameraRig;         // リセット時にカメラを瞬間移動させる用

        // ---- 外から見る用(画面表示など) ----
        public float Gauge { get; private set; }
        public KagetsuSkills Skills { get; } = new KagetsuSkills();
        public bool IsGuarding { get; private set; }
        public bool IsDodging => dodgeFrame > 0;
        public bool IsInvincible => dodgeFrame >= DodgeInvulnStart && dodgeFrame <= DodgeInvulnEnd;
        public int DodgeCooldownLeft => dodgeCooldown;
        public string CurrentMoveName => current != null ? current.Name : (IsDodging ? "回避" : (IsGuarding ? "ガード" : ""));
        public int LastComboBonusPercent { get; private set; }

        // ---- 技の状態 ----
        MoveData current;                   // 今出している技(null なら出していない)
        Command currentCommand;
        int attackFrame;                    // 技が始まってから何F目か
        int curStartup;                     // 今の技の「出るまで」(仮面解放で短くなる)
        bool contacted;                     // 当たった or ガードされた(キャンセルできる)
        bool connected;                     // ヒットした(ウルトの演出に入る・隙が変わる)
        float lungeTravelled;
        Command? bufferedCommand;
        int bufferLeft;
        int hitstopLeft;
        int tick;                           // 経過フレーム

        // ---- 回避 ----
        int dodgeFrame;
        int dodgeCooldown;
        Vector3 dodgeDir;
        bool dodgeBuffered;
        int dodgeBufferLeft;

        // ---- Update → FixedUpdate への入力の受け渡し ----
        Command? pressedCommand;
        bool pressedDodge;
        int lastHeavyTick = -100, lastSkillTick = -100;

        FightInput input;
        Transform visual;
        Renderer maskRenderer;
        Color maskBaseColor;
        MotionHandle maskPulse;
        bool kamenVisualOn;

        void Awake()
        {
            input = new FightInput();
        }

        void Start()
        {
            visual = transform.Find("Visual");
            if (visual != null)
            {
                // 待機中の呼吸(LitMotion で Visual をゆっくり上下させる)
                LMotion.Create(0f, 0.025f, 1.1f)
                    .WithEase(Ease.InOutSine)
                    .WithLoops(-1, LoopType.Yoyo)
                    .Bind(visual, (y, v) => v.localPosition = new Vector3(0f, y, 0f))
                    .AddTo(gameObject);

                var mask = visual.Find("Mask");
                if (mask != null)
                {
                    maskRenderer = mask.GetComponent<Renderer>();
                    maskBaseColor = maskRenderer.material.color;
                }
            }
        }

        void OnDestroy()
        {
            input?.Dispose();
        }

        // ================================================================
        //  入力(Update)
        // ================================================================
        void Update()
        {
            // 押した瞬間の入力は Update で拾う(FixedUpdate だと取りこぼすことがある)
            if (input.Light.WasPressedThisFrame()) pressedCommand = Command.Light;
            if (input.Medium.WasPressedThisFrame()) pressedCommand = Command.Medium;
            if (input.Heavy.WasPressedThisFrame())
            {
                lastHeavyTick = tick;
                pressedCommand = (tick - lastSkillTick <= UltSimultaneousFrames) ? Command.Ult : Command.Heavy;
            }
            if (input.Skill.WasPressedThisFrame())
            {
                lastSkillTick = tick;
                if (tick - lastHeavyTick <= UltSimultaneousFrames) pressedCommand = Command.Ult;
                else
                {
                    // 方向キーでスキルが変わる: W+P = スキル2、S+P = スキル3、P だけ = スキル1
                    float y = input.MoveValue.y;
                    pressedCommand = y > 0.5f ? Command.Skill2 : (y < -0.5f ? Command.Skill3 : Command.Skill1);
                }
            }
            if (input.Dodge.WasPressedThisFrame()) pressedDodge = true;
            if (input.Reset.WasPressedThisFrame()) ResetTraining();

            UpdateVisuals();
        }

        // ================================================================
        //  ゲームの進行(FixedUpdate、1F ごと)
        // ================================================================
        void FixedUpdate()
        {
            tick++;
            if (pressedCommand.HasValue)
            {
                bufferedCommand = pressedCommand;
                bufferLeft = BufferFrames;
                pressedCommand = null;
            }
            if (pressedDodge)
            {
                dodgeBuffered = true;
                dodgeBufferLeft = BufferFrames;
                pressedDodge = false;
            }

            if (hitstopLeft > 0) { hitstopLeft--; return; }

            // スキルの時間を進める。仮面解放が終わったら呪印をまとめて爆発させる
            if (Skills.Tick(out int marks) && marks > 0) CurseBurst(marks);
            if (dodgeCooldown > 0) dodgeCooldown--;

            TryStartBuffered();

            if (dodgeFrame > 0) TickDodge();
            else if (current != null) TickMove();
            else
            {
                IsGuarding = input.Guard.IsPressed();
                if (!IsGuarding) Walk();
            }

            FaceTarget();
        }

        void TryStartBuffered()
        {
            if (bufferedCommand.HasValue)
            {
                var cmd = bufferedCommand.Value;
                // ゲージが足りないときの O+P は、ふつうの強攻撃として扱う
                if (cmd == Command.Ult && Gauge < MaxGauge) cmd = Command.Heavy;

                if (CanStart(cmd))
                {
                    StartMove(cmd);
                    bufferedCommand = null;
                }
                else if (--bufferLeft <= 0) bufferedCommand = null;
            }

            if (dodgeBuffered)
            {
                if (current == null && dodgeFrame == 0 && dodgeCooldown == 0)
                {
                    StartDodge();
                    dodgeBuffered = false;
                }
                else if (--dodgeBufferLeft <= 0) dodgeBuffered = false;
            }
        }

        // ---- 技を出せるか ----
        bool CanStart(Command cmd)
        {
            if (dodgeFrame > 0) return false;
            if (!Skills.CanUse(cmd)) return false;
            if (cmd == Command.Ult && Gauge < MaxGauge) return false;
            if (current == null) return true;

            // 強・スキルの出始めに O+P がそろったらウルトに切り替える
            if (cmd == Command.Ult && !contacted && attackFrame <= UltEarlyConvertFrames &&
                (currentCommand == Command.Heavy || currentCommand == Command.Skill1 || currentCommand == Command.Skill2))
                return true;

            if (!contacted) return false;

            // ここから下は「当たった技をキャンセルしてつなげる」ルール
            if (current.IsNormal)
            {
                if (cmd <= Command.Heavy) return KagetsuMoves.Normal(cmd).ComboRank > current.ComboRank; // 弱→中→強
                return true; // 通常技 → スキル・ウルト
            }
            if (currentCommand == Command.Skill1) return cmd == Command.Skill1 || cmd == Command.Ult; // 影穿ちの連続
            if (currentCommand == Command.Skill2) return cmd == Command.Ult;
            return false;
        }

        void StartMove(Command cmd)
        {
            if (current != null) EndMove(); // キャンセル

            MoveData move;
            switch (cmd)
            {
                case Command.Skill1: move = Skills.NextKageUgachi(); break;
                case Command.Skill2: move = KagetsuMoves.MikazukiBarai; break;
                case Command.Skill3: move = KagetsuMoves.KamenKaihou; break;
                case Command.Ult: move = KagetsuMoves.MeigetsuZan; Gauge = 0f; break;
                default: move = KagetsuMoves.Normal(cmd); break;
            }

            BeginMove(move, cmd);
            Skills.OnMoveStarted(cmd);
            if (cmd == Command.Skill3) TrainingHUD.AddTextPopup("仮面解放", new Color(0.9f, 0.3f, 0.4f), transform.position + Vector3.up * 2.3f);
        }

        void BeginMove(MoveData move, Command cmd)
        {
            current = move;
            currentCommand = cmd;
            attackFrame = 0;
            curStartup = Skills.ScaleStartup(move.Startup);
            contacted = false;
            connected = false;
            lungeTravelled = 0f;
            IsGuarding = false;
        }

        void EndMove()
        {
            Skills.OnMoveEnded(currentCommand);
            current = null;
        }

        int CurrentTotalFrames()
        {
            int recovery = (!connected && current.WhiffRecovery >= 0) ? current.WhiffRecovery : current.Recovery;
            if (current.ScriptedHits != null && current.ScriptedHits.Length > 0)
                return current.ScriptedHits[current.ScriptedHits.Length - 1].Frame + recovery;
            return curStartup + current.Active + recovery;
        }

        void TickMove()
        {
            attackFrame++;

            // 踏み込み
            if (current.LungeDistance > 0f && attackFrame >= current.LungeStart && attackFrame <= current.LungeEnd)
            {
                int frames = current.LungeEnd - current.LungeStart + 1;
                Lunge(current.LungeDistance / frames);
            }

            if (current.ScriptedHits != null)
            {
                // 決まったフレームに必ず当たる(ウルトの連続斬り)
                foreach (var sh in current.ScriptedHits)
                {
                    if (sh.Frame == attackFrame)
                    {
                        Strike(sh.Damage, sh.Hitstun, sh.Effect, false, true, 3);
                    }
                }
            }
            else
            {
                bool active = attackFrame > curStartup && attackFrame <= curStartup + current.Active;
                if (active && !contacted && current.Damage > 0 && TargetIsInReach(current.Reach, current.Arc))
                {
                    var outcome = Strike(current.Damage, current.Hitstun, current.Effect, current.GuardBreak, false, HitstopFrames);
                    contacted = true;
                    if (outcome == HitOutcome.Hit || outcome == HitOutcome.GuardCrushed) connected = true;

                    // ウルトが当たったら連続斬りの演出へ
                    if (current == KagetsuMoves.MeigetsuZan && outcome == HitOutcome.Hit)
                    {
                        BeginMove(KagetsuMoves.MeigetsuZanRush, Command.Ult);
                        connected = true;
                        return;
                    }
                }
            }

            if (attackFrame >= CurrentTotalFrames()) EndMove();
        }

        /// <summary>攻撃を当てる(連刃のボーナスとゲージもここで計算)</summary>
        HitOutcome Strike(int baseDamage, int hitstun, HitEffect effect, bool guardBreak, bool unblockable, int hitstop)
        {
            if (TargetSandbag == null) return HitOutcome.Hit;

            // パッシブ「連刃」: 2ヒット目から1ヒットごとに +3%、最大 +24%
            int hitNumber = TargetSandbag.NextComboHitNumber();
            int bonusPercent = Mathf.Min((hitNumber - 1) * 3, 24);
            int damage = Mathf.RoundToInt(baseDamage * (1f + bonusPercent / 100f));

            var result = TargetSandbag.TakeHit(new HitInfo
            {
                Damage = damage,
                Hitstun = hitstun,
                Hitstop = hitstop,
                Effect = effect,
                GuardBreak = guardBreak,
                Unblockable = unblockable,
                AttackerPosition = transform.position,
            });

            if (result.Outcome == HitOutcome.Hit)
            {
                LastComboBonusPercent = bonusPercent;
                Skills.OnHit();
                AddGauge(result.DamageDealt * 0.10f); // 与えたダメージの 10%
            }
            else
            {
                AddGauge(3f); // ガードされた
            }
            hitstopLeft = hitstop;
            return result.Outcome;
        }

        /// <summary>仮面解放の終わり: 呪印 1つにつき 15 ダメージ(連刃も効く)</summary>
        void CurseBurst(int marks)
        {
            TrainingHUD.AddTextPopup($"呪印 ×{marks}", new Color(0.8f, 0.3f, 1f),
                (Target != null ? Target.position : transform.position) + Vector3.up * 2.6f);
            Strike(marks * KagetsuSkills.CurseDamagePerMark, 12, HitEffect.Normal, false, true, 6);
        }

        public void AddGauge(float amount)
        {
            Gauge = Mathf.Clamp(Gauge + amount, 0f, MaxGauge);
        }

        // ================================================================
        //  移動・回避
        // ================================================================
        void Walk()
        {
            if (Target == null) return;

            Vector2 stick = input.MoveValue;
            if (stick.magnitude < 0.2f) return; // スティックの遊び
            float v = stick.y, h = stick.x;

            Vector3 forward = ForwardToTarget();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 dir = (forward * v + right * h).normalized;
            // 前に倒しているときは前歩き速度、それ以外は後ろ・横歩き速度。倒し具合で遅くもなる
            float speed = (v > Mathf.Abs(h) ? ForwardSpeed : OtherSpeed) * Mathf.Clamp01(stick.magnitude) * Skills.SpeedScale;
            MoveTo(transform.position + dir * speed * Time.fixedDeltaTime);
        }

        void StartDodge()
        {
            Vector2 stick = input.MoveValue;
            Vector3 forward = ForwardToTarget();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            // 方向なしなら後ろへ
            dodgeDir = stick.magnitude < 0.2f ? -forward : (forward * stick.y + right * stick.x).normalized;
            dodgeFrame = 0;
            IsGuarding = false;
            TickDodge();
        }

        void TickDodge()
        {
            dodgeFrame++;
            // はじめ速く、終わりゆっくり
            float prev = DodgeCurve((dodgeFrame - 1) / (float)DodgeFrames);
            float now = DodgeCurve(dodgeFrame / (float)DodgeFrames);
            MoveTo(transform.position + dodgeDir * (now - prev) * DodgeDistance);

            if (dodgeFrame >= DodgeFrames)
            {
                dodgeFrame = 0;
                dodgeCooldown = DodgeCooldown;
            }
        }

        static float DodgeCurve(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        void Lunge(float distance)
        {
            if (Target == null) return;
            Vector3 forward = ForwardToTarget();
            float contact = BodyRadius + Sandbag.BodyRadius + 0.05f;
            float dist = FlatDistanceToTarget();
            float step = Mathf.Min(distance, Mathf.Max(0f, dist - contact)); // 相手に当たる距離で止まる
            lungeTravelled += step;
            MoveTo(transform.position + forward * step);
        }

        /// <summary>めり込み防止と見えない壁を通してから移動する</summary>
        void MoveTo(Vector3 pos)
        {
            if (Target != null)
            {
                Vector3 fromTarget = pos - Target.position;
                fromTarget.y = 0f;
                float minDist = BodyRadius + Sandbag.BodyRadius;
                if (fromTarget.magnitude < minDist)
                {
                    Vector3 push = fromTarget.sqrMagnitude > 0.0001f ? fromTarget.normalized : -ForwardToTarget();
                    pos = new Vector3(Target.position.x, pos.y, Target.position.z) + push * minDist;
                }
            }
            transform.position = Arena.ClampInside(pos, BodyRadius);
        }

        Vector3 ForwardToTarget()
        {
            if (Target == null) return transform.forward;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
        }

        float FlatDistanceToTarget()
        {
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            return to.magnitude;
        }

        bool TargetIsInReach(float reach, float arc)
        {
            if (Target == null) return false;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            // 体の表面からリーチ分届くか(相手の太さも少し含める)
            if (dist > BodyRadius + reach + Sandbag.BodyRadius * 0.5f) return false;
            return dist < 0.01f || Vector3.Angle(transform.forward, to) <= arc * 0.5f;
        }

        void FaceTarget()
        {
            Vector3 f = ForwardToTarget();
            transform.rotation = Quaternion.LookRotation(f);
        }

        void ResetTraining()
        {
            current = null;
            bufferedCommand = null;
            dodgeFrame = 0;
            dodgeCooldown = 0;
            IsGuarding = false;
            Skills.ResetAll();
            transform.position = StartPosition;
            if (TargetSandbag != null) TargetSandbag.ResetState();
            FaceTarget();
            if (CameraRig != null) CameraRig.Snap();
        }

        // ================================================================
        //  見た目(剣の振り・回避の傾き・仮面の光)
        // ================================================================
        static readonly Vector3 RestPos = new Vector3(0.38f, 1.3f, 0.05f);
        static readonly Quaternion RestRot = Quaternion.Euler(45f, 15f, 0f);
        static readonly Quaternion GuardRot = Quaternion.Euler(-15f, -80f, 0f);
        static readonly Vector3 GuardPos = new Vector3(0.3f, 1.45f, 0.35f);

        void UpdateVisuals()
        {
            UpdateSwordPose();
            UpdateBodyLean();
            UpdateMask();
        }

        void UpdateSwordPose()
        {
            if (SwordPivot == null) return;
            float k15 = 1f - Mathf.Exp(-15f * Time.deltaTime);

            if (current == null)
            {
                Quaternion rot = IsGuarding ? GuardRot : RestRot;
                Vector3 pos = IsGuarding ? GuardPos : RestPos;
                SwordPivot.localRotation = Quaternion.Slerp(SwordPivot.localRotation, rot, IsGuarding ? 0.5f : k15);
                SwordPivot.localPosition = Vector3.Lerp(SwordPivot.localPosition, pos, IsGuarding ? 0.5f : k15);
                return;
            }

            var pose = current.Pose;
            Quaternion wind = Quaternion.Euler(pose.WindEuler), end = Quaternion.Euler(pose.EndEuler);
            Vector3 windPos = RestPos + pose.WindOffset, endPos = RestPos + pose.EndOffset;
            int f = attackFrame;

            if (current.ScriptedHits != null)
            {
                // 連続斬り: ヒットのたびに左右交互に振る
                int index = 0, segStart = 0, segEnd = current.ScriptedHits[0].Frame;
                for (int i = 0; i < current.ScriptedHits.Length; i++)
                {
                    if (f >= current.ScriptedHits[i].Frame)
                    {
                        index = i + 1;
                        segStart = current.ScriptedHits[i].Frame;
                        segEnd = i + 1 < current.ScriptedHits.Length ? current.ScriptedHits[i + 1].Frame : segStart + current.Recovery;
                    }
                }
                float k = Mathf.Clamp01((f - segStart) / (float)Mathf.Max(1, Mathf.Min(4, segEnd - segStart)));
                bool flip = index % 2 == 1;
                Quaternion a = flip ? end : wind, b = flip ? wind : end;
                SwordPivot.localRotation = Quaternion.Slerp(a, b, k);
                SwordPivot.localPosition = RestPos;
                return;
            }

            int startup = curStartup, active = Mathf.Max(1, current.Active);
            int recovery = Mathf.Max(1, CurrentTotalFrames() - startup - current.Active);
            if (f <= startup)
            {
                float k = startup > 0 ? (float)f / startup : 1f;
                SwordPivot.localRotation = Quaternion.Slerp(RestRot, wind, k);
                SwordPivot.localPosition = Vector3.Lerp(RestPos, windPos, k);
            }
            else if (f <= startup + current.Active)
            {
                float k = (float)(f - startup) / active;
                SwordPivot.localRotation = Quaternion.Slerp(wind, end, k);
                SwordPivot.localPosition = Vector3.Lerp(windPos, endPos, k);
            }
            else
            {
                float k = Mathf.Clamp01((float)(f - startup - current.Active) / recovery);
                SwordPivot.localRotation = Quaternion.Slerp(end, RestRot, k * k);
                SwordPivot.localPosition = Vector3.Lerp(endPos, RestPos, k * k);
            }
        }

        /// <summary>回避中は進む方向に体を傾ける</summary>
        void UpdateBodyLean()
        {
            if (visual == null) return;
            Quaternion want = Quaternion.identity;
            if (dodgeFrame > 0)
            {
                Vector3 localDir = transform.InverseTransformDirection(dodgeDir);
                want = Quaternion.Euler(localDir.z * 20f, 0f, -localDir.x * 20f);
            }
            visual.localRotation = Quaternion.Slerp(visual.localRotation, want, 1f - Mathf.Exp(-20f * Time.deltaTime));
        }

        /// <summary>仮面解放中は仮面が赤く脈打つ(LitMotion)</summary>
        void UpdateMask()
        {
            if (maskRenderer == null) return;
            bool on = Skills.KamenActive;
            if (on == kamenVisualOn) return;
            kamenVisualOn = on;

            maskPulse.TryCancel();
            if (on)
            {
                var red = new Color(0.9f, 0.1f, 0.15f);
                var dark = new Color(0.45f, 0.02f, 0.06f);
                maskPulse = LMotion.Create(0f, 1f, 0.35f)
                    .WithEase(Ease.InOutSine)
                    .WithLoops(-1, LoopType.Yoyo)
                    .Bind(maskRenderer, (t, r) => r.material.color = Color.Lerp(dark, red, t))
                    .AddTo(gameObject);
            }
            else
            {
                maskRenderer.material.color = maskBaseColor;
            }
        }
    }
}
