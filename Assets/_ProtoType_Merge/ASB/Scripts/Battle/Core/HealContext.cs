namespace ASB.Work.Battle.Core
{
    public class HealContext
    {
        public BattleCharactor Caster;
        public BattleCharactor Target;
        public float HealAmount;
        public int SkillIndex;

        // 부활 컨텍스트: true면 힐 대신 ReviveHpRatio 비율로 대상을 부활시킨다(연출은 힐 경로 재사용).
        public bool IsRevive;
        public float ReviveHpRatio;
    }
}
