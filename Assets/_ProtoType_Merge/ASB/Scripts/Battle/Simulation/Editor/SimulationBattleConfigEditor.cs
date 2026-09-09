using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디트 모드에서 데이터 테이블 SO를 직접 읽어 드롭다운 목록을 만든다.
/// (런타임 DHCsvTemplateCatalog 싱글턴은 에디트 모드에 없을 수 있으므로 테이블을 직접 로드한다.)
/// </summary>
internal static class SimCatalogEditorSource
{
    internal struct PlayerOption
    {
        public string Key;
        public string Label;
    }

    internal struct EnemyOption
    {
        public string Key;
        public string Label;
    }

    // 중요: 런타임 카탈로그가 실제로 쓰는 키를 그대로 사용한다.
    // 프로젝트에 중복 데이터 테이블(예: BE* vs FEP*)이 있어 FindAssets로 잡으면
    // 런타임과 다른 키가 나올 수 있으므로, 카탈로그 컴포넌트의 GetAll*를 사용한다.
    private static DHCsvTemplateCatalog FindCatalog()
        => UnityEngine.Object.FindFirstObjectByType<DHCsvTemplateCatalog>(FindObjectsInactive.Include);

    internal static List<PlayerOption> LoadPlayers()
    {
        var result = new List<PlayerOption>();
        DHCsvTemplateCatalog cat = FindCatalog();
        if (cat == null)
            return result;

        foreach (DHPlayerUnitTemplate p in cat.GetAllPlayerUnitTemplates())
        {
            if (p == null || string.IsNullOrWhiteSpace(p.UnitKey))
                continue;
            result.Add(new PlayerOption
            {
                Key = p.UnitKey,
                Label = $"{p.UnitName} / {p.ClassName} [{p.UnitKey}]"
            });
        }
        return result;
    }

    internal static List<EnemyOption> LoadEnemyGroups()
    {
        var result = new List<EnemyOption>();
        DHCsvTemplateCatalog cat = FindCatalog();
        if (cat == null)
            return result;

        foreach (DHEnemyGroupTemplate g in cat.GetAllEnemyGroupTemplates())
        {
            if (g == null || string.IsNullOrWhiteSpace(g.GroupKey))
                continue;
            int members = g.Members != null ? g.Members.Count : 0;
            result.Add(new EnemyOption
            {
                Key = g.GroupKey,
                Label = $"{g.GroupName} [{g.GroupKey}] · {members}명"
            });
        }
        return result;
    }
}

[CustomEditor(typeof(SimulationBattleConfig))]
public sealed class SimulationBattleConfigEditor : Editor
{
    private const int MaxAllies = 6;

    private List<SimCatalogEditorSource.PlayerOption> players = new List<SimCatalogEditorSource.PlayerOption>();
    private List<SimCatalogEditorSource.EnemyOption> enemyGroups = new List<SimCatalogEditorSource.EnemyOption>();
    private string[] playerLabels = new string[0];
    private string[] enemyLabels = new string[0];

    private void OnEnable() => ReloadCatalog();

    private void ReloadCatalog()
    {
        players = SimCatalogEditorSource.LoadPlayers();
        enemyGroups = SimCatalogEditorSource.LoadEnemyGroups();
        playerLabels = players.ConvertAll(p => p.Label).ToArray();
        enemyLabels = enemyGroups.ConvertAll(e => e.Label).ToArray();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (players.Count == 0 || enemyGroups.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "카탈로그를 찾지 못했습니다. DHCsvTemplateCatalog가 있는 씬(예: BattleSimulationScene의 DataStorage)을 열어야 드롭다운이 채워집니다.",
                MessageType.Warning);
            if (GUILayout.Button("카탈로그 다시 불러오기"))
                ReloadCatalog();
            EditorGUILayout.Space();
        }

