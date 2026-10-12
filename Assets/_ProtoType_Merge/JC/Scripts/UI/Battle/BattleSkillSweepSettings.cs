using UnityEngine;

/// <summary>전투 스킬 5버튼의 공통 시각 설정. 출전 버튼 셰이더의 시각 옵션만 제공하며 사용 가능 판정은 하지 않습니다.</summary>
public class BattleSkillSweepSettings : MonoBehaviour
{
    [Tooltip("사용 가능한 히어로/코어 버튼의 반복 강조를 켭니다. 선택 테두리는 별도입니다.")]
    public bool showAvailableEffect = true;
    [Header("외곽 광선 — 출전 버튼과 같은 옵션")]
    [Tooltip("외곽 광선의 색입니다.")] public Color glowColor = new Color(.2f, .65f, 1f, 1f);
    [Tooltip("버튼 높이 대비 바깥 광선 폭입니다. 0이면 바깥 확장 없음.")]
    [Range(0, .2f)] public float glowWidth = .008f;
    [Tooltip("버튼 높이 대비 안쪽 광선 폭입니다. 0이면 안쪽 확장 없음.")]
    [Range(0, .4f)] public float glowInnerWidth = .025f;
    [Tooltip("광선 강도입니다. 0이면 광선을 끕니다.")] [Range(0, 8)] public float glowIntensity = 1.2f;
    [Tooltip("스프라이트 밝기 경계의 강조량입니다. 0이면 알파 외곽만 사용합니다.")] [Range(0, 3)] public float glowLumEdge = 1;
    [Header("스윕")]
    [Tooltip("지나가는 빛띠 텍스처입니다. 기존 UIGleamSweep을 사용합니다.")] public Texture2D gleamTexture;
    [Tooltip("빛띠의 색입니다.")] public Color sweepColor = new Color(.2f, .65f, 1f, 1f);
    [Tooltip("빛띠 강도입니다. 0이면 스윕을 숨깁니다.")] [Range(0, 8)] public float sweepIntensity = .8f;
    [Tooltip("빛띠의 기울기(도)입니다.")] [Range(-90, 90)] public float gleamTilt = 18;
    [Tooltip("빛띠 이동 범위 배율입니다.")] [Range(.2f, 3)] public float gleamScale = 1.2f;
    [Tooltip("빛띠 텍스처의 가로 폭 배율입니다.")] [Range(.05f, 2)] public float gleamWidth = 1;
    [Tooltip("빛띠 경계의 부드러움입니다. 광택 텍스처 가로 UV 기준이며 0은 기존 외형, 값이 클수록 양쪽 경계가 넓게 흐려집니다. 0.025는 약한 흐림입니다. 설정이 연결된 버튼에 적용됩니다.")]
    [InspectorName("빛띠 경계 부드러움"), Range(0, .1f)] public float gleamSoftness;
    [Tooltip("매 주기 시작 후 대기 시간(초)입니다.")] [Range(0, 10)] public float sweepDelay;
    [Tooltip("빛띠 이동 시간(초)입니다. 출전 버튼 셰이더와 같은 시간 계산을 사용합니다.")] [Range(.05f, 10)] public float sweepDuration = .6f;
    [Tooltip("반복 주기(초)입니다. 지연+이동 시간 이상으로 설정하는 것을 권장합니다. 배속과 무관합니다.")] [Range(.1f, 30)] public float sweepCycle = 2.5f;
    [Header("블룸")]
    [Tooltip("번짐에 섞을 색입니다.")] public Color bloomColor = new Color(.439f, .671f, .918f, 1);
    [Tooltip("원본 색과 블룸 색의 혼합량입니다. 0=원본, 1=지정 색.")] [Range(0, 1)] public float bloomTint = .85f;
    [Tooltip("번짐을 만드는 밝기 기준입니다. 높일수록 밝은 부분만 번집니다.")] [Range(0, 1)] public float bloomThreshold = .5f;
    [Tooltip("번짐 반경(텍스처 픽셀)입니다.")] [Range(2, 80)] public float bloomRadius = 14;
    [Tooltip("번짐 강도입니다. 0이면 끕니다.")] [Range(0, 12)] public float bloomIntensity = .4f;
    [Tooltip("번짐의 밝기 압축량입니다. 높일수록 강한 빛이 부드러워집니다.")] [Range(0, 4)] public float bloomSoftKnee = 2.2f;
    [Header("선택 테두리")]
    [Tooltip("선택 테두리 색입니다. 호버해도 유지됩니다.")] public Color selectedColor = new Color(1, .75f, .1f, 1);
    [Tooltip("선택 테두리 바깥 폭(버튼 높이 대비 비율)입니다.")] [Range(0, .2f)] public float selectedWidth = .025f;
    [Tooltip("선택 테두리 강도입니다. 0이면 발광을 끕니다.")] [Range(0, 8)] public float selectedIntensity = 2;
    [Header("선택 ON 순환 연출")]
    [Tooltip("켜면 선택 중 외곽의 두 빛이 시계 방향으로 순환하고 내부 빛띠가 반복됩니다. 끄면 기존 고정 테두리입니다. 연결된 버튼에만 적용합니다.")]
    [InspectorName("순환 선택 효과")] public bool selectedOrbit;
    [Tooltip("각 빛이 테두리를 한 바퀴 도는 실제 시간(초)입니다. 작을수록 빠르며 게임 시간 배율과 무관합니다.")]
    [InspectorName("외곽 한 바퀴 시간"), Min(.1f)] public float orbitPeriod = 2.4f;
    [Tooltip("두 빛 사이 간격 대비 꼬리 길이입니다. 클수록 밝은 구간이 길어집니다. 0.55가 기본입니다.")]
    [InspectorName("외곽 빛 꼬리 길이"), Range(.05f, .95f)] public float orbitTrail = .55f;
    [Tooltip("기존 테두리 위에 입히는 백색광입니다. 0이면 기존 순환광, 양수이면 백색 테두리와 넓은 색상 광선을 함께 표시합니다.")]
    [InspectorName("순환광 백색 테두리"), Range(0, 3)] public float orbitWhiteRim;
    [Tooltip("백색 테두리 모드의 색상 광선 번짐 배율입니다. 선택 테두리 바깥 폭을 기준으로 넓어집니다.")]
    [InspectorName("순환광 번짐 배율"), Range(1, 4)] public float orbitGlowSpread = 1;
    [Tooltip("내부 빛띠가 왼쪽에서 오른쪽으로 반복되는 실제 시간(초)입니다. 작을수록 빠릅니다.")]
    [InspectorName("내부 스윕 주기"), Min(.1f)] public float selectedSweepPeriod = 2;
    [Tooltip("버튼 가로 UV 기준의 내부 빛띠 폭입니다. 값이 클수록 넓고 부드러운 띠가 됩니다.")]
    [InspectorName("내부 스윕 폭"), Range(.02f, .4f)] public float selectedSweepWidth = .14f;
    [Tooltip("선택 중 내부 빛띠의 밝기입니다. 0이면 내부 스윕만 끕니다.")]
    [InspectorName("내부 스윕 강도"), Range(0, 3)] public float selectedSweepIntensity = .45f;
    [Tooltip("선택 중 내부를 지나는 빛띠의 색입니다. 외곽은 위의 선택 테두리 색을 사용합니다.")]
    [InspectorName("내부 스윕 색")] public Color selectedSweepColor = new Color(.75f, .9f, 1, 1);

