using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 구체 둘레를 도는 궤도 리본들을 개수만큼 자동 생성·관리.
/// 인스펙터에서 count(1~12)를 바꾸면 리본이 다시 만들어진다.
/// 각 리본 = OrbitSpinner + Tip + TrailRenderer(가산 발광).
/// </summary>
[ExecuteAlways]
public class RibbonOrbitController : MonoBehaviour
{
    [Header("Count")]
    [Range(1, 12)]
    [Tooltip("궤도 리본 개수. 1~12. 바꾸면 자동 리빌드. 많을수록 화려하지만 오버드로우 증가.")]
    public int count = 6;

    [Header("Material")]
    [Tooltip("리본 트레일 머티리얼(가산 발광 권장). 비우면 Assets/_Testbed/Ribbon_Additive.mat 자동 로드.")]
    public Material ribbonMaterial;

    [Header("Radius (구체 반경보다 크게)")]
    [Tooltip("궤도 기본 반경. 구체 반경보다 크게 두면 리본이 바깥을 돈다. 적정값 1.5~4.")]
    public float baseRadius = 2.4f;
    [Tooltip("리본별 반경 편차. 적정값 0~1. 클수록 궤도가 들쭉날쭉.")]
    public float radiusJitter = 0.45f;

    [Header("Speed")]
    [Tooltip("기본 회전 속도(도/초). 적정값 40~160.")]
    public float baseSpeed = 95f;
    [Tooltip("리본별 속도 편차(도/초). 적정값 0~60.")]
    public float speedJitter = 35f;
    [Tooltip("켜면 리본들이 번갈아 반대 방향으로 돈다(교차 궤도).")]
    public bool alternateDirection = true;

    [Header("Trail")]
    [Tooltip("리본 폭(머리쪽). 적정값 0.1~0.4. 구체 축소했으니 상대적으로 크게.")]
    public float width = 0.22f;
    [Tooltip("꼬리쪽 폭 배수(테이퍼). 적정값 0.1~0.6.")]
    public float tailWidthScale = 0.25f;
    [Tooltip("트레일 잔상 길이(초). 짧으면 혜성꼬리, 길면 닫힌 링. 적정값 0.3~1.5.")]
    public float trailTime = 0.7f;

    [Header("Tilt")]
    [Tooltip("궤도면 기울기 분산(도). 0이면 전부 수평궤도, 크면 다양한 평면. 적정값 30~90.")]
    public float tiltSpread = 70f;

    void OnEnable()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (this == null) return;
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Rebuild();
        };
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (ribbonMaterial == null)
        {
#if UNITY_EDITOR
            ribbonMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Testbed/Ribbon_Additive.mat");
#endif
        }

        // clear existing children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        int n = Mathf.Clamp(count, 1, 12);
        for (int i = 0; i < n; i++)
        {
            float frac = (float)i / n;
            float ang = 360f * frac;
            // deterministic per-index variation
            float h = Mathf.Abs(Mathf.Sin((i + 1) * 12.9898f) * 43758.5453f);
            h -= Mathf.Floor(h); // 0..1 pseudo-random
            float tilt = tiltSpread * (h - 0.5f) * 2f;

            Vector3 axis = Quaternion.AngleAxis(ang, Vector3.up)
                         * (Quaternion.AngleAxis(tilt, Vector3.forward) * Vector3.up);

            float radius = baseRadius + radiusJitter * (h - 0.5f) * 2f;
            float dir = (alternateDirection && (i % 2 == 1)) ? -1f : 1f;
            float speed = (baseSpeed + speedJitter * (h - 0.5f) * 2f) * dir;

            BuildOrbiter(i, axis, speed, ang, radius);
        }
    }

    void BuildOrbiter(int i, Vector3 axis, float speed, float phase, float radius)
    {
        var orb = new GameObject("Orbiter_" + i);
        orb.transform.SetParent(transform, false);
        orb.transform.localPosition = Vector3.zero;
        var sp = orb.AddComponent<OrbitSpinner>();
        sp.axis = axis; sp.degreesPerSecond = speed; sp.startPhase = phase;

        var tip = new GameObject("Tip");
        tip.transform.SetParent(orb.transform, false);
        tip.transform.localPosition = new Vector3(radius, 0f, 0f);

        var tr = tip.AddComponent<TrailRenderer>();
        tr.time = trailTime;
        tr.minVertexDistance = 0.03f;
        tr.widthMultiplier = width;
        var wc = new AnimationCurve();
        wc.AddKey(0f, 1f); wc.AddKey(1f, Mathf.Clamp01(tailWidthScale));
        tr.widthCurve = wc;
        tr.numCapVertices = 4; tr.numCornerVertices = 4;
        tr.alignment = LineAlignment.View;
        tr.textureMode = LineTextureMode.Stretch;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.generateLightingData = false;
        tr.sharedMaterial = ribbonMaterial;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        tr.colorGradient = grad;
    }
}
