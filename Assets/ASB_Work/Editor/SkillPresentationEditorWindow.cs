using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillPresentationData(애니메이션 override/이펙트/투사체/사운드)를 skillIndex 기준으로 한 화면에서 편집합니다.
/// CSV(SkillData)는 절대 수정하지 않고, 참고용으로만 읽어서 보여줍니다.
/// </summary>
public class SkillPresentationEditorWindow : EditorWindow
{
    private const string CatalogAssetPath = "Assets/ASB_Work/Skills/SkillPresentationCatalog.asset";
    private const string PresentationFolder = "Assets/ASB_Work/Skills";

    private SkillPresentationCatalog _catalog;
    private readonly List<SkillPresentationData> _presentations = new List<SkillPresentationData>();
    private SkillPresentationData _selected;
    private Vector2 _listScroll;
    private Vector2 _detailScroll;
    private bool _foldNew = true;
    private bool _foldLegacy;
    private int _newSkillIndexInput;

    private int _selectedTab; // 0 = Data Edit, 1 = Runtime Preview
    private SkillPresentationPreviewController _cachedPreviewController;
    private int _previewSkillIndex;

    [MenuItem("Battle/Skill Presentation Editor")]
    public static void Open()
    {
        GetWindow<SkillPresentationEditorWindow>("Skill Presentation Editor");
    }

    private void OnEnable()
    {
        _catalog = AssetDatabase.LoadAssetAtPath<SkillPresentationCatalog>(CatalogAssetPath);
        RefreshPresentationList();
    }

