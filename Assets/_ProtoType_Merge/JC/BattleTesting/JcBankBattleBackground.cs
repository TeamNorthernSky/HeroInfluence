using System;
using UnityEngine;

namespace JC.BattleTesting
{
    [CreateAssetMenu(menuName = "JC/전투/은행 실내 배경")]
    public sealed class JcBankBattleBackground : ScriptableObject
    {
        [Tooltip("은행 실내 외형만 포함하는 전용 프리팹입니다. 전투 그리드와 판정은 포함하지 않습니다.")]
        public GameObject visualPrefab;
        // JC | 2026-11-05 | 테스트와 본게임이 동일한 은행 재질 저장값을 사용합니다.
        public JcBankMaterialProfile materialProfile;
        // JC | 2026-11-05 | 은행 본게임·테스트가 같은 고정 실내 조명을 사용하며 탐사 시각을 상속하지 않습니다.
        [Tooltip("본게임 은행전과 BankTestScene이 공유하는 실내 조명입니다. 시간대와 무관한 천장 조명·환경광·반사를 저장합니다.")]
        public JcBankEnvironmentProfile environmentProfile;
        // JC | 2026-11-06 | 은행에만 접점 위치를 지정하고 계산기는 보스에서도 재사용할 수 있도록 분리합니다.
        [Tooltip("은행 본게임·BankTestScene이 공유하는 접점 음영 설정입니다. 다른 전투에는 적용하지 않습니다.")]
        public JC.Environment.JcContactShadingProfile contactShadingProfile;
        // JC | 2026-11-06 | 은행 테스트와 본게임에서 툰·보조광 실험값을 공유합니다.
        [Tooltip("은행 PBR 툰과 그림자 없는 보조광 설정입니다. 보스·일반 전투에는 적용하지 않습니다.")]
        public JcBankToonProfile toonProfile;
        // JC | 2026-11-06 | 반 칸 이동한 은행 진형에 맞춘 기본 A뷰의 수평 이동량입니다. 공용 포즈·원근감 계산은 유지합니다.
        [InspectorName("기본 카메라 X 이동량"), Tooltip("공통 A뷰의 원근감 계산 후 월드 X에 더합니다. 은행 본게임·BankTestScene에서만 적용하며 Y/Z·회전·시야각은 유지합니다.")]
        public float viewAHorizontalOffset;
        // JC | 2026-11-06 | 4개 스킬의 직접 편집 구도를 본게임과 테스트에서 공유합니다.
        [Tooltip("은행 블랙불릿 1~4번 스킬의 개별 카메라 프로필입니다. JC/은행/블랙불릿 스킬 카메라 편집 메뉴에서 구도를 저장합니다.")]
        public JcBankSkillCameraProfile skillCameraProfile;
        [Tooltip("은행 전투가 있는 이벤트 구역 번호입니다.")]
        public int zoneId = 1;
        [Tooltip("은행 점령전의 전투 키입니다. 재도전도 같은 키로 판별합니다.")]
        public string[] battleKeys = { "BE002", "BE003", "BE004" };
        [Tooltip("은행 배경을 사용할 튜토리얼 전투 키입니다.")]
        public string tutorialKey = "TUT_02";
        [Tooltip("은행 전용 AO 렌더러를 등록한 URP 설정입니다. 다른 파이프라인에서는 기존 렌더러를 유지합니다.")]
        public UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset renderPipeline;
        [Min(0), Tooltip("은행 내부에서만 사용할 AO 강화 렌더러 번호입니다.")]
        public int rendererIndex;

        public void ApplyCamera(Camera camera)
        {
            if (camera == null || renderPipeline == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != renderPipeline) return;
            if (camera.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var data)) data.SetRenderer(rendererIndex);
        }

        public bool Matches(bool tutorial, string lesson, int zone, string battleKey)
        {
            if (tutorial) return string.Equals(lesson, tutorialKey, StringComparison.Ordinal);
            if (zone != zoneId || string.IsNullOrWhiteSpace(battleKey)) return false;
            string key = battleKey.Trim();
            if (key.StartsWith("Start_", StringComparison.Ordinal)) key = key.Substring(6);
            return Array.Exists(battleKeys ?? Array.Empty<string>(), candidate => candidate == key);
        }

        public bool Matches(CombatContext context) => context != null &&
            Matches(context.IsTutorial, context.TutorialBattleKey,
                context.EventBattle?.ZoneId ?? -1, context.EventBattle?.BattleKey);

        public static JcBankBattleBackground Load() =>
            Resources.Load<JcBankBattleBackground>("JC_BankBattleBackground");
    }
}
