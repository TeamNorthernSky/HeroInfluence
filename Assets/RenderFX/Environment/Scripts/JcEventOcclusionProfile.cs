using UnityEngine;

// JC | 2026-11-04 | 이벤트 가림 표시의 저장값을 실시간 초안과 분리하며 원본 NPC/건물 재질은 수정하지 않습니다.
[CreateAssetMenu(menuName = "JC Environment/NPC·이벤트 가림 표시 프로필")]
public sealed class JcEventOcclusionProfile : ScriptableObject
{
    [Range(0, 1), Tooltip("건물에 가려진 NPC·이벤트 메시의 표시 불투명도입니다. 0은 표시하지 않습니다.")]
    public float opacity = .25f;
}
