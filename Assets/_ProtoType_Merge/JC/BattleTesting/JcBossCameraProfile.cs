using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-16 | 기본뷰와 모든 스킬 연출의 기준 배치를 함께 보관합니다. 레이아웃을 다시 저장해도 기준점은 바뀌지 않습니다.
    public sealed class JcBossCameraProfile : JcBattleCameraProfile
    {
        [InspectorName("카메라 기준 캐릭터 배치 (X / Z 타일)")]
        [Tooltip("기본 카메라를 맞춘 때의 전체+캐릭터 오프셋입니다. 현재 배치와의 차이만 기본 카메라에 적용하고 스킬은 이동된 기본뷰에서 계산합니다.")]
        public Vector2 layoutReference;
        // JC | 2026-11-17 | 기본뷰와 같은 보스 전용 프로필에 추적 초점 값을 보관합니다. 은행/야외 프로필은 변경하지 않습니다.
        [Min(0f), InspectorName("중앙 대상 왼쪽 오프셋 (m)")]
        [Tooltip("중앙 대상의 추적 초점을 진형 왼쪽으로 옮기는 최대 거리입니다. 광역기는 실제 대상 열 평균으로 보간합니다. 0이면 보정 없음이며 보스 본게임·테스트가 공유합니다.")]
        public float followMiddleOffset = 1.8f;
        [Min(0f), InspectorName("오른쪽 대상 왼쪽 오프셋 (m)")]
        [Tooltip("오른쪽 대상의 추적 초점을 진형 왼쪽으로 옮기는 최대 거리입니다. 광역기는 실제 대상 열 평균으로 보간합니다. 0이면 보정 없음이며 보스 본게임·테스트가 공유합니다.")]
        public float followRightOffset = 3.6f;

        // JC | 2026-11-17 | 사용자 정지 화면의 X/Y 이동량을 스킬 기준 위치에 더합니다. 기본뷰와 배치 이동에 누적하지 않습니다.
        [InspectorName("쓰러지면 안돼 위치 보정 (X / Y m)")]
        [Tooltip("보스전 광역 공격 구도에 더하는 월드 X/Y 이동량입니다. 음수는 X 감소/하강, 0은 이동 없음입니다. Z와 추적 회전은 유지하며 본게임·테스트가 공유합니다.")]
        public Vector2 nekoBarrageOffset = new Vector2(-1.54322f, -2.34772f);

        // JC | 2026-11-17 | 왼쪽 열은 무보정이며 음수는 허용하지 않습니다. 실제 투사체/캐릭터가 아니라 초점에만 사용합니다.
        public float FollowOffsetForColumn(int column) => column == 1 ? Mathf.Max(0f, followMiddleOffset) :
            column == 2 ? Mathf.Max(0f, followRightOffset) : 0f;
    }
}
