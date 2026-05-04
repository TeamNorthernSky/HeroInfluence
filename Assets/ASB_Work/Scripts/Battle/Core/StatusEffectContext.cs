namespace ASB.Work.Battle.Core
{
    public struct StatusEffectContext
    {
        public BattleCharactor Caster;
        public BattleCharactor Target;
        public string EffectType;
        public int DurationTurn;
    }
}
