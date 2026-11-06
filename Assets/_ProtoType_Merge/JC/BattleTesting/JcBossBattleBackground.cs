using System;
using UnityEngine;

namespace JC.BattleTesting
{
    [CreateAssetMenu(menuName = "JC/전투/보스 무대 배경")]
    public sealed class JcBossBattleBackground : ScriptableObject
    {
        [Tooltip("전투 판정 없이 무대·바닥·기둥 외형만 포함하는 프리팹입니다.")]
        public GameObject visualPrefab;
        public JcBossCameraProfile cameraProfile;
        public Material glassProfile;
        // JC | 2026-11-05 | 테스트와 실제 BE490이 같은 전용 환경 보정을 사용하며 일반 전투 프로필과 분리합니다.
        [Tooltip("기존 환경 위에 적용할 보스전 전용 조명·음영·무대 재질 설정입니다.")]
        public JcBossEnvironmentProfile environmentProfile;
        // JC | 2026-11-05 | 실내 표면·바닥 줄눈도 테스트/실제 BE490에서 같은 프로필을 사용합니다.
        [Tooltip("보스 무대의 석재·바닥·가구 표면 보정입니다. 원본 재질은 유지합니다.")]
        public JcBossSurfaceProfile surfaceProfile;
        // JC | 2026-11-05 | 최신 월드의 방향광 설정과 공통 시간대 프로필을 원경에서 재사용합니다.
        [Tooltip("월드맵에서 가져온 태양 설정입니다. 실내에는 비추지 않습니다.")] public Light citySun;
        [Tooltip("월드와 공유하는 네 시간대 프로필입니다.")] public JC.Environment.JcDayNightProfile cityTimeProfile;
        [Min(40), Tooltip("고층 원경까지 태양 그림자를 표시할 거리입니다. 보스전 동안만 적용합니다.")] public float cityShadowDistance=300;
        [Tooltip("보스전 이벤트 구역 번호입니다.")] public int zoneId = 2;
        [Tooltip("이 배경을 사용할 전투 키입니다. Start_ 접두어는 자동으로 제외합니다.")]
        public string battleKey = "BE490";

        public bool Matches(int zone, string key)
        {
            key = key?.Trim();
            if (key != null && key.StartsWith("Start_", StringComparison.Ordinal)) key = key.Substring(6);
            return zone == zoneId && !string.IsNullOrWhiteSpace(key) && key == battleKey;
        }
        public bool Matches(CombatContext context) => context != null && !context.IsTutorial &&
            Matches(context.EventBattle?.ZoneId ?? -1, context.EventBattle?.BattleKey);
        public bool Matches(JcEnemyPreset preset) => preset != null &&
            (!preset.IsEventBattle || Matches(preset.eventZoneId, preset.eventBattleKey));
        public static JcBossBattleBackground Load() => Resources.Load<JcBossBattleBackground>("JC_BossBattleBackground");
    }
}
