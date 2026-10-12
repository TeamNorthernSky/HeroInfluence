namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 근접 접근/복귀 이동 효과음 키 결정. 스킬 Phase 키 → 유닛 이동 프로필 키 → 빈 문자열(무음) 순.
    /// </summary>
    public static class MovementSfxKeyResolver
    {
        public static string ResolveApproach(MovePhase movePhase, UnitMovementProfile movement)
        {
            return Resolve(movePhase?.SfxKey, movement != null ? movement.ApproachSfxKey : null);
        }

        public static string ResolveReturn(ReturnPhase returnPhase, UnitMovementProfile movement)
        {
            return Resolve(returnPhase?.SfxKey, movement != null ? movement.ReturnSfxKey : null);
        }

        private static string Resolve(string phaseKey, string unitKey)
        {
            string key = DHAudioClipCatalog.NormalizeKey(phaseKey);
            return string.IsNullOrEmpty(key) ? DHAudioClipCatalog.NormalizeKey(unitKey) : key;
        }
    }
}
