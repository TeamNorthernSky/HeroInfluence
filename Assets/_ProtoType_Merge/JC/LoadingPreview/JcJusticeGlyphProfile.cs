using UnityEngine;

namespace JC.LoadingPreview
{
    // JC | 2026-11-12 | 비교 씬 전용 저장값입니다. 실제 로딩은 이 프로필을 참조하지 않습니다.
    public sealed class JcJusticeGlyphProfile : ScriptableObject
    {
        [Tooltip("원형 중심 기준 UI 좌표. X는 오른쪽, Y는 위쪽입니다.")] public Vector2 startCenter = new Vector2(-30f, 0f);
        [Tooltip("타격 후 유지할 중심의 UI 좌표입니다.")] public Vector2 endCenter = new Vector2(6f, 0f);
        [Range(.25f, 2f), Tooltip("기존 수정안 초기 크기 기준 배율입니다. 1이 기준입니다.")] public float initialSize = 1f;
        [Range(.25f, 2f), Tooltip("기존 수정안 최종 크기 기준 배율입니다. 1이 기준입니다.")] public float finalSize = 1f;
    }
}
