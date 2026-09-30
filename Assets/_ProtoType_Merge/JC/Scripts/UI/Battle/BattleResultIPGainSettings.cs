using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>전투 결과 IP 표시 전용 설정. 실제 보상에는 관여하지 않습니다.</summary>
public sealed class BattleResultIPGainSettings : MonoBehaviour
{
    [Tooltip("모든 카드의 EXP·랭크 연출이 끝난 뒤 IP 숫자 연출 시작까지의 대기 시간(실제 초)입니다. 0이면 해당 단계 종료 직후 시작합니다. 대기 중 배경 클릭은 완료 잔상으로, 확인은 최종값으로 진행합니다.")]
    [Min(0f)] public float startDelay = 0.5f;
    [Tooltip("획득량을 보유량으로 옮기는 전체 시간(실제 초)입니다. 획득량과 무관하며 0이면 즉시 완료합니다.")]
    [Min(0f)] public float duration = 0.8f;
    [Tooltip("가로는 경과 시간, 세로는 진행률입니다. 기본은 끝에서 느려지는 감속입니다. 0~1로 제한하고 역행을 막으며 마지막 값은 정확히 맞춥니다.")]
    public AnimationCurve progressCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));
    [Tooltip("왼쪽 보유 IP 숫자의 색입니다.")]
    public Color currentColor = Color.white;
    [Tooltip("오른쪽 남은 획득량과 + 기호의 색입니다.")]
    public Color gainColor = new Color(1f, 0.78f, 0.15f);
    [Tooltip("보유량과 + 기호 사이 여백입니다. 원본 폰트 크기의 배수이며 0이면 추가 여백이 없습니다. 크게 하면 두 영역이 멀어집니다. 다음 카드 표시부터 적용합니다.")]
    [Min(0f)] public float currentToPlusGap = 0.8f;
    [Tooltip("+ 기호와 획득량 사이 여백입니다. 원본 폰트 크기의 배수이며 0이면 추가 여백이 없습니다. 획득량은 왼쪽 정렬하여 자릿수가 줄어도 앞에 빈칸을 남기지 않습니다. 다음 카드 표시부터 적용합니다.")]
    [Min(0f)] public float plusToGainGap = 0f;
    [Tooltip("목표값 도달 후 숫자 잔상이 사라지는 시간(초)입니다. 0이면 잔상을 생략합니다.")]
    [Min(0f)] public float echoDuration = 0.3f;
    [Tooltip("완료 잔상의 마지막 배율입니다. 원래 숫자의 크기는 변하지 않습니다.")]
    [Min(1f)] public float echoScale = 1.35f;
    [Tooltip("완료 잔상의 시작 불투명도입니다. 종료 시 0이 됩니다.")]
    [Range(0f, 1f)] public float echoAlpha = 0.45f;
    private static readonly List<BattleResultIPGainSettings> Active = new List<BattleResultIPGainSettings>();
    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);
    public static BattleResultIPGainSettings ForScene(Scene scene)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            var s = Active[i];
            if (!s) { Active.RemoveAt(i); continue; }
            if (s.isActiveAndEnabled && s.gameObject.scene == scene) return s;
        }
        return null;
    }
}
