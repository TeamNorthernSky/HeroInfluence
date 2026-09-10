using UnityEngine;

/// <summary>탐사 경계 스크롤 공용 설정. 에디터 입력 정책은 로컬 에디터 설정에서 관리한다.</summary>
[CreateAssetMenu(menuName = "JC/플레이 입력 프로필")]
public sealed class JcPointerProfile : ScriptableObject
{
    [Tooltip("화면 안쪽 스크롤 시작 영역의 폭(표시 픽셀). 1 이상. 안쪽 끝에서는 정지, 화면 경계에서 기본 속도에 도달합니다.")]
    [Min(1)] public float innerPixels = 10;
    [Tooltip("화면 밖에서 기본 속도부터 최대 속도까지 증가하는 거리(표시 픽셀). 1 이상. 에디터 자동 편집 전환 거리로도 사용합니다.")]
    [Min(1)] public float outerPixels = 200;
    [Tooltip("화면 경계의 기본 이동 속도(월드 단위/초). 0이면 화면 안쪽에서는 이동하지 않습니다. 가두기/전체화면에서도 이 속도를 사용합니다.")]
    [Min(0)] public float boundarySpeed = 12;
    [Tooltip("화면 밖 거리 끝의 최대 이동 속도(월드 단위/초). 기본 속도보다 작은 값은 기본 속도로 보정합니다.")]
    [Min(0)] public float maximumSpeed = 30;
    [Tooltip("이동 시작·방향 변경 시 속도 응답률(1/초). 클수록 빨리 반응합니다. 0이면 보간 없이 즉시 적용하며, 영역 이탈은 항상 즉시 정지합니다.")]
    [Min(0)] public float acceleration = 20;
    [Tooltip("렌더 높이1080px에서 기본 커서 이미지 전체 크기(px). 기본48, 범위1~512. 렌더 높이에 비례하며 Game뷰 표시 배율을 중복 적용하지 않습니다. 원본512px 유지.")]
    [Range(1, 512)] public float normalCursorSize = 48;
    [Tooltip("렌더 높이1080px에서 스크롤 커서 이미지 전체 크기(px). 기본96으로 기본 커서의 가로·세로2배. 범위1~512. 렌더 높이에 비례하며 원본512px 유지.")]
    [Range(1, 512)] public float scrollCursorSize = 96;

    private static JcPointerProfile cached;
    public static JcPointerProfile Current
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<JcPointerProfile>("JcPointerProfile");
            if (cached == null)
            {
                cached = CreateInstance<JcPointerProfile>();
                cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return cached;
        }
    }
}
