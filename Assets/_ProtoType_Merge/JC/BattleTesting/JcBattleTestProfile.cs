using System;
using UnityEngine;

namespace JC.BattleTesting
{
    // 원본 대응: ASB/Scripts/Battle/Simulation/SimulationBattleConfig.cs의 아군 입력.
    // 차이: 저장소 키가 아닌 테스트 초기조건만 저장하며 협회 구매/비용 처리는 포함하지 않습니다.
    [Serializable]
    public sealed class JcAllySetup
    {
        [Tooltip("기존 히어로 데이터 키입니다. 변경 후 설정 적용 및 재시작이 필요합니다.")]
        public string unitKey;
        [Tooltip("아군 진영의 논리 슬롯 1~6입니다. 다른 아군과 중복할 수 없습니다.")]
        [Range(1, 6)] public int slot = 1;
        [Tooltip("레벨 1~8은 F/D/C/B/A/S/SS/SSS입니다. 기존 해금 규칙으로 기본·강화 스킬을 선택합니다.")]
        [Range(1, 8)] public int level = 1;
        [Tooltip("전투 시작 HP 비율입니다. 실제 전투에서 감소한 HP는 프로필에 저장하지 않습니다.")]
        [Range(0.01f, 1f)] public float hpRatio = 1f;
        [Tooltip("전투 시작 IP입니다. 최종 최대 IP를 넘으면 설정 오류로 표시합니다.")]
        [Min(0)] public float initialIP = 100;
        [Tooltip("무기 데이터 키입니다. 빈 값이면 해당 히어로 테이블의 기본 무기를 사용합니다.")]
        public string weaponKey;
        [Tooltip("무기 강화 레벨 1~5입니다. 무기 스탯과 스킬 계수 모두에 적용합니다.")]
        [Range(1, 5)] public int weaponLevel = 1;
        [Tooltip("트레이닝 공격력 강화 단계 0~3입니다. 기존 트레이닝 증가량을 임시 스탯에만 더합니다.")]
        [Range(0, 3)] public int attackTraining;
        [Tooltip("트레이닝 체력 강화 단계 0~3입니다. 기존 트레이닝 증가량을 임시 스탯에만 더합니다.")]
        [Range(0, 3)] public int healthTraining;
        [Tooltip("스킬 계열 1~4의 수치 레벨입니다. 랭크로 선택된 테이블 행의 ValueLv/SubValueLv 1~5를 사용합니다.")]
        public int[] skillLevels = { 1, 1, 1, 1 };
    }

    // 원본 대응: SimulationBattleConfig + 기존 JcDofControllerEditor의 명시적 .asset 저장 방식.
    // 차이: 초기 설정만 저장합니다. 카메라/환경/VFX 자체 설정 및 전투 진행 상태는 저장하지 않습니다.
    [CreateAssetMenu(menuName = "JC/전투 테스트/프로필", fileName = "JcBattleTestProfile")]
    public sealed class JcBattleTestProfile : ScriptableObject
    {
        [Tooltip("불러올 전투 초기 설정입니다. 적용에는 설정 적용 후 전투 재시작이 필요합니다.")]
        public JcBattleTestSettings settings = new JcBattleTestSettings();
    }

    [Serializable]
    public sealed class JcBattleTestSettings
    {
        [Tooltip("아군 4명의 초기 설정입니다. 실제 전투 진행 값은 저장하지 않습니다.")]
        public JcAllySetup[] allies = {
            new JcAllySetup { unitKey = "10001", slot = 5 },
            new JcAllySetup { unitKey = "10002", slot = 1 },
            new JcAllySetup { unitKey = "10003", slot = 3 },
            new JcAllySetup { unitKey = "10004", slot = 2 }
        };
        [Tooltip("적 전투 초기 조건입니다. 프리셋 변경은 아군 및 카메라·환경 설정을 바꾸지 않습니다.")]
        public JcEnemyPreset enemyPreset;
        [Tooltip("켜면 아군은 피해로 HP가 1 미만으로 내려가지 않습니다. 즉시 적용하며 사망자를 부활시키지 않습니다.")]
        public bool alliesCannotDie;
        [Tooltip("전투 판정용 난수의 시작 시드를 고정합니다. 같은 설정과 행동 순서로 비교할 때 사용합니다.")]
        public bool useFixedSeed;
        [Tooltip("고정 시드 정수입니다. VFX 난수와 분리된 전투 난수에 사용합니다.")]
        public int seed = 1005;
    }
}
