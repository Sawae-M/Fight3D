using UnityEngine;

namespace Fight3D
{
    /// <summary>
    /// CPU の頭脳。1F ごとに相手の様子を見て、キーボードの代わりにボタンを押す。
    /// - 離れていたら近づく(たまに相手の周りを回る)。遠くまで届くスキルを出すこともある
    /// - 近ければ 弱→中→強(→スキル)の連続技を狙う。当たったときだけ次の技を押す
    /// - 相手の技の出始めを見て、ガード・回避・受け止め(ゴウガ)で反応することがある
    /// - ゲージが満タンで近ければウルト
    /// 強さの数値は Tuning.cs の Tuning.Cpu(仕様書で「これから決める」ので、すべて仮)。
    /// </summary>
    public class CpuBrain
    {
        public Vector2 Move { get; private set; }  // 移動の入力(y = 前/後ろ、x = 右/左)
        public bool Guard { get; private set; }    // ガードボタンを押しているか

        readonly System.Random random = new System.Random(Tuning.Cpu.Seed);  // CPU 専用の乱数(種が同じなら毎回同じ動き)

        int thinkLeft;              // 次に考えるまでの残り(F)
        int guardLeft;              // ガードを続ける残り(F)
        float strafeDir;            // 相手の周りを回る向き(-1 = 左、0 = 回らない、1 = 右)
        Command[] plan;             // 今狙っている連続技(ボタンの順番)
        int planIndex;              // 連続技の次に押すボタンの番号
        int lastPressedSerial = -1; // 最後に「次の技」を押したときの自分の技の番号(同じ技で2回押さないため)
        int lastReactedSerial = -1; // 最後に反応した相手の技の番号(同じ技に2回反応しないため)

        /// <summary>1F 分考えて、ボタンを押す(FighterController の FixedUpdate から呼ばれる)</summary>
        public void Think(FighterController self)
        {
            var opp = self.OpponentFighter;
            Move = Vector2.zero;

            // 攻撃を受けて動けない間は何もしない(ガードも離す)
            if (self.InHitstun) { Guard = false; guardLeft = 0; plan = null; return; }

            // ---- 相手の技に反応する(ガード・回避・受け止め) ----
            if (opp != null && ShouldReact(self, opp) && React(self, opp)) return;  // 受け止め・回避を押したら、このF はそれだけ

            if (guardLeft > 0)
            {
                guardLeft--;
                Guard = true;
                return;
            }
            Guard = false;

            // ---- 技を出している最中: 当たったら連続技の次を押す ----
            if (self.IsAttacking)
            {
                if (self.MoveContacted && plan != null && planIndex < plan.Length && self.MoveSerial != lastPressedSerial)
                {
                    lastPressedSerial = self.MoveSerial;
                    if (Roll(Tuning.Cpu.ComboChance)) self.PressCommand(plan[planIndex++]);
                    else plan = null;  // わざとつなげないこともある
                }
                return;
            }
            if (!self.CanAct) return;

            // ---- 何もしていないとき: 歩きながら、ときどき次の行動を考える ----
            float dist = self.DistanceToOpponent;
            Walk(dist);
            if (thinkLeft > 0) { thinkLeft--; return; }
            thinkLeft = Tuning.Cpu.ThinkInterval + random.Next(Tuning.Cpu.ThinkJitter + 1);
            Decide(self, dist);
        }

        /// <summary>相手の技の出始めで、まだ反応していない技か</summary>
        bool ShouldReact(FighterController self, FighterController opp)
        {
            if (!opp.IsInStartup || opp.MoveSerial == lastReactedSerial) return false;
            if (self.DistanceToOpponent > Tuning.Cpu.ReactRange) return false;
            if (!self.CanAct && !self.IsGuarding) return false;  // 技を出している最中は反応できない
            return opp.AttackFrameNow >= Tuning.Cpu.ReactDelay;
        }

