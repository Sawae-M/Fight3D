using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Fight3D
{
    /// <summary>
    /// 本番の3Dモデル(Blender で作った FBX)のアニメを、キャラの状態に合わせて再生するクラス。
    /// - FighterController の状態(技・ガード・回避・やられ・移動など)を毎フレーム見て、流すアニメ(クリップ)を選ぶ
    /// - 技・回避・やられのアニメは「今その動作の何F目か」に時間を合わせる(ヒットストップで止まれば、アニメも止まる)
    /// - 待機・歩き・ガードはくり返し再生する
    /// Animator Controller は使わず、Playables でクリップを直接流す(コードだけで完結させるため)。
    /// アニメの数値は見た目だけなので、Tuning.cs ではなくこのファイルの上にまとめている。
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        // ---- 見た目の調整値(すべて [仮]) ----
        const float CrossFade = 0.10f;        // 待機・歩きなどに切り替えるときに混ぜる時間(秒)
        const float ActionCrossFade = 0.04f;  // 技・回避・やられに切り替えるときに混ぜる時間(秒)。タイミングが大事なので短く
        const float WalkSpeedThreshold = 0.4f;// これより速く動いていたら歩きのアニメにする(m/秒)
        const int StaggerAnimFrames = 30;     // のけぞりがこのF以上なら「よろけ」のアニメにする

        /// <summary>技の名前(MoveData.Name)→ アニメのクリップ名。Blender の kagetsu_anims.py / gouga_anims.py と同じ名前</summary>
        static readonly Dictionary<string, string> MoveClips = new Dictionary<string, string>
        {
            { "弱攻撃", "Light" }, { "中攻撃", "Medium" }, { "強攻撃", "Heavy" },
            // カゲツ
            { "影穿ち", "KageUgachi" }, { "影穿ち(3)", "KageUgachi3" }, { "三日月払い", "MikazukiBarai" },
            { "仮面解放", "KamenKaihou" }, { "冥月斬", "MeigetsuZan" }, { "冥月斬・連斬", "MeigetsuZanRush" },
            // ゴウガ
            { "岩砕き", "Gansai" }, { "剛腕引き", "Gouwan" }, { "受け止め", "Uketome" }, { "受け止め・反撃", "UketomeCounter" },
            { "大地崩し", "DaichiKuzushi" }, { "大地崩し・連打", "DaichiKuzushiRush" },
        };

        FighterController fighter;  // 状態を見るキャラ
        TrailRenderer weaponTrail;  // 剣の軌跡(カゲツだけ。無ければ null)
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();  // 名前 → クリップ

        PlayableGraph graph;               // アニメを流す仕組み
        AnimationMixerPlayable mixer;      // 2つのクリップを混ぜる(切り替えをなめらかにする)
        AnimationClipPlayable now, before; // 今のクリップと、切り替える前のクリップ
        string nowName;                    // 今のクリップの名前
        float fade = 1f, fadeTime;         // 切り替えの進み具合(0→1)と、混ぜる時間
        float freeTime;                    // くり返しアニメの再生時間(秒)
        Vector3 lastPos;                   // 前のフレームの位置(歩く速さを測る)

        /// <summary>今流しているアニメの名前(テストや画面表示用)</summary>
        public string CurrentClip => nowName;

        /// <summary>
        /// モデルにアニメの再生役を付ける。animator はモデルの Animator(Humanoid のアバター入り)、
        /// clipList はモデルの FBX に入っているクリップ全部。
        /// </summary>
        public static CharacterAnimator Attach(GameObject root, Animator animator, FighterController fighter, IEnumerable<AnimationClip> clipList)
        {
            var ca = root.AddComponent<CharacterAnimator>();
            ca.fighter = fighter;
            ca.weaponTrail = root.GetComponentInChildren<TrailRenderer>();
            foreach (var c in clipList)
                if (c != null && !c.name.StartsWith("__preview__")) ca.clips[c.name] = c;  // プレビュー用の余分なクリップは除く

            animator.applyRootMotion = false;  // 位置の移動はゲームのプログラムがする
            ca.graph = PlayableGraph.Create(root.name + "_Anim");
            ca.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);  // 時間はこちらで決めて、毎フレーム Evaluate する
            ca.mixer = AnimationMixerPlayable.Create(ca.graph, 2);
            var output = AnimationPlayableOutput.Create(ca.graph, "Anim", animator);
            output.SetSourcePlayable(ca.mixer);
            ca.lastPos = root.transform.position;
            ca.Play("Idle", ActionCrossFade);
            return ca;
        }

        /// <summary>消えるときに呼ばれる。アニメの仕組みを片付ける</summary>
        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>
        /// 毎フレームの最後に呼ばれる(キャラの動きが決まった後)。状態に合うアニメを選び、時間を合わせてポーズを作る
        /// </summary>
        void LateUpdate()
        {
            if (fighter == null || !graph.IsValid()) return;
            float dt = Time.deltaTime;

            // 動いている速さ(体の向きに対する前後・左右)
            Vector3 vel = dt > 0f ? (transform.position - lastPos) / dt : Vector3.zero;
            lastPos = transform.position;
            Vector3 local = transform.InverseTransformDirection(vel);

            string clip;
            float? syncedTime = null;  // 動作の何F目かに合わせる時間(秒)。null ならくり返し再生
            const float fps = Tuning.Game.FramesPerSecond;

            var opp = fighter.OpponentFighter;
            if (fighter.KnockedDown)
            {
                clip = "KO";
            }
            else if (opp != null && opp.KnockedDown)
            {
                clip = "Win";  // 相手が倒れていたら勝ちポーズ
            }
            else if (fighter.InHitstun)
            {
                if (fighter.LastHitEffect == HitEffect.Launch || fighter.IsAirborne) clip = "Launch";
                else if (fighter.LastHitEffect == HitEffect.Stagger) clip = "Stagger";
                else clip = "Hit";
                syncedTime = fighter.FramesSinceHitTaken / fps;
            }
            else if (fighter.IsDodging)
            {
                clip = "Dodge";
                syncedTime = fighter.DodgeFrameNow / fps;
            }
            else if (fighter.CurrentMove != null && MoveClips.TryGetValue(fighter.CurrentMove.Name, out var moveClip))
            {
                clip = moveClip;
                syncedTime = fighter.AttackFrameNow / fps;
            }
            else if (fighter.IsGuarding)
            {
                clip = "Guard";
            }
            else if (new Vector2(local.x, local.z).magnitude > WalkSpeedThreshold)
            {
                if (Mathf.Abs(local.z) >= Mathf.Abs(local.x)) clip = local.z > 0f ? "WalkForward" : "WalkBack";
                else clip = local.x > 0f ? "StrafeRight" : "StrafeLeft";
            }
            else
            {
                clip = "Idle";
            }
            if (!clips.ContainsKey(clip)) clip = "Idle";  // そのキャラに無いアニメなら待機にする
            if (!clips.ContainsKey(clip)) return;         // 待機のアニメも無いときは何もしない

            bool isAction = syncedTime.HasValue;
            // 剣の軌跡は、技を出している間だけ出す(歩き・やられ・回避では出さない)
            if (weaponTrail != null) weaponTrail.emitting = fighter.CurrentMove != null && !fighter.InHitstun;
            // 同じ技をもう一度出したとき(時間が巻き戻った)も、最初から流し直す
            bool restarted = isAction && clip == nowName && now.IsValid() && syncedTime.Value + 0.001f < now.GetTime();
            if (clip != nowName || restarted) Play(clip, isAction ? ActionCrossFade : CrossFade);

            if (nowName == null) return;

            // 時間を合わせる
            var c = clips[nowName];
            if (isAction)
            {
                now.SetTime(Mathf.Min(syncedTime.Value, c.length));  // 動作がアニメより長いときは最後のポーズで止める
            }
            else
            {
                freeTime += dt;
                now.SetTime(c.isLooping ? Mathf.Repeat(freeTime, Mathf.Max(0.01f, c.length)) : Mathf.Min(freeTime, c.length));
            }

            // 前のクリップから今のクリップへ、なめらかに切り替える
            fade = fadeTime > 0f ? Mathf.Min(1f, fade + dt / fadeTime) : 1f;
            mixer.SetInputWeight(0, fade);
            mixer.SetInputWeight(1, 1f - fade);
            graph.Evaluate(0f);
        }

        /// <summary>クリップを切り替える。前のクリップは blend 秒かけて消えていく</summary>
        void Play(string clipName, float blend)
        {
            if (!clips.TryGetValue(clipName, out var c)) return;
            if (before.IsValid())
            {
                graph.Disconnect(mixer, 1);
                before.Destroy();
            }
            if (now.IsValid())
            {
                graph.Disconnect(mixer, 0);
                before = now;
                graph.Connect(before, 0, mixer, 1);
            }
            now = AnimationClipPlayable.Create(graph, c);
            now.SetApplyFootIK(false);
            graph.Connect(now, 0, mixer, 0);
            nowName = clipName;
            freeTime = 0f;
            fade = 0f;
            fadeTime = blend;
        }
    }
}