    private void RefreshPresentationList()
    {
        _presentations.Clear();
        string[] guids = AssetDatabase.FindAssets("t:SkillPresentationData", new[] { PresentationFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<SkillPresentationData>(path);
            if (data != null)
            {
                _presentations.Add(data);
            }
        }

        _presentations.Sort((a, b) => a.SkillIndex.CompareTo(b.SkillIndex));
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawCatalogField();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        _selectedTab = GUILayout.Toolbar(_selectedTab, new[] { "Data Edit", "Runtime Preview" });
        EditorGUILayout.Space();

        if (_selectedTab == 0)
        {
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            DrawRuntimePreviewTab();
        }
    }

    private void DrawCatalogField()
    {
        EditorGUI.BeginChangeCheck();
        _catalog = (SkillPresentationCatalog)EditorGUILayout.ObjectField(
            "Catalog", _catalog, typeof(SkillPresentationCatalog), false);
        if (EditorGUI.EndChangeCheck())
        {
            // 사용자가 직접 다른 카탈로그를 지정한 경우 그대로 존중합니다.
        }
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(220));
        EditorGUILayout.LabelField("Skill Presentations", EditorStyles.boldLabel);

        _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
        foreach (SkillPresentationData data in _presentations)
        {
            bool isSelected = data == _selected;
            string label = $"{data.SkillIndex} : {data.name}";
            bool newSelected = GUILayout.Toggle(isSelected, label, "Button");
            if (newSelected && !isSelected)
            {
                _selected = data;
                _previewSkillIndex = data.SkillIndex;
                bool phaseCue = data.PresentationSchemaVersion >= 1;
                _foldNew = phaseCue;       // 스키마에 맞는 쪽을 자동으로 펼침
                _foldLegacy = !phaseCue;
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("New Skill Index", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _newSkillIndexInput = EditorGUILayout.IntField(_newSkillIndexInput);
        if (GUILayout.Button("추가", GUILayout.Width(50)))
        {
            CreateNewPresentation(_newSkillIndexInput);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void CreateNewPresentation(int skillIndex)
    {
        if (_presentations.Any(p => p.SkillIndex == skillIndex))
        {
            EditorUtility.DisplayDialog("중복", $"이미 skillIndex {skillIndex} 프리셋이 존재합니다.", "확인");
            return;
        }

        var data = ScriptableObject.CreateInstance<SkillPresentationData>();
        data.SkillIndex = skillIndex;

        string assetPath = $"{PresentationFolder}/SkillPresentation_{skillIndex}.asset";
        AssetDatabase.CreateAsset(data, assetPath);
        AssetDatabase.SaveAssets();

        RefreshPresentationList();
        _selected = data;
        _previewSkillIndex = data.SkillIndex;

        if (_catalog != null)
        {
            SkillPresentationSyncUtility.UpsertBinding(_catalog, skillIndex, data);
        }
    }

    private void DrawDetail()
    {
        EditorGUILayout.BeginVertical();

        if (_selected == null)
        {
            EditorGUILayout.HelpBox("좌측에서 스킬을 선택하거나 새로 추가하세요.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

        var so = new SerializedObject(_selected);
        so.Update();

        EditorGUILayout.LabelField("Skill Binding", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("SkillIndex"));
        DrawCsvReadOnlyInfo(_selected.SkillIndex);

        EditorGUILayout.Space();
        SerializedProperty schemaProp = so.FindProperty("PresentationSchemaVersion");
        bool isPhaseCue = schemaProp != null && schemaProp.intValue >= 1;
        EditorGUILayout.LabelField("Presentation Schema", isPhaseCue ? "PhaseCue (1)" : "Legacy (0)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(isPhaseCue))
        {
            if (GUILayout.Button("Upgrade To Phase Cue (Schema = 1)") && schemaProp != null)
            {
                schemaProp.intValue = 1;
                isPhaseCue = true;
            }
        }
        EditorGUILayout.HelpBox(isPhaseCue
            ? "PhaseCue: 이펙트/사운드는 아래 Presentation Phases의 Cue로만 재생됩니다. 빈 Cue = 의도적 무연출(레거시 폴백 아님)."
            : "Legacy: 기존 director 경로(아래 Attack/Hit Effect·Sound id)를 사용합니다. Upgrade 시 Cue 경로로 전환됩니다.",
            MessageType.Info);

        // ── 공용 (전 스키마) ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation (비우면 CSV/산술 폴백)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("AnimationStateName")); // base 컨트롤러 state 드롭다운
        EditorGUILayout.PropertyField(so.FindProperty("TargetAnimationTriggerOverride"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Timing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("UseAnimEvent"));
        EditorGUILayout.PropertyField(so.FindProperty("HitDelay"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit / 타겟 피격 (공용: 전 스키마)", EditorStyles.boldLabel);
        SerializedProperty enableHitEffect = so.FindProperty("EnableHitEffect");
        EditorGUILayout.PropertyField(enableHitEffect);
        using (new EditorGUI.DisabledScope(enableHitEffect != null && !enableHitEffect.boolValue))
        {
            EditorGUILayout.PropertyField(so.FindProperty("HitEffectId"));  // EffectRegistry id
            EditorGUILayout.PropertyField(so.FindProperty("HitSoundId"));   // SoundRegistry id
        }
        EditorGUILayout.PropertyField(so.FindProperty("SfxVolume"));

        // ── 신 방식 (Phase Cue, Schema=1) ──
        EditorGUILayout.Space();
        _foldNew = EditorGUILayout.Foldout(_foldNew, "신 방식 — Presentation Phases (Schema=1)", true);
        if (_foldNew)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(so.FindProperty("MovePrepare"), true);
            EditorGUILayout.PropertyField(so.FindProperty("Move"), true);
            EditorGUILayout.PropertyField(so.FindProperty("AttackPrepare"), true);
            EditorGUILayout.PropertyField(so.FindProperty("Attack"), true); // Beats + 각 beat AnimationStateName/ Cue 드롭다운
            EditorGUILayout.PropertyField(so.FindProperty("Return"), true);
            EditorGUILayout.PropertyField(so.FindProperty("Post"), true);
        
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Moving Attack (진입 이동 + 공격)", EditorStyles.boldLabel);
                            EditorGUILayout.PropertyField(so.FindProperty("MovingAttack"), true);
                            so.ApplyModifiedProperties();
                            MovingAttackPathSceneTool.DrawInspectorControls(_selected);
                            so.Update();
            EditorGUI.indentLevel--;
        }

        // ── 옛 방식 (Legacy, Schema=0) ──
        EditorGUILayout.Space();
        _foldLegacy = EditorGUILayout.Foldout(_foldLegacy, "옛 방식 — Legacy (Schema=0, director 경로)", true);
        if (_foldLegacy)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(so.FindProperty("AnimationTriggerOverride"));

            EditorGUILayout.LabelField("Attack Effect", EditorStyles.miniBoldLabel);
            SerializedProperty enableAttackEffect = so.FindProperty("EnableAttackEffect");
            EditorGUILayout.PropertyField(enableAttackEffect);
            using (new EditorGUI.DisabledScope(enableAttackEffect != null && !enableAttackEffect.boolValue))
            {
                EditorGUILayout.PropertyField(so.FindProperty("AttackEffectId"));
                EditorGUILayout.PropertyField(so.FindProperty("AttackEffectPrefab"));
                EditorGUILayout.PropertyField(so.FindProperty("AttackEffectPositionOffset"));
                EditorGUILayout.PropertyField(so.FindProperty("AttackEffectRotationOffset"));
            }

            EditorGUILayout.LabelField("Attack Sound", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("AttackSoundId"));
            EditorGUILayout.PropertyField(so.FindProperty("AttackSfxClip"));

            EditorGUILayout.LabelField("Hit Effect/Sound (legacy prefab/clip)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("HitEffectPrefab"));
            EditorGUILayout.PropertyField(so.FindProperty("HitEffectPositionOffset"));
            EditorGUILayout.PropertyField(so.FindProperty("HitEffectRotationOffset"));
            EditorGUILayout.PropertyField(so.FindProperty("HitSfxClip"));

            EditorGUILayout.LabelField("Projectile", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("ProjectilePrefab"));
            EditorGUILayout.PropertyField(so.FindProperty("FlightTime"));
            EditorGUILayout.PropertyField(so.FindProperty("TrajectoryType"));
            EditorGUILayout.PropertyField(so.FindProperty("ArcHeight"));
            EditorGUILayout.PropertyField(so.FindProperty("ScaleByCellSize"));
            EditorGUI.indentLevel--;
        }

        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("저장 (카탈로그 자동 동기화)"))
        {
            EditorUtility.SetDirty(_selected);
            AssetDatabase.SaveAssets();

            if (_catalog != null)
            {
                SkillPresentationSyncUtility.UpsertBinding(_catalog, _selected.SkillIndex, _selected);
            }
            else
            {
                Debug.LogWarning("[SkillPresentationEditorWindow] Catalog가 지정되지 않아 카탈로그 동기화를 건너뜁니다.");
            }

            RefreshPresentationList();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawRuntimePreviewTab()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Runtime Preview는 Play 모드에서만 가능합니다.", MessageType.Info);
            return;
        }

        if (_cachedPreviewController == null)
        {
            _cachedPreviewController = Object.FindFirstObjectByType<SkillPresentationPreviewController>();
        }

        if (_cachedPreviewController == null)
        {
            EditorGUILayout.HelpBox("씬에 SkillPresentationPreviewController가 없습니다.", MessageType.Warning);
            return;
        }

        _previewSkillIndex = EditorGUILayout.IntField("Skill Index", _previewSkillIndex);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actor", _cachedPreviewController.ActorName);
        EditorGUILayout.LabelField("Target", _cachedPreviewController.TargetName);
        EditorGUILayout.LabelField("State", _cachedPreviewController.IsPlaying ? "실행 중" : "대기");

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("< Prev"))
        {
            StepPreviewSelection(-1);
        }
        if (GUILayout.Button("Next >"))
        {
            StepPreviewSelection(1);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_cachedPreviewController.IsPlaying))
        {
            if (GUILayout.Button("Play Selected Skill", GUILayout.Height(30)))
            {
                _cachedPreviewController.SetSelectedSkillIndex(_previewSkillIndex);
                _cachedPreviewController.PlaySelectedSkill();
            }
        }

        if (GUILayout.Button("Reset Preview"))
        {
            _cachedPreviewController.ResetPreview();
        }

        // 아군(부활 대상) 수동 제어 — 부활 스킬(4040 등) 연출 확인용.
        if (_cachedPreviewController.HasAllyTarget)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"아군: {_cachedPreviewController.AllyTargetName} ({(_cachedPreviewController.IsAllyTargetDead ? "사망" : "생존")})");
            using (new EditorGUI.DisabledScope(_cachedPreviewController.IsPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(_cachedPreviewController.IsAllyTargetDead))
                {
                    if (GUILayout.Button("아군 죽이기"))
                    {
                        _cachedPreviewController.KillAllyTarget();
                    }
                }
                using (new EditorGUI.DisabledScope(!_cachedPreviewController.IsAllyTargetDead))
                {
                    if (GUILayout.Button("아군 살리기"))
                    {
                        _cachedPreviewController.ReviveAllyTarget();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void StepPreviewSelection(int direction)
    {
        if (_presentations.Count == 0)
        {
            return;
        }

        int currentIndex = _selected != null ? _presentations.IndexOf(_selected) : -1;
        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, _presentations.Count - 1);

        _selected = _presentations[nextIndex];
        _previewSkillIndex = _selected.SkillIndex;
    }

    private void DrawCsvReadOnlyInfo(int skillIndex)
    {
        string skillName = "CSV 데이터 없음";
        string animTrigger = "-";

        if (DHCsvTemplateCatalog.Instance != null)
        {
            SkillData source = DHCsvTemplateCatalog.Instance.GetSkillTemplate(skillIndex);
            if (source != null)
            {
                skillName = string.IsNullOrWhiteSpace(source.skillName) ? "(이름 없음)" : source.skillName;
                animTrigger = string.IsNullOrWhiteSpace(source.AnimationTrigger) ? "-" : source.AnimationTrigger;
            }
        }
        else
        {
            skillName = "CSV 데이터 없음 (Play 모드가 아니거나 DHCsvTemplateCatalog 미초기화)";
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("CSV skillName", skillName);
            EditorGUILayout.TextField("CSV AnimationTrigger", animTrigger);
        }
    }
}
