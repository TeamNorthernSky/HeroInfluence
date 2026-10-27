using UnityEngine;

/// <summary>
/// 부모(구체 중심) 둘레를 기울어진 평면에서 도는 회전체.
/// 끝의 Tip 자식에 TrailRenderer를 달면 궤도 리본이 그려진다.
/// </summary>
public class OrbitSpinner : MonoBehaviour
{
    [Header("Orbit")]
    [Tooltip("자전축(로컬). 기울여 두면 궤도면이 비스듬해진다. 정규화는 자동. 예: (0.3,1,0.2).")]
    public Vector3 axis = Vector3.up;

    [Tooltip("회전 속도(도/초). 적정값 30~180. 부호로 방향 반전.")]
    public float degreesPerSecond = 90f;

    [Tooltip("시작 위상 오프셋(도). 여러 리본을 0/120/240 등으로 분산.")]
    public float startPhase = 0f;

    void OnEnable()
    {
        // 시작 위상 적용
        transform.localRotation = Quaternion.AngleAxis(startPhase, axis.sqrMagnitude > 0 ? axis.normalized : Vector3.up);
    }

    void Update()
    {
        Vector3 a = axis.sqrMagnitude > 0 ? axis.normalized : Vector3.up;
        transform.Rotate(a, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