        DrawAllies();
        EditorGUILayout.Space();
        DrawEnemy();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawValidationSummary();
    }

    private void DrawAllies()
    {
        SerializedProperty alliesProp = serializedObject.FindProperty("allies");
        EditorGUILayout.LabelField("아군 (위→아래 = 전투 배치 순서, 활성 1~6명)", EditorStyles.boldLabel);

        int removeIndex = -1;
        for (int i = 0; i < alliesProp.arraySize; i++)
        {
            SerializedProperty ally = alliesProp.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty enabledProp = ally.FindPropertyRelative("Enabled");
                    enabledProp.boolValue = EditorGUILayout.ToggleLeft($"슬롯 {i + 1} 사용", enabledProp.boolValue, GUILayout.Width(120f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("삭제", GUILayout.Width(48f)))
                        removeIndex = i;
                }

                using (new EditorGUI.DisabledScope(!ally.FindPropertyRelative("Enabled").boolValue))
                {
                    DrawUnitDropdown(ally);
                    SerializedProperty levelProp = ally.FindPropertyRelative("Level");
                    levelProp.intValue = Mathf.Max(1, EditorGUILayout.IntField("레벨", levelProp.intValue));

                    SerializedProperty weaponProp = ally.FindPropertyRelative("WeaponKey");
                    weaponProp.stringValue = EditorGUILayout.TextField(new GUIContent("무기 키(선택)", "비우면 기본 무기 규칙을 따른다"), weaponProp.stringValue);

                    SerializedProperty skillProp = ally.FindPropertyRelative("SkillIndex");
                    skillProp.intValue = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("스킬 인덱스(선택)", "0이면 기본 스킬"), skillProp.intValue));

                    SerializedProperty hpProp = ally.FindPropertyRelative("HpRatio");
                    hpProp.floatValue = EditorGUILayout.Slider("시작 HP 비율", Mathf.Clamp(hpProp.floatValue <= 0f ? 1f : hpProp.floatValue, 0.01f, 1f), 0.01f, 1f);
                }
            }
        }

        if (removeIndex >= 0)
            alliesProp.DeleteArrayElementAtIndex(removeIndex);

        using (new EditorGUI.DisabledScope(alliesProp.arraySize >= MaxAllies))
        {
            if (GUILayout.Button("아군 슬롯 추가"))
                AppendAlly(alliesProp);
        }
    }

    private void DrawUnitDropdown(SerializedProperty ally)
    {
        SerializedProperty keyProp = ally.FindPropertyRelative("UnitTemplateKey");
        if (players.Count == 0)
        {
            EditorGUILayout.TextField("유닛 키", keyProp.stringValue);
            return;
        }

        int current = players.FindIndex(p => p.Key == keyProp.stringValue);
        if (current < 0)
            current = 0;
        int selected = EditorGUILayout.Popup("유닛", current, playerLabels);
        if (selected >= 0 && selected < players.Count)
            keyProp.stringValue = players[selected].Key;
    }

    private void DrawEnemy()
    {
        EditorGUILayout.LabelField("적", EditorStyles.boldLabel);

        SerializedProperty groupProp = serializedObject.FindProperty("enemyGroupKey");
        SerializedProperty levelProp = serializedObject.FindProperty("enemyLevel");

        if (enemyGroups.Count == 0)
        {
            EditorGUILayout.TextField("적 그룹 키", groupProp.stringValue);
        }
        else
        {
            int current = enemyGroups.FindIndex(e => e.Key == groupProp.stringValue);
            if (current < 0)
                current = 0;
            int selected = EditorGUILayout.Popup("적 그룹", current, enemyLabels);
            if (selected >= 0 && selected < enemyGroups.Count)
                groupProp.stringValue = enemyGroups[selected].Key;
        }

        levelProp.intValue = Mathf.Clamp(EditorGUILayout.IntField("적 레벨", levelProp.intValue), 1, SimulationUnitFactory.MaxSimulationLevel);
    }

    /// <summary>런타임 카탈로그 없이 테이블 기준으로만 보조 경고를 낸다(프리팹 존재는 런타임에서 최종 검증).</summary>
    private void DrawValidationSummary()
    {
        var config = (SimulationBattleConfig)target;
        var messages = new List<string>();

        int enabledCount = 0;
        if (config.allies != null)
        {
            for (int i = 0; i < config.allies.Count; i++)
            {
                SimulationAllyInput ally = config.allies[i];
                if (ally == null || !ally.Enabled)
                    continue;
                enabledCount++;

                if (players.Count > 0 && players.FindIndex(p => p.Key == ally.UnitTemplateKey) < 0)
                    messages.Add($"슬롯 {i + 1}: 유닛 키 '{ally.UnitTemplateKey}'가 테이블에 없습니다.");
            }
        }

        if (enabledCount == 0)
            messages.Add("활성화된 아군이 없습니다(최소 1명).");
        if (enabledCount > MaxAllies)
            messages.Add($"활성화된 아군이 {enabledCount}명입니다(최대 {MaxAllies}명).");

        if (enemyGroups.Count > 0 && enemyGroups.FindIndex(e => e.Key == config.enemyGroupKey) < 0)
            messages.Add($"적 그룹 키 '{config.enemyGroupKey}'가 테이블에 없습니다.");

        if (messages.Count == 0)
            EditorGUILayout.HelpBox("검증 통과(보조). 프리팹 존재 여부는 Play 시 최종 확인됩니다.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("확인 필요:\n- " + string.Join("\n- ", messages), MessageType.Warning);
    }

    private void AppendAlly(SerializedProperty alliesProp)
    {
        int index = alliesProp.arraySize;
        alliesProp.InsertArrayElementAtIndex(index);
        SerializedProperty ally = alliesProp.GetArrayElementAtIndex(index);
        ally.FindPropertyRelative("Enabled").boolValue = true;
        ally.FindPropertyRelative("UnitTemplateKey").stringValue = players.Count > 0 ? players[0].Key : string.Empty;
        ally.FindPropertyRelative("Level").intValue = 1;
        ally.FindPropertyRelative("WeaponKey").stringValue = string.Empty;
        ally.FindPropertyRelative("SkillIndex").intValue = 0;
        ally.FindPropertyRelative("HpRatio").floatValue = 1f;
    }
}
