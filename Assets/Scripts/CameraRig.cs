using Unity.Cinemachine;
using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// 背後カメラ(仕様書「4. カメラ」)を Cinemachine で作る。
    ///
    /// しくみ:
    /// - 「Anchor」: 自分のキャラの位置にあり、いつも相手の方を向いている見えない目印
    /// - Cinemachine カメラは Anchor の右後ろ・上について行く(CinemachineFollow。距離は Tuning.Camera)
    /// - 「LookPoint」: 自分と相手の間(相手寄り)の点。カメラはここを画面中央に映す(CinemachineRotationComposer)
    /// </summary>
    [DefaultExecutionOrder(100)] // キャラが動いた後に目印を動かす
    public class CameraRig : MonoBehaviour
    {
        public Transform Player;  // 自分のキャラ(カメラはこの後ろにつく)
        public Transform Target;  // 相手(カメラはこちらを向く)
        public float LookHeight = Tuning.Camera.LookHeight;  // 見る点の高さ(m)。値は Tuning.cs
        [Range(0f, 1f)] public float LookTowardTarget = Tuning.Camera.LookTowardTarget;  // 見る点を自分と相手の間のどこにするか(0 = 自分、1 = 相手)

        public Transform Anchor { get; private set; }  // カメラがついて行く目印
        public Transform LookPoint { get; private set; }  // カメラが画面中央に映す点
        public CinemachineCamera Vcam { get; private set; }  // Cinemachine のカメラ本体

        /// <summary>カメラ一式を作る</summary>
        public static CameraRig Create(Camera mainCamera, Transform player, Transform target)
        {
            // 実際に映すカメラには CinemachineBrain を付ける
            if (mainCamera.GetComponent<CinemachineBrain>() == null)
                mainCamera.gameObject.AddComponent<CinemachineBrain>();

            var rigGo = new GameObject("CameraRig");
            var rig = rigGo.AddComponent<CameraRig>();
            rig.Player = player;
            rig.Target = target;

            rig.Anchor = new GameObject("Anchor").transform;
            rig.Anchor.SetParent(rigGo.transform, false);
            rig.LookPoint = new GameObject("LookPoint").transform;
            rig.LookPoint.SetParent(rigGo.transform, false);
            rig.UpdatePoints();

            // Cinemachine カメラ
            var vcamGo = new GameObject("CM FightCamera");
            vcamGo.transform.SetParent(rigGo.transform, false);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = Tuning.Camera.FieldOfView;  // 視野角

            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(Tuning.Camera.SideOffset, Tuning.Camera.Height, -Tuning.Camera.Distance); // 右に少しずれた、後方・上(値は Tuning.cs)
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTarget;
            follow.TrackerSettings.PositionDamping = Vector3.one * Tuning.Camera.PositionDamping;
            follow.TrackerSettings.RotationDamping = Vector3.one * Tuning.Camera.RotationDamping;

            var composer = vcamGo.AddComponent<CinemachineRotationComposer>();
            composer.Damping = Vector2.one * Tuning.Camera.RotationDamping;

            vcam.Follow = rig.Anchor;
            vcam.LookAt = rig.LookPoint;
            vcam.PreviousStateIsValid = false; // 最初はなめらか移動なしで正しい位置へ
            rig.Vcam = vcam;
            return rig;
        }

        // キャラは FixedUpdate で動くので、目印も FixedUpdate で動かす
        void FixedUpdate() => UpdatePoints();

        /// <summary>目印(Anchor と LookPoint)を、今のキャラと相手の位置に合わせて動かす</summary>
        void UpdatePoints()
        {
            if (Player == null || Target == null || Anchor == null) return;

            Vector3 toTarget = Target.position - Player.position;
            toTarget.y = 0f;
            Anchor.position = Player.position;
            if (toTarget.sqrMagnitude > 0.0001f) Anchor.rotation = Quaternion.LookRotation(toTarget);

            LookPoint.position = Vector3.Lerp(Player.position, Target.position, LookTowardTarget) + Vector3.up * LookHeight;
        }

        /// <summary>リセット時などにカメラを瞬間移動させる</summary>
        public void Snap()
        {
            UpdatePoints();
            if (Vcam != null) Vcam.PreviousStateIsValid = false;
        }
    }
}
