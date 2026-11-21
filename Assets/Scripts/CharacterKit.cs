using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// キャラごとに違う部分(技の表・スキルの状態・パッシブ・画面の説明)をまとめる土台。
    /// 歩く・ガード・回避・攻撃を当てる などの共通の動きは FighterController が受け持ち、
    /// 「どの技を出すか」「ダメージを何倍にするか」などはこのクラスに聞く。
    /// カゲツ = KagetsuSkills、ゴウガ = GougaSkills がこれを引き継いでいる。
    /// </summary>
    public abstract class CharacterKit
    {
        /// <summary>キャラの名前(画面表示用)</summary>
        public abstract string CharacterName { get; }

        /// <summary>最大体力</summary>
        public abstract int MaxHp { get; }

        /// <summary>今の移動速度の倍率(1 = ふつう)</summary>
        public virtual float SpeedScale => 1f;

        /// <summary>技の「出るまで」を今の状態に合わせて変える(仮面解放など)。ふつうはそのまま</summary>
        public virtual int ScaleStartup(int startup) => startup;

        /// <summary>そのスキルを今使えるか(回数・使えない時間など)</summary>
        public virtual bool CanUse(Command c) => true;

        /// <summary>弱・中・強のボタンに対応する通常技</summary>
        public abstract MoveData Normal(Command c);

        /// <summary>スキル1〜3 のボタンで出る技(呼ばれた時点の状態で選ぶ)</summary>
        public abstract MoveData SkillMove(Command c);

        /// <summary>ウルトの最初の一撃</summary>
        public abstract MoveData UltMove { get; }

        /// <summary>ウルトが当たった後の演出(連続攻撃)</summary>
        public abstract MoveData UltRush { get; }

        /// <summary>受け止めの構え中に攻撃を受けたときに出す反撃の技(無いキャラは null)</summary>
        public virtual MoveData CounterMove => null;

        /// <summary>技を出し始めたとき(回数を数える・使えない時間を始める など)</summary>
        public virtual void OnMoveStarted(Command c) { }

        /// <summary>技が終わったとき</summary>
        public virtual void OnMoveEnded(Command c) { }

        /// <summary>攻撃がヒットしたとき</summary>
        public virtual void OnHit() { }

        /// <summary>1F 進める(使えない時間を減らす など)。owner は自分のキャラ</summary>
        public virtual void Tick(FighterController owner) { }

        /// <summary>スキルの状態をすべて最初に戻す</summary>
        public virtual void ResetAll() { }

        /// <summary>当たったスキル(from)を、別の技(to)でキャンセルしてつなげられるか。ふつうはつながらない</summary>
        public virtual bool CanCancelSkill(Command from, Command to) => false;

        /// <summary>
        /// 与えるダメージの倍率(パッシブなど)。
        /// hitNumber = 連続技の何ヒット目か、source = どのボタンの技か、owner = 自分のキャラ
        /// </summary>
        public virtual float DamageScale(int hitNumber, Command source, FighterController owner) => 1f;

        /// <summary>体力が 0 になる攻撃を受けたとき、耐えるか(ゴウガの不屈)。耐えるなら true</summary>
        public virtual bool TrySurviveLethal() => false;

        /// <summary>ダメージ倍率の表示名(右上のパネル用。例: 連刃ボーナス)</summary>
        public abstract string BonusLabel { get; }

        /// <summary>左上の操作説明に出す、技の行(スキル1〜3・ウルト)</summary>
        public abstract string[] MoveHelpLines { get; }

        /// <summary>下のパネルに出す、スキルの状態(1行に1つ)</summary>
        public abstract string[] StatusLines();

        // ---- 見た目(剣・腕の構えの位置と向き)。値は見た目だけなので各キャラのファイルに置く ----
        /// <summary>構えのときの剣(腕)の支点の位置</summary>
        public abstract Vector3 RestPos { get; }
        /// <summary>構えのときの剣(腕)の向き</summary>
        public abstract Quaternion RestRot { get; }
        /// <summary>ガードのときの剣(腕)の支点の位置</summary>
        public abstract Vector3 GuardPos { get; }
        /// <summary>ガードのときの剣(腕)の向き</summary>
        public abstract Quaternion GuardRot { get; }
    }
}
