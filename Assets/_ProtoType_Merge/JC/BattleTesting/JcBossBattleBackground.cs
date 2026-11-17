using System;
using UnityEngine;

namespace JC.BattleTesting
{
    [CreateAssetMenu(menuName = "JC/전투/보스 무대 배경")]
    public sealed class JcBossBattleBackground : ScriptableObject
    {
        [Tooltip("전투 판정 없이 무대·바닥·기둥 외형만 포함하는 프리팹입니다.")]
        public GameObject visualPrefab;
        // JC | 2026-11-17 | 시간대별 조명·유리·재질·툰·접점 저장값을 본게임과 테스트가 공유합니다.
        public JcIndoorPeriodProfiles periodProfiles;
        public JcBossCameraProfile cameraProfile;
        // JC | 2026-11-16 | 본게임/테스트 보스만 공유하는 배치 프로필이며 카메라·은행 설정은 독립 유지합니다.
        [Tooltip("실내 배치물·캐릭터·전투 그리드의 타일 단위 오프셋입니다.")] public JcBossLayoutProfile layoutProfile;
        public Material glassProfile;
        // JC | 2026-11-09 | 은행과 같은 계산기를 사용하지만 보스 테스트/BE490 전용 프로필만 참조합니다.
        [Tooltip("보스 전용 툰 설정입니다. 기본 꺼짐이며 은행 프로필과 독립적입니다.")] public JcBankToonProfile toonProfile;
        [Tooltip("보스 실내 전용 접점 음영입니다. 원경에는 적용하지 않습니다.")] public JC.Environment.JcContactShadingProfile contactShadingProfile;
        // JC | 2026-11-05 | 테스트와 실제 BE490이 같은 전용 환경 보정을 사용하며 일반 전투 프로필과 분리합니다.
        [Tooltip("기존 환경 위에 적용할 보스전 전용 조명·음영·무대 재질 설정입니다.")]
        public JcBossEnvironmentProfile environmentProfile;
        // JC | 2026-11-09 | 창문광과 임시 천장광을 동시에 처리하는 보스 전용 Forward+ 설정입니다.
        [Tooltip("보스전 동안만 복제해 사용하는 렌더링 설정입니다. 은행·탐사·일반전의 설정은 바꾸지 않습니다.")]
        public UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset interiorPipeline;
        // JC | 2026-11-05 | 실내 표면·바닥 줄눈도 테스트/실제 BE490에서 같은 프로필을 사용합니다.
        [Tooltip("보스 무대의 석재·바닥·가구 표면 보정입니다. 원본 재질은 유지합니다.")]
        public JcBossSurfaceProfile surfaceProfile;
        // JC | 2026-11-05 | 최신 월드의 방향광 설정과 공통 시간대 프로필을 원경에서 재사용합니다.
        [Tooltip("월드맵에서 가져온 태양 설정입니다. 실내에는 비추지 않습니다.")] public Light citySun;
        [Tooltip("월드와 공유하는 네 시간대 프로필입니다.")] public JC.Environment.JcDayNightProfile cityTimeProfile;
        // JC | 2026-11-17 | 탐사 하늘의 표시만 재사용하며 이 프로필의 환경광·반사·라이트 값은 적용하지 않습니다.
        [Tooltip("원경 하늘의 시간대별 배경·구름·천체 표시입니다. 실내 IBL과 조명에는 적용하지 않습니다.")] public JC.Env.JcTimePreviewSettings citySkyProfile;
        [Min(40), Tooltip("고층 원경까지 태양 그림자를 표시할 거리입니다. 보스전 동안만 적용합니다.")] public float cityShadowDistance=300;
        // JC | 2026-11-17 | 보스 원경만의 비용/밝기를 제한하며 실내 조명·IBL·탐사 프로필에는 적용하지 않습니다.
        [Header("원경 최적화")]
        public JcBossCityLodLibrary cityLodLibrary;
        [Range(0,1), Tooltip("저녁·새벽 가로등의 원본 대비 광량입니다.")] public float cityLampBrightness=.35f;
        [Range(.05f,.9f), Tooltip("Spot 내부 각도 비율입니다. 낮추면 밝은 중심에서 바깥 경계까지 넓게 감쇠합니다.")] public float cityLampInnerRatio=.2f;
        [Range(1,128), Tooltip("카메라 근처에서 동시에 사용할 원경 가로등 수입니다.")] public int cityLampBudget=48;
        [Min(20), Tooltip("원경 가로등의 최대 표시 거리이며 마지막 25% 구간에서 부드럽게 꺼집니다.")] public float cityLampDistance=650;
        [Min(0), Tooltip("이 높이보다 작은 원경 소품은 태양 그림자를 만들지 않습니다.")] public float citySmallShadowHeight=3;
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
