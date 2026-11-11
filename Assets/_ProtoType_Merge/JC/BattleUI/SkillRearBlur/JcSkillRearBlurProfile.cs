using UnityEngine;

namespace JC.BattleUI
{
    // JC | 2026-11-11 | 탐사 DOF와 분리된 전투 후방 2단계 설정입니다. 적 진형 기준의 월드 후방 거리를 사용하며 개체 보호 마스크는 없습니다.
    [CreateAssetMenu(menuName = "JC/전투/스킬 후방 블러 프로필")]
    public sealed class JcSkillRearBlurProfile : ScriptableObject
    {
        [InspectorName("후방 블러 사용"), Tooltip("첫 튜토리얼·일반 야외 전투의 원거리 공격 카메라에 적용합니다. 점령·은행·보스·코어는 제외합니다.")]
        public bool useBlur = true;
        [Header("약한 후방 흐림")]
        [InspectorName("시작 거리"), Min(0), Tooltip("적 진형 중심에서 아군 반대 방향으로 잰 월드 거리입니다. 기본 9는 현재 적 후열에서 대략 두 칸 뒤이며 카메라 이동과 무관합니다.")]
        public float nearOffset = 9;
        [InspectorName("전환 폭"), Min(.1f), Tooltip("선명한 배경에서 약한 흐림까지 부드럽게 반경을 늘릴 월드 거리입니다.")]
        public float nearWidth = 3;
        [InspectorName("흐림 반경"), Range(0, 12), Tooltip("1080p 기준 픽셀 반경입니다. 출력 높이에 비례하여 같은 화면 비율을 유지합니다.")]
        public float nearRadius = 3;
        [InspectorName("흐림 강도"), Range(0, 1), Tooltip("약한 단계의 반경에 곱합니다. 원본 이미지와 섞지 않고 반경 자체가 증가합니다.")]
        public float nearStrength = .55f;
        [Header("강한 원경 흐림")]
        [InspectorName("시작 거리"), Min(0), Tooltip("같은 적 진형 중심으로부터의 월드 거리입니다. 약한 흐림의 전환이 끝난 뒤부터 적용됩니다.")]
        public float farOffset = 18;
        [InspectorName("전환 폭"), Min(.1f), Tooltip("약한 흐림에서 강한 흐림으로 반경을 늘리는 월드 거리입니다. 임계점의 단절을 방지합니다.")]
        public float farWidth = 6;
        [InspectorName("흐림 반경"), Range(0, 64), Tooltip("1080p 기준 픽셀 반경입니다. 해상도 축소 없이 원형 보케 샘플로 계산합니다.")]
        public float farRadius = 18;
        [InspectorName("흐림 강도"), Range(0, 1), Tooltip("약한 반경에서 강한 반경으로 증가하는 정도입니다. 0이면 약한 반경을 유지합니다.")]
        public float farStrength = 1;
        [Header("연출과 품질")]
        [InspectorName("진입 시간"), Min(0), Tooltip("1배속 기준 초입니다. 전투 배속·Timeline·타격 정지를 따릅니다.")]
        public float enterSeconds = .2f;
        [InspectorName("해제 시간"), Min(0), Tooltip("카메라 복귀와 함께 흐림을 줄이는 1배속 기준 초입니다.")]
        public float exitSeconds = .25f;
        [InspectorName("고품질 샘플링"), Tooltip("원형 샘플을 24개에서 96개로 늘립니다. 큰 반경에서는 켜는 것을 권장합니다.")]
        public bool highQuality = true;

        // JC | 2026-11-11 | 강한 구간이 약한 구간 앞에 오지 않도록 실제 계산에서만 순서를 보장합니다. 저장값은 몰래 덮어쓰지 않습니다.
        public Vector4 Distances()
        {
            float start = Mathf.Max(0, nearOffset);
            float end = start + Mathf.Max(.1f, nearWidth);
            float far = Mathf.Max(end, Mathf.Max(0, farOffset));
            return new Vector4(start, end, far, far + Mathf.Max(.1f, farWidth));
        }
    }
}
