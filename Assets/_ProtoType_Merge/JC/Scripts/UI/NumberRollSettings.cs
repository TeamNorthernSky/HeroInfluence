using UnityEngine;

/// <summary>씬 UI 설정 오브젝트에 배치하고 각 RollingNumberText에 연결합니다.</summary>
public sealed class NumberRollSettings : MonoBehaviour
{
    [Tooltip("숫자가 목표값까지 바뀌는 전체 실제 시간(초)입니다. 변화량과 무관하며 0이면 즉시 표시합니다. 재생 시작 시 읽습니다.")]
    [Min(0f)] public float duration = 0.8f;
    [Tooltip("가로축은 경과 시간, 세로축은 진행률입니다. 기본은 끝에서 느려지는 감속입니다. 역행과 범위 초과는 제한됩니다. 재생 시작 시 읽습니다.")]
    public AnimationCurve progressCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));
}
