using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// 円形ステージ「月蝕の闘技場」の設定と、見えない壁の処理。
    /// </summary>
    public static class Arena
    {

        /// <summary>
        /// 見えない壁: 円の外に出ようとしたら円の内側に戻す。
        /// bodyRadius はキャラの体の太さ(半径)。
        /// </summary>
        public static Vector3 ClampInside(Vector3 pos, float bodyRadius)
        {
            float limit = Tuning.Stage.Radius - bodyRadius;
            Vector2 flat = new Vector2(pos.x, pos.z);
            if (flat.magnitude > limit)
            {
                flat = flat.normalized * limit;
                pos.x = flat.x;
                pos.z = flat.y;
            }
            return pos;
        }

        /// <summary>壁に押し付けられているか(後で「壁際のけぞり延長」に使う)</summary>
        public static bool IsAtWall(Vector3 pos, float bodyRadius)
        {
            Vector2 flat = new Vector2(pos.x, pos.z);
            return flat.magnitude >= Tuning.Stage.Radius - bodyRadius - 0.01f;  // 0.01m は計算の誤差のぶん
        }
    }
}
