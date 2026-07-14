using UnityEngine;

/// <summary>
/// 불 광원의 흔들림. 노이즈 기반으로 intensity와 위치를 미세하게 떨어
/// 타오르는 불의 일렁이는 조명을 만든다. Point Light에 부착.
/// </summary>
[RequireComponent(typeof(Light))]
public class FireLightFlicker : MonoBehaviour
{
    Light _light;

    [Header("Intensity")]
    [Tooltip("광원의 기준 밝기. 깜빡임은 이 값을 중심으로 위아래로 흔들린다. 적정값 3~10 (Point Light, 대형 구체 기준).")]
    public float baseIntensity = 6f;

    [Tooltip("밝기 흔들림 폭. baseIntensity ± 이 값 범위로 깜빡인다. 적정값 0.5~3. baseIntensity의 20~40% 정도가 자연스럽다.")]
    public float intensityAmplitude = 1.5f;

    [Tooltip("밝기 깜빡임 속도(노이즈 샘플 주파수). 적정값 4~12. 높을수록 빠르고 잘게 떨린다.")]
    public float intensitySpeed = 7f;

    [Header("Position Jitter (local)")]
    [Tooltip("광원이 흔들리는 위치 이동 폭(로컬, units). 적정값 0.05~0.3. 너무 크면 그림자/하이라이트가 출렁인다.")]
    public float posAmplitude = 0.12f;

    [Tooltip("위치 흔들림 속도. 적정값 5~12. intensitySpeed와 살짝 다르게 두면 더 유기적이다.")]
    public float posSpeed = 9f;

    Vector3 _basePos;
    float _seed;

    void Awake()
    {
        _light = GetComponent<Light>();
        _basePos = transform.localPosition;
        _seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time;
        // two desynced perlin samples => organic flicker
        float f = Mathf.PerlinNoise(_seed, t * intensitySpeed);
        float f2 = Mathf.PerlinNoise(_seed + 13.7f, t * intensitySpeed * 0.5f);
        float flick = (f * 0.7f + f2 * 0.3f) * 2f - 1f; // -1..1
        _light.intensity = Mathf.Max(0f, baseIntensity + flick * intensityAmplitude);

        float jx = Mathf.PerlinNoise(_seed + 1f, t * posSpeed) - 0.5f;
        float jy = Mathf.PerlinNoise(_seed + 2f, t * posSpeed) - 0.5f;
        float jz = Mathf.PerlinNoise(_seed + 3f, t * posSpeed) - 0.5f;
        transform.localPosition = _basePos + new Vector3(jx, jy, jz) * posAmplitude;
    }
}
