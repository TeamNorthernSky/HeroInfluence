using System;
using UnityEngine;

namespace JC.BattleTesting
{
    [CreateAssetMenu(menuName = "JC/전투/은행 실내 배경")]
    public sealed class JcBankBattleBackground : ScriptableObject
    {
        [Tooltip("은행 실내 외형만 포함하는 전용 프리팹입니다. 전투 그리드와 판정은 포함하지 않습니다.")]
        public GameObject visualPrefab;
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
