using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260622 / 260628 churn 수정] 호버 오버레이(BaseHoverOverlay)에 부착. 스윕 셰이더의 _SweepTime을 직접 구동한다.
/// - OnEnable(=호버 시작)에 타이머를 0으로 리셋 → 매 호버마다 스윕이 처음부터 재생(이전 쿨다운 비이월).
/// - 매 프레임 (Time.time - enableTime)을 _SweepTime에 먹인다. 셰이더는 _Time.y 대신 이 값을 쓰므로
///   시간 기준(_Time.y vs Time.time) 불일치 문제가 없다.
///
/// [churn 수정] 과거에는 image.material(=공유 머티리얼 .asset)에 직접 SetFloat 하여
/// 플레이할 때마다 UIHoverGlowSweep.mat 에셋이 변경(dirty)되어 팀 전체에 커밋 노이즈가 발생했다.
/// 이제는 Awake/OnEnable에서 공유 머티리얼을 1회 복제한 per-instance 인스턴스를 만들어 그쪽에만 쓴다.
/// → 공유 .asset은 절대 건드리지 않으며, 각 오버레이가 자기 인스턴스를 가지므로 동시 호버에도 안전하다.
/// (UGUI/CanvasRenderer는 Renderer처럼 MaterialPropertyBlock을 쓸 수 없어 인스턴스 머티리얼이 정석.)
/// </summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class SweepCooldownReset : MonoBehaviour
{
    private Image image;
    private Material instanced;   // 공유 .asset이 아닌 per-instance 복제본. 런타임 전용.
    private float enableTime;

    private void Awake()
    {
        image = GetComponent<Image>();
        EnsureInstancedMaterial();
    }

    private void OnEnable()
    {
        EnsureInstancedMaterial();
        enableTime = Time.time;
        Push();
    }

    private void OnDestroy()
    {
        // 런타임 생성 인스턴스는 직접 정리해 누수 방지.
        if (instanced != null) Destroy(instanced);
    }

    /// <summary>
    /// 공유 머티리얼을 1회 복제해 image.material을 인스턴스로 교체한다.
    /// 스윕 머티리얼(_SweepTime 보유)이 아니면 아무것도 하지 않는다.
    /// </summary>
    private void EnsureInstancedMaterial()
    {
        if (image == null) image = GetComponent<Image>();
        if (image == null) return;

        if (instanced == null)
        {
            var src = image.material;
            if (src == null || !src.HasProperty("_SweepTime")) return; // 스윕 오버레이가 아님
            instanced = new Material(src) { name = src.name + " (Instance)" };
        }

        if (image.material != instanced)
            image.material = instanced;
    }

    private void Update() => Push();

    private void Push()
    {
        if (instanced != null)
            instanced.SetFloat("_SweepTime", Time.time - enableTime);
    }
}
