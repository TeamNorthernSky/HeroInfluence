using UnityEngine;

/// <summary>
/// 화염 혀 메시의 '화르르' 솟구침. 높이(scale.y)를 펄스시키고
/// 셰이더의 _Flicker 위상을 인스턴스별로 줘서 제각각 일렁이게 한다.
/// 메시 화염 혀(FireTongue) 각 조각에 부착.
/// </summary>
public class FlameTonguePulse : MonoBehaviour
{
    [Tooltip("기준 높이 배수. 적정값 1~2.5.")]
    public float baseHeight = 1.6f;
    [Tooltip("높이 펄스 폭(배수). 적정값 0.2~0.8. 클수록 크게 솟구침.")]
    public float heightAmplitude = 0.5f;
    [Tooltip("펄스 속도. 적정값 2~8.")]
    public float pulseSpeed = 4f;
    [Tooltip("좌우 두께 펄스 폭. 적정값 0~0.3.")]
    public float widthAmplitude = 0.12f;

    Renderer _r;
    MaterialPropertyBlock _mpb;
    float _phase;
    Vector3 _baseScale;

    void Awake()
    {
        _r = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _phase = Random.value * 10f;
        _baseScale = transform.localScale;
    }

    void Update()
    {
        float t = Time.time * pulseSpeed + _phase;
        float h = baseHeight + Mathf.Sin(t) * heightAmplitude
                             + Mathf.Sin(t * 2.3f) * heightAmplitude * 0.4f;
        float w = 1f + Mathf.Sin(t * 1.7f + 1f) * widthAmplitude;
        transform.localScale = new Vector3(_baseScale.x * w, _baseScale.y * Mathf.Max(0.2f, h), _baseScale.z * w);

        if (_r != null)
        {
            _r.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Flicker", _phase * 6.28f);
            _r.SetPropertyBlock(_mpb);
        }
    }
}
