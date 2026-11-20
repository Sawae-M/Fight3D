namespace Fight3D
{
    public enum AttackType { Light, Medium, Heavy }

    /// <summary>
    /// 1つの技のデータ。時間はすべてフレーム(1F = 1/60秒)。
    /// 数値は仕様書「8. 通常攻撃」の仮の値。
    /// </summary>
    public class AttackData
    {
        public AttackType Type;
        public string Name;
        public int Damage;
        public int Startup;   // 出るまで
        public int Active;    // 当たっている時間
        public int Recovery;  // 終わりの隙
        public float Reach;   // リーチ(m)
        public int Hitstun;   // 当たった相手が動けない時間(仕様書に無いので仮)

        public int TotalFrames => Startup + Active + Recovery;

        public static readonly AttackData Light = new AttackData
        { Type = AttackType.Light, Name = "弱", Damage = 30, Startup = 5, Active = 3, Recovery = 10, Reach = 1.6f, Hitstun = 16 };

        public static readonly AttackData Medium = new AttackData
        { Type = AttackType.Medium, Name = "中", Damage = 60, Startup = 9, Active = 4, Recovery = 18, Reach = 2.0f, Hitstun = 22 };

        public static readonly AttackData Heavy = new AttackData
        { Type = AttackType.Heavy, Name = "強", Damage = 100, Startup = 15, Active = 5, Recovery = 28, Reach = 2.4f, Hitstun = 28 };

        public static AttackData Get(AttackType type)
        {
            switch (type)
            {
                case AttackType.Light: return Light;
                case AttackType.Medium: return Medium;
                default: return Heavy;
            }
        }
    }
}
