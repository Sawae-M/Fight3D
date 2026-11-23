using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// ゴウガのキャラ設定とスキルの状態(使えない時間・パッシブ「不屈」・ウルトの威力)。
    /// 数値は Tuning.cs(仕様書「10. ゴウガ」の値)、技の表は Tuning.cs の GougaMoves。
    /// </summary>
    public class GougaSkills : CharacterKit
    {
        // 数値(使えない時間・倍率)は Tuning.cs の Tuning.Gouga にある

        public int GansaiCooldownLeft { get; private set; }   // 岩砕きが使えるまでの残り(F)
        public int GouwanCooldownLeft { get; private set; }   // 剛腕引きが使えるまでの残り(F)
        public int UketomeCooldownLeft { get; private set; }  // 受け止めが使えるまでの残り(F)
        public bool FukutsuUsed { get; private set; }         // 不屈の「体力 1 で耐える」をこのラウンドで使ったか

        /// <summary>キャラの名前</summary>
        public override string CharacterName => "ゴウガ";
        /// <summary>最大体力(値は Tuning.cs)</summary>
        public override int MaxHp => Tuning.Gouga.MaxHp;
        /// <summary>歩く速さはいつも 0.85倍</summary>
        public override float SpeedScale => Tuning.Gouga.SpeedScale;
        /// <summary>ダメージ倍率の表示名</summary>
        public override string BonusLabel => "不屈・ウルト強化";

        /// <summary>そのスキルを今使えるか(使えない時間が残っていないか)</summary>
        public override bool CanUse(Command c)
        {
            switch (c)
            {
                case Command.Skill1: return GansaiCooldownLeft <= 0;
                case Command.Skill2: return GouwanCooldownLeft <= 0;
                case Command.Skill3: return UketomeCooldownLeft <= 0;
                default: return true;
            }
        }

        /// <summary>弱・中・強のボタンに対応する通常技</summary>
        public override MoveData Normal(Command c) => GougaMoves.Normal(c);

        /// <summary>スキル1〜3 のボタンで出る技</summary>
        public override MoveData SkillMove(Command c)
        {
            switch (c)
            {
                case Command.Skill1: return GougaMoves.Gansai;
                case Command.Skill2: return GougaMoves.Gouwan;
                default: return GougaMoves.Uketome;
            }
        }

        /// <summary>ウルト「大地崩し」の最初のパンチ</summary>
        public override MoveData UltMove => GougaMoves.DaichiKuzushi;

        /// <summary>大地崩しが当たった後の連続パンチ</summary>
        public override MoveData UltRush => GougaMoves.DaichiKuzushiRush;

        /// <summary>受け止めからの反撃</summary>
        public override MoveData CounterMove => GougaMoves.UketomeCounter;

        /// <summary>技を出し始めたとき: スキルの使えない時間を始める</summary>
        public override void OnMoveStarted(Command c)
        {
            switch (c)
            {
                case Command.Skill1: GansaiCooldownLeft = Tuning.Gouga.GansaiCooldown; break;
                case Command.Skill2: GouwanCooldownLeft = Tuning.Gouga.GouwanCooldown; break;
                case Command.Skill3: UketomeCooldownLeft = Tuning.Gouga.UketomeCooldown; break;
            }
        }

        /// <summary>1F 進める(使えない時間を減らす)</summary>
        public override void Tick(FighterController owner)
        {
            if (GansaiCooldownLeft > 0) GansaiCooldownLeft--;
            if (GouwanCooldownLeft > 0) GouwanCooldownLeft--;
            if (UketomeCooldownLeft > 0) UketomeCooldownLeft--;
        }

        /// <summary>スキルの状態をすべて最初に戻す(不屈の「耐える」も使える状態に戻す)</summary>
        public override void ResetAll()
        {
            GansaiCooldownLeft = 0;
            GouwanCooldownLeft = 0;
            UketomeCooldownLeft = 0;
            FukutsuUsed = false;
        }

        /// <summary>剛腕引きをつかんだ後は、ウルトでキャンセルできる([仮])</summary>
        public override bool CanCancelSkill(Command from, Command to)
        {
            return from == Command.Skill2 && to == Command.Ult;
        }

        /// <summary>
        /// パッシブ「不屈」: 体力 50%以下で 1.1倍、25%以下で 1.2倍。
        /// ウルト「大地崩し」: さらに 1 + 0.5 × 失った体力の割合(最大 1.5倍)。
        /// </summary>
        public override float DamageScale(int hitNumber, Command source, FighterController owner)
        {
            float hpRatio = owner.MaxHp > 0 ? (float)owner.Hp / owner.MaxHp : 1f;  // 残り体力の割合(0〜1)
            float scale = FukutsuScale(hpRatio);
            if (source == Command.Ult)
            {
                float lost = 1f - hpRatio;  // 失った体力の割合(0〜1)
                scale *= 1f + (Tuning.Gouga.UltMaxScale - 1f) * lost;
            }
            return scale;
        }

        /// <summary>不屈の攻撃力の倍率(hpRatio: 残り体力の割合)</summary>
        public static float FukutsuScale(float hpRatio)
        {
            if (hpRatio <= Tuning.Gouga.FukutsuQuarterHp) return Tuning.Gouga.FukutsuQuarterScale;
            if (hpRatio <= Tuning.Gouga.FukutsuHalfHp) return Tuning.Gouga.FukutsuHalfScale;
            return 1f;
        }

        /// <summary>不屈: 体力が 0 になる攻撃を受けても、1ラウンドに1回だけ体力 1 で耐える</summary>
        public override bool TrySurviveLethal()
        {
            if (FukutsuUsed) return false;
            FukutsuUsed = true;
            return true;
        }

        /// <summary>左上の操作説明(技の行)</summary>
        public override string[] MoveHelpLines => new[] { "岩砕き: P / RB", "剛腕引き: W+P / 上+RB", "受け止め: S+P / 下+RB", "大地崩し: O+P / B+RB" };

        /// <summary>下のパネルのスキル状態(1行に1つ)</summary>
        public override string[] StatusLines()
        {
            return new[]
            {
                Cooldown("岩砕き", GansaiCooldownLeft),
                Cooldown("剛腕引き", GouwanCooldownLeft),
                Cooldown("受け止め", UketomeCooldownLeft),
            };
        }

        /// <summary>「技名: OK」か「技名: 残り秒数」の文字を作る</summary>
        static string Cooldown(string name, int framesLeft)
        {
            return framesLeft > 0 ? $"{name}: {framesLeft / 60f:0.0}秒" : $"{name}: OK";
        }

        // ---- 見た目: 右腕の構え(見た目だけの数値) ----
        static readonly Vector3 restPos = new Vector3(0.5f, 1.45f, 0.1f);         // 構えの右腕の支点の位置(右肩)
        static readonly Quaternion restRot = Quaternion.Euler(35f, -10f, 0f);      // 構えの右腕の向き(拳を胸の前に)
        static readonly Vector3 guardPos = new Vector3(0.35f, 1.55f, 0.25f);       // ガードの右腕の支点の位置
        static readonly Quaternion guardRot = Quaternion.Euler(-40f, -60f, 0f);    // ガードの右腕の向き(顔の前で横に)
        /// <summary>構えの右腕の支点の位置</summary>
        public override Vector3 RestPos => restPos;
        /// <summary>構えの右腕の向き</summary>
        public override Quaternion RestRot => restRot;
        /// <summary>ガードの右腕の支点の位置</summary>
        public override Vector3 GuardPos => guardPos;
        /// <summary>ガードの右腕の向き</summary>
        public override Quaternion GuardRot => guardRot;
    }
}
