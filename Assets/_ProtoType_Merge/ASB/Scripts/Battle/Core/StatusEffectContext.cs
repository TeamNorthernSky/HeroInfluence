namespace ASB.Work.Battle.Core
{
    public struct StatusEffectContext
    {
        public BattleCharactor Caster;
        public BattleCharactor Target;
        public StatusEffectType EffectType;
        public int DurationTurn;
        public float Value;
    }
}
