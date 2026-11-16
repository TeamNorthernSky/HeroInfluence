using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-16 | 보스 배치만 저장하며 카메라·은행·영속 파티 위치와 분리합니다.
    [CreateAssetMenu(menuName = "JC/전투/보스 실내 배치")]
    public sealed class JcBossLayoutProfile : ScriptableObject
    {
        [Tooltip("바닥 타일 수 기준 X/Z 이동입니다. 전체와 개별 오프셋을 더해 적용합니다.")]
        public Vector2 allOffset;
        public Vector2 characterOffset;
        public Vector2 objectOffset;
    }
}
