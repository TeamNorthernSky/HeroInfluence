using System.Collections.Generic;
using UnityEngine;
using Cell = ASB.Work.BattleGrid.GridCell;

/// <summary>셀의 투명도와 파란 테두리 스윕을 조절합니다. 대상 판정과 공유 재질 값은 변경하지 않습니다.</summary>
public class BattleCellVisualSettings : MonoBehaviour
{
    [Tooltip("현재 턴의 노란 셀 불투명도입니다. 0=투명, 1=원래 재질의 불투명도.")]
    [Range(0, 1)] public float currentTurnAlpha = .65f;
    [Tooltip("직접 선택 가능한 보라 셀 불투명도입니다. 0=투명, 1=원래 재질.")]
    [Range(0, 1)] public float selectableAlpha = .65f;
    [Tooltip("확정 범위의 빨간 셀 불투명도입니다. 0=투명, 1=원래 재질.")]
    [Range(0, 1)] public float confirmedAreaAlpha = .65f;
    [Tooltip("선택 불가 검정 셀 불투명도입니다. 0=투명, 1=원래 재질.")]
    [Range(0, 1)] public float unavailableAlpha = .35f;
    [Header("파란 테두리 스윕 글로우")]
    [Tooltip("현재 표시 중인 셀의 파란 테두리에만 빛띠를 표시합니다. 중앙 색상과 금색 테두리는 빛나지 않습니다.")]
    public bool useSweepGlow = true;
    [Tooltip("지나가는 빛의 색입니다. 알파는 효과 전체 강도에 곱해집니다.")]
    [ColorUsage(true, true)] public Color sweepColor = new Color(.35f, .75f, 1f, 1f);
    [Tooltip("빛의 밝기입니다. 0이면 효과를 숨깁니다. 셀 불투명도도 함께 적용됩니다.")]
    [Range(0, 8)] public float sweepIntensity = 2f;
    [Tooltip("빛띠의 반쪽 폭입니다. 셀 텍스처 한 변을 1로 하는 비율이며 작을수록 가늘어집니다.")]
    [Range(.01f, .5f)] public float sweepWidth = .12f;
    [Tooltip("빛띠 주변의 부드러운 번짐 폭입니다. 0이면 중심 빛띠만, 1이면 넓게 번집니다. 번짐도 파란 테두리 안에 제한됩니다.")]
    [Range(0, 1)] public float sweepSoftness = .5f;
    [Tooltip("빛띠의 기울기(도)입니다. 0이면 수직 띠가 왼쪽에서 오른쪽으로 이동합니다. 셀 텍스처 방향을 기준으로 합니다.")]
    [Range(-80, 80)] public float sweepTilt = 18f;
    [Tooltip("빛띠 한 번이 셀을 통과하는 시간(초)입니다. 전투 배속과 무관합니다.")]
    [Min(.05f)] public float sweepDuration = .8f;
    [Tooltip("빛띠가 지나간 뒤 다음 이동까지 쉬는 시간(초)입니다. 0이면 바로 반복합니다.")]
    [Min(0)] public float sweepInterval = 1.5f;
    [Tooltip("빛띠의 이동 방향을 반대로 바꿉니다.")]
    public bool reverseSweep;
    [Tooltip("기존 GridCell의 Plane Renderer 12개입니다. 유닛 Renderer에 투명도가 적용되지 않도록 명시적으로 연결합니다.")]
    [SerializeField] private Renderer[] cellRenderers = new Renderer[0];

    private readonly Dictionary<Cell, Renderer> renderers = new Dictionary<Cell, Renderer>();
    private readonly Dictionary<Renderer, MaterialPropertyBlock> originals = new Dictionary<Renderer, MaterialPropertyBlock>();
    private MaterialPropertyBlock working;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public void Apply(Cell cell, float alpha)
    {
        if (!isActiveAndEnabled || cell == null) return;
        if (!renderers.TryGetValue(cell, out var renderer))
        {
            foreach (var candidate in cellRenderers)
            {
                if (candidate == null) continue;
                var owner = candidate.GetComponentInParent<Cell>();
                if (owner != null) renderers[owner] = candidate;
            }
            renderers.TryGetValue(cell, out renderer);
        }
        if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(BaseColor)) return;
        if (!originals.ContainsKey(renderer))
        {
            var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original); originals.Add(renderer, original);
        }
        if (working == null) working = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(working);
        var color = renderer.sharedMaterial.GetColor(BaseColor);
        color.a *= Mathf.Clamp01(alpha);
        working.SetColor(BaseColor, color);
        if (renderer.sharedMaterial.HasProperty("_SweepEnabled"))
        {
            working.SetFloat("_SweepEnabled", useSweepGlow ? 1f : 0f);
            working.SetColor("_SweepColor", sweepColor);
            working.SetFloat("_SweepIntensity", sweepIntensity);
            working.SetFloat("_SweepWidth", sweepWidth);
            working.SetFloat("_SweepSoftness", sweepSoftness);
            working.SetFloat("_SweepTilt", sweepTilt);
            working.SetFloat("_SweepDuration", Mathf.Max(.05f, sweepDuration));
            working.SetFloat("_SweepInterval", Mathf.Max(0, sweepInterval));
            working.SetFloat("_SweepReverse", reverseSweep ? 1f : 0f);
            working.SetFloat("_SweepTime", Time.unscaledTime);
        }
        renderer.SetPropertyBlock(working);
    }

    private void OnDisable()
    {
        foreach (var item in originals) if (item.Key != null) item.Key.SetPropertyBlock(item.Value);
        originals.Clear(); renderers.Clear();
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(BattleCellVisualSettings))]
public class BattleCellVisualSettingsEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.HelpBox("스윕은 셀 원본 이미지의 파란 테두리에만 적용됩니다. 전투 중 CellBox 값을 바꾸면 즉시 반영됩니다. 셀 전체의 투명도는 위의 상태별 불투명도를 따릅니다.", UnityEditor.MessageType.Info);
    }
}
#endif
