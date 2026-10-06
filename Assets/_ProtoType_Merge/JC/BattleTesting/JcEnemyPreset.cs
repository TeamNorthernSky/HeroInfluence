using UnityEngine;
namespace JC.BattleTesting
{
    public enum JcBossActionMode { NormalAI, RepeatSkill }

    // 원본 대응: EnemySpawnPlan/EnemySpawnEntry의 적 조합 입력, BossController.ComputePhase의 시작 상태.
    // 프리팹·데이터 카탈로그의 대체본이 아닙니다. 기존 유닛 키와 논리 슬롯만 저장합니다.
    [CreateAssetMenu(menuName = "JC/전투 테스트/적 프리셋", fileName = "JcEnemyPreset")]
    public sealed class JcEnemyPreset : ScriptableObject
    {
        [Tooltip("전체 보스전이면 증폭기와 자동 페이즈 전환을 포함합니다. 단독 테스트는 시작 페이즈를 고정합니다.")]
        public bool fullEncounter;
        [Tooltip("단독 테스트 시작 페이즈입니다. 3은 독립 HP를 가진 장막 상태로 시작합니다.")]
        [Range(1, 3)] public int startPhase = 1;
        [Tooltip("적 레벨입니다. 기존 테이블의 레벨 증가 스탯을 적용합니다.")]
        [Min(1)] public int enemyLevel = 1;
        [Tooltip("율리아 본체와 장막이 사용하는 적 진영 논리 슬롯입니다.")]
        [Range(1, 6)] public int bossSlot = 5;
        [Tooltip("정상 AI 또는 지정한 스킬 반복입니다. 직접 조작 UI는 이번 구현 범위에 포함하지 않습니다.")]
        public JcBossActionMode actionMode;
        [Tooltip("반복할 실제 스킬 ID입니다. 지정 페이즈에서 사용할 수 없으면 전투 시작을 차단합니다.")]
        public int repeatSkillId = 400013;
    }

}
