using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// 攻撃を当てられる相手(サンドバッグ・キャラ)が持つ決まりごと。
    /// FighterController はこれを通して攻撃を当てるので、相手がサンドバッグでも CPU のキャラでも同じように戦える。
    /// </summary>
    public interface IFightTarget
    {
        /// <summary>相手の位置・向き(MonoBehaviour が持っている transform)</summary>
        Transform transform { get; }

        /// <summary>体の太さ(半径 m)。めり込み防止とリーチの計算に使う</summary>
        float BodyRadius { get; }

        /// <summary>次に当たる攻撃が「連続技の何ヒット目」になるか</summary>
        int NextComboHitNumber();

        /// <summary>攻撃を受ける。どうなったか(ヒット・ガード・回避など)を返す</summary>
        HitResult TakeHit(HitInfo info);

        /// <summary>つかみ技で、frames F かけて dest まで引き寄せられる</summary>
        void StartPull(Vector3 dest, int frames);
    }
}
