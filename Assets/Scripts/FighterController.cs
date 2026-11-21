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
        public const float BodyRadius = 0.35f;  // 体の太さ(半径 m)。壁やめり込みの判定に使う
        const float ForwardSpeed = 4.0f;    // 前歩き m/秒
        const float OtherSpeed = 3.0f;      // 後ろ・横歩き m/秒
        const int BufferFrames = 8;         // 先行入力の猶予
        const int HitstopFrames = 4;        // 当たった瞬間に少し止まる演出
        const int UltSimultaneousFrames = 3;// O+P を同時とみなす猶予
        const int UltEarlyConvertFrames = 3;// 強・スキルの出始めならウルトに切り替える
        public const float MaxGauge = 100f;  // ウルトゲージの最大(仕様書: 100)

        // 回避ステップ(仕様書「6. 共通の動き」)
        const float DodgeDistance = 2.5f;
        const int DodgeFrames = 24;  // 回避の全体の時間(F)
        const int DodgeInvulnStart = 4, DodgeInvulnEnd = 12;  // 無敵になる時間(4F目〜12F目)
        const int DodgeCooldown = 20;  // 回避が終わってから次に使えるまで(F)

        public Transform Target;            // 相手(今はサンドバッグ)
        public Sandbag TargetSandbag;  // 相手のサンドバッグ(攻撃を当てる先)
        public Transform SwordPivot;        // 剣の支点(見た目用)
        public Vector3 StartPosition;  // リセットで戻る位置
        public CameraRig CameraRig;         // リセット時にカメラを瞬間移動させる用

        // ---- 外から見る用(画面表示など) ----
        public float Gauge { get; private set; }
        public KagetsuSkills Skills { get; } = new KagetsuSkills();  // カゲツのスキルの状態
        public bool IsGuarding { get; private set; }  // ガード中か
        public bool IsDodging => dodgeFrame > 0;  // 回避中か
        public bool IsInvincible => dodgeFrame >= DodgeInvulnStart && dodgeFrame <= DodgeInvulnEnd;  // 無敵中か(回避の途中)
        public int DodgeCooldownLeft => dodgeCooldown;  // 回避が次に使えるまでの残り(F)
        public string CurrentMoveName => current != null ? current.Name : (IsDodging ? "回避" : (IsGuarding ? "ガード" : ""));  // 今の動作の名前(画面表示用)
        public int LastComboBonusPercent { get; private set; }  // 直前のヒットの連刃ボーナス(%)

        // ---- 技の状態 ----
        MoveData current;                   // 今出している技(null なら出していない)
        Command currentCommand;  // 今の技を出したボタン
        int attackFrame;                    // 技が始まってから何F目か
        int curStartup;                     // 今の技の「出るまで」(仮面解放で短くなる)
        bool contacted;                     // 当たった or ガードされた(キャンセルできる)
        bool connected;                     // ヒットした(ウルトの演出に入る・隙が変わる)
        float lungeTravelled;  // 今の技で踏み込んだ距離(m)
        Command? bufferedCommand;  // 先行入力された技(なければ null)
        int bufferLeft;  // 先行入力が消えるまでの残り(F)
        int hitstopLeft;  // ヒットストップの残り(F)。この間は時間が止まる
        int tick;                           // 経過フレーム

        // ---- 回避 ----
        int dodgeFrame;
        int dodgeCooldown;  // 回避が次に使えるまでの残り(F)
        Vector3 dodgeDir;  // 回避で進む向き
        bool dodgeBuffered;  // 回避が先行入力されているか
        int dodgeBufferLeft;  // 回避の先行入力が消えるまでの残り(F)

        // ---- Update → FixedUpdate への入力の受け渡し ----
        Command? pressedCommand;
        bool pressedDodge;  // このフレームで回避ボタンが押されたか
        int lastHeavyTick = -100, lastSkillTick = -100;  // 強・スキルを最後に押したフレーム(O+P 同時押しの判定用)

        FightInput input;  // 入力
        Transform visual;  // 見た目の部品のまとめ役(Visual)
        Renderer maskRenderer;  // 仮面の見た目(仮面解放で色を変える)
        Color maskBaseColor;  // 仮面の元の色
        MotionHandle maskPulse;  // 仮面が脈打つ LitMotion のアニメ
        bool kamenVisualOn;  // 仮面解放の見た目が今ついているか

        /// <summary>作られた瞬間に1回だけ呼ばれる。入力の準備をする</summary>
        void Awake()
        {
            input = new FightInput();
        }

        /// <summary>最初のフレームの前に1回だけ呼ばれる。待機の呼吸アニメと仮面の色の準備をする</summary>
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

        /// <summary>消えるときに呼ばれる。入力を片付ける</summary>
        void OnDestroy()
        {
            input?.Dispose();
        }

        // ================================================================
        //  入力(Update)
        // ================================================================
        /// <summary>画面の更新ごとに呼ばれる。押した瞬間のボタンを拾い、見た目を更新する</summary>
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
        /// <summary>1秒に60回(1F ごと)呼ばれる。ゲームの進行(技・移動・回避・ガード)をここで進める</summary>
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

        /// <summary>先行入力された技や回避を、出せるタイミングになったら出す</summary>
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

        /// <summary>今この技を出せるかを判定する(使えない時間・ゲージ・連続技のつながりのルール)</summary>
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

        /// <summary>ボタンに対応する技を選んで出し始める</summary>
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

        /// <summary>技の状態をリセットして、指定した技の1F目から始める</summary>
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

        /// <summary>今の技を終わらせる</summary>
        void EndMove()
        {
            Skills.OnMoveEnded(currentCommand);
            current = null;
        }

        /// <summary>今の技の全体のフレーム数(当たったか外したかで隙が変わる技もある)</summary>
        int CurrentTotalFrames()
        {
            int recovery = (!connected && current.WhiffRecovery >= 0) ? current.WhiffRecovery : current.Recovery;
            if (current.ScriptedHits != null && current.ScriptedHits.Length > 0)
                return current.ScriptedHits[current.ScriptedHits.Length - 1].Frame + recovery;
            return curStartup + current.Active + recovery;
        }

        /// <summary>技を1F進める(踏み込み・当たり判定・ウルトの演出への切り替え)</summary>
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

        /// <summary>ウルトゲージを増やす(0〜100 の範囲に収める)</summary>
        public void AddGauge(float amount)
        {
            Gauge = Mathf.Clamp(Gauge + amount, 0f, MaxGauge);
        }

        // ================================================================
        //  移動・回避
        // ================================================================
        /// <summary>スティック/WASD の入力で歩く(相手の方向を基準に前後左右)</summary>
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

        /// <summary>回避ステップを始める(方向は押している移動キー。なければ後ろ)</summary>
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

        /// <summary>回避ステップを1F進める</summary>
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

        /// <summary>回避の進み具合(0〜1)。はじめ速く、終わりゆっくりになる曲線</summary>
        static float DodgeCurve(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>技の踏み込みで前に進む(相手に当たる距離で止まる)</summary>
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

        /// <summary>自分から相手への水平な向き(長さ1)</summary>
        Vector3 ForwardToTarget()
        {
            if (Target == null) return transform.forward;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
        }

        /// <summary>自分と相手の水平な距離(m)</summary>
        float FlatDistanceToTarget()
        {
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            return to.magnitude;
        }

        /// <summary>相手が技の届く範囲(距離と前方の角度)にいるか</summary>
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

        /// <summary>いつも相手の方を向く(ロックオン)</summary>
        void FaceTarget()
        {
            Vector3 f = ForwardToTarget();
            transform.rotation = Quaternion.LookRotation(f);
        }

        /// <summary>トレーニングのリセット(位置・技・スキル・サンドバッグを最初の状態に戻す)</summary>
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
        static readonly Quaternion RestRot = Quaternion.Euler(45f, 15f, 0f);  // 構えの剣の向き(前下がり)
        static readonly Quaternion GuardRot = Quaternion.Euler(-15f, -80f, 0f);  // ガードの剣の向き(体の前で横に)
        static readonly Vector3 GuardPos = new Vector3(0.3f, 1.45f, 0.35f);  // ガードの剣の支点の位置

        /// <summary>見た目をまとめて更新する</summary>
        void UpdateVisuals()
        {
            UpdateSwordPose();
            UpdateBodyLean();
            UpdateMask();
        }

        /// <summary>今の技の何F目かに合わせて剣の向きと位置を動かす(見た目だけ)</summary>
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
