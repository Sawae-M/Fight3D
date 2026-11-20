using Unity.Cinemachine;
using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// 背後カメラ(仕様書「4. カメラ」)を Cinemachine で作る。
    ///
    /// しくみ:
    /// - 「Anchor」: 自分のキャラの位置にあり、いつも相手の方を向いている見えない目印
    /// - Cinemachine カメラは Anchor の後方 4m・高さ 2.5m について行く(CinemachineFollow)
    /// - 「LookPoint」: 自分と相手の間(相手寄り)の点。カメラはここを画面中央に映す(CinemachineRotationComposer)
    /// </summary>
    [DefaultExecutionOrder(100)] // キャラが動いた後に目印を動かす
    public class CameraRig : MonoBehaviour
    {
        public Transform Player;
        public Transform Target;
        public float LookHeight = 1.0f;
        [Range(0f, 1f)] public float LookTowardTarget = 0.7f;

        public Transform Anchor { get; private set; }
        public Transform LookPoint { get; private set; }
        public CinemachineCamera Vcam { get; private set; }

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
            vcam.Lens.FieldOfView = 50f;

            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 2.5f, -4f); // 後方 4m・高さ 2.5m
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTarget;
            follow.TrackerSettings.PositionDamping = new Vector3(0.15f, 0.15f, 0.15f);
            follow.TrackerSettings.RotationDamping = new Vector3(0.2f, 0.2f, 0.2f);

            var composer = vcamGo.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.2f, 0.2f);

            vcam.Follow = rig.Anchor;
            vcam.LookAt = rig.LookPoint;
            vcam.PreviousStateIsValid = false; // 最初はなめらか移動なしで正しい位置へ
            rig.Vcam = vcam;
            return rig;
        }

        // キャラは FixedUpdate で動くので、目印も FixedUpdate で動かす
        void FixedUpdate() => UpdatePoints();

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