        /// <summary>
        /// 相手の技に反応する: 受け止め(ゴウガ)・回避・ガードのどれかを選ぶ(何もしないこともある)。
        /// 受け止め・回避のボタンを押したときは true
        /// </summary>
        bool React(FighterController self, FighterController opp)
        {
            lastReactedSerial = opp.MoveSerial;
            plan = null;
            if (self.Kit.CounterMove != null && self.Kit.CanUse(Command.Skill3) && opp.CurrentCommand != Command.Ult && Roll(Tuning.Cpu.CounterChance))
            {
                self.PressCommand(Command.Skill3);  // 受け止め
                return true;
            }
            if (Roll(Tuning.Cpu.DodgeChance) && self.DodgeCooldownLeft == 0)
            {
                Move = new Vector2(random.Next(2) == 0 ? -1f : 1f, 0f);  // 横によける
                self.PressDodge();
                return true;
            }
            if (Roll(Tuning.Cpu.GuardChance)) guardLeft = Tuning.Cpu.GuardHoldFrames;
            return false;
        }

        /// <summary>歩く: 遠ければ近づき、近すぎれば少し下がる。ときどき相手の周りを回る</summary>
        void Walk(float dist)
        {
            float forward = 0f;
            if (dist > Tuning.Cpu.AttackRange) forward = 1f;
            else if (dist < Tuning.Cpu.KeepRange) forward = -0.6f;
            Move = new Vector2(strafeDir, forward);
        }

        /// <summary>次の行動を決める(ウルト・スキル・連続技・回り込み)</summary>
        void Decide(FighterController self, float dist)
        {
            strafeDir = Roll(Tuning.Cpu.StrafeChance) ? (random.Next(2) == 0 ? -1f : 1f) : 0f;
            var kit = self.Kit;

            // ゲージ満タンで近ければウルト
            if (self.Gauge >= Tuning.Fighter.MaxGauge && dist <= Tuning.Cpu.UltRange)
            {
                Start(self, Command.Ult);
                return;
            }

            // カゲツ: ときどき仮面解放
            if (kit is KagetsuSkills && kit.CanUse(Command.Skill3) && Roll(Tuning.Cpu.BuffChance))
            {
                Start(self, Command.Skill3);
                return;
            }

            if (dist > Tuning.Cpu.AttackRange)
            {
                // 離れているとき: 遠くまで届くスキル(カゲツ = 影穿ち、ゴウガ = 岩砕き / 剛腕引き)
                if (!Roll(Tuning.Cpu.RangedSkillChance)) return;
                if (kit is GougaSkills && dist <= 4f && kit.CanUse(Command.Skill2) && random.Next(2) == 0) Start(self, Command.Skill2);
                else if (dist <= 4.5f && kit.CanUse(Command.Skill1)) Start(self, Command.Skill1);
                return;
            }

            // 近いとき: 連続技。最後をスキルにすることもある
            Command finisher = Command.Heavy;
            if (Roll(Tuning.Cpu.SkillFinishChance))
            {
                if (kit.CanUse(Command.Skill2) && random.Next(2) == 0) finisher = Command.Skill2;
                else if (kit.CanUse(Command.Skill1)) finisher = Command.Skill1;
            }
            var route = new System.Collections.Generic.List<Command>();  // 連続技のボタンの順番
            switch (random.Next(3))
            {
                case 0: route.AddRange(new[] { Command.Medium, Command.Heavy }); break;  // 弱は出さず中から
                case 1: route.Add(Command.Medium); break;
                default: route.AddRange(new[] { Command.Light, Command.Medium, Command.Heavy }); break;
            }
            if (route[route.Count - 1] != finisher) route.Add(finisher);
            plan = route.ToArray();
            // 最初の1つを押す(残りは当たったときに押す)
            planIndex = 1;
            lastPressedSerial = -1;
            self.PressCommand(plan[0]);
        }

        /// <summary>1つだけ技を出す(連続技の続きは狙わない)</summary>
        void Start(FighterController self, Command cmd)
        {
            plan = null;
            self.PressCommand(cmd);
        }

        /// <summary>確率 chance(0〜1)で true</summary>
        bool Roll(float chance) => random.NextDouble() < chance;
    }
}
