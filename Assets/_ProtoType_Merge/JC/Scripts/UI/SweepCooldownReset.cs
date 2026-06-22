using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260622] 호버 오버레이(BaseHoverOverlay)에 부착. 스윕 셰이더의 _SweepTime을 직접 구동한다.
/// - OnEnable(=호버 시작)에 타이머를 0으로 리셋 → 매 호버마다 스윕이 처음부터 재생(이전 쿨다운 비이월).
/// - 매 프레임 (Time.time - enableTime)을 _SweepTime에 먹인다. 셰이더는 _Time.y 대신 이 값을 쓰므로
///   시간 기준(_Time.y vs Time.time) 불일치 문제가 없다.
/// 호버는 동시에 하나만 활성되므로 공유 머티리얼이라도 정확하다.
/// </summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class SweepCooldownReset : MonoBehaviour
{
    private Image image;
    private float enableTime;

    private void Awake() => image = GetComponent<Image>();

    private void OnEnable()
    {
        if (image == null) image = GetComponent<Image>();
        enableTime = Time.time;
        Push();
    }

    private void Update() => Push();

    private void Push()
    {
        var m = image != null ? image.material : null;
        if (m != null && m.HasProperty("_SweepTime"))
            m.SetFloat("_SweepTime", Time.time - enableTime);
    }
}
