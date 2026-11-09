using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-09 | 기존 은행·보스 프로필 GUID와 필드명을 유지한 채 공통 조절 계약만 상속합니다.
    // 광원 열거형은 기존 프로필에 남겨 Spot/Point 저장 번호가 뒤바뀌지 않게 합니다.
    public abstract class JcIndoorEnvironmentProfile : ScriptableObject
    {
        public enum CharacterLighting
        {
            [InspectorName("기존 전투별 방식 유지")] Preserve = 0,
            [InspectorName("부드러운 캐릭터 명암")] Soft = 1,
            [InspectorName("실내 실제 조명만")] InteriorLights = 2
        }
        [InspectorName("캐릭터 명암 방식"), Tooltip("기존 방식은 은행의 부드러운 명암과 보스의 창문·추가광 명암을 유지합니다.")]
        public CharacterLighting characterLighting;
        [HideInInspector] public UnityEngine.Rendering.Universal.ScriptableRendererFeature ssaoFeature;
        [Header("SSAO · 주변 음영")]
        [InspectorName("SSAO 사용"), Tooltip("은행은 기존 SSAO를 사용하고 보스는 실내 표면·캐릭터에만 적용합니다. 도시 원경에는 적용하지 않습니다.")]
        public bool ssaoEnabled = true;
        [Range(0,3), InspectorName("SSAO 강도"), Tooltip("주변 형상에 의한 음영 강도입니다.")] public float ssaoIntensity = .8f;
        [Range(.02f,2), InspectorName("SSAO 반경"), Tooltip("음영을 검사할 월드 거리입니다.")] public float ssaoRadius = .65f;
        [Range(0,1), InspectorName("SSAO 직접광 영향"), Tooltip("직접 조명을 받는 표면의 음영 반영 비율입니다.")] public float ssaoDirectLightingStrength = .35f;
        [InspectorName("SSAO 절반 해상도"), Tooltip("은행 SSAO의 해상도를 낮춥니다. 보스는 표면에서 계산하므로 화면 해상도를 따릅니다.")] public bool ssaoDownsample;
    }
}