    public void Apply(Material available, Material selected)
    {
        if (available != null)
        {
            available.SetColor("_GlowColor", glowColor); available.SetFloat("_GlowWidth", glowWidth);
            available.SetFloat("_GlowInnerWidth", glowInnerWidth); available.SetFloat("_GlowIntensity", glowIntensity);
            available.SetFloat("_GlowLumEdge", glowLumEdge); available.SetTexture("_GleamTex", gleamTexture);
            available.SetColor("_SweepColor", sweepColor); available.SetFloat("_SweepIntensity", sweepIntensity);
            available.SetFloat("_GleamTilt", gleamTilt); available.SetFloat("_GleamScale", gleamScale);
            available.SetFloat("_GleamWidth", gleamWidth); available.SetFloat("_SweepDelay", sweepDelay);
            available.SetFloat("_GleamSoftness", gleamSoftness);
            available.SetFloat("_SweepDuration", Mathf.Max(.05f, sweepDuration)); available.SetFloat("_SweepCycle", Mathf.Max(.1f, sweepCycle));
            available.SetColor("_BloomColor", bloomColor); available.SetFloat("_BloomTint", bloomTint);
            available.SetFloat("_BloomThreshold", bloomThreshold); available.SetFloat("_BloomRadius", bloomRadius);
            available.SetFloat("_BloomIntensity", bloomIntensity); available.SetFloat("_BloomSoftKnee", bloomSoftKnee);
        }
        if (selected != null)
        {
            selected.SetColor("_GlowColor", selectedColor); selected.SetFloat("_GlowWidth", selectedWidth);
            selected.SetFloat("_GlowIntensity", selectedIntensity);
            selected.SetFloat("_SelectedMotion", selectedOrbit ? 1 : 0);
            selected.SetFloat("_OrbitPeriod", Mathf.Max(.1f, orbitPeriod));
            selected.SetFloat("_OrbitTrail", orbitTrail);
            selected.SetFloat("_OrbitWhiteRim", orbitWhiteRim);
            // 순환광 선두가 원본 테두리를 덮고, 나머지 광선은 기존 가산 발광을 유지합니다.
            selected.SetFloat("_DstBlend", selectedOrbit && orbitWhiteRim > 0
                ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                : (float)UnityEngine.Rendering.BlendMode.One);
            selected.SetFloat("_OrbitGlowSpread", orbitGlowSpread);
            selected.SetFloat("_SelectedSweepPeriod", Mathf.Max(.1f, selectedSweepPeriod));
            selected.SetFloat("_SelectedSweepWidth", selectedSweepWidth);
            selected.SetFloat("_SelectedSweepIntensity", selectedSweepIntensity);
            selected.SetColor("_SelectedSweepColor", selectedSweepColor);
        }
    }
}
