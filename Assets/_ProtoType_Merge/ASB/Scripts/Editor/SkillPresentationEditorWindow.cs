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
    private const string CatalogAssetPath = "Assets/_ProtoType_Merge/ASB/Skills/SkillPresentationCatalog.asset";
    private const string PresentationFolder = "Assets/_ProtoType_Merge/ASB/Skills";

    private SkillPresentationCatalog _catalog;
    private readonly List<SkillPresentationData> _presentations = new List<SkillPresentationData>();
    private SkillPresentationData _selected;
    private Vector2 _listScroll;
    private Vector2 _detailScroll;
    private int _newSkillIndexInput;

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
        EditorGUILayout.BeginHorizontal();

        DrawList();
        DrawDetail();

        EditorGUILayout.EndHorizontal();
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
            if (GUILayout.Toggle(isSelected, label, "Button"))
            {
                _selected = data;
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
        EditorGUILayout.LabelField("Animation Override (비우면 CSV 값 사용)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("AnimationTriggerOverride"));
        EditorGUILayout.PropertyField(so.FindProperty("TargetAnimationTriggerOverride"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attack Effect", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("AttackEffectPrefab"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Effect", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("HitEffectPrefab"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Timing (CSV 미지원 임시)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("UseAnimEvent"));
        EditorGUILayout.PropertyField(so.FindProperty("HitDelay"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projectile (선택)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("ProjectilePrefab"));
        EditorGUILayout.PropertyField(so.FindProperty("FlightTime"));
        EditorGUILayout.PropertyField(so.FindProperty("TrajectoryType"));
        EditorGUILayout.PropertyField(so.FindProperty("ArcHeight"));
        EditorGUILayout.PropertyField(so.FindProperty("ScaleByCellSize"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("AttackSfxClip"));
        EditorGUILayout.PropertyField(so.FindProperty("HitSfxClip"));
        EditorGUILayout.PropertyField(so.FindProperty("SfxVolume"));

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
