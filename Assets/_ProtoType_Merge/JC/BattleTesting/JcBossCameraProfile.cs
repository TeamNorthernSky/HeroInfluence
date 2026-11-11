using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-11 | 보스 기본뷰와 무대 경계만 보존합니다. 이전 독립 블랙불릿 aim은 정본1/2 공통 선택으로 대체했습니다.
    public sealed class JcBossCameraProfile : JcBattleCameraProfile
    {
        [InspectorName("오른쪽 가상 벽 X (무대 로컬)"), Range(3f,49f)] public float rightWallX = 19f;
        [InspectorName("오른쪽 연출 허용 폭"), Range(0f,12f)] public float rightMargin = 3f;
    }
}
