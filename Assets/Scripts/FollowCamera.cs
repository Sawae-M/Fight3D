using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// 背後カメラ(仕様書「4. カメラ」)。
    /// 自分のキャラの後方 4m・高さ 2.5m から、相手の方を向いて追いかける。
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform Player;
        public Transform Target;
        public float Distance = 4f;
        public float Height = 2.5f;
        public float LookHeight = 1.0f;
        public float Smooth = 10f;

        void LateUpdate()
        {
            if (Player == null || Target == null) return;

            Vector3 toTarget = Target.position - Player.position;
            toTarget.y = 0f;
            Vector3 forward = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Player.forward;

            Vector3 wantPos = Player.position - forward * Distance + Vector3.up * Height;
            float k = 1f - Mathf.Exp(-Smooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, wantPos, k);

            // 相手が画面の中央付近に来るように、相手(と自分の間の少し相手寄り)を見る
            Vector3 lookAt = Vector3.Lerp(Player.position, Target.position, 0.7f) + Vector3.up * LookHeight;
            Quaternion wantRot = Quaternion.LookRotation(lookAt - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, k);
        }

        /// <summary>すぐに正しい位置へ移動する(開始時用)</summary>
        public void SnapNow()
        {
            float s = Smooth;
            Smooth = 10000f;
            LateUpdate();
            Smooth = s;
        }
    }
}
