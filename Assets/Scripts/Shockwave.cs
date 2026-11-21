using LitMotion;
using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// ゴウガの岩砕きの衝撃波。地面をまっすぐ 5m 進み、途中で相手に触れたら1回だけ当たる。
    /// 動きは FixedUpdate(1秒に60回)で進める。出てくるときの見た目だけ LitMotion。
    /// </summary>
    public class Shockwave : MonoBehaviour
    {
        // 数値(速さ・距離・幅)は Tuning.cs の Tuning.Gouga にある

        FighterController owner;  // 衝撃波を出したキャラ(当たったらこのキャラの攻撃として計算する)
        MoveData move;            // 元の技(ダメージ・効果)
        Transform target;         // 相手
        Vector3 direction;        // 進む向き(長さ1、水平)
        float travelled;          // ここまでに進んだ距離(m)
        bool hasHit;              // もう当たったか(1回しか当たらない)

        /// <summary>衝撃波を出す(origin = 出る位置、direction = 進む向き)</summary>
        public static Shockwave Spawn(FighterController owner, MoveData move, Vector3 origin, Vector3 direction, Transform target)
        {
            float width = Tuning.Gouga.ShockwaveHalfWidth * 2f;  // 見た目の横幅(m)
            var go = DollBuilder.Part(PrimitiveType.Cube, "Shockwave", null,
                new Vector3(origin.x, 0.15f, origin.z), new Vector3(width, 0.3f, 0.5f), new Color(0.75f, 0.5f, 0.25f));
            go.transform.rotation = Quaternion.LookRotation(direction);

            var wave = go.AddComponent<Shockwave>();
            wave.owner = owner;
            wave.move = move;
            wave.target = target;
            wave.direction = direction;

            // 出てくる瞬間に横に広がる(見た目だけ)
            LMotion.Create(0.2f, 1f, 0.1f)
                .WithEase(Ease.OutBack)
                .Bind(go.transform, (k, t) => t.localScale = new Vector3(width * k, 0.3f, 0.5f))
                .AddTo(go);
            return wave;
        }

        /// <summary>1秒に60回(1F ごと)呼ばれる。前に進んで、相手に触れたら当てる</summary>
        void FixedUpdate()
        {
            float step = Tuning.Gouga.ShockwaveSpeed / Tuning.Game.FramesPerSecond;  // 1F に進む距離(m)
            transform.position += direction * step;
            travelled += step;

            if (!hasHit && owner != null && target != null)
            {
                Vector3 to = target.position - transform.position;
                to.y = 0f;
                if (to.magnitude <= Tuning.Gouga.ShockwaveHalfWidth + Tuning.Sandbag.BodyRadius)
                {
                    hasHit = true;
                    owner.ShockwaveHit(move);
                }
            }

            // 5m 進んだら消える。出したキャラがいなくなったとき(キャラ切り替え)も消える
            if (travelled >= Tuning.Gouga.ShockwaveRange || owner == null) Destroy(gameObject);
        }
    }
}
