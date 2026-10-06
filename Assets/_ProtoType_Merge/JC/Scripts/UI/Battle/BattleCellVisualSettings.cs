using System.Collections.Generic;
using UnityEngine;
using Cell = ASB.Work.BattleGrid.GridCell;

/// <summary>기존 셀 상태에 맞춰 테두리 프리팹과 스윕을 표시합니다. 대상 판정은 변경하지 않습니다.</summary>
public class BattleCellVisualSettings : MonoBehaviour
{
    [Tooltip("설정 씬에서 저장한 외형·스윕·상하 운동 프로필입니다. 연결된 경우 아래 구형 스윕 값 대신 프로필을 사용합니다.")]
    public BattleCellBorderProfile profile;
    [Tooltip("현재 턴의 노란 테두리 불투명도입니다. 내부 채움은 프리팹에서 별도로 조절합니다.")]
    [Range(0, 1)] public float currentTurnAlpha = .65f;
    [Tooltip("직접 선택 가능한 보라 테두리 불투명도입니다. 내부 채움은 프리팹에서 별도로 조절합니다.")]
    [Range(0, 1)] public float selectableAlpha = .65f;
    [Tooltip("확정 범위의 빨간 테두리 불투명도입니다. 내부 채움은 프리팹에서 별도로 조절합니다.")]
    [Range(0, 1)] public float confirmedAreaAlpha = .65f;
    [Tooltip("선택 불가 테두리 불투명도입니다. 프리팹 내부 채움의 25% 불투명도와 독립적입니다.")]
    [Range(0, 1)] public float unavailableAlpha = .35f;
    [Header("테두리 스윕 글로우")]
    [Tooltip("프리팹의 안쪽·바깥쪽 테두리에 같은 빛띠를 표시합니다. 내부 채움은 빛나지 않습니다.")]
    public bool useSweepGlow = true;
    [Tooltip("지나가는 빛의 색입니다. 알파는 효과 전체 강도에 곱해집니다.")]
    [ColorUsage(true, true)] public Color sweepColor = Color.white;
    [Tooltip("빛의 밝기입니다. 0이면 효과를 숨깁니다. 셀 불투명도도 함께 적용됩니다.")]
    [Range(0, 8)] public float sweepIntensity = 2f;
    [Tooltip("빛띠의 반쪽 폭입니다. 셀 텍스처 한 변을 1로 하는 비율이며 작을수록 가늘어집니다.")]
    [Range(.01f, .5f)] public float sweepWidth = .12f;
    [Tooltip("빛띠 주변의 부드러운 번짐 폭입니다. 0이면 중심 빛띠만, 1이면 넓게 번집니다. 번짐은 테두리 안에 제한됩니다.")]
    [Range(0, 1)] public float sweepSoftness = .5f;
    [Tooltip("빛띠의 기울기(도)입니다. 0이면 수직 띠가 왼쪽에서 오른쪽으로 이동합니다. 셀 텍스처 방향을 기준으로 합니다.")]
    [Range(-80, 80)] public float sweepTilt = 18f;
    [Tooltip("빛띠 한 번이 셀을 통과하는 시간(초)입니다. 전투 배속과 무관합니다.")]
    [Min(.05f)] public float sweepDuration = .8f;
    [Tooltip("빛띠가 지나간 뒤 다음 이동까지 쉬는 시간(초)입니다. 0이면 바로 반복합니다.")]
    [Min(0)] public float sweepInterval = 1.5f;
    [Tooltip("빛띠의 이동 방향을 반대로 바꿉니다.")]
    public bool reverseSweep;
    [Header("상태별 테두리 프리팹")]
    [Tooltip("노란 현재 턴 표시입니다. 색상·두께·내부 채움은 이 프리팹에서 조절합니다.")]
    public BattleCellBorderVisual currentTurnPrefab;
    [Tooltip("보라색 선택 가능 표시입니다.")]
    public BattleCellBorderVisual selectablePrefab;
    [Tooltip("빨간 확정 범위 표시입니다.")]
    public BattleCellBorderVisual confirmedAreaPrefab;
    [Tooltip("어두운 회색 테두리와 빨간 25% 채움의 선택 불가 표시입니다.")]
    public BattleCellBorderVisual unavailablePrefab;
    [Tooltip("기존 GridCell의 Plane Renderer 12개입니다. 유닛 Renderer에 투명도가 적용되지 않도록 명시적으로 연결합니다.")]
    [SerializeField] private Renderer[] cellRenderers = new Renderer[0];

