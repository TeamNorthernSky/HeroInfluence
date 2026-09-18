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
            available.SetFloat("_SweepDuration", Mathf.Max(.05f, sweepDuration)); available.SetFloat("_SweepCycle", Mathf.Max(.1f, sweepCycle));
            available.SetColor("_BloomColor", bloomColor); available.SetFloat("_BloomTint", bloomTint);
            available.SetFloat("_BloomThreshold", bloomThreshold); available.SetFloat("_BloomRadius", bloomRadius);
            available.SetFloat("_BloomIntensity", bloomIntensity); available.SetFloat("_BloomSoftKnee", bloomSoftKnee);
        }
        if (selected != null)
        {
            selected.SetColor("_GlowColor", selectedColor); selected.SetFloat("_GlowWidth", selectedWidth);
            selected.SetFloat("_GlowIntensity", selectedIntensity);
        }
    }
}
