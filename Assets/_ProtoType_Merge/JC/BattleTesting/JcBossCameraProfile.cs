using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-16 | 기본뷰와 모든 스킬 연출의 기준 배치를 함께 보관합니다. 레이아웃을 다시 저장해도 기준점은 바뀌지 않습니다.
    public sealed class JcBossCameraProfile : JcBattleCameraProfile
    {
        [InspectorName("카메라 기준 캐릭터 배치 (X / Z 타일)")]
        [Tooltip("기본 카메라를 맞춘 때의 전체+캐릭터 오프셋입니다. 현재 배치와의 차이만 기본 카메라에 적용하고 스킬은 이동된 기본뷰에서 계산합니다.")]
        public Vector2 layoutReference;
    }
}