    private readonly Dictionary<Cell, Renderer> renderers = new Dictionary<Cell, Renderer>();
    private readonly Dictionary<Renderer, MaterialPropertyBlock> originals = new Dictionary<Renderer, MaterialPropertyBlock>();
    private MaterialPropertyBlock working;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private sealed class Presentation
    {
        public Renderer source;
        public bool sourceEnabled;
        public readonly BattleCellBorderMotion motion = new BattleCellBorderMotion();
        public readonly Dictionary<BattleCellVisualState, BattleCellBorderVisual> views = new Dictionary<BattleCellVisualState, BattleCellBorderVisual>();
        public readonly Dictionary<BattleCellVisualState, BattleCellBorderVisual> templates = new Dictionary<BattleCellVisualState, BattleCellBorderVisual>();
    }
    private readonly Dictionary<Cell, Presentation> presentations = new Dictionary<Cell, Presentation>();

    public void Apply(Cell cell, float alpha, BattleCellVisualState state = BattleCellVisualState.Hidden)
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
        if (renderer == null) return;
        if (ApplyPrefab(cell, renderer, state, alpha)) return;
        if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(BaseColor)) return;
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

    private BattleCellBorderVisual PrefabFor(BattleCellVisualState state)
    {
        switch (state)
        {
            case BattleCellVisualState.CurrentTurn: return currentTurnPrefab;
            case BattleCellVisualState.Selectable: return selectablePrefab;
            case BattleCellVisualState.ConfirmedArea: return confirmedAreaPrefab;
            case BattleCellVisualState.Unavailable: return unavailablePrefab;
            default: return null;
        }
    }

    private bool ApplyPrefab(Cell cell, Renderer source, BattleCellVisualState state, float alpha)
    {
        bool configured = currentTurnPrefab != null && selectablePrefab != null && confirmedAreaPrefab != null && unavailablePrefab != null;
        if (!configured)
        {
            if (presentations.TryGetValue(cell, out var old))
            {
                foreach (var view in old.views.Values) if (view != null) view.gameObject.SetActive(false);
                source.enabled = old.sourceEnabled;
            }
            return false;
        }
        if (!presentations.TryGetValue(cell, out var presentation))
        {
            presentation = new Presentation { source = source, sourceEnabled = source.enabled };
            presentations.Add(cell, presentation);
        }
        source.enabled = false;
        if (state == BattleCellVisualState.Hidden) presentation.motion.Reset();
        float lift = profile != null && state != BattleCellVisualState.Hidden ? presentation.motion.Evaluate(state, profile.settings, Time.unscaledTimeAsDouble) : 0f;
        var prefab = PrefabFor(state);
        if (prefab != null)
        {
            if (!presentation.views.TryGetValue(state, out var view) || view == null || presentation.templates[state] != prefab)
            {
                if (view != null) DestroyView(view);
                view = Instantiate(prefab, source.transform.parent, false);
                view.name = prefab.name;
                view.transform.localPosition = source.transform.localPosition;
                view.transform.localRotation = source.transform.localRotation;
                // 표시 크기는 프리팹 size가 결정합니다. 원본 10m Plane의 축소 배율을 복사하지 않습니다.
                view.transform.localScale = Vector3.one;
                view.SetDriven(true);
                presentation.views[state] = view;
                presentation.templates[state] = prefab;
            }
        }
        foreach (var pair in presentation.views)
        {
            if (pair.Value == null) continue;
            bool visible = pair.Key == state && prefab != null;
            if (pair.Value.gameObject.activeSelf != visible) pair.Value.gameObject.SetActive(visible);
            if (visible)
            {
                pair.Value.transform.localPosition = source.transform.localPosition + Vector3.up * lift;
                if (profile != null) pair.Value.RenderProfile(profile.settings, state, alpha, Time.unscaledTime, source.transform.position.y);
                else pair.Value.Render(this, alpha, Time.unscaledTime);
            }
        }
        return true;
    }

    private static void DestroyView(BattleCellBorderVisual view)
    {
        view.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(view.gameObject);
        else DestroyImmediate(view.gameObject);
    }

    private void OnDisable()
    {
        foreach (var item in presentations.Values)
        {
            if (item.source != null) item.source.enabled = item.sourceEnabled;
            foreach (var view in item.views.Values) if (view != null) DestroyView(view);
        }
        presentations.Clear();
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
        serializedObject.Update();
        UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
        if (serializedObject.FindProperty("profile").objectReferenceValue == null)
            DrawPropertiesExcluding(serializedObject, "m_Script", "profile");
        else
            foreach (string name in new[] { "currentTurnAlpha", "selectableAlpha", "confirmedAreaAlpha", "unavailableAlpha", "currentTurnPrefab", "selectablePrefab", "confirmedAreaPrefab", "unavailablePrefab", "cellRenderers" })
                UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
        serializedObject.ApplyModifiedProperties();
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.HelpBox("외형·스윕·상하 운동은 JC_TestScenes의 BattleCellBorderSettings 씬에서 조절하고 프로필로 저장합니다. 이 오브젝트는 프로필과 셀 표시 연결을 담당합니다. 내부 채움은 독립적인 불투명도를 사용합니다.", UnityEditor.MessageType.Info);
    }
}
#endif
